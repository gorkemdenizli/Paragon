using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossSpawnerController : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("'Press E to spawn boss' yazısı — aktif/inaktif edilir.")]
    [SerializeField] private GameObject promptText;

    [Header("Portal")]
    [SerializeField] private Animator portalAnimator;
    [Tooltip("E basıldıktan kaç saniye sonra boss spawn olur.")]
    [SerializeField] private float spawnDelay = 3.5f;

    [Header("Boss")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform  bossSpawnPoint;

    [Header("Boss UI (Scene References)")]
    [Tooltip("Sahnede varsayılan olarak inaktif olan boss health bar root. Boss spawn olunca aktif edilir.")]
    [SerializeField] private GameObject bossHealthBarRoot;
    [Tooltip("Sahnedeki boss health slider. Inspector'dan atanır, prefab'a bağlı olması gerekmez.")]
    [SerializeField] private Slider   bossHealthSlider;
    [Tooltip("Sahnedeki 'X / X' health text. Inspector'dan atanır.")]
    [SerializeField] private TMP_Text bossHealthText;

    [Header("Boss Health Scaling")]
    [SerializeField] private int   bossBaseHealth                = 3000;
    [SerializeField] private float bossHealthGrowthPerDifficulty = 1.10f;
    [SerializeField] private float bossMaxHealthMultiplier        = 5f;

    [Header("Boss Damage Scaling")]
    [SerializeField] private int   bossBaseDamage                = 10;
    [SerializeField] private float bossDamageGrowthPerDifficulty = 1.07f;
    [SerializeField] private float bossDamageMaxMultiplier        = 2.5f;

    [Header("Phase Thresholds (scaled max health fraction, 0-1)")]
    [Tooltip("Bu canın altında Phase 2 başlar. Varsayılan: ~%83")]
    [SerializeField] private float phase2HealthFraction = 0.833f;
    [Tooltip("Bu canın altında Phase 3 başlar. Varsayılan: ~%33")]
    [SerializeField] private float phase3HealthFraction = 0.333f;

    [Header("Boss Battle Ammo Drop")]
    [Tooltip("Boss spawn olduktan kaç saniye sonra ilk EnemyWalker gelsin.")]
    [SerializeField] private float bossFirstSpawnDelay = 6f;
    [Tooltip("Boss battle'da EnemyWalker orb'larından alınacak minimum mermi.")]
    [SerializeField] private int ammoDropMin = 20;
    [Tooltip("Boss battle'da EnemyWalker orb'larından alınacak maksimum mermi.")]
    [SerializeField] private int ammoDropMax = 30;

    private bool _playerInRange;
    private bool _bossSpawned;

    // ── Trigger zone ─────────────────────────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = true;
        if (promptText != null && !_bossSpawned)
            promptText.SetActive(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInRange = false;
        if (promptText != null)
            promptText.SetActive(false);
    }

    // ── Called by PlayerController on Interact press ─────────────────────────────

    public void HandlePlayerInteract()
    {
        if (!_playerInRange || _bossSpawned) return;
        TriggerBossSpawn();
    }

    // ── Spawn sequence ────────────────────────────────────────────────────────────

    void TriggerBossSpawn()
    {
        _bossSpawned = true;
        if (promptText != null) promptText.SetActive(false);
        if (portalAnimator != null) portalAnimator.SetTrigger("PortalOpening");
        StartCoroutine(SpawnAfterDelay());
    }

    IEnumerator SpawnAfterDelay()
    {
        yield return new WaitForSeconds(spawnDelay);
        if (portalAnimator != null) portalAnimator.SetTrigger("PortalOpen");
        SpawnBoss();
    }

    void SpawnBoss()
    {
        if (bossPrefab == null || bossSpawnPoint == null)
        {
            Debug.LogError("[BossSpawner] bossPrefab veya bossSpawnPoint atanmamış!", this);
            return;
        }

        int level = RunDifficultyManager.instance?.CurrentDifficultyLevel ?? 0;

        int scaledHealth = Mathf.RoundToInt(bossBaseHealth *
            Mathf.Min(Mathf.Pow(bossHealthGrowthPerDifficulty, level), bossMaxHealthMultiplier));
        int scaledDamage = Mathf.RoundToInt(bossBaseDamage *
            Mathf.Min(Mathf.Pow(bossDamageGrowthPerDifficulty, level), bossDamageMaxMultiplier));

        Debug.Log($"[BossSpawner] Difficulty {level} → Health {scaledHealth}, Damage {scaledDamage}");

        if (bossHealthBarRoot != null) bossHealthBarRoot.SetActive(true);

        GameObject go = Instantiate(bossPrefab, bossSpawnPoint.position, Quaternion.identity);

        if (go.TryGetComponent<BossHealthController>(out var hc))
            hc.Initialize(scaledHealth, bossHealthSlider, bossHealthText);
        else
            Debug.LogWarning("[BossSpawner] Boss prefab'da BossHealthController bulunamadı!", this);

        if (go.TryGetComponent<BossBattle>(out var battle))
            battle.SetupDifficulty(scaledHealth, phase2HealthFraction, phase3HealthFraction, scaledDamage);
        else
            Debug.LogWarning("[BossSpawner] Boss prefab'da BossBattle bulunamadı!", this);

        EnemySpawnManager.instance?.StartBossBattleMode(ammoDropMin, ammoDropMax, bossFirstSpawnDelay);
    }
}
