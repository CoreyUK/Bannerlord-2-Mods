using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace StrategicCampaignAI;

internal static class StrategicAiHelpers
{
    public static bool IsFortification(Settlement settlement)
    {
        return settlement.IsTown || settlement.IsCastle;
    }

    public static bool IsFriendly(IFaction observer, IFaction other)
    {
        if (observer == other)
        {
            return true;
        }

        return !FactionManager.IsAtWarAgainstFaction(observer, other);
    }

    public static bool IsEnemy(IFaction observer, IFaction other)
    {
        return FactionManager.IsAtWarAgainstFaction(observer, other);
    }

    public static float Distance(Settlement settlement, Settlement other)
    {
        return settlement.GetPosition2D.Distance(other.GetPosition2D);
    }

    public static float Distance(MobileParty party, Settlement settlement)
    {
        return party.GetPosition2D.Distance(settlement.GetPosition2D);
    }

    public static float Distance(MobileParty party, MobileParty other)
    {
        return party.GetPosition2D.Distance(other.GetPosition2D);
    }

    public static bool HasFriendlyFortificationNear(Settlement target, IFaction faction, float radius)
    {
        return Settlement.All.Any(settlement =>
            settlement != target &&
            IsFortification(settlement) &&
            IsFriendly(faction, settlement.MapFaction) &&
            Distance(target, settlement) <= radius);
    }

    public static bool HasEnemyFortificationNear(Settlement target, IFaction faction, float radius)
    {
        return Settlement.All.Any(settlement =>
            settlement != target &&
            IsFortification(settlement) &&
            IsEnemy(faction, settlement.MapFaction) &&
            Distance(target, settlement) <= radius);
    }

    public static bool IsFrontline(Settlement target, IFaction faction)
    {
        return IsFortification(target) &&
               IsFriendly(faction, target.MapFaction) &&
               HasEnemyFortificationNear(target, faction, StrategicAiTuning.FrontlineScanRadius);
    }

    public static bool IsEnemyFrontlineTarget(Settlement target, IFaction faction)
    {
        return IsFortification(target) &&
               IsEnemy(faction, target.MapFaction) &&
               HasFriendlyFortificationNear(target, faction, StrategicAiTuning.FrontlineScanRadius);
    }

    public static bool IsCapital(Settlement settlement, IFaction faction)
    {
        return faction is Kingdom kingdom &&
               kingdom.InitialHomeSettlement == settlement;
    }

    public static bool IsHighValueTown(Settlement settlement)
    {
        return settlement.IsTown &&
               settlement.Town != null &&
               settlement.Town.Prosperity >= StrategicAiTuning.HighProsperityThreshold;
    }

    public static bool IsLowGarrisonFortification(Settlement settlement)
    {
        return IsFortification(settlement) &&
               settlement.Party != null &&
               settlement.Party.EstimatedStrength <= StrategicAiTuning.LowGarrisonStrength;
    }

    public static bool IsChokepoint(Settlement target)
    {
        if (!IsFortification(target))
        {
            return false;
        }

        int nearbyFortifications = Settlement.All.Count(settlement =>
            settlement != target &&
            IsFortification(settlement) &&
            Distance(target, settlement) <= StrategicAiTuning.FrontlineScanRadius);

        return nearbyFortifications <= 2;
    }

    public static StrategicLordPersonality GetPersonality(Hero? hero)
    {
        if (hero == null)
        {
            return StrategicLordPersonality.Balanced;
        }

        if (hero.GetTraitLevel(DefaultTraits.Valor) >= 1)
        {
            return StrategicLordPersonality.Aggressive;
        }

        if (hero.GetTraitLevel(DefaultTraits.Calculating) >= 1)
        {
            return StrategicLordPersonality.Cautious;
        }

        if (hero.GetTraitLevel(DefaultTraits.Generosity) <= -1)
        {
            return StrategicLordPersonality.Greedy;
        }

        if (hero.GetTraitLevel(DefaultTraits.Honor) >= 1 || hero.GetTraitLevel(DefaultTraits.Mercy) >= 1)
        {
            return StrategicLordPersonality.Honorable;
        }

        return StrategicLordPersonality.Balanced;
    }

    public static float GetPersonalityOffenseMultiplier(Hero? hero)
    {
        return GetPersonality(hero) switch
        {
            StrategicLordPersonality.Aggressive => 1.25f,
            StrategicLordPersonality.Cautious => 0.82f,
            StrategicLordPersonality.Greedy => 1.08f,
            StrategicLordPersonality.Honorable => 0.95f,
            _ => 1f
        };
    }

    public static float GetPersonalityDefenseMultiplier(Hero? hero)
    {
        return GetPersonality(hero) switch
        {
            StrategicLordPersonality.Cautious => 1.3f,
            StrategicLordPersonality.Honorable => 1.2f,
            StrategicLordPersonality.Aggressive => 0.9f,
            _ => 1f
        };
    }

    public static float GetEconomicValue(Settlement settlement)
    {
        float score = 0f;

        if (settlement.Town != null)
        {
            score += settlement.Town.Prosperity * 0.09f;
            score += settlement.Town.FoodStocks * 0.7f;
            score += settlement.Town.Villages.Count * 120f;
        }

        score += settlement.BoundVillages.Count * 80f;
        return score;
    }

    public static bool HasCulturalClaim(Kingdom kingdom, Settlement settlement)
    {
        return settlement.Culture == kingdom.Culture ||
               StrategicAiState.GetLostClaim(kingdom) == settlement;
    }

    public static bool IsMercenaryLed(MobileParty party)
    {
        return party.ActualClan != null &&
               (party.ActualClan.IsClanTypeMercenary || party.ActualClan.IsUnderMercenaryService);
    }

    public static bool IsMinorOrRebelFaction(IFaction faction)
    {
        return faction.IsMinorFaction || faction.IsRebelClan;
    }

    public static bool HasFriendlyNoblePrisoners(Settlement settlement, IFaction faction)
    {
        return settlement.Party?.PrisonerHeroes.Any(character =>
        {
            Hero? hero = character.HeroObject;
            return hero != null &&
            hero.IsLord &&
            hero.MapFaction != null &&
            IsFriendly(faction, hero.MapFaction);
        }) == true;
    }

    public static bool IsFactionLeadershipImprisoned(Kingdom kingdom)
    {
        return kingdom.Leader != null && kingdom.Leader.IsPrisoner;
    }

    public static float GetWeatherSeasonTargetMultiplier(Settlement target, IFaction faction)
    {
        float multiplier = 1f;

        string season = CampaignTime.Now.GetSeasonOfYear.ToString();
        if (season.IndexOf("Winter", StringComparison.OrdinalIgnoreCase) >= 0 &&
            (faction.Culture == null || target.Culture != faction.Culture))
        {
            multiplier *= StrategicAiTuning.WinterCampaignMultiplier;
        }

        if (Campaign.Current?.Models?.MapWeatherModel != null)
        {
            string weather = Campaign.Current.Models.MapWeatherModel
                .UpdateWeatherForPosition(target.Position, CampaignTime.Now)
                .ToString();

            if (weather.IndexOf("Blizzard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weather.IndexOf("Storm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                weather.IndexOf("Snow", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                multiplier *= StrategicAiTuning.SevereWeatherTargetMultiplier;
            }
        }

        return multiplier;
    }

    public static float NearbyEnemyLordStrength(Settlement settlement, IFaction faction, float radius)
    {
        return MobileParty.AllLordParties.Where(party =>
                party.IsActive &&
                party.MapFaction != null &&
                IsEnemy(faction, party.MapFaction) &&
                party.GetPosition2D.Distance(settlement.GetPosition2D) <= radius)
            .Sum(party => party.GetTotalLandStrengthWithFollowers(false));
    }

    public static float PredictedEnemyThreatStrength(Settlement settlement, IFaction faction, float radius)
    {
        return MobileParty.AllLordParties.Where(party =>
                party.IsActive &&
                party.MapFaction != null &&
                IsEnemy(faction, party.MapFaction) &&
                Distance(party, settlement) <= radius &&
                (party.TargetSettlement == settlement ||
                 party.ShortTermTargetSettlement == settlement ||
                 party.AiBehaviorTarget.Distance(settlement.Position) <= StrategicAiTuning.FrontlineScanRadius))
            .Sum(party => party.GetTotalLandStrengthWithFollowers(false));
    }

    public static float NearbyFriendlyLordStrength(Settlement settlement, IFaction faction, float radius)
    {
        return MobileParty.AllLordParties.Where(party =>
                party.IsActive &&
                party.MapFaction != null &&
                IsFriendly(faction, party.MapFaction) &&
                party.GetPosition2D.Distance(settlement.GetPosition2D) <= radius)
            .Sum(party => party.GetTotalLandStrengthWithFollowers(false));
    }

    public static Settlement? FindBestDefensiveSettlement(Kingdom kingdom)
    {
        Settlement? bestSettlement = null;
        float bestScore = 0f;

        foreach (Settlement settlement in kingdom.Settlements.Where(IsFortification))
        {
            float enemyStrength = NearbyEnemyLordStrength(settlement, kingdom, StrategicAiTuning.HomelandDefenseThreatRadius) +
                                  PredictedEnemyThreatStrength(settlement, kingdom, StrategicAiTuning.HomelandDefenseThreatRadius * 1.35f);
            float localDefense = settlement.Party?.EstimatedStrength ?? 0f;
            if (enemyStrength <= 1f && !settlement.IsUnderSiege && !IsLowGarrisonFortification(settlement))
            {
                continue;
            }

            float score = enemyStrength - localDefense * 0.35f;
            if (settlement.IsUnderSiege)
            {
                score += 900f;
            }

            if (IsCapital(settlement, kingdom))
            {
                score *= StrategicAiTuning.CapitalDefenseMultiplier;
            }

            if (IsHighValueTown(settlement))
            {
                score *= StrategicAiTuning.HighProsperityDefenseMultiplier;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestSettlement = settlement;
            }
        }

        return bestSettlement;
    }

    public static Settlement? FindBestAggressiveTarget(Kingdom kingdom, Army army)
    {
        MobileParty? leader = army.LeaderParty;
        if (leader == null)
        {
            return null;
        }

        Settlement? bestSettlement = null;
        float bestScore = 0f;

        foreach (IFaction enemyFaction in kingdom.FactionsAtWarWith)
        {
            foreach (Settlement settlement in Settlement.All.Where(settlement => IsFortification(settlement) && IsEnemy(kingdom, settlement.MapFaction)))
            {
                if (!IsEnemyFrontlineTarget(settlement, kingdom) ||
                    StrategicAiState.IsTargetLockedByAnotherArmy(army, settlement) ||
                    StrategicAiState.IsTargetOnCooldown(settlement) ||
                    (IsMinorOrRebelFaction(settlement.MapFaction) && !settlement.IsTown && GetEconomicValue(settlement) < 500f))
                {
                    continue;
                }

                float distance = Distance(leader, settlement);
                float defense = settlement.Party?.EstimatedStrength ?? 0f;
                float relief = NearbyEnemyLordStrength(settlement, kingdom, StrategicAiTuning.SiegeRadarRadius);
                float score = 10000f / MathF.Max(20f, distance + 20f);
                score += settlement.IsTown ? 350f : 160f;
                score += GetEconomicValue(settlement);
                score -= defense * 0.25f;
                score -= relief * 0.18f;

                if (IsChokepoint(settlement))
                {
                    score *= StrategicAiTuning.ChokepointTargetMultiplier;
                }

                if (HasCulturalClaim(kingdom, settlement))
                {
                    score *= StrategicAiTuning.ClaimTargetMultiplier;
                }

                StrategicFactionStatus status = StrategicAiState.GetFactionStatus(kingdom);
                if (status.WarGoal == StrategicWarGoal.WeakenEconomy && settlement.IsTown)
                {
                    score *= StrategicAiTuning.EconomicTargetMultiplier;
                }

                if (status.WarGoal == StrategicWarGoal.ReclaimLostFief && StrategicAiState.GetLostClaim(kingdom) == settlement)
                {
                    score *= StrategicAiTuning.ClaimTargetMultiplier;
                }

                if (status.WarGoal == StrategicWarGoal.ForcePeace)
                {
                    score *= StrategicAiTuning.PeacePressureOffenseMultiplier;
                }

                if (HasFriendlyNoblePrisoners(settlement, kingdom))
                {
                    score *= StrategicAiTuning.NoblePrisonerReliefMultiplier;
                }

                if (IsFactionLeadershipImprisoned(kingdom))
                {
                    score *= StrategicAiTuning.PeacePressureOffenseMultiplier;
                }

                if (IsMercenaryLed(leader))
                {
                    score *= StrategicAiTuning.MercenaryEconomicTargetMultiplier;
                }

                score *= GetWeatherSeasonTargetMultiplier(settlement, kingdom);
                score *= GetPersonalityOffenseMultiplier(leader.LeaderHero);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSettlement = settlement;
                }
            }
        }

        return bestSettlement;
    }

    public static Settlement? FindBestStagingSettlement(Kingdom kingdom, Settlement target)
    {
        return kingdom.Settlements
            .Where(IsFortification)
            .Where(settlement => Distance(settlement, target) <= StrategicAiTuning.StagingTargetDistance)
            .OrderBy(settlement => Distance(settlement, target))
            .FirstOrDefault();
    }

    public static MobileParty? FindEnemyArmyToShadow(Kingdom kingdom, MobileParty leader)
    {
        return MobileParty.AllLordParties
            .Where(party =>
                party.IsActive &&
                party.Army != null &&
                party.Army.LeaderParty == party &&
                party.MapFaction != null &&
                IsEnemy(kingdom, party.MapFaction) &&
                Distance(leader, party) <= StrategicAiTuning.ShadowMaxDistance &&
                Distance(leader, party) >= StrategicAiTuning.ShadowMinDistance)
            .OrderByDescending(party => party.Army?.EstimatedStrength ?? party.GetTotalLandStrengthWithFollowers(true))
            .FirstOrDefault();
    }

    public static MobileParty? FindBestInterceptorTarget(Kingdom kingdom, MobileParty leader)
    {
        return MobileParty.AllLordParties
            .Where(party =>
                party.IsActive &&
                party.MapFaction != null &&
                IsEnemy(kingdom, party.MapFaction) &&
                Distance(leader, party) <= StrategicAiTuning.InterceptorRadius &&
                party.GetTotalLandStrengthWithFollowers(false) <= leader.GetTotalLandStrengthWithFollowers(true) * 0.9f)
            .OrderBy(party => Distance(leader, party))
            .FirstOrDefault();
    }

    public static Settlement? FindRecentFriendlyCapture(Kingdom kingdom)
    {
        return kingdom.Settlements
            .Where(settlement => IsFortification(settlement) && StrategicAiState.WasRecentlyCaptured(settlement))
            .OrderBy(settlement => settlement.Party?.EstimatedStrength ?? float.MaxValue)
            .FirstOrDefault();
    }

    public static float DistanceToNearestFriendlyFortification(Settlement target, IFaction faction)
    {
        float best = float.MaxValue;

        foreach (Settlement settlement in Settlement.All)
        {
            if (settlement == target || !IsFortification(settlement) || !IsFriendly(faction, settlement.MapFaction))
            {
                continue;
            }

            best = MathF.Min(best, Distance(target, settlement));
        }

        return best;
    }

    public static Settlement? FindNearestFriendlyFortification(MobileParty party)
    {
        Settlement? bestSettlement = null;
        float bestDistance = float.MaxValue;
        IFaction faction = party.MapFaction;

        foreach (Settlement settlement in Settlement.All)
        {
            if (!IsFortification(settlement) || !IsFriendly(faction, settlement.MapFaction))
            {
                continue;
            }

            float distance = Distance(party, settlement);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestSettlement = settlement;
            }
        }

        return bestSettlement;
    }

    public static Settlement? FindBestRetreatSettlement(MobileParty party)
    {
        Settlement? bestSettlement = null;
        float bestScore = float.MinValue;

        foreach (Settlement settlement in Settlement.All)
        {
            if (!IsFortification(settlement) || !IsFriendly(party.MapFaction, settlement.MapFaction))
            {
                continue;
            }

            float distance = Distance(party, settlement);
            float enemyThreat = NearbyEnemyLordStrength(settlement, party.MapFaction, StrategicAiTuning.HomelandDefenseThreatRadius);
            float friendlySupport = NearbyFriendlyLordStrength(settlement, party.MapFaction, StrategicAiTuning.LocalAllyRadius);
            float garrison = settlement.Party?.EstimatedStrength ?? 0f;
            float foodAndProsperity = 0f;

            if (settlement.Town != null)
            {
                foodAndProsperity = settlement.Town.FoodStocks * 0.4f + settlement.Town.Prosperity * 0.03f;
            }

            float score = garrison * 0.3f + friendlySupport * 0.4f + foodAndProsperity - enemyThreat * 0.9f - distance * 8f;
            if (settlement.IsUnderSiege)
            {
                score -= 1500f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestSettlement = settlement;
            }
        }

        return bestSettlement;
    }

    public static bool IsDeepInEnemyTerritory(Army army)
    {
        MobileParty? leader = army.LeaderParty;
        if (leader == null || leader.MapFaction == null)
        {
            return false;
        }

        Settlement? nearestFriendly = FindNearestFriendlyFortification(leader);
        if (nearestFriendly == null || Distance(leader, nearestFriendly) < StrategicAiTuning.DeepTargetFriendlyRadius)
        {
            return false;
        }

        return Settlement.All.Any(settlement =>
            IsFortification(settlement) &&
            IsEnemy(leader.MapFaction, settlement.MapFaction) &&
            Distance(leader, settlement) <= StrategicAiTuning.FrontlineScanRadius);
    }

    public static IEnumerable<MobileParty> EnemyLordPartiesNear(MobileParty party, float radius)
    {
        return MobileParty.AllPartiesWithoutPartyComponent.Where(other =>
            other != party &&
            other.IsActive &&
            other.IsLordParty &&
            other.MapFaction != null &&
            IsEnemy(party.MapFaction, other.MapFaction) &&
            Distance(party, other) <= radius);
    }

    public static IEnumerable<MobileParty> FriendlyLordPartiesNear(MobileParty party, float radius)
    {
        return MobileParty.AllPartiesWithoutPartyComponent.Where(other =>
            other != party &&
            other.IsActive &&
            other.IsLordParty &&
            other.MapFaction != null &&
            IsFriendly(party.MapFaction, other.MapFaction) &&
            Distance(party, other) <= radius);
    }
}
