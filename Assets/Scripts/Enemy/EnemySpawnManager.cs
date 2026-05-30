using UnityEngine;

public class EnemySpawnManager : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private EnemySpawnPoint[] spawnPoints;

    [Header("Spawn Validation")]
    [Tooltip("Spawn positions closer than this to the player are skipped.")]
    [SerializeField] private float minDistanceFromPlayer = 5f;
    [Tooltip("Radius of the overlap check at the spawn position.")]
    [SerializeField] private float spawnOverlapRadius = 0.5f;
    [Tooltip("Layers checked for overlap at spawn position (e.g. Enemy + Ground + Obstacle).")]
    [SerializeField] private LayerMask spawnBlockingLayers;

    [Header("Player Proximity Spawn")]
    [SerializeField] private bool enableProximitySpawn = true;
    [SerializeField] private GameObject proximityEnemyPrefab;
    [SerializeField] private int proximityEnemiesPerSpawn = 1;
    [Tooltip("Seconds between each proximity spawn burst.")]
    [SerializeField] private float proximitySpawnInterval = 8f;
    [Tooltip("Box size (width, height) around the player checked for nearby enemies.")]
    [SerializeField] private Vector2 noEnemyBoxSize = new Vector2(24f, 10f);
    [Tooltip("Seconds without nearby enemies before proximity spawn triggers.")]
    [SerializeField] private float noEnemyTimeout = 5f;
    [Tooltip("Horizontal distance from player where proximity enemies spawn.")]
    [SerializeField] private float proximitySpawnDistance = 4f;
    [SerializeField] private LayerMask enemyLayer;

    private Transform _player;
    private float _noEnemyTimer;
    private float _proximityCooldown;

    void Start()
    {
        if (PlayerHealthController.instance != null)
            _player = PlayerHealthController.instance.transform;

        // Stagger initial timers so all points don't fire simultaneously.
        foreach (var sp in spawnPoints)
        {
            if (sp != null)
                sp._timer = Random.Range(0f, sp.GetCurrentSingleInterval());
        }
    }

    void Update()
    {
        int maxAlive = RunDifficultyManager.instance?.CurrentMaxAliveEnemies ?? int.MaxValue;

        foreach (var sp in spawnPoints)
        {
            if (!sp.isEnabled || sp.enemyPrefab == null) continue;

            sp._timer -= Time.deltaTime;
            if (sp._timer > 0f) continue;

            if (EnemyHealthController.AliveCount < maxAlive)
                TrySpawnFrom(sp);

            sp._timer = sp.GetCurrentSingleInterval();
        }

        if (enableProximitySpawn)
            TickProximitySpawn();
    }

    void TrySpawnFrom(EnemySpawnPoint sp)
    {
        Vector2 pos = sp.transform.position;
        if (_player != null && Vector2.Distance(pos, _player.position) < minDistanceFromPlayer) return;
        if (Physics2D.OverlapCircle(pos, spawnOverlapRadius, spawnBlockingLayers)) return;

        var inst = Instantiate(sp.enemyPrefab, pos, Quaternion.identity);
        RunDifficultyManager.instance?.ApplyScalingIfEligible(sp.enemyPrefab, inst);
    }

    void TickProximitySpawn()
    {
        if (_player == null)
        {
            if (PlayerHealthController.instance != null)
                _player = PlayerHealthController.instance.transform;
            return;
        }

        bool hasNearbyEnemies = Physics2D.OverlapBox(_player.position, noEnemyBoxSize, 0f, enemyLayer);

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

        int maxAlive = RunDifficultyManager.instance?.CurrentMaxAliveEnemies ?? int.MaxValue;
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
        Gizmos.DrawCube(_player.position, noEnemyBoxSize);
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireCube(_player.position, noEnemyBoxSize);
    }
#endif
}
