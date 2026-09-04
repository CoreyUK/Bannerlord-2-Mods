using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace VeteranTroops.Mcm;

public sealed class VeteranTroopsMcmSettings : AttributeGlobalSettings<VeteranTroopsMcmSettings>
{
    public override string Id => "VeteranTroops";
    public override string DisplayName => "Veteran Troops";
    public override string FolderName => "VeteranTroops";
    public override string FormatType => "json";

    [SettingPropertyInteger("Points Per Battle", 5, 200, "0", Order = 0, RequireRestart = false, HintText = "Base points a surviving soldier earns from an average-sized battle. Bigger battles, sieges and taking casualties all scale this up; defeats halve it.")]
    [SettingPropertyGroup("Progression", GroupOrder = 0)]
    public int PointsPerBattle { get; set; } = 40;

    [SettingPropertyInteger("Seasoned Threshold", 10, 2000, "0", Order = 1, RequireRestart = false, HintText = "Average points per soldier a stack needs to become Seasoned.")]
    [SettingPropertyGroup("Progression")]
    public int SeasonedThreshold { get; set; } = 100;

    [SettingPropertyInteger("Veteran Threshold", 10, 4000, "0", Order = 2, RequireRestart = false, HintText = "Average points per soldier a stack needs to become Veteran.")]
    [SettingPropertyGroup("Progression")]
    public int VeteranThreshold { get; set; } = 250;

    [SettingPropertyInteger("Hardened Threshold", 10, 8000, "0", Order = 3, RequireRestart = false, HintText = "Average points per soldier a stack needs to become Hardened.")]
    [SettingPropertyGroup("Progression")]
    public int HardenedThreshold { get; set; } = 500;

    [SettingPropertyFloatingInteger("Upgrade Carry-Over", 0f, 1f, "#0%", Order = 4, RequireRestart = false, HintText = "Share of a stack's experience that follows troops when they are upgraded to the next tier.")]
    [SettingPropertyGroup("Progression")]
    public float UpgradeCarryOver { get; set; } = 0.5f;

    [SettingPropertyFloatingInteger("Health Bonus Per Tier", 0f, 25f, "0.0'%'", Order = 0, RequireRestart = false, HintText = "Extra hit points per veterancy tier, applied when troops spawn in battle.")]
    [SettingPropertyGroup("Effects", GroupOrder = 1)]
    public float HealthBonusPercentPerTier { get; set; } = 5f;

    [SettingPropertyFloatingInteger("Morale Bonus Per Tier", 0f, 30f, "0.0", Order = 1, RequireRestart = false, HintText = "Extra starting battle morale per veterancy tier. Vanilla morale runs from 0 to 100.")]
    [SettingPropertyGroup("Effects")]
    public float MoraleBonusPerTier { get; set; } = 5f;

    [SettingPropertyBool("Show Tier Messages", Order = 0, RequireRestart = false, HintText = "Announce it in the log when a stack reaches a new tier.")]
    [SettingPropertyGroup("Notifications", GroupOrder = 2)]
    public bool ShowTierMessages { get; set; } = true;
}
