using UnityEngine;

public interface IDifficultyScalableEnemy
{
    void ApplyDifficultyScaling(float healthMultiplier, float damageMultiplier, float speedMultiplier);
}

// Enemy prefab'larına ekle. Base değerleri Awake'te sibling component'lerden otomatik okur.
// RunDifficultyManager.ApplyScalingIfEligible() tarafından spawn sonrası bir kere çağrılır.
public class EnemyDifficultyScaler : MonoBehaviour, IDifficultyScalableEnemy
{
    private int   _baseHealth;
    private int   _baseDamage;
    private float _baseMoveSpeed;

    private void Awake()
    {
        // Serialized field değerleri Awake order'ından bağımsız olarak hazır gelir.
        var health = GetComponent<EnemyHealthController>();
        if (health != null) _baseHealth = health.BaseMaxHealth;

        var dmg = GetComponentInChildren<DamagePlayer>();
        if (dmg != null) _baseDamage = dmg.BaseDamageAmount;

        var groundChase = GetComponent<EnemyGroundChaseController>();
        var flyer       = GetComponent<EnemyFlyerController>();
        if      (groundChase != null) _baseMoveSpeed = groundChase.BaseMoveSpeed;
        else if (flyer       != null) _baseMoveSpeed = flyer.BaseMoveSpeed;
    }

    public void ApplyDifficultyScaling(float healthMult, float damageMult, float speedMult)
    {
        if (_baseHealth > 0)
            GetComponent<EnemyHealthController>()?.SetMaxHealth(Mathf.RoundToInt(_baseHealth * healthMult));

        if (_baseDamage > 0)
            GetComponentInChildren<DamagePlayer>()?.SetDamage(Mathf.RoundToInt(_baseDamage * damageMult));

        if (_baseMoveSpeed > 0f)
        {
            float scaledSpeed = _baseMoveSpeed * speedMult;
            GetComponent<EnemyGroundChaseController>()?.SetMoveSpeed(scaledSpeed);
            GetComponent<EnemyFlyerController>()?.SetMoveSpeed(scaledSpeed);
        }
    }
}
