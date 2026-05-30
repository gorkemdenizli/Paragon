using UnityEngine;

public class LadderZone : MonoBehaviour
{
    [SerializeField] public Transform bottomPoint;
    [SerializeField] public Transform topPoint;
    [SerializeField] public bool  canPlayerUse   = true;
    [SerializeField] public float enterDistance  = 0.25f;
    [SerializeField] public float exitDistance   = 0.25f;
    [SerializeField] public LadderPlatformGate gate;

    public Vector2 BottomPosition => bottomPoint != null
        ? (Vector2)bottomPoint.position
        : (Vector2)transform.position;

    public Vector2 TopPosition => topPoint != null
        ? (Vector2)topPoint.position
        : (Vector2)transform.position + Vector2.up * 3f;

    public float LadderX  => transform.position.x;
    public bool  IsValid  => bottomPoint != null && topPoint != null && canPlayerUse;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (bottomPoint == null)
            Debug.LogWarning($"[LadderZone] {gameObject.name}: bottomPoint atanmamış!", this);
        if (topPoint == null)
            Debug.LogWarning($"[LadderZone] {gameObject.name}: topPoint atanmamış!", this);
    }

    void OnDrawGizmos()
    {
        Vector3 bot = bottomPoint != null ? bottomPoint.position : transform.position;
        Vector3 top = topPoint    != null ? topPoint.position    : transform.position + Vector3.up * 3f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(bot, top);

        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(bot, 0.12f);
        Gizmos.DrawSphere(top, 0.12f);

        UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.25f);
        UnityEditor.Handles.Label(top + Vector3.up * 0.2f, "Top");
        UnityEditor.Handles.Label(bot + Vector3.down * 0.25f, "Bot");
    }
#endif
}
