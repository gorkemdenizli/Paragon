using UnityEngine;

public class EnemySpawnPoint : MonoBehaviour
{
    [SerializeField] public bool isEnabled = true;
    [SerializeField] public GameObject enemyPrefab;
    [SerializeField] public int enemiesPerSpawn = 1;
    [SerializeField] public float spawnInterval = 5f;

    internal float _timer; // unused in single-tick system; kept for backward compat

    // overrideCount >= 0 ise o sayı kullanılır; -1 ise kendi enemiesPerSpawn değeri.
    public GameObject[] SpawnEnemies(int overrideCount = -1)
    {
        if (!isEnabled || enemyPrefab == null) return null;
        int count   = overrideCount >= 0 ? overrideCount : enemiesPerSpawn;
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
            gameObject.name + (enemyPrefab != null ? $"\n{enemyPrefab.name} ×{enemiesPerSpawn}" : "\n(no prefab)"));
    }
#endif
}
