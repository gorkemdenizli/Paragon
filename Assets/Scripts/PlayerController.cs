using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    private const string AnimSpeed = "speed";
    private const string AnimYVelocity = "yVelocity";
    private const string AnimIsOnGround = "isOnGround";
    private const string AnimJumpPressed = "jumpPressed";
    private const string AnimIsDead = "isDead";
    private const string AnimShotFired = "shotFired";
    private const string AnimDoubleJump = "doubleJump";

    // short post-jump window: treat as not grounded even if overlap hits (animator / triggers)
    private const float JumpGroundSuppressDuration = 0.12f;

    [Header("References")]
    [SerializeField] private Rigidbody2D theRB;
    [SerializeField] private Animator anim;
    [SerializeField] private Transform groundPoint;

    [Header("Movement")]
    [Tooltip("Walk max speed (blend tree ~0.4).")]
    [FormerlySerializedAs("moveSpeed")]
    [SerializeField] private float walkSpeed = 6f;
    [Tooltip("Run max speed (blend tree 1).")]
    [SerializeField] private float runSpeed = 12f;
    [SerializeField] private float groundAcceleration = 80f;
    [SerializeField] private float groundDeceleration = 70f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float jumpCoyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    [SerializeField] private int maxExtraJumps = 1;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashTime = 0.2f;
    [SerializeField] private float waitAfterDashing;
    [SerializeField] private Slider dashCooldownSlider;

    [Header("Gravity")]
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float lowJumpMultiplier = 2f;

    [Header("Combat")]
    [SerializeField] private Weapon weapon;
    [Tooltip("Body sprite for flipX aim sync; if empty, root localScale X is used.")]
    [SerializeField] private SpriteRenderer characterSprite;

    [Header("Ground Check")]
    [SerializeField] private LayerMask whatIsGround;
    [SerializeField] private float groundCheckRadius = 0.2f;

    [Header("Input")]
    [Tooltip("If Move/Jump refs are empty, binds Player map from this asset.")]
    [SerializeField] private InputActionAsset inputActionAsset;
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference fireAction;
    [SerializeField] private InputActionReference dashAction;
    [SerializeField] private InputActionReference bombAction;
    [SerializeField] private InputActionReference interactAction;

    [Header("Bomb")]
    [SerializeField] private Transform bombPoint;
    [SerializeField] private GameObject bomb;
    [SerializeField] private float bombThrowForce = 15f;
    [SerializeField] private int maxGrenades = 3;
    [SerializeField] private float grenadeRegenTime = 20f;
    [SerializeField] private Slider grenadeRegenSlider;
    [SerializeField] private TMP_Text grenadeCountText;

    [Header("Other")]
    [SerializeField] public PlayerAbilityTracker abilities;

    public bool canMove;
    public Vector2 mouseWorldPos { get; private set; }
    public Vector2 aimDirection { get; private set; }
    public int facingDirection { get; private set; }

    private Camera mainCam;
    private float originalGravity;
    private GateController gate;

    private Vector2 moveInput;
    private float lastGroundedTime = -100f;
    private float jumpGraceEnd = -100f;
    private float jumpBufferTimer;
    private int extraJumpsLeft;
    private bool wasOnGround;
    private bool isOnGround;
    private float dashRechargeCounter;
    private float dashCounter;
    private bool isDashing;
    private int dashDirection;
    private int currentGrenades;
    private int grenadesAwaitingRegen;
    private float grenadeRegenCounter;
    private bool hasShotFiredParam;
    private bool hasDoubleJumpParam;

    private InputActionMap embeddedPlayerMap;
    private InputAction embeddedMove;
    private InputAction embeddedJump;
    private InputAction embeddedSprint;
    private InputAction embeddedFire;
    private InputAction embeddedDash;
    private InputAction embeddedBomb;
    private InputAction embeddedInteract;
    private bool useEmbeddedPlayerActions;

    // --- lifecycle ---

    // init animator, input mode, optional trigger flags
    void Awake()
    {
        if (anim == null)
            anim = GetComponentInChildren<Animator>(true);
        if (anim != null)
            anim.updateMode = AnimatorUpdateMode.Fixed;

        TryUseEmbeddedActionsFromAsset();
        CacheAnimatorTriggersIfPresent();
    }

    // hook input map or references
    void OnEnable()
    {
        if (useEmbeddedPlayerActions)
            EnableEmbeddedPlayerInput(true);
        else
            EnableReferencePlayerInput(true);
    }

    // unhook input teardown
    void OnDisable()
    {
        if (useEmbeddedPlayerActions)
            EnableEmbeddedPlayerInput(false);
        else
            EnableReferencePlayerInput(false);
    }

    // re-open actions after scene load / duplicate player teardown
    public void RestoreInputAfterSceneLoad()
    {
        if (useEmbeddedPlayerActions)
            embeddedPlayerMap.Enable();
        else
            SetAllReferenceActionsEnabled(true);

        canMove = true;
        gate = FindFirstObjectByType<GateController>();
    }

    // cache camera gravity abilities gate jump charges
    void Start()
    {
        mainCam = Camera.main;
        originalGravity = theRB.gravityScale;
        abilities = GetComponent<PlayerAbilityTracker>();
        gate = FindFirstObjectByType<GateController>();
        canMove = true;
        extraJumpsLeft = maxExtraJumps;
        currentGrenades = maxGrenades;
        UpdateGrenadeUI();
    }

    // aim fire ground jump buffer each frame
    void Update()
    {
        if (Time.timeScale == 0f) return;

        moveInput = ReadMoveInput();
        RefreshCameraIfNeeded();
        UpdateAimFacingAndWeaponInput();
        RefreshGroundedState();
        OnLandedClearStaleJumpTrigger();
        PollJumpInput();
        TickJumpBuffer();
        TickGrenadeRegen();
    }

    // push blend params and flags to animator after movement
    void LateUpdate()
    {
        PushAnimatorParameters();
    }

    // dash physics or grounded move + jump gravity tweak
    void FixedUpdate()
    {
        if (dashRechargeCounter > 0f)
            dashRechargeCounter -= Time.fixedDeltaTime;

        if (dashCooldownSlider != null)
            dashCooldownSlider.value = waitAfterDashing > 0f ? dashRechargeCounter / waitAfterDashing : 0f;

        if (isDashing)
        {
            TickDashPhysics();
            return;
        }

        if (!canMove)
        {
            theRB.linearVelocity = Vector2.zero;
            return;
        }

        ApplyHorizontalMove();
        ApplyJumpGravityTuning();
    }

    // --- input setup ---

    // use Player map from asset when no per-field references assigned
    void TryUseEmbeddedActionsFromAsset()
    {
        if (moveAction != null || inputActionAsset == null)
            return;

        embeddedPlayerMap = inputActionAsset.FindActionMap("Player", throwIfNotFound: true);
        embeddedMove = embeddedPlayerMap.FindAction("Move", throwIfNotFound: true);
        embeddedJump = embeddedPlayerMap.FindAction("Jump", throwIfNotFound: true);
        embeddedSprint = embeddedPlayerMap.FindAction("Sprint", throwIfNotFound: true);
        embeddedFire = embeddedPlayerMap.FindAction("Attack", throwIfNotFound: true);
        embeddedDash = embeddedPlayerMap.FindAction("Dash", throwIfNotFound: true);
        embeddedBomb = embeddedPlayerMap.FindAction("Bomb", throwIfNotFound: true);
        embeddedInteract = embeddedPlayerMap.FindAction("Interact", throwIfNotFound: false);
        useEmbeddedPlayerActions = true;
    }

    // enable map and hook performed, or reverse
    void EnableEmbeddedPlayerInput(bool on)
    {
        if (on)
        {
            embeddedPlayerMap.Enable();
            embeddedDash.performed += StartDash;
            embeddedBomb.performed += DropBomb;
            if (embeddedInteract != null)
                embeddedInteract.performed += Interact;
        }
        else
        {
            embeddedDash.performed -= StartDash;
            embeddedBomb.performed -= DropBomb;
            if (embeddedInteract != null)
                embeddedInteract.performed -= Interact;
            embeddedPlayerMap.Disable();
        }
    }

    // reference actions: unsubscribe before disable on teardown
    void EnableReferencePlayerInput(bool on)
    {
        void TogglePerformed(InputActionReference r, System.Action<InputAction.CallbackContext> handler, bool subscribe)
        {
            if (r == null)
                return;
            if (subscribe)
                r.action.performed += handler;
            else
                r.action.performed -= handler;
        }

        if (!on)
        {
            TogglePerformed(dashAction, StartDash, false);
            TogglePerformed(bombAction, DropBomb, false);
            TogglePerformed(interactAction, Interact, false);
        }

        SetAllReferenceActionsEnabled(on);

        if (on)
        {
            TogglePerformed(dashAction, StartDash, true);
            TogglePerformed(bombAction, DropBomb, true);
            TogglePerformed(interactAction, Interact, true);
        }
    }

    // enable or disable one reference action if assigned
    static void SetRefEnabled(InputActionReference r, bool enabled)
    {
        if (r == null)
            return;
        if (enabled)
            r.action.Enable();
        else
            r.action.Disable();
    }

    // toggle whole reference set move fire dash etc
    void SetAllReferenceActionsEnabled(bool enabled)
    {
        SetRefEnabled(moveAction, enabled);
        SetRefEnabled(jumpAction, enabled);
        SetRefEnabled(fireAction, enabled);
        SetRefEnabled(dashAction, enabled);
        SetRefEnabled(bombAction, enabled);
        SetRefEnabled(sprintAction, enabled);
        SetRefEnabled(interactAction, enabled);
    }

    // --- animator ---

    // detect optional shotFired / doubleJump triggers on controller
    void CacheAnimatorTriggersIfPresent()
    {
        hasShotFiredParam = false;
        hasDoubleJumpParam = false;
        if (anim == null)
            return;

        foreach (AnimatorControllerParameter p in anim.parameters)
        {
            if (p.type != AnimatorControllerParameterType.Trigger)
                continue;
            if (p.name == AnimShotFired)
                hasShotFiredParam = true;
            if (p.name == AnimDoubleJump)
                hasDoubleJumpParam = true;
        }
    }

    // speed blend y vel grounded dead bool to animator
    void PushAnimatorParameters()
    {
        if (anim == null)
            return;

        float absVx = Mathf.Abs(theRB.linearVelocity.x);
        bool wantsRun = IsSprintHeld();
        float speedBlend = isOnGround && canMove ? LocomotionBlendSpeed(absVx, wantsRun) : 0f;

        anim.SetFloat(AnimSpeed, speedBlend);
        anim.SetFloat(AnimYVelocity, theRB.linearVelocity.y);
        anim.SetBool(AnimIsOnGround, isOnGround);

        bool dead = PlayerHealthController.instance != null &&
                    PlayerHealthController.instance.currentHealth <= 0;
        anim.SetBool(AnimIsDead, dead);
    }

    // blend tree helper: 0 idle ~0.4 walk 1 run
    float LocomotionBlendSpeed(float absVx, bool wantsRun)
    {
        float runCap = Mathf.Max(0.001f, runSpeed);
        float walkCap = Mathf.Max(0.001f, walkSpeed);

        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            if (wantsRun)
                return Mathf.Clamp01(absVx / runCap);
            return Mathf.Clamp01(absVx / walkCap) * 0.4f;
        }

        return Mathf.Clamp01(absVx / runCap);
    }

    // --- update steps ---

    // lazy resolve main camera
    void RefreshCameraIfNeeded()
    {
        if (mainCam == null)
            mainCam = Camera.main;
    }

    // planar mouse aim, facing, sprite flipX, held fire + reload on Weapon
    void UpdateAimFacingAndWeaponInput()
    {
        if (mainCam != null)
            mouseWorldPos = AimPlaneUtil.ScreenToWorldOnPlane(mainCam, transform.position);
        else
            mouseWorldPos = (Vector2)transform.position + Vector2.right * facingDirection;

        Vector2 selfPos = transform.position;
        aimDirection = ((Vector2)mouseWorldPos - selfPos).normalized;
        if (aimDirection.sqrMagnitude < 1e-6f)
            aimDirection = Vector2.right * facingDirection;

        if (aimDirection.x > 0.01f)
            facingDirection = 1;
        else if (aimDirection.x < -0.01f)
            facingDirection = -1;

        if (characterSprite != null)
        {
            characterSprite.flipX = mouseWorldPos.x < selfPos.x;
            transform.localScale = Vector3.one;
        }
        else
            transform.localScale = new Vector3(facingDirection, 1f, 1f);

        if (!canMove || weapon == null)
            return;

        bool fireHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
        if (fireHeld || IsFirePressed())
        {
            if (weapon.TryShoot() && hasShotFiredParam && anim != null)
                anim.SetTrigger(AnimShotFired);
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            weapon.TryManualReload();
    }

    // overlap ground coyote timer refresh extra jumps
    void RefreshGroundedState()
    {
        isOnGround = groundPoint != null
                     && Time.time >= jumpGraceEnd
                     && Physics2D.OverlapCircle(groundPoint.position, groundCheckRadius, whatIsGround);

        if (isOnGround)
        {
            lastGroundedTime = Time.time;
            extraJumpsLeft = maxExtraJumps;
        }
    }

    // landed this frame clear stuck jumpPressed trigger
    void OnLandedClearStaleJumpTrigger()
    {
        if (isOnGround && !wasOnGround && anim != null)
            anim.ResetTrigger(AnimJumpPressed);
        wasOnGround = isOnGround;
    }

    // count buffer down try consume jump when grounded
    void TickJumpBuffer()
    {
        if (jumpBufferTimer <= 0f)
            return;

        jumpBufferTimer -= Time.deltaTime;
        TryConsumeJumpBuffer();
    }

    // --- movement / physics ---

    // dash velocity until end then restore gravity
    void TickDashPhysics()
    {
        if (dashCounter > 0f)
        {
            dashCounter -= Time.fixedDeltaTime;
            theRB.linearVelocity = new Vector2(dashSpeed * dashDirection, theRB.linearVelocity.y);
            return;
        }

        isDashing = false;
        dashRechargeCounter = waitAfterDashing;
        theRB.gravityScale = originalGravity;
    }

    // accel toward walk or run target vx (scaled by equipped armor type)
    void ApplyHorizontalMove()
    {
        float armorMult = ArmorController.instance != null ? ArmorController.instance.SpeedMultiplier : 1f;
        float targetMax = (IsSprintHeld() ? runSpeed : walkSpeed) * armorMult;

        // Guard against Dpad composite diagonal normalization: when W/Up is held with A/D the
        // composite can output 0.707 instead of 1.0 on X. Clamp to [-1, 1] after restoring
        // the full magnitude so horizontal speed is never reduced by a jump-key press.
        float inputX = Mathf.Clamp(moveInput.x, -1f, 1f);
        if (Mathf.Abs(moveInput.y) > 0.5f && Mathf.Abs(inputX) > 0.01f)
            inputX = Mathf.Sign(inputX);

        float targetVx = inputX * targetMax;
        bool hasHorizontalInput = Mathf.Abs(inputX) > 0.01f;
        // No deceleration in air when player isn't pressing a horizontal key — prevents W (jump key
        // that's also the Move "Up" composite binding) from zeroing moveInput.x and braking mid-air.
        float accel = hasHorizontalInput ? groundAcceleration : (isOnGround ? groundDeceleration : 0f);
        float newVx = Mathf.MoveTowards(theRB.linearVelocity.x, targetVx, accel * Time.fixedDeltaTime);
        theRB.linearVelocity = new Vector2(newVx, theRB.linearVelocity.y);
    }

    // fall faster short hop if jump released early
    void ApplyJumpGravityTuning()
    {
        if (theRB.linearVelocity.y < 0f)
        {
            theRB.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
        else if (theRB.linearVelocity.y > 0f && !IsJumpHeld())
        {
            theRB.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        }
    }

    // --- input reads ---

    // embedded move vector or reference move action
    Vector2 ReadMoveInput()
    {
        if (useEmbeddedPlayerActions && embeddedMove != null)
            return embeddedMove.ReadValue<Vector2>();
        return moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
    }

    // sprint ref embedded or shift fallback
    bool IsSprintHeld()
    {
        if (sprintAction != null)
            return sprintAction.action.IsPressed();
        if (useEmbeddedPlayerActions && embeddedSprint != null)
            return embeddedSprint.IsPressed();
        return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
    }

    // attack held embedded or reference
    bool IsFirePressed()
    {
        if (useEmbeddedPlayerActions && embeddedFire != null)
            return embeddedFire.IsPressed();
        return fireAction != null && fireAction.action.IsPressed();
    }

    // jump held for variable jump height
    bool IsJumpHeld()
    {
        if (useEmbeddedPlayerActions && embeddedJump != null)
            return embeddedJump.IsPressed();
        return jumpAction != null && jumpAction.action.IsPressed();
    }

    // --- fire jump interact ---

    // Polls each jump binding independently so pressing a second key while the first is held
    // still registers — Unity's performed callback doesn't re-fire when action is already held.
    void PollJumpInput()
    {
        if (!canMove) return;
        if (!AnyJumpBindingPressedThisFrame()) return;
        jumpBufferTimer = jumpBufferTime;
        TryConsumeJumpBuffer();
    }

    bool AnyJumpBindingPressedThisFrame()
    {
        InputAction action = useEmbeddedPlayerActions
            ? embeddedJump
            : jumpAction != null ? jumpAction.action : null;
        if (action == null) return false;
        foreach (var control in action.controls)
        {
            if (control is UnityEngine.InputSystem.Controls.ButtonControl btn && btn.wasPressedThisFrame)
                return true;
        }
        return false;
    }

    // ground or coyote jump else double if allowed
    void TryConsumeJumpBuffer()
    {
        if (jumpBufferTimer <= 0f || !canMove || groundPoint == null)
            return;

        bool canGroundJump = isOnGround || (Time.time - lastGroundedTime <= jumpCoyoteTime);
        if (canGroundJump)
        {
            DoJump(jumpForce, isDoubleJump: false);
            return;
        }

        bool canDouble = extraJumpsLeft > 0 && (abilities == null || abilities.canDoubleJump);
        if (canDouble)
            DoJump(jumpForce * 0.85f, isDoubleJump: true);
    }

    // vertical impulse animator triggers jump grace window
    void DoJump(float force, bool isDoubleJump)
    {
        theRB.linearVelocity = new Vector2(theRB.linearVelocity.x, force);
        jumpBufferTimer = 0f;
        lastGroundedTime = -100f;
        jumpGraceEnd = Time.time + JumpGroundSuppressDuration;

        // Force isOnGround false immediately so the animator bool is already false
        // when the trigger fires this same frame — otherwise RefreshGroundedState ran
        // earlier in Update and the bool is still true, causing the condition to fail
        // and the trigger to be silently consumed.
        isOnGround = false;

        if (anim == null)
            return;

        anim.SetBool(AnimIsOnGround, false);

        if (isDoubleJump)
        {
            extraJumpsLeft--;
            if (hasDoubleJumpParam)
                anim.SetTrigger(AnimDoubleJump);
            else
                anim.SetTrigger(AnimJumpPressed);
        }
        else
        {
            anim.SetTrigger(AnimJumpPressed);
        }
    }

    // interact current gate if any
    void Interact(InputAction.CallbackContext context)
    {
        if (!canMove)
            return;
        if (gate != null)
            gate.HandlePlayerInteract();
    }

    // ability check spawn bomb with rb velocity
    void DropBomb(InputAction.CallbackContext context)
    {
        if (!canMove || bomb == null || bombPoint == null)
            return;
        if (abilities != null && !abilities.canDropBomb)
            return;
        if (currentGrenades <= 0)
            return;

        GameObject newBomb = Instantiate(bomb, bombPoint.position, Quaternion.identity);
        Rigidbody2D rb = newBomb.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.linearVelocity = aimDirection * bombThrowForce;

        currentGrenades--;
        if (grenadesAwaitingRegen == 0)
            grenadeRegenCounter = grenadeRegenTime;
        grenadesAwaitingRegen++;
        UpdateGrenadeUI();
    }

    // count down regen queue one grenade at a time update slider and text
    void TickGrenadeRegen()
    {
        if (grenadesAwaitingRegen <= 0)
        {
            if (grenadeRegenSlider != null)
                grenadeRegenSlider.value = 0f;
            return;
        }

        grenadeRegenCounter -= Time.deltaTime;

        if (grenadeRegenSlider != null)
            grenadeRegenSlider.value = grenadeRegenTime > 0f ? grenadeRegenCounter / grenadeRegenTime : 0f;

        if (grenadeRegenCounter <= 0f)
        {
            currentGrenades++;
            grenadesAwaitingRegen--;

            if (grenadesAwaitingRegen > 0)
                grenadeRegenCounter = grenadeRegenTime;

            UpdateGrenadeUI();
        }
    }

    void UpdateGrenadeUI()
    {
        if (grenadeCountText != null)
            grenadeCountText.text = currentGrenades.ToString();
    }

    // start horizontal dash zero gravity until timer ends
    void StartDash(InputAction.CallbackContext context)
    {
        if (!canMove)
            return;
        if (abilities != null && !abilities.canDash)
            return;
        if (isDashing || dashRechargeCounter > 0f)
            return;

        dashDirection = Mathf.Abs(moveInput.x) > 0.1f
            ? Mathf.RoundToInt(Mathf.Sign(moveInput.x))
            : facingDirection;

        isDashing = true;
        dashCounter = dashTime;
        theRB.gravityScale = 0f;
    }
}
