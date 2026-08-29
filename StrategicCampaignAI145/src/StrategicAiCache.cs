using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace StrategicCampaignAI145;

/// <summary>
/// Memoises the expensive map-wide queries used while scoring targets.
///
/// GetTargetScoreForFaction is called by the vanilla AI for every party against
/// every candidate settlement. The uncached helpers walked Settlement.All five
/// times plus every lord party on the map per call, which is what produced the
/// reported single-digit frame rates. Results are keyed by (settlement, faction)
/// and rebuilt once per campaign hour, which is ample resolution for a layer
/// that only re-plans every four hours.
/// </summary>
internal static class StrategicAiCache
{
    private readonly struct Key : IEquatable<Key>
    {
        private readonly Settlement _settlement;
        private readonly IFaction? _faction;

        public Key(Settlement settlement, IFaction? faction)
        {
            _settlement = settlement;
            _faction = faction;
        }

        public bool Equals(Key other)
        {
            return ReferenceEquals(_settlement, other._settlement) && ReferenceEquals(_faction, other._faction);
        }

        public override bool Equals(object? obj) => obj is Key other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _settlement?.GetHashCode() ?? 0;
                return (hash * 397) ^ (_faction?.GetHashCode() ?? 0);
            }
        }
    }

    private static int _stampHour = int.MinValue;

    private static readonly Dictionary<Key, bool> FriendlyFortNear = new();
    private static readonly Dictionary<Key, bool> EnemyFortNear = new();
    private static readonly Dictionary<Key, float> DistanceToFriendlyBase = new();
    private static readonly Dictionary<Key, float> NearbyEnemyStrength = new();
    private static readonly Dictionary<Key, float> NearbyMajorEnemyStrength = new();
    private static readonly Dictionary<Key, float> WeatherMultiplier = new();
    private static readonly Dictionary<Settlement, float> EconomicValues = new();

    // Fortification positions never change, so chokepoint status is computed once.
    private static readonly Dictionary<Settlement, bool> Chokepoints = new();

    private static readonly List<Settlement> Fortifications = new();
    private static readonly List<MobileParty> ActiveLordParties = new();

    public static void Reset()
    {
        _stampHour = int.MinValue;
        FriendlyFortNear.Clear();
        EnemyFortNear.Clear();
        DistanceToFriendlyBase.Clear();
        NearbyEnemyStrength.Clear();
        NearbyMajorEnemyStrength.Clear();
        WeatherMultiplier.Clear();
        EconomicValues.Clear();
        Chokepoints.Clear();
        Fortifications.Clear();
        ActiveLordParties.Clear();
    }

    private static int CurrentHour()
    {
        return (int)(CampaignTime.Now.ToDays * 24d);
    }

    /// <summary>Drops per-hour results and refreshes the shared settlement/party snapshots.</summary>
    private static void EnsureFresh()
    {
        int hour = CurrentHour();
        if (hour == _stampHour)
        {
            return;
        }

        _stampHour = hour;

        FriendlyFortNear.Clear();
        EnemyFortNear.Clear();
        DistanceToFriendlyBase.Clear();
        NearbyEnemyStrength.Clear();
        NearbyMajorEnemyStrength.Clear();
        WeatherMultiplier.Clear();
        EconomicValues.Clear();

        Fortifications.Clear();
        foreach (Settlement settlement in Settlement.All)
        {
            if (settlement != null && (settlement.IsTown || settlement.IsCastle))
            {
                Fortifications.Add(settlement);
            }
        }

        ActiveLordParties.Clear();
        foreach (MobileParty party in MobileParty.AllLordParties)
        {
            if (party != null && party.IsActive && party.MapFaction != null)
            {
                ActiveLordParties.Add(party);
            }
        }
    }

    /// <summary>All towns and castles on the map. Do not mutate the returned list.</summary>
    public static List<Settlement> GetFortifications()
    {
        EnsureFresh();
        return Fortifications;
    }

    /// <summary>All active lord parties that currently have a map faction. Do not mutate.</summary>
    public static List<MobileParty> GetActiveLordParties()
    {
        EnsureFresh();
        return ActiveLordParties;
    }

    public static bool HasFriendlyFortificationNear(Settlement target, IFaction faction, float radius)
    {
        EnsureFresh();
        var key = new Key(target, faction);
        if (FriendlyFortNear.TryGetValue(key, out bool cached))
        {
            return cached;
        }

        bool result = false;
        for (int i = 0; i < Fortifications.Count; i++)
        {
            Settlement settlement = Fortifications[i];
            if (settlement != target &&
                StrategicAiHelpers.IsOwnTerritory(faction, settlement) &&
                StrategicAiHelpers.Distance(target, settlement) <= radius)
            {
                result = true;
                break;
            }
        }

        FriendlyFortNear[key] = result;
        return result;
    }

    public static bool HasEnemyFortificationNear(Settlement target, IFaction faction, float radius)
    {
        EnsureFresh();
        var key = new Key(target, faction);
        if (EnemyFortNear.TryGetValue(key, out bool cached))
        {
            return cached;
        }

        bool result = false;
        for (int i = 0; i < Fortifications.Count; i++)
        {
            Settlement settlement = Fortifications[i];
            if (settlement != target &&
                StrategicAiHelpers.IsEnemy(faction, settlement.MapFaction) &&
                StrategicAiHelpers.Distance(target, settlement) <= radius)
            {
                result = true;
                break;
            }
        }

        EnemyFortNear[key] = result;
        return result;
    }

    public static float DistanceToNearestFriendlyFortification(Settlement target, IFaction faction)
    {
        EnsureFresh();
        var key = new Key(target, faction);
        if (DistanceToFriendlyBase.TryGetValue(key, out float cached))
        {
            return cached;
        }

        float best = float.MaxValue;
        for (int i = 0; i < Fortifications.Count; i++)
        {
            Settlement settlement = Fortifications[i];
            if (settlement == target || !StrategicAiHelpers.IsOwnTerritory(faction, settlement))
            {
                continue;
            }

            float distance = StrategicAiHelpers.Distance(target, settlement);
            if (distance < best)
            {
                best = distance;
            }
        }

        DistanceToFriendlyBase[key] = best;
        return best;
    }

    /// <summary>
    /// Strength of hostile lord parties within radius.
    /// <paramref name="majorOnly"/> restricts the count to rival kingdoms, which
    /// is what threat assessment wants: every kingdom is permanently at war with
    /// the minor and bandit factions, so counting those marks nearly every fief
    /// as threatened. Target scoring leaves it false, because a minor faction
    /// party really can relieve a siege.
    /// </summary>
    public static float NearbyEnemyLordStrength(Settlement settlement, IFaction faction, float radius, bool majorOnly = false)
    {
        EnsureFresh();
        var key = new Key(settlement, faction);
        Dictionary<Key, float> cache = majorOnly ? NearbyMajorEnemyStrength : NearbyEnemyStrength;
        if (cache.TryGetValue(key, out float cached))
        {
            return cached;
        }

        float total = 0f;
        for (int i = 0; i < ActiveLordParties.Count; i++)
        {
            MobileParty party = ActiveLordParties[i];
            if (!StrategicAiHelpers.IsEnemy(faction, party.MapFaction))
            {
                continue;
            }

            if (majorOnly && !StrategicAiHelpers.IsMajorWarFaction(party.MapFaction))
            {
                continue;
            }

            if (StrategicAiHelpers.Distance(party, settlement) <= radius)
            {
                total += party.GetTotalLandStrengthWithFollowers(false);
            }
        }

        cache[key] = total;
        return total;
    }

    public static bool IsChokepoint(Settlement target)
    {
        EnsureFresh();
        if (Chokepoints.TryGetValue(target, out bool cached))
        {
            return cached;
        }

        int nearby = 0;
        for (int i = 0; i < Fortifications.Count; i++)
        {
            Settlement settlement = Fortifications[i];
            if (settlement != target && StrategicAiHelpers.Distance(target, settlement) <= StrategicAiTuning.FrontlineScanRadius)
            {
                nearby++;
                if (nearby > 2)
                {
                    break;
                }
            }
        }

        bool result = nearby <= 2;
        Chokepoints[target] = result;
        return result;
    }

    public static float GetEconomicValue(Settlement settlement)
    {
        EnsureFresh();
        if (EconomicValues.TryGetValue(settlement, out float cached))
        {
            return cached;
        }

        float score = 0f;
        Town? town = settlement.Town;
        if (town != null)
        {
            score += town.Prosperity * 0.09f;
            score += town.FoodStocks * 0.7f;
            if (town.Villages != null)
            {
                score += town.Villages.Count * 120f;
            }
        }

        if (settlement.BoundVillages != null)
        {
            score += settlement.BoundVillages.Count * 80f;
        }

        EconomicValues[settlement] = score;
        return score;
    }

    public static float GetWeatherSeasonMultiplier(Settlement target, IFaction faction)
    {
        EnsureFresh();
        var key = new Key(target, faction);
        if (WeatherMultiplier.TryGetValue(key, out float cached))
        {
            return cached;
        }

        float multiplier = StrategicAiHelpers.ComputeWeatherSeasonTargetMultiplier(target, faction);
        WeatherMultiplier[key] = multiplier;
        return multiplier;
    }
}
