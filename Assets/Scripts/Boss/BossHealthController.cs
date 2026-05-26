using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossHealthController : MonoBehaviour
{
    public static BossHealthController instance;

    private HitFlash _hitFlash;

    private void Awake()
    {
        instance = this;
        _hitFlash = GetComponent<HitFlash>();
    }

    [SerializeField] private Slider bossHealthSlider;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] public int currentHealth;
    [SerializeField] private int maxHealth;
    [SerializeField] private BossBattle theBoss;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentHealth = maxHealth;

        UpdateHealthSlider(currentHealth, maxHealth);
    }

    public void UpdateHealthSlider(int currentHealth, int maxHealth)
    {
        if (bossHealthSlider != null)
        {
            bossHealthSlider.maxValue = maxHealth;
            bossHealthSlider.value = currentHealth;
        }

        if (healthText != null)
        {
            healthText.text = currentHealth + " / " + maxHealth;
        }
    }

    // Returns true if this hit killed the boss.
    public bool DamageBoss(int damageAmount)
    {
        _hitFlash?.Flash();

        currentHealth -= damageAmount;

        bool killed = false;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            theBoss.EndBattle();
            killed = true;
        }

        UpdateHealthSlider(currentHealth, maxHealth);
        return killed;
    }


}
