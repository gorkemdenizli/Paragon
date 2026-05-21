using UnityEngine;

public class EnemyDrops : MonoBehaviour
{
    [Header("Health pickup")]
    [SerializeField] private bool shouldDropHealthPickup = true;
    [Range(0f, 1f)]
    [SerializeField] private float healthPickupChance;
    [SerializeField] private GameObject healthPickupPrefab;

    [Header("Ammo Orbs")]
    [SerializeField] private bool dropAmmoOrbs = true;
    [SerializeField] private GameObject ammoOrbPrefab;
    [Min(0)] [SerializeField] private int minOrbCount = 0;
    [Min(0)] [SerializeField] private int maxOrbCount = 5;
    [SerializeField] private float spawnSpreadRadius = 0.4f;
    [Min(1)] [SerializeField] private int minAmmoPerOrb = 1;
    [Min(1)] [SerializeField] private int maxAmmoPerOrb = 5;
    [SerializeField] private float orbHomingSpeed = 10f;
    [SerializeField] private float orbAbsorbDistance = 0.35f;
    [Tooltip("Seconds after spawn before orbs fly toward the player (e.g. after death VFX).")]
    [SerializeField] private float orbHomingDelay = 0.5f;
    [Tooltip("Outward burst speed from the death point into spawn spread radius.")]
    [SerializeField] private float orbBurstSpeed = 8f;

    void OnValidate()
    {
        if (maxOrbCount < minOrbCount)
            maxOrbCount = minOrbCount;
        if (maxAmmoPerOrb < minAmmoPerOrb)
            maxAmmoPerOrb = minAmmoPerOrb;
    }

    public void TrySpawnDrops(Vector3 position)
    {
        if (shouldDropHealthPickup && healthPickupPrefab != null && Random.value <= healthPickupChance)
            Instantiate(healthPickupPrefab, position, Quaternion.identity);

        TrySpawnAmmoOrbs(position);
    }

    void TrySpawnAmmoOrbs(Vector3 position)
    {
        if (!dropAmmoOrbs || ammoOrbPrefab == null)
            return;

        Transform absorbTarget = PlayerHealthController.instance != null
            ? PlayerHealthController.instance.AmmoAbsorbPoint
            : null;

        int count = Random.Range(minOrbCount, maxOrbCount + 1);

        for (int i = 0; i < count; i++)
        {
            int ammo = Random.Range(minAmmoPerOrb, maxAmmoPerOrb + 1);

            GameObject orbObj = Instantiate(ammoOrbPrefab, position, Quaternion.identity);
            if (orbObj.TryGetComponent(out AmmoOrbPickup orb))
            {
                orb.Init(
                    ammo,
                    absorbTarget,
                    orbHomingSpeed,
                    orbAbsorbDistance,
                    position,
                    spawnSpreadRadius,
                    orbBurstSpeed,
                    orbHomingDelay);
            }
        }
    }
}
