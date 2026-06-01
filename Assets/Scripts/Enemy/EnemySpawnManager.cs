using UnityEngine;

public class EnemySpawnManager : MonoBehaviour
{
    public static EnemySpawnManager instance;

    [Header("Spawn Points")]
    [SerializeField] private EnemySpawnPoint[] spawnPoints;

    [Header("Spawn Validation")]
    [Tooltip("Spawn positions closer than this to the player are skipped.")]
    [SerializeField] private float minDistanceFromPlayer = 5f;
    [Tooltip("Radius of the overlap check at the spawn position.")]
    [SerializeField] private float spawnOverlapRadius = 0.5f;
    [Tooltip("Layers checked for overlap at spawn position (e.g. Enemy + Ground + Obstacle).")]
    [SerializeField] private LayerMask spawnBlockingLayers;

    [Header("Boss Battle")]
    [Tooltip("During boss battle, proximity spawn stops when this many enemies are alive (regardless of RunDifficultyManager's cap).")]
    [SerializeField] private int bossBattleMaxAlive = 20;
    [Tooltip("Açıksa boss savaşında spawn olan düşmanların ladder kullanımı kapatılır. Default kapalı = boss savaşında da merdiven kullanırlar.")]
    [SerializeField] private bool disableLaddersDuringBossBattle = false;

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
    [Tooltip("Minimum horizontal distance from player where proximity enemies spawn.")]
    [SerializeField] private float proximitySpawnMinDistance = 5f;
    [Tooltip("Maximum horizontal distance from player where proximity enemies spawn.")]
    [SerializeField] private float proximitySpawnMaxDistance = 9f;
    [SerializeField] private LayerMask enemyLayer;

    private Transform _player;
    private float _noEnemyTimer;
    private float _proximityCooldown;

    // Boss battle state
    private bool _bossBattleMode;
    private int  _bossAmmoDropMin;
    private int  _bossAmmoDropMax;

    public static int BossAmmoDropMin => instance != null && instance._bossBattleMode ? instance._bossAmmoDropMin : -1;
    public static int BossAmmoDropMax => instance != null && instance._bossBattleMode ? instance._bossAmmoDropMax : -1;

    void Awake()
    {
        instance = this;
    }

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

    public void StartBossBattleMode(int ammoMin, int ammoMax, float firstSpawnDelay = 6f)
    {
        _bossBattleMode  = true;
        _bossAmmoDropMin = ammoMin;
        _bossAmmoDropMax = ammoMax;

        foreach (var sp in spawnPoints)
            if (sp != null) sp.isEnabled = false;

        // Boss başlayınca arenadaki tüm mevcut düşmanları (walker + flyer) temizle.
        foreach (var e in FindObjectsByType<EnemyHealthController>(FindObjectsSortMode.None))
            Destroy(e.gameObject);

        enableProximitySpawn = true;
        _proximityCooldown   = firstSpawnDelay;
    }

    void Update()
    {
        foreach (var sp in spawnPoints)
        {
            if (!sp.isEnabled || sp.enemyPrefab == null) continue;

            sp._timer -= Time.deltaTime;
            if (sp._timer > 0f) continue;

            if (TypeBelowCap(sp.enemyPrefab))
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

        // Boss battle mode: skip "no nearby enemies" check entirely
        if (!_bossBattleMode)
        {
            bool hasNearbyEnemies = Physics2D.OverlapBox(_player.position, noEnemyBoxSize, 0f, enemyLayer);
            if (hasNearbyEnemies)
            {
                _noEnemyTimer = 0f;
                return;
            }
            _noEnemyTimer += Time.deltaTime;
            if (_noEnemyTimer < noEnemyTimeout) return;
        }

        _proximityCooldown -= Time.deltaTime;
        if (_proximityCooldown > 0f) return;

        SpawnNearPlayer();
        _proximityCooldown = Mathf.Max(0.1f, proximitySpawnInterval);
    }

    void SpawnNearPlayer()
    {
        if (proximityEnemyPrefab == null) return;

        for (int i = 0; i < proximityEnemiesPerSpawn; i++)
        {
            // Boss-mode toplam tavanı + her zaman per-type cap. (AliveCount/AliveWalkers/AliveFlyers
            // her Instantiate'te anında güncellendiğinden her iterasyonda yeniden kontrol edilir.)
            if (_bossBattleMode && EnemyHealthController.AliveCount >= bossBattleMaxAlive) break;
            if (!TypeBelowCap(proximityEnemyPrefab)) break;

            float eachDir  = Random.value > 0.5f ? 1f : -1f;
            float eachDist = Random.Range(proximitySpawnMinDistance, proximitySpawnMaxDistance);
            Vector2 eachPos = (Vector2)_player.position + new Vector2(eachDir * eachDist, 0f);
            var inst = Instantiate(proximityEnemyPrefab, eachPos, Quaternion.identity);
            RunDifficultyManager.instance?.ApplyScalingIfEligible(proximityEnemyPrefab, inst);
            if (_bossBattleMode && disableLaddersDuringBossBattle)
                inst.GetComponent<EnemyLadderNavigator>()?.SetUseLadders(false);
        }
    }

    // Verilen prefab'ın tipine (walker/flyer) göre o tipin alive sayısı cap'in altında mı?
    bool TypeBelowCap(GameObject prefab)
    {
        var rdm    = RunDifficultyManager.instance;
        bool flyer = prefab.GetComponent<EnemyFlyerController>() != null;
        int alive  = flyer ? EnemyHealthController.AliveFlyers : EnemyHealthController.AliveWalkers;
        int max    = flyer
            ? (rdm != null ? rdm.CurrentMaxAliveFlyers  : int.MaxValue)
            : (rdm != null ? rdm.CurrentMaxAliveWalkers : int.MaxValue);
        return alive < max;
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
