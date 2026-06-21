namespace WeaponDurability;

public static class WeaponDurabilitySettings
{
    public static bool DurabilityLossEnabled { get; set; } = true;
    public static int HitDurabilityLoss { get; set; } = 1;
    public static int RepairCostPerMissingPointDivisor { get; set; } = 80;
    public static bool RequireSmithingSkill { get; set; } = true;
    public static int SmithingSkillPerMissingPoint { get; set; } = 1;
    public static bool AllowCompanionRepair { get; set; } = true;
    public static bool EnableCompanionDurability { get; set; } = true;
    public static bool EnableRegularTroopDurability { get; set; } = true;
    public static bool RequireRepairMaterials { get; set; } = true;
    public static int MaterialUnitsPerMissingFiftyPercent { get; set; } = 1;
    public static bool ShowInventoryMarkers { get; set; } = true;
}
