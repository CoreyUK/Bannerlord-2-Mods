using System;
using System.Linq;
using System.Reflection;

namespace VeteranTroops;

/// <summary>
/// Plain settings object used by the mod. When the MCM bridge assembly is
/// loaded its values are read through reflection so the main assembly never
/// needs a hard reference to MCM. Defaults apply otherwise.
/// </summary>
public sealed class VeteranTroopsSettings
{
    /// <summary>Base points a surviving soldier earns from an average-sized battle.</summary>
    public int PointsPerBattle { get; set; } = 40;

    /// <summary>Average points per soldier needed for each tier.</summary>
    public int SeasonedThreshold { get; set; } = 100;

    public int VeteranThreshold { get; set; } = 250;

    public int HardenedThreshold { get; set; } = 500;

    /// <summary>Extra health limit per tier above Green, as a percentage.</summary>
    public float HealthBonusPercentPerTier { get; set; } = 5f;

    /// <summary>Extra starting battle morale per tier above Green (vanilla morale runs 0 to 100).</summary>
    public float MoraleBonusPerTier { get; set; } = 5f;

    /// <summary>Fraction of a stack's average points that follows troops when they upgrade.</summary>
    public float UpgradeCarryOver { get; set; } = 0.5f;

    /// <summary>Show a message when a stack reaches a new tier.</summary>
    public bool ShowTierMessages { get; set; } = true;

    internal static VeteranTroopsSettings Effective => TryReadMcmSettings() ?? new VeteranTroopsSettings();

    private static VeteranTroopsSettings? TryReadMcmSettings()
    {
        try
        {
            Type? settingsType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("VeteranTroops.Mcm.VeteranTroopsMcmSettings", false))
                .FirstOrDefault(type => type != null);

            object? instance = settingsType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetValue(null);
            if (instance == null)
            {
                return null;
            }

            var defaults = new VeteranTroopsSettings();
            var settings = new VeteranTroopsSettings
            {
                PointsPerBattle = (int)GetPropertyValue(instance, nameof(PointsPerBattle), defaults.PointsPerBattle),
                SeasonedThreshold = (int)GetPropertyValue(instance, nameof(SeasonedThreshold), defaults.SeasonedThreshold),
                VeteranThreshold = (int)GetPropertyValue(instance, nameof(VeteranThreshold), defaults.VeteranThreshold),
                HardenedThreshold = (int)GetPropertyValue(instance, nameof(HardenedThreshold), defaults.HardenedThreshold),
                HealthBonusPercentPerTier = (float)GetPropertyValue(instance, nameof(HealthBonusPercentPerTier), defaults.HealthBonusPercentPerTier),
                MoraleBonusPerTier = (float)GetPropertyValue(instance, nameof(MoraleBonusPerTier), defaults.MoraleBonusPerTier),
                UpgradeCarryOver = (float)GetPropertyValue(instance, nameof(UpgradeCarryOver), defaults.UpgradeCarryOver),
                ShowTierMessages = (bool)GetPropertyValue(instance, nameof(ShowTierMessages), defaults.ShowTierMessages)
            };

            // Keep the bands ordered even if the user drags the sliders past each other.
            settings.VeteranThreshold = Math.Max(settings.VeteranThreshold, settings.SeasonedThreshold + 1);
            settings.HardenedThreshold = Math.Max(settings.HardenedThreshold, settings.VeteranThreshold + 1);
            return settings;
        }
        catch
        {
            return null;
        }
    }

    private static object GetPropertyValue(object instance, string propertyName, object fallback)
    {
        return instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance) ?? fallback;
    }
}
