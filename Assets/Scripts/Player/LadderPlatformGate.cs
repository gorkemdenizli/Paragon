using UnityEngine;

public class LadderPlatformGate : MonoBehaviour
{
    [SerializeField] private Collider2D gateCollider;
    [SerializeField] private bool debugLogs = false;

    private Collider2D _pendingRestoreCollider;

    void Awake()
    {
        if (gateCollider == null) gateCollider = GetComponent<Collider2D>();
    }

    public void IgnoreForPlayer(Collider2D playerCollider)
    {
        if (gateCollider == null || playerCollider == null) return;
        Physics2D.IgnoreCollision(playerCollider, gateCollider, true);
        if (debugLogs) Debug.Log("[Gate] Ignore started.");
    }

    public void RestoreForPlayer(Collider2D playerCollider)
    {
        if (gateCollider == null || playerCollider == null) return;
        if (IsPlayerStillInsideGate(playerCollider))
        {
            _pendingRestoreCollider = playerCollider;
            if (debugLogs) Debug.Log("[Gate] Restore pending — player still inside.");
            return;
        }
        Physics2D.IgnoreCollision(playerCollider, gateCollider, false);
        _pendingRestoreCollider = null;
        if (debugLogs) Debug.Log("[Gate] Collision restored.");
    }

    public bool IsPlayerStillInsideGate(Collider2D playerCollider)
    {
        if (gateCollider == null || playerCollider == null) return false;
        return gateCollider.bounds.Intersects(playerCollider.bounds);
    }

    void Update()
    {
        if (_pendingRestoreCollider == null) return;
        if (IsPlayerStillInsideGate(_pendingRestoreCollider)) return;
        Physics2D.IgnoreCollision(_pendingRestoreCollider, gateCollider, false);
        if (debugLogs) Debug.Log("[Gate] Deferred restore applied.");
        _pendingRestoreCollider = null;
    }
}
