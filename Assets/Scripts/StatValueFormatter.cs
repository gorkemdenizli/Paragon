using UnityEngine;

public static class StatValueFormatter
{
    public static string Format(StatType stat, float value)
    {
        return stat switch
        {
            StatType.CritChance    => value.ToString("0.#")  + "%",
            StatType.CritDamage    => value.ToString("0.#")  + "%",
            StatType.Luck          => value.ToString("0.##") + "x",
            StatType.XPGain        => value.ToString("0.##") + "x",
            StatType.ReloadSpeed   => value.ToString("0.##") + "s",
            StatType.RateOfFire    => value.ToString("0.#"),
            StatType.Accuracy      => value.ToString("0.#"),
            StatType.MaxHealth
            or StatType.MaxArmor
            or StatType.MagazineSize => Mathf.RoundToInt(value).ToString(),
            _                        => value.ToString("0.#")
        };
    }
}
