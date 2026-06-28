using UnityEngine;

public class EnemySpawnPoint : MonoBehaviour
{
    [SerializeField] public bool isEnabled = true;
    [SerializeField] public GameObject enemyPrefab;

    [SerializeField] public int   baseSpawnCount    = 1;
    [SerializeField] public int   maxSpawnCount     = 3;
    [SerializeField] public float baseSpawnInterval = 5f;
    [SerializeField] public float minSpawnInterval  = 2f;

    internal float _timer; // per-point spawn timer, driven by EnemySpawnManager

    public float GetCurrentSingleInterval()
    {
        var rdm = RunDifficultyManager.instance;
        float rateMult   = rdm != null ? rdm.SpawnRateMultiplier    : 1f;
        float minSingle  = rdm != null ? rdm.MinSingleSpawnInterval : 0.6f;
        int   countBonus = rdm != null ? rdm.SpawnCountBonus        : 0;

        float interval = Mathf.Max(baseSpawnInterval / rateMult, minSpawnInterval);
        int   count    = Mathf.Min(baseSpawnCount + countBonus, Mathf.Max(maxSpawnCount, 1));
        return Mathf.Max(interval / Mathf.Max(count, 1), minSingle);
    }

    public GameObject[] SpawnEnemies(int overrideCount = -1)
    {
        if (!isEnabled || enemyPrefab == null) return null;
        int count   = overrideCount >= 0 ? overrideCount : baseSpawnCount;
        var spawned = new GameObject[count];
        for (int i = 0; i < count; i++)
            spawned[i] = Instantiate(enemyPrefab, transform.position, Quaternion.identity);
        return spawned;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, 0.4f);
    }

    void OnDrawGizmos()
    {
        if (!isEnabled) return;
        Gizmos.color = new Color(1f, 0f, 1f, 0.35f);
        Gizmos.DrawSphere(transform.position, 0.3f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f,
            gameObject.name + (enemyPrefab != null ? $"\n{enemyPrefab.name} ×{baseSpawnCount} / {baseSpawnInterval:F1}s" : "\n(no prefab)"));
    }
#endif
}
