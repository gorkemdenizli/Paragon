using UnityEngine;

// Keeps this GameObject at a fixed world-space offset from its parent,
// ignoring the parent's rotation. Attach to UI children (healthbar, damage numbers)
// of rotating enemies.
public class WorldSpaceAnchor : MonoBehaviour
{
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.8f, 0f);

    private Transform _parent;

    void Awake()
    {
        _parent = transform.parent;
    }

    void LateUpdate()
    {
        if (_parent == null) return;
        transform.position = _parent.position + worldOffset;
        transform.rotation = Quaternion.identity;
    }
}
