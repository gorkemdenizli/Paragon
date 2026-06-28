using UnityEngine;

public class LadderTopEntryZone : MonoBehaviour
{
    [SerializeField] private LadderZone ladder;
    [SerializeField] private bool debugLogs = false;

    private PlayerClimbController _pcc;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_pcc != null) return;
        var pcc = other.GetComponentInParent<PlayerClimbController>();
        if (pcc == null) return;
        _pcc = pcc;
        _pcc.SetTopEntryZone(ladder);
        if (debugLogs) Debug.Log("[TopEntry] Player entered.");
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (_pcc == null) return;
        var pcc = other.GetComponentInParent<PlayerClimbController>();
        if (pcc != _pcc) return;
        _pcc.ClearTopEntryZone(ladder);
        _pcc = null;
        if (debugLogs) Debug.Log("[TopEntry] Player exited.");
    }
}
