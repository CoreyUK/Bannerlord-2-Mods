using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace StrategicCampaignAI145;

/// <summary>
/// Shapes vanilla's target scoring with strategic preferences.
///
/// Every preference below is expressed as a multiplier that is accumulated and
/// then clamped before being applied. The clamp is essential: applied directly,
/// the offensive penalties compounded to roughly 1/5000 of the vanilla score
/// while the defensive bonuses compounded to about 20x, so the AI concluded that
/// no enemy settlement was ever worth attacking and every one of its own was
/// worth guarding. That is the cause of the reported passive armies that circle
/// their own fiefs and never commit to a siege.
/// </summary>
public sealed class StrategicTargetScoreModel : DefaultTargetScoreCalculatingModel
{
    public override float GetTargetScoreForFaction(Settlement targetSettlement, Army.ArmyTypes missionType, MobileParty mobileParty, float ourStrength)
    {
        float score;
        try
        {
            score = base.GetTargetScoreForFaction(targetSettlement, missionType, mobileParty, ourStrength);
        }
        catch (Exception)
        {
            return 0f;
        }

        try
        {
            return score * GetOffensiveMultiplier(targetSettlement, mobileParty, ourStrength, score);
        }
        catch (Exception)
        {
            // Never let a strategic preference turn into a crash inside the AI's
            // decision loop -- fall back to the unmodified vanilla score.
            return score;
        }
    }

    private static float GetOffensiveMultiplier(Settlement targetSettlement, MobileParty mobileParty, float ourStrength, float baseScore)
    {
        if (baseScore <= 0f || targetSettlement == null || mobileParty == null)
        {
            return 1f;
        }

        IFaction? ourFaction = mobileParty.MapFaction;
        IFaction? theirFaction = targetSettlement.MapFaction;

        if (ourFaction == null ||
            theirFaction == null ||
            !StrategicAiHelpers.IsFortification(targetSettlement) ||
            !StrategicAiHelpers.IsEnemy(ourFaction, theirFaction))
        {
            return 1f;
        }

        float multiplier = 1f;

        if (StrategicAiState.IsTargetOnCooldown(targetSettlement))
        {
            multiplier *= StrategicAiTuning.SiegeViabilityWeakReliefMultiplier;
        }

        if (StrategicAiHelpers.IsMinorOrRebelFaction(theirFaction) &&
            !targetSettlement.IsTown &&
            StrategicAiHelpers.GetEconomicValue(targetSettlement) < 500f)
        {
            multiplier *= StrategicAiTuning.MinorFactionTargetMultiplier;
        }

        // Frontline versus depth. A target we can actually reach and hold beats a
        // deep raid into territory we have no base near.
        if (StrategicAiHelpers.HasOwnFortificationNear(targetSettlement, ourFaction, StrategicAiTuning.FrontlineScanRadius))
        {
            multiplier *= StrategicAiTuning.FrontlineTargetMultiplier;
        }
        else if (StrategicAiHelpers.HasEnemyFortificationNear(targetSettlement, ourFaction, StrategicAiTuning.FrontlineScanRadius))
        {
            multiplier *= StrategicAiTuning.DeepTargetMultiplier;
        }

        if (StrategicAiHelpers.DistanceToNearestFriendlyFortification(targetSettlement, ourFaction) > StrategicAiTuning.OverextendedTargetDistance)
        {
            multiplier *= StrategicAiTuning.OverextendedTargetMultiplier;
        }

        if (StrategicAiHelpers.IsChokepoint(targetSettlement))
        {
            multiplier *= StrategicAiTuning.ChokepointTargetMultiplier;
        }

        if (targetSettlement.IsTown || StrategicAiHelpers.GetEconomicValue(targetSettlement) > 500f)
        {
            multiplier *= StrategicAiTuning.EconomicTargetMultiplier;
        }

        if (StrategicAiHelpers.IsMercenaryLed(mobileParty))
        {
            multiplier *= StrategicAiTuning.MercenaryEconomicTargetMultiplier;
        }

        if (ourFaction is Kingdom kingdom && StrategicAiHelpers.HasCulturalClaim(kingdom, targetSettlement))
        {
            multiplier *= StrategicAiTuning.ClaimTargetMultiplier;
        }

        if (StrategicAiHelpers.HasFriendlyNoblePrisoners(targetSettlement, ourFaction))
        {
            multiplier *= StrategicAiTuning.NoblePrisonerReliefMultiplier;
        }

        Army? army = mobileParty.Army;
        if (army != null)
        {
            // Hold the objective this army already committed to, and mildly
            // discourage piling onto another army's. Applying only the penalty
            // made two armies swap targets indefinitely.
            if (StrategicAiState.GetTargetLock(army) == targetSettlement)
            {
                multiplier *= StrategicAiTuning.CurrentObjectiveStickinessMultiplier;
            }
            else if (StrategicAiState.IsTargetLockedByAnotherArmy(army, targetSettlement))
            {
                multiplier *= StrategicAiTuning.DuplicateTargetPenalty;
            }
        }

        StrategicFactionStatus factionStatus = StrategicAiState.GetFactionStatus(ourFaction);
        if (factionStatus.IsExhausted)
        {
            multiplier *= StrategicAiTuning.WarExhaustionOffenseMultiplier;
        }

        if (factionStatus.WantsPeace || factionStatus.WarGoal == StrategicWarGoal.ForcePeace)
        {
            multiplier *= StrategicAiTuning.PeacePressureOffenseMultiplier;
        }

        if (ourFaction is Kingdom leadershipKingdom && StrategicAiHelpers.IsFactionLeadershipImprisoned(leadershipKingdom))
        {
            multiplier *= StrategicAiTuning.PeacePressureOffenseMultiplier;
        }

        float likelyReliefStrength = StrategicAiHelpers.NearbyEnemyLordStrength(
            targetSettlement,
            ourFaction,
            StrategicAiTuning.SiegeRadarRadius);

        if (likelyReliefStrength > ourStrength * 1.25f)
        {
            multiplier *= StrategicAiTuning.SiegeViabilityWeakReliefMultiplier;
        }

        if (mobileParty.GetNumDaysForFoodToLast() < 3)
        {
            multiplier *= StrategicAiTuning.SiegeViabilityWeakReliefMultiplier;
        }

        multiplier *= StrategicAiHelpers.GetPersonalityOffenseMultiplier(mobileParty.LeaderHero);
        multiplier *= StrategicAiHelpers.GetWeatherSeasonTargetMultiplier(targetSettlement, ourFaction);

        return MBMath.ClampFloat(multiplier, StrategicAiTuning.MinOffenseMultiplier, StrategicAiTuning.MaxOffenseMultiplier);
    }

    public override float CalculateDefensivePatrollingScoreForSettlement(Settlement settlement, bool isTargetingPort, MobileParty mobileParty)
    {
        float score;
        try
        {
            score = base.CalculateDefensivePatrollingScoreForSettlement(settlement, isTargetingPort, mobileParty);
        }
        catch (Exception)
        {
            return 0f;
        }

        try
        {
            return score * GetDefensiveMultiplier(settlement, mobileParty, score);
        }
        catch (Exception)
        {
            return score;
        }
    }

    private static float GetDefensiveMultiplier(Settlement settlement, MobileParty mobileParty, float baseScore)
    {
        if (baseScore <= 0f || settlement == null || mobileParty == null)
        {
            return 1f;
        }

        IFaction? ourFaction = mobileParty.MapFaction;
        if (ourFaction == null || !StrategicAiHelpers.IsFriendly(ourFaction, settlement.MapFaction))
        {
            return 1f;
        }

        float multiplier = 1f;

        if (StrategicAiHelpers.IsCapital(settlement, ourFaction))
        {
            multiplier *= StrategicAiTuning.CapitalDefenseMultiplier;
        }

        if (StrategicAiHelpers.IsHighValueTown(settlement))
        {
            multiplier *= StrategicAiTuning.HighProsperityDefenseMultiplier;
        }

        if (settlement.IsUnderSiege || StrategicAiHelpers.IsLowGarrisonFortification(settlement))
        {
            multiplier *= StrategicAiTuning.ThreatenedFortificationDefenseMultiplier;
        }

        if (StrategicAiHelpers.HasFriendlyNoblePrisoners(settlement, ourFaction))
        {
            multiplier *= StrategicAiTuning.NoblePrisonerReliefMultiplier;
        }

        if (StrategicAiState.GetFactionStatus(ourFaction).IsExhausted)
        {
            multiplier *= StrategicAiTuning.WarExhaustionDefenseMultiplier;
        }

        if (StrategicAiHelpers.IsMercenaryLed(mobileParty))
        {
            multiplier *= StrategicAiTuning.MercenaryDefenseMultiplier;
        }

        multiplier *= StrategicAiHelpers.GetPersonalityDefenseMultiplier(mobileParty.LeaderHero);

        return MBMath.ClampFloat(multiplier, StrategicAiTuning.MinDefenseMultiplier, StrategicAiTuning.MaxDefenseMultiplier);
    }
}
