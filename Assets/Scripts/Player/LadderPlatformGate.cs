using System.Collections.Generic;
using UnityEngine;

public class LadderPlatformGate : MonoBehaviour
{
    [SerializeField] private Collider2D gateCollider;
    [SerializeField] private bool debugLogs = false;

    private readonly List<Collider2D> _pendingRestores = new();

    void Awake()
    {
        if (gateCollider == null) gateCollider = GetComponent<Collider2D>();
    }

    // ── Generic API (player + enemy) ─────────────────────────────────────────────

    public void IgnoreForCollider(Collider2D target)
    {
        if (gateCollider == null || target == null) return;
        Physics2D.IgnoreCollision(target, gateCollider, true);
        if (debugLogs) Debug.Log($"[Gate] Ignore for {target.name}.");
    }

    public void RestoreForCollider(Collider2D target)
    {
        if (gateCollider == null || target == null) return;
        if (IsColliderStillInsideGate(target))
        {
            if (!_pendingRestores.Contains(target)) _pendingRestores.Add(target);
            if (debugLogs) Debug.Log($"[Gate] Restore pending for {target.name}.");
            return;
        }
        Physics2D.IgnoreCollision(target, gateCollider, false);
        if (debugLogs) Debug.Log($"[Gate] Restored for {target.name}.");
    }

    public bool IsColliderStillInsideGate(Collider2D target)
    {
        if (gateCollider == null || target == null) return false;
        return gateCollider.bounds.Intersects(target.bounds);
    }

    // ── Player delegates (PlayerClimbController unchanged) ───────────────────────

    public void IgnoreForPlayer(Collider2D p)          => IgnoreForCollider(p);
    public void RestoreForPlayer(Collider2D p)          => RestoreForCollider(p);
    public bool IsPlayerStillInsideGate(Collider2D p)  => IsColliderStillInsideGate(p);

    // ── Deferred restore ─────────────────────────────────────────────────────────

    void Update()
    {
        for (int i = _pendingRestores.Count - 1; i >= 0; i--)
        {
            if (IsColliderStillInsideGate(_pendingRestores[i])) continue;
            Physics2D.IgnoreCollision(_pendingRestores[i], gateCollider, false);
            if (debugLogs) Debug.Log($"[Gate] Deferred restore for {_pendingRestores[i].name}.");
            _pendingRestores.RemoveAt(i);
        }
    }
}
