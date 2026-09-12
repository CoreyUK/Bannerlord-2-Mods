namespace FieldFortifications;

/// <summary>Campaign-to-mission handoff and the fixed tuning values.</summary>
public static class FortificationState
{
    /// <summary>Works bought for the current encounter. Cleared when the player's map event ends.</summary>
    public static bool Barricades;
    public static bool Ballista;
    public static bool Mangonel;

    public static bool AnyPending => Barricades || Ballista || Mangonel;

    public static void Clear()
    {
        Barricades = false;
        Ballista = false;
        Mangonel = false;
    }

    /// <summary>Scene prefab spawned for each obstacle segment.</summary>
    public const string SegmentPrefab = "siege_barricade_a";

    /// <summary>Dynamic blocker navmesh piece attached to each segment so the AI paths around it.</summary>
    public const string SegmentNavMeshPrefab = "siege_barricade_state1234_destroyed_blocker_dnm";

    public const string BallistaPrefab = "ballista_a";
    public const string MangonelPrefab = "mangonel_a";

    /// <summary>Card icons, borrowed from the game's own UI.</summary>
    public const string BarricadeIcon = @"General\Icons\Walls";
    public const string BallistaIcon = @"Order\SiegeIcons\siege_ballista";
    public const string MangonelIcon = @"Order\SiegeIcons\siege_catapult";

    /// <summary>Alpha applied to placement ghosts.</summary>
    public const float GhostAlpha = 0.5f;

    /// <summary>Degrees per second the arrow keys turn a held item.</summary>
    public const float PlacementTurnSpeed = 60f;

    /// <summary>Furthest from the infantry spawn an item may be placed, in metres.</summary>
    public const float MaxPlacementDistance = 200f;

    /// <summary>Seconds a mangonel may sit waiting for a crew to reload before it reloads itself.</summary>
    public const float AutoReloadSeconds = 8f;

    /// <summary>Metres beyond the outermost barricade where an engine sits by default.</summary>
    public const float EngineFlankOffset = 10f;

    /// <summary>Number of segments in the line.</summary>
    public const int SegmentCount = 4;

    /// <summary>Metres between segment centres: roughly one barricade width plus a gap the AI can funnel through.</summary>
    public const float SegmentPitch = 8f;

    /// <summary>Half the width of one barricade's spiked face, for the cavalry hit test.</summary>
    public const float SegmentHalfWidth = 2.6f;

    /// <summary>Half the depth of the spiked zone in front of and behind a barricade's centre line.</summary>
    public const float SegmentHalfDepth = 1.6f;

    /// <summary>Horses slower than this (metres per second) just bump the stakes and take nothing.</summary>
    public const float SpikeMinSpeed = 3.5f;

    /// <summary>Damage to a horse hitting the spikes is SpikeBaseDamage plus SpikeDamagePerSpeed times its speed.</summary>
    public const float SpikeBaseDamage = 20f;
    public const float SpikeDamagePerSpeed = 6f;
    public const float SpikeMaxDamage = 90f;

    /// <summary>Seconds before the same horse can be hurt by the spikes again.</summary>
    public const float SpikeCooldown = 1.5f;

    /// <summary>Metres in front of the infantry spawn line where the works go when the deployment boundary is unknown.</summary>
    public const float DistanceInFront = 35f;

    /// <summary>Closest the works may sit to the spawn line, even on a cramped map.</summary>
    public const float MinDistanceInFront = 20f;

    /// <summary>How far forward to look for the boundary edge before giving up and using the fixed distance.</summary>
    public const float MaxDistanceInFront = 150f;

    /// <summary>Metres beyond the boundary edge, so the blocked ground can never overlap a deployment slot.</summary>
    public const float BoundaryMargin = 4f;
}
