using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace VeteranTroops;

/// <summary>
/// Tracks campaign experience for each troop type in the player's main party.
///
/// Bannerlord has no per-soldier identity, so memory lives on the stack: every
/// troop type carries a pool of "veteran points" and its tier is the average
/// per soldier. Survivors of a battle earn points, the fallen take their share
/// of the pool with them, recruits dilute the average, and upgrades carry part
/// of it to the next tier. Only dictionaries of primitives keyed by troop
/// StringId are saved, so the mod is safe to add or remove mid-campaign.
/// </summary>
public sealed class VeteranTroopsBehavior : CampaignBehaviorBase
{
    private const float MinBattleSizeFactor = 0.5f;
    private const float MaxBattleSizeFactor = 2f;
    private const int ReferenceEnemyCount = 100;
    private const float DefeatFactor = 0.5f;
    private const float BloodiedFactor = 1.5f;
    private const float SiegeFactor = 1.5f;
    private const float HideoutFactor = 1.25f;

    /// <summary>Total points per troop StringId for the main party.</summary>
    private Dictionary<string, int> _pointsByTroop = new();

    /// <summary>Battles a stack has come through, for a later chronicle view.</summary>
    private Dictionary<string, int> _battlesByTroop = new();

    /// <summary>Roster counts at the last reconciliation, used to scale points when troops leave.</summary>
    private Dictionary<string, int> _lastKnownCounts = new();

    /// <summary>Runtime only. Lets tier messages fire once per promotion.</summary>
    private readonly Dictionary<string, VeteranTier> _lastTierByTroop = new();

    private VeteranTroopsSettings _settings = new();

    public static VeteranTroopsBehavior? Instance =>
        Campaign.Current?.CampaignBehaviorManager?.GetBehavior<VeteranTroopsBehavior>();

    public VeteranTroopsSettings Settings => _settings;

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
        CampaignEvents.PlayerUpgradedTroopsEvent.AddNonSerializedListener(this, OnPlayerUpgradedTroops);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("vt_points_by_troop", ref _pointsByTroop);
        dataStore.SyncData("vt_battles_by_troop", ref _battlesByTroop);
        dataStore.SyncData("vt_last_known_counts", ref _lastKnownCounts);

        // Older saves, or a save where the dictionaries were never written, come back null.
        _pointsByTroop ??= new Dictionary<string, int>();
        _battlesByTroop ??= new Dictionary<string, int>();
        _lastKnownCounts ??= new Dictionary<string, int>();
    }

    // ------------------------------------------------------------------ queries

    /// <summary>Average veteran points per soldier for a stack in the main party. Zero when untracked.</summary>
    public float GetAveragePoints(CharacterObject troop)
    {
        if (troop == null || !_pointsByTroop.TryGetValue(troop.StringId, out int points))
        {
            return 0f;
        }

        int count = GetMainPartyCount(troop);
        return count <= 0 ? 0f : points / (float)count;
    }

    public VeteranTier GetTier(CharacterObject troop)
    {
        return VeteranTierExtensions.FromAveragePoints(GetAveragePoints(troop), _settings);
    }

    public int GetBattlesSurvived(CharacterObject troop)
    {
        return troop != null && _battlesByTroop.TryGetValue(troop.StringId, out int battles) ? battles : 0;
    }

    // ------------------------------------------------------------------ events

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        _settings = VeteranTroopsSettings.Effective;
        ReconcileRoster();
        SeedLastTiers();
    }

    private void OnHourlyTick()
    {
        MobileParty? mainParty = MobileParty.MainParty;
        if (mainParty == null || mainParty.MapEvent != null)
        {
            // Battle losses are handled by OnMapEventEnded with exact casualty numbers.
            return;
        }

        ReconcileRoster();
    }

    private void OnMapEventEnded(MapEvent mapEvent)
    {
        if (mapEvent == null || !mapEvent.IsPlayerMapEvent || MobileParty.MainParty == null)
        {
            return;
        }

        MapEventParty? playerParty = FindMainPartyInEvent(mapEvent);
        if (playerParty == null)
        {
            return;
        }

        _settings = VeteranTroopsSettings.Effective;

        float perSurvivor = ComputePointsPerSurvivor(mapEvent);
        TroopRoster roster = MobileParty.MainParty.MemberRoster;
        TroopRoster died = playerParty.DiedInBattle;
        TroopRoster wounded = playerParty.WoundedInBattle;

        // The fallen take their share of the stack's memory with them. The roster is
        // already post-battle here, so the pre-battle count is survivors plus dead.
        if (died != null)
        {
            for (int i = 0; i < died.Count; i++)
            {
                TroopRosterElement element = died.GetElementCopyAtIndex(i);
                if (!IsTrackable(element.Character) || element.Number <= 0)
                {
                    continue;
                }

                int survivors = roster.GetTroopCount(element.Character);
                RemoveSoldiers(element.Character.StringId, element.Number, survivors + element.Number);
            }
        }

        for (int i = 0; i < roster.Count; i++)
        {
            TroopRosterElement element = roster.GetElementCopyAtIndex(i);
            CharacterObject troop = element.Character;
            if (!IsTrackable(troop) || element.Number <= 0)
            {
                continue;
            }

            bool bloodied = (died != null && died.GetTroopCount(troop) > 0) ||
                            (wounded != null && wounded.GetTroopCount(troop) > 0);
            float gain = perSurvivor * (bloodied ? BloodiedFactor : 1f) * element.Number;
            AddPoints(troop.StringId, MathF.Round(gain), element.Number);
            _battlesByTroop[troop.StringId] = GetBattlesSurvived(troop) + 1;
        }

        SnapshotCounts();
        AnnounceTierChanges();
    }

    private void OnPlayerUpgradedTroops(CharacterObject upgradeFrom, CharacterObject upgradeTo, int number)
    {
        if (!IsTrackable(upgradeFrom) || !IsTrackable(upgradeTo) || number <= 0)
        {
            return;
        }

        if (!_pointsByTroop.TryGetValue(upgradeFrom.StringId, out int points) || points <= 0)
        {
            SnapshotCounts();
            return;
        }

        // The roster may or may not have changed yet when this fires, so use the
        // last reconciled count as the pre-upgrade size instead of reading it live.
        int before = _lastKnownCounts.TryGetValue(upgradeFrom.StringId, out int last)
            ? last
            : GetMainPartyCount(upgradeFrom) + number;
        before = Math.Max(before, number);

        float average = points / (float)before;
        int removed = MathF.Round(average * number);
        int carried = MathF.Round(removed * MBMath.ClampFloat(_settings.UpgradeCarryOver, 0f, 1f));

        SetPoints(upgradeFrom.StringId, points - removed, before - number);
        AddPoints(upgradeTo.StringId, carried, GetMainPartyCount(upgradeTo) + number);

        SnapshotCounts();
        AnnounceTierChanges();
    }

    // ------------------------------------------------------------------ bookkeeping

    /// <summary>
    /// Scales each stack's pool when troops have left the party outside a battle
    /// (garrisoned, dismissed, deserted). Points are never added here: recruits
    /// simply dilute the average, which is the intended effect.
    /// </summary>
    private void ReconcileRoster()
    {
        if (MobileParty.MainParty == null)
        {
            return;
        }

        var ids = new List<string>(_pointsByTroop.Keys);
        foreach (string id in ids)
        {
            int current = GetMainPartyCount(id);
            if (current <= 0)
            {
                _pointsByTroop.Remove(id);
                continue;
            }

            if (_lastKnownCounts.TryGetValue(id, out int last) && last > current)
            {
                int points = _pointsByTroop[id];
                SetPoints(id, MathF.Round(points * (current / (float)last)), current);
            }
        }

        SnapshotCounts();
    }

    private void SnapshotCounts()
    {
        _lastKnownCounts.Clear();
        if (MobileParty.MainParty == null)
        {
            return;
        }

        TroopRoster roster = MobileParty.MainParty.MemberRoster;
        for (int i = 0; i < roster.Count; i++)
        {
            TroopRosterElement element = roster.GetElementCopyAtIndex(i);
            if (IsTrackable(element.Character) && element.Number > 0)
            {
                _lastKnownCounts[element.Character.StringId] = element.Number;
            }
        }
    }

    private void RemoveSoldiers(string troopId, int removed, int countBefore)
    {
        if (removed <= 0 || countBefore <= 0 || !_pointsByTroop.TryGetValue(troopId, out int points))
        {
            return;
        }

        float average = points / (float)countBefore;
        SetPoints(troopId, MathF.Round(points - (average * removed)), countBefore - removed);
    }

    private void AddPoints(string troopId, int gain, int count)
    {
        if (gain <= 0)
        {
            return;
        }

        _pointsByTroop.TryGetValue(troopId, out int points);
        SetPoints(troopId, points + gain, count);
    }

    /// <summary>
    /// Writes a stack's pool, dropping empty stacks and capping the average so a
    /// long-lived stack cannot bank enough points to shrug off dilution forever.
    /// </summary>
    private void SetPoints(string troopId, int points, int count)
    {
        if (points <= 0 || count <= 0)
        {
            _pointsByTroop.Remove(troopId);
            return;
        }

        long cap = (long)_settings.HardenedThreshold * 2L * count;
        _pointsByTroop[troopId] = (int)Math.Min(points, Math.Min(cap, int.MaxValue / 2));
    }

    private float ComputePointsPerSurvivor(MapEvent mapEvent)
    {
        BattleSideEnum playerSide = mapEvent.PlayerSide;
        BattleSideEnum enemySide = playerSide == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker;

        int enemyAtStart = mapEvent.GetMapEventSide(enemySide)?.HealthyTroopCountAtMapEventStart ?? 0;
        float sizeFactor = MBMath.ClampFloat(enemyAtStart / (float)ReferenceEnemyCount, MinBattleSizeFactor, MaxBattleSizeFactor);

        float typeFactor = mapEvent.IsSiegeAssault ? SiegeFactor : mapEvent.IsHideoutBattle ? HideoutFactor : 1f;

        bool playerWon = mapEvent.HasWinner && mapEvent.WinningSide == playerSide;
        float outcomeFactor = playerWon ? 1f : DefeatFactor;

        return _settings.PointsPerBattle * sizeFactor * typeFactor * outcomeFactor;
    }

    private static MapEventParty? FindMainPartyInEvent(MapEvent mapEvent)
    {
        PartyBase mainParty = PartyBase.MainParty;
        foreach (MapEventSide side in new[] { mapEvent.AttackerSide, mapEvent.DefenderSide })
        {
            if (side?.Parties == null)
            {
                continue;
            }

            foreach (MapEventParty party in side.Parties)
            {
                if (party?.Party == mainParty)
                {
                    return party;
                }
            }
        }

        return null;
    }

    private static bool IsTrackable(CharacterObject? troop)
    {
        return troop != null && !troop.IsHero && !string.IsNullOrEmpty(troop.StringId);
    }

    private static int GetMainPartyCount(CharacterObject troop)
    {
        return MobileParty.MainParty?.MemberRoster?.GetTroopCount(troop) ?? 0;
    }

    private static int GetMainPartyCount(string troopId)
    {
        TroopRoster? roster = MobileParty.MainParty?.MemberRoster;
        if (roster == null)
        {
            return 0;
        }

        for (int i = 0; i < roster.Count; i++)
        {
            TroopRosterElement element = roster.GetElementCopyAtIndex(i);
            if (element.Character != null && element.Character.StringId == troopId)
            {
                return element.Number;
            }
        }

        return 0;
    }

    // ------------------------------------------------------------------ messages

    private void SeedLastTiers()
    {
        _lastTierByTroop.Clear();
        TroopRoster? roster = MobileParty.MainParty?.MemberRoster;
        if (roster == null)
        {
            return;
        }

        for (int i = 0; i < roster.Count; i++)
        {
            CharacterObject troop = roster.GetElementCopyAtIndex(i).Character;
            if (IsTrackable(troop))
            {
                _lastTierByTroop[troop.StringId] = GetTier(troop);
            }
        }
    }

    /// <summary>Announces promotions only. Dilution and losses lower a tier quietly.</summary>
    private void AnnounceTierChanges()
    {
        TroopRoster? roster = MobileParty.MainParty?.MemberRoster;
        if (roster == null)
        {
            return;
        }

        for (int i = 0; i < roster.Count; i++)
        {
            CharacterObject troop = roster.GetElementCopyAtIndex(i).Character;
            if (!IsTrackable(troop))
            {
                continue;
            }

            VeteranTier tier = GetTier(troop);
            _lastTierByTroop.TryGetValue(troop.StringId, out VeteranTier previous);
            _lastTierByTroop[troop.StringId] = tier;

            if (tier > previous && _settings.ShowTierMessages)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Your {troop.Name} are now {tier.DisplayName()}.",
                    new Color(0.72f, 0.58f, 0.22f, 1f)));
            }
        }
    }
}
