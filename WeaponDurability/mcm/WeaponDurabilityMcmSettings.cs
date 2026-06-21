using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using WeaponDurability;

namespace WeaponDurability.Mcm;

public sealed class WeaponDurabilityMcmSettings : AttributeGlobalSettings<WeaponDurabilityMcmSettings>
{
    public override string Id => "WeaponDurability";
    public override string DisplayName => "Weapon Durability";
    public override string FolderName => "WeaponDurability";
    public override string FormatType => "json";

    [SettingPropertyBool("Enable Durability Loss", Order = 0, RequireRestart = false)]
    [SettingPropertyGroup("Durability", GroupOrder = 0)]
    public bool DurabilityLossEnabled
    {
        get => WeaponDurabilitySettings.DurabilityLossEnabled;
        set => WeaponDurabilitySettings.DurabilityLossEnabled = value;
    }

    [SettingPropertyInteger("Durability Lost Per Hit", 1, 10, "0", Order = 1, RequireRestart = false)]
    [SettingPropertyGroup("Durability")]
    public int HitDurabilityLoss
    {
        get => WeaponDurabilitySettings.HitDurabilityLoss;
        set => WeaponDurabilitySettings.HitDurabilityLoss = value;
    }

    [SettingPropertyBool("Show Inventory Markers", Order = 2, RequireRestart = false)]
    [SettingPropertyGroup("Durability")]
    public bool ShowInventoryMarkers
    {
        get => WeaponDurabilitySettings.ShowInventoryMarkers;
        set => WeaponDurabilitySettings.ShowInventoryMarkers = value;
    }

    [SettingPropertyBool("Enable Companion Durability", Order = 3, RequireRestart = false, HintText = "Companion equipped weapons lose durability and can be repaired like player weapons.")]
    [SettingPropertyGroup("Durability")]
    public bool EnableCompanionDurability
    {
        get => WeaponDurabilitySettings.EnableCompanionDurability;
        set => WeaponDurabilitySettings.EnableCompanionDurability = value;
    }

    [SettingPropertyBool("Enable Regular Troop Durability", Order = 4, RequireRestart = false, HintText = "Regular troop weapons use party-wide durability by troop type and are repaired with denars only.")]
    [SettingPropertyGroup("Durability")]
    public bool EnableRegularTroopDurability
    {
        get => WeaponDurabilitySettings.EnableRegularTroopDurability;
        set => WeaponDurabilitySettings.EnableRegularTroopDurability = value;
    }

    [SettingPropertyInteger("Repair Cost Divisor", 10, 250, "0", Order = 0, RequireRestart = false, HintText = "Higher values make repairs cheaper.")]
    [SettingPropertyGroup("Repair", GroupOrder = 1)]
    public int RepairCostPerMissingPointDivisor
    {
        get => WeaponDurabilitySettings.RepairCostPerMissingPointDivisor;
        set => WeaponDurabilitySettings.RepairCostPerMissingPointDivisor = value;
    }

    [SettingPropertyBool("Require Smithing Skill", Order = 1, RequireRestart = false)]
    [SettingPropertyGroup("Repair")]
    public bool RequireSmithingSkill
    {
        get => WeaponDurabilitySettings.RequireSmithingSkill;
        set => WeaponDurabilitySettings.RequireSmithingSkill = value;
    }

    [SettingPropertyInteger("Smithing Required Per Missing %", 0, 5, "0", Order = 2, RequireRestart = false, HintText = "At 1, a 50% weapon requires 50 Smithing. Set 0 to make the check cosmetic.")]
    [SettingPropertyGroup("Repair")]
    public int SmithingSkillPerMissingPoint
    {
        get => WeaponDurabilitySettings.SmithingSkillPerMissingPoint;
        set => WeaponDurabilitySettings.SmithingSkillPerMissingPoint = value;
    }

    [SettingPropertyBool("Allow Companion Repairs", Order = 3, RequireRestart = false, HintText = "Uses the best-smithing hero in the party when repairing.")]
    [SettingPropertyGroup("Repair")]
    public bool AllowCompanionRepair
    {
        get => WeaponDurabilitySettings.AllowCompanionRepair;
        set => WeaponDurabilitySettings.AllowCompanionRepair = value;
    }

    [SettingPropertyBool("Require Repair Materials", Order = 4, RequireRestart = false, HintText = "Requires smithing materials based on weapon tier and damage.")]
    [SettingPropertyGroup("Repair")]
    public bool RequireRepairMaterials
    {
        get => WeaponDurabilitySettings.RequireRepairMaterials;
        set => WeaponDurabilitySettings.RequireRepairMaterials = value;
    }

    [SettingPropertyInteger("Material Units Per Missing 50%", 1, 5, "0", Order = 5, RequireRestart = false)]
    [SettingPropertyGroup("Repair")]
    public int MaterialUnitsPerMissingFiftyPercent
    {
        get => WeaponDurabilitySettings.MaterialUnitsPerMissingFiftyPercent;
        set => WeaponDurabilitySettings.MaterialUnitsPerMissingFiftyPercent = value;
    }
}
