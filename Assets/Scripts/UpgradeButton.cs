using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Populates one upgrade card and handles tier-based sprite swapping.
// Attach to each upgrade button prefab instance.
public class UpgradeButton : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text currentValueText;
    [SerializeField] private TMP_Text projectedValueText;

    [Header("Button")]
    [SerializeField] private Button button;

    [Header("Tier Sprites (Normal + Hovered per tier)")]
    [SerializeField] private Sprite lowNormal;
    [SerializeField] private Sprite lowHovered;
    [SerializeField] private Sprite midNormal;
    [SerializeField] private Sprite midHovered;
    [SerializeField] private Sprite highNormal;
    [SerializeField] private Sprite highHovered;

    // Call this from UpgradeCanvasController to set up the card before displaying.
    public void Populate(UpgradeDefinition def, UpgradeTier tier, Action onClicked)
    {
        if (def == null) return;

        float bonus = def.GetBonus(tier);

        if (titleText       != null) titleText.text       = def.upgradeName;
        if (descriptionText != null) descriptionText.text = def.GetDescription(tier);

        // Current and projected values from PlayerStats
        if (PlayerStats.instance != null)
        {
            float current   = PlayerStats.instance.GetCurrentValue(def.statType);
            float projected = PlayerStats.instance.GetProjectedValue(def.statType, bonus);

            if (currentValueText   != null) currentValueText.text   = StatValueFormatter.Format(def.statType, current);
            if (projectedValueText != null) projectedValueText.text = StatValueFormatter.Format(def.statType, projected);
        }

        ApplyTierSprites(tier);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClicked?.Invoke());
    }

    private void ApplyTierSprites(UpgradeTier tier)
    {
        if (button == null) return;

        Sprite normal  = tier == UpgradeTier.High ? highNormal  : tier == UpgradeTier.Mid ? midNormal  : lowNormal;
        Sprite hovered = tier == UpgradeTier.High ? highHovered : tier == UpgradeTier.Mid ? midHovered : lowHovered;

        if (normal != null && button.image != null)
            button.image.sprite = normal;

        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = hovered,
            pressedSprite     = hovered,
            selectedSprite    = hovered
        };
    }
}
