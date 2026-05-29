using UnityEngine;

public class EnemySpawnManager : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private EnemySpawnPoint[] spawnPoints;

    [Header("Spawn Validation")]
    [Tooltip("Spawn positions closer than this to the player are skipped.")]
    [SerializeField] private float minDistanceFromPlayer = 5f;
    [Tooltip("Radius of the overlap check at the spawn position. Blocks spawns inside enemies or obstacles.")]
    [SerializeField] private float spawnOverlapRadius = 0.5f;
    [Tooltip("Layers checked for overlap at spawn position (e.g. Enemy + Ground + Obstacle).")]
    [SerializeField] private LayerMask spawnBlockingLayers;
    [Tooltip("Spawn interval used when RunDifficultyManager is not present.")]
    [SerializeField] private float fallbackSpawnInterval = 2.5f;

    [Header("Player Proximity Spawn")]
    [SerializeField] private bool enableProximitySpawn = true;
    [SerializeField] private GameObject proximityEnemyPrefab;
    [SerializeField] private int proximityEnemiesPerSpawn = 1;
    [Tooltip("Seconds between each proximity spawn burst.")]
    [SerializeField] private float proximitySpawnInterval = 8f;
    [Tooltip("Radius around the player checked for nearby enemies.")]
    [SerializeField] private float noEnemyRadius = 12f;
    [Tooltip("Seconds without nearby enemies before proximity spawn triggers.")]
    [SerializeField] private float noEnemyTimeout = 5f;
    [Tooltip("Horizontal distance from player where proximity enemies spawn.")]
    [SerializeField] private float proximitySpawnDistance = 4f;
    [SerializeField] private LayerMask enemyLayer;

    private Transform _player;
    private float _spawnTimer;
    private float _noEnemyTimer;
    private float _proximityCooldown;

    void Start()
    {
        if (PlayerHealthController.instance != null)
            _player = PlayerHealthController.instance.transform;

        _spawnTimer = GetCurrentSingleInterval();
    }

    void Update()
    {
        _spawnTimer -= Time.deltaTime;
        if (_spawnTimer <= 0f)
        {
            _spawnTimer = GetCurrentSingleInterval();
            TrySpawnOne();
        }

        if (enableProximitySpawn)
            TickProximitySpawn();
    }

    void TrySpawnOne()
    {
        int maxAlive = RunDifficultyManager.instance != null
            ? RunDifficultyManager.instance.CurrentMaxAliveEnemies
            : int.MaxValue;

        if (EnemyHealthController.AliveCount >= maxAlive) return;
        if (spawnPoints.Length == 0) return;

        // Random start index so the same point isn't always preferred.
        int start = Random.Range(0, spawnPoints.Length);

        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var sp = spawnPoints[(start + i) % spawnPoints.Length];

            if (!sp.isEnabled || sp.enemyPrefab == null) continue;

            Vector2 pos = sp.transform.position;

            // Skip if too close to player.
            if (_player != null && Vector2.Distance(pos, _player.position) < minDistanceFromPlayer)
                continue;

            // Skip if something is already at this position.
            if (Physics2D.OverlapCircle(pos, spawnOverlapRadius, spawnBlockingLayers))
                continue;

            // Valid point — spawn exactly 1 enemy.
            var inst = Instantiate(sp.enemyPrefab, pos, Quaternion.identity);
            RunDifficultyManager.instance?.ApplyScalingIfEligible(sp.enemyPrefab, inst);
            return;
        }
        // No valid point found — skip this tick.
    }

    float GetCurrentSingleInterval() =>
        RunDifficultyManager.instance != null
            ? RunDifficultyManager.instance.CurrentSingleSpawnInterval
            : fallbackSpawnInterval;

    void TickProximitySpawn()
    {
        if (_player == null)
        {
            if (PlayerHealthController.instance != null)
                _player = PlayerHealthController.instance.transform;
            return;
        }

        bool hasNearbyEnemies = Physics2D.OverlapCircle(_player.position, noEnemyRadius, enemyLayer);

        if (hasNearbyEnemies)
        {
            _noEnemyTimer = 0f;
            return;
        }

        _noEnemyTimer += Time.deltaTime;
        if (_noEnemyTimer < noEnemyTimeout) return;

        _proximityCooldown -= Time.deltaTime;
        if (_proximityCooldown > 0f) return;

        SpawnNearPlayer();
        _proximityCooldown = Mathf.Max(0.1f, proximitySpawnInterval);
    }

    void SpawnNearPlayer()
    {
        if (proximityEnemyPrefab == null) return;

        int maxAlive = RunDifficultyManager.instance != null
            ? RunDifficultyManager.instance.CurrentMaxAliveEnemies
            : int.MaxValue;

        if (EnemyHealthController.AliveCount >= maxAlive) return;

        float dir = Random.value > 0.5f ? 1f : -1f;
        Vector2 spawnPos = (Vector2)_player.position + new Vector2(dir * proximitySpawnDistance, 0f);

        for (int i = 0; i < proximityEnemiesPerSpawn; i++)
        {
            if (EnemyHealthController.AliveCount >= maxAlive) break;
            var inst = Instantiate(proximityEnemyPrefab, spawnPos, Quaternion.identity);
            RunDifficultyManager.instance?.ApplyScalingIfEligible(proximityEnemyPrefab, inst);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!enableProximitySpawn) return;
        if (!Application.isPlaying || _player == null) return;
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.15f);
        Gizmos.DrawSphere(_player.position, noEnemyRadius);
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(_player.position, noEnemyRadius);
    }
#endif
}
