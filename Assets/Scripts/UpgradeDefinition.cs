using UnityEngine;

public enum UpgradeTier { Low = 0, Mid = 1, High = 2 }

// One ScriptableObject asset per upgradeable stat (13 total).
// Create via: Assets > Create > Game > Upgrade Definition
[CreateAssetMenu(fileName = "UpgradeDefinition", menuName = "Game/Upgrade Definition", order = 2)]
public class UpgradeDefinition : ScriptableObject
{
    [Tooltip("Which stat this upgrade affects.")]
    public StatType statType;

    [Tooltip("Display name shown on the upgrade card (e.g. 'Damage').")]
    public string upgradeName;

    [Tooltip("Description template. Use {0} for the bonus value. E.g. 'Increase damage by {0}%'")]
    public string descriptionTemplate;

    [Tooltip("Bonus value per tier: [0]=Low, [1]=Mid, [2]=High. " +
             "For %-based stats this is the percentage number (e.g. 10 = 10%). " +
             "For Luck/XPGain this is the additive multiplier amount (e.g. 0.2).")]
    public float[] tierBonuses = new float[3] { 5f, 10f, 20f };

    // Returns the bonus value for the given tier.
    public float GetBonus(UpgradeTier tier) => tierBonuses[(int)tier];

    // Builds the formatted description string for a given tier.
    public string GetDescription(UpgradeTier tier)
    {
        if (string.IsNullOrEmpty(descriptionTemplate)) return string.Empty;
        float bonus = GetBonus(tier);
        return string.Format(descriptionTemplate, bonus);
    }
}
