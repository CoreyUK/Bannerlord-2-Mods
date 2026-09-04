namespace VeteranTroops;

/// <summary>
/// Veterancy bands. The value is the number of tiers above Green, which is what
/// the effect multipliers scale by.
/// </summary>
public enum VeteranTier
{
    Green = 0,
    Seasoned = 1,
    Veteran = 2,
    Hardened = 3
}

public static class VeteranTierExtensions
{
    public static string DisplayName(this VeteranTier tier)
    {
        return tier switch
        {
            VeteranTier.Seasoned => "Seasoned",
            VeteranTier.Veteran => "Veteran",
            VeteranTier.Hardened => "Hardened",
            _ => "Green"
        };
    }

    /// <summary>Tier for an average points-per-soldier value under the given settings.</summary>
    public static VeteranTier FromAveragePoints(float averagePoints, VeteranTroopsSettings settings)
    {
        if (averagePoints >= settings.HardenedThreshold)
        {
            return VeteranTier.Hardened;
        }

        if (averagePoints >= settings.VeteranThreshold)
        {
            return VeteranTier.Veteran;
        }

        if (averagePoints >= settings.SeasonedThreshold)
        {
            return VeteranTier.Seasoned;
        }

        return VeteranTier.Green;
    }
}
