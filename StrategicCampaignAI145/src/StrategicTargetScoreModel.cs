using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace StrategicCampaignAI145;

public sealed class StrategicTargetScoreModel : DefaultTargetScoreCalculatingModel
{
    public override float GetTargetScoreForFaction(Settlement targetSettlement, Army.ArmyTypes missionType, MobileParty mobileParty, float ourStrength)
    {
        float score = base.GetTargetScoreForFaction(targetSettlement, missionType, mobileParty, ourStrength);

        if (score <= 0f ||
            mobileParty.MapFaction == null ||
            !StrategicAiHelpers.IsFortification(targetSettlement) ||
            !StrategicAiHelpers.IsEnemy(mobileParty.MapFaction, targetSettlement.MapFaction))
        {
            return score;
        }

        if (StrategicAiState.IsTargetOnCooldown(targetSettlement))
        {
            return score * StrategicAiTuning.SiegeViabilityWeakReliefMultiplier;
        }

        if (StrategicAiHelpers.IsMinorOrRebelFaction(targetSettlement.MapFaction) &&
            !targetSettlement.IsTown &&
            StrategicAiHelpers.GetEconomicValue(targetSettlement) < 500f)
        {
            score *= StrategicAiTuning.MinorFactionTargetMultiplier;
        }

        bool isFrontline = StrategicAiHelpers.HasFriendlyFortificationNear(
            targetSettlement,
            mobileParty.MapFaction,
            StrategicAiTuning.FrontlineScanRadius);

        if (isFrontline)
        {
            score *= StrategicAiTuning.FrontlineTargetMultiplier;
        }
        else
        {
            bool hasEnemyDepthAroundTarget = StrategicAiHelpers.HasEnemyFortificationNear(
                targetSettlement,
                mobileParty.MapFaction,
                StrategicAiTuning.FrontlineScanRadius);

            if (hasEnemyDepthAroundTarget)
            {
                score *= StrategicAiTuning.DeepTargetMultiplier;
            }
        }

        float distanceToFriendlyBase = StrategicAiHelpers.DistanceToNearestFriendlyFortification(
            targetSettlement,
            mobileParty.MapFaction);

        if (distanceToFriendlyBase > StrategicAiTuning.OverextendedTargetDistance)
        {
            score *= StrategicAiTuning.OverextendedTargetMultiplier;
        }

        if (StrategicAiHelpers.IsChokepoint(targetSettlement))
        {
            score *= StrategicAiTuning.ChokepointTargetMultiplier;
        }

        if (targetSettlement.IsTown || StrategicAiHelpers.GetEconomicValue(targetSettlement) > 500f)
        {
            score *= StrategicAiTuning.EconomicTargetMultiplier;
        }

        if (StrategicAiHelpers.IsMercenaryLed(mobileParty))
        {
            score *= StrategicAiTuning.MercenaryEconomicTargetMultiplier;
        }

        if (mobileParty.MapFaction is Kingdom kingdom && StrategicAiHelpers.HasCulturalClaim(kingdom, targetSettlement))
        {
            score *= StrategicAiTuning.ClaimTargetMultiplier;
        }

        if (StrategicAiHelpers.HasFriendlyNoblePrisoners(targetSettlement, mobileParty.MapFaction))
        {
            score *= StrategicAiTuning.NoblePrisonerReliefMultiplier;
        }

        if (mobileParty.Army != null && StrategicAiState.IsTargetLockedByAnotherArmy(mobileParty.Army, targetSettlement))
        {
            score *= StrategicAiTuning.DuplicateTargetPenalty;
        }

        StrategicFactionStatus factionStatus = StrategicAiState.GetFactionStatus(mobileParty.MapFaction);
        if (factionStatus.IsExhausted)
        {
            score *= StrategicAiTuning.WarExhaustionOffenseMultiplier;
        }

        if (factionStatus.WantsPeace || factionStatus.WarGoal == StrategicWarGoal.ForcePeace)
        {
            score *= StrategicAiTuning.PeacePressureOffenseMultiplier;
        }

        if (mobileParty.MapFaction is Kingdom leadershipKingdom && StrategicAiHelpers.IsFactionLeadershipImprisoned(leadershipKingdom))
        {
            score *= StrategicAiTuning.PeacePressureOffenseMultiplier;
        }

        float likelyReliefStrength = StrategicAiHelpers.NearbyEnemyLordStrength(
            targetSettlement,
            mobileParty.MapFaction,
            StrategicAiTuning.SiegeRadarRadius);

        if (likelyReliefStrength > ourStrength * 1.25f)
        {
            score *= StrategicAiTuning.SiegeViabilityWeakReliefMultiplier;
        }

        if (mobileParty.GetNumDaysForFoodToLast() < 3)
        {
            score *= StrategicAiTuning.SiegeViabilityWeakReliefMultiplier;
        }

        score *= StrategicAiHelpers.GetPersonalityOffenseMultiplier(mobileParty.LeaderHero);
        score *= StrategicAiHelpers.GetWeatherSeasonTargetMultiplier(targetSettlement, mobileParty.MapFaction);
        return score;
    }

    public override float CalculateDefensivePatrollingScoreForSettlement(Settlement settlement, bool isTargetingPort, MobileParty mobileParty)
    {
        float score = base.CalculateDefensivePatrollingScoreForSettlement(settlement, isTargetingPort, mobileParty);

        if (mobileParty.MapFaction == null || !StrategicAiHelpers.IsFriendly(mobileParty.MapFaction, settlement.MapFaction))
        {
            return score;
        }

        if (StrategicAiHelpers.IsCapital(settlement, mobileParty.MapFaction))
        {
            score *= StrategicAiTuning.CapitalDefenseMultiplier;
        }

        if (StrategicAiHelpers.IsHighValueTown(settlement))
        {
            score *= StrategicAiTuning.HighProsperityDefenseMultiplier;
        }

        if (settlement.IsUnderSiege || StrategicAiHelpers.IsLowGarrisonFortification(settlement))
        {
            score *= 2f;
        }

        if (StrategicAiHelpers.HasFriendlyNoblePrisoners(settlement, mobileParty.MapFaction))
        {
            score *= StrategicAiTuning.NoblePrisonerReliefMultiplier;
        }

        StrategicFactionStatus factionStatus = StrategicAiState.GetFactionStatus(mobileParty.MapFaction);
        if (factionStatus.IsExhausted)
        {
            score *= StrategicAiTuning.WarExhaustionDefenseMultiplier;
        }

        if (StrategicAiHelpers.IsMercenaryLed(mobileParty))
        {
            score *= StrategicAiTuning.MercenaryDefenseMultiplier;
        }

        score *= StrategicAiHelpers.GetPersonalityDefenseMultiplier(mobileParty.LeaderHero);
        return score;
    }
}

