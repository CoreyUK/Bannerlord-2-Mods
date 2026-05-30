using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace StrategicCampaignAI;

public sealed class StrategicCampaignAiBehavior : CampaignBehaviorBase
{
    public override void RegisterEvents()
    {
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        CampaignEvents.VillageBeingRaided.AddNonSerializedListener(this, OnVillageBeingRaided);
    }

    public override void SyncData(IDataStore dataStore)
    {
        StrategicAiState.SyncData(dataStore);
    }

    private static void OnDailyTick()
    {
        foreach (Kingdom kingdom in Kingdom.All.Where(kingdom => !kingdom.IsEliminated))
        {
            StrategicAiState.SetFactionStatus(kingdom, CalculateFactionStatus(kingdom));
            UpdateWarGoal(kingdom);
            StrategicAiState.SetFactionStatus(kingdom, CalculateFactionStatus(kingdom));
            AssignArmyRoles(kingdom);

            foreach (Army army in kingdom.Armies)
            {
                int currentDays = StrategicAiState.GetEnemyTerritoryDays(army);
                StrategicAiState.SetEnemyTerritoryDays(
                    army,
                    StrategicAiHelpers.IsDeepInEnemyTerritory(army) ? currentDays + 1 : 0);
            }
        }
    }

    private static void OnHourlyTick()
    {
        foreach (Kingdom kingdom in Kingdom.All.Where(kingdom => !kingdom.IsEliminated))
        {
            StrategicAiState.SetFactionStatus(kingdom, CalculateFactionStatus(kingdom));
            UpdateWarGoal(kingdom);
            StrategicAiState.SetFactionStatus(kingdom, CalculateFactionStatus(kingdom));

            foreach (Army army in kingdom.Armies.ToList())
            {
                TryRunAssignedArmyRole(kingdom, army);
                TryRetreatFromBadSiege(army);
                TryReinforceFragileGarrison(army);
            }
        }
    }

    private static StrategicFactionStatus CalculateFactionStatus(Kingdom kingdom)
    {
        var status = new StrategicFactionStatus
        {
            OwnedStrength = kingdom.CurrentTotalStrength,
            ActiveWars = kingdom.FactionsAtWarWith.Count,
            OwnedFortifications = kingdom.Settlements.Count(StrategicAiHelpers.IsFortification)
        };

        foreach (IFaction enemyFaction in kingdom.FactionsAtWarWith)
        {
            status.EnemyStrength += enemyFaction.CurrentTotalStrength;
        }

        status.ThreatenedFortifications = kingdom.Settlements.Count(settlement =>
            StrategicAiHelpers.IsFortification(settlement) &&
            (settlement.IsUnderSiege ||
             StrategicAiHelpers.NearbyEnemyLordStrength(settlement, kingdom, StrategicAiTuning.HomelandDefenseThreatRadius) >
             (settlement.Party?.EstimatedStrength ?? 0f)));
        status.RaidedVillages = kingdom.Settlements
            .SelectMany(settlement => settlement.BoundVillages)
            .Count(village => village.Settlement.IsUnderRaid || village.Settlement.IsRaided);

        return status;
    }

    private static void UpdateWarGoal(Kingdom kingdom)
    {
        if (!StrategicAiState.ShouldReevaluateWarGoal(kingdom))
        {
            return;
        }

        StrategicFactionStatus status = StrategicAiState.GetFactionStatus(kingdom);
        StrategicWarGoal goal;

        if (status.IsExhausted)
        {
            goal = StrategicWarGoal.ForcePeace;
        }
        else if (kingdom.InitialHomeSettlement != null &&
                 StrategicAiHelpers.NearbyEnemyLordStrength(kingdom.InitialHomeSettlement, kingdom, StrategicAiTuning.HomelandDefenseThreatRadius) > 500f)
        {
            goal = StrategicWarGoal.DefendCapitalRegion;
        }
        else if (StrategicAiState.GetLostClaim(kingdom) != null)
        {
            goal = StrategicWarGoal.ReclaimLostFief;
        }
        else if (status.RaidedVillages > 0)
        {
            goal = StrategicWarGoal.WeakenEconomy;
        }
        else
        {
            goal = StrategicWarGoal.BorderWar;
        }

        StrategicAiState.SetWarGoal(kingdom, goal);
    }

    private static void AssignArmyRoles(Kingdom kingdom)
    {
        List<Army> armies = kingdom.Armies
            .Where(army => army.LeaderParty != null && !army.LeaderParty.IsMainParty)
            .OrderByDescending(army => army.EstimatedStrength)
            .ToList();

        if (armies.Count == 0)
        {
            return;
        }

        StrategicFactionStatus status = StrategicAiState.GetFactionStatus(kingdom);
        for (int i = 0; i < armies.Count; i++)
        {
            StrategicArmyRole role;
            if (status.WarGoal == StrategicWarGoal.ForcePeace || status.WarGoal == StrategicWarGoal.DefendCapitalRegion)
            {
                role = i == 0 ? StrategicArmyRole.Defender : i == 1 ? StrategicArmyRole.Interceptor : StrategicArmyRole.Reserve;
            }
            else
            {
                role = i == 0 ? StrategicArmyRole.Aggressor : i == 1 ? StrategicArmyRole.Defender : i == 2 ? StrategicArmyRole.Interceptor : StrategicArmyRole.Reserve;
            }

            StrategicAiState.SetRole(armies[i], role);
        }
    }

    private static void TryRunAssignedArmyRole(Kingdom kingdom, Army army)
    {
        MobileParty? leader = army.LeaderParty;
        if (leader == null || leader.IsMainParty || leader.MapFaction == null || leader.BesiegedSettlement != null)
        {
            return;
        }

        if (TryKeepCommittedObjective(kingdom, army, leader))
        {
            return;
        }

        if (TryStageForSharedOperation(kingdom, army, leader))
        {
            return;
        }

        Settlement? consolidationTarget = StrategicAiHelpers.FindRecentFriendlyCapture(kingdom);
        if (consolidationTarget != null && StrategicAiHelpers.Distance(leader, consolidationTarget) <= StrategicAiTuning.ConsolidationPatrolRadius * 2f)
        {
            StrategicAiState.SetTargetLock(army, consolidationTarget);
            leader.SetMovePatrolAroundSettlement(consolidationTarget, MobileParty.NavigationType.Default, false);
            return;
        }

        switch (StrategicAiState.GetRole(army))
        {
            case StrategicArmyRole.Defender:
                MoveToDefensiveTarget(kingdom, army, leader);
                break;
            case StrategicArmyRole.Interceptor:
                MoveToInterceptorTarget(kingdom, army, leader);
                break;
            case StrategicArmyRole.Reserve:
                MoveToReservePosition(kingdom, army, leader);
                break;
            default:
                MoveToAggressiveTarget(kingdom, army, leader);
                break;
        }
    }

    private static void MoveToAggressiveTarget(Kingdom kingdom, Army army, MobileParty leader)
    {
        Settlement? target = StrategicAiHelpers.FindBestAggressiveTarget(kingdom, army);
        if (target == null)
        {
            return;
        }

        StrategicAiState.SetTargetLock(army, target);
        StrategicAiState.SetOperationTarget(kingdom, target);
        leader.SetMoveBesiegeSettlement(target, MobileParty.NavigationType.Default);
    }

    private static bool TryKeepCommittedObjective(Kingdom kingdom, Army army, MobileParty leader)
    {
        if (!StrategicAiState.IsCommittedToCurrentObjective(army) ||
            leader.GetNumDaysForFoodToLast() < 3 ||
            StrategicAiState.GetFactionStatus(kingdom).WarGoal == StrategicWarGoal.ForcePeace)
        {
            return false;
        }

        Settlement? lockedTarget = StrategicAiState.GetTargetLock(army);
        if (lockedTarget == null || StrategicAiState.IsTargetOnCooldown(lockedTarget))
        {
            return false;
        }

        if (StrategicAiHelpers.IsEnemy(kingdom, lockedTarget.MapFaction))
        {
            if (!StrategicAiHelpers.IsEnemyFrontlineTarget(lockedTarget, kingdom))
            {
                return false;
            }

            leader.SetMoveBesiegeSettlement(lockedTarget, MobileParty.NavigationType.Default);
            return true;
        }

        if (StrategicAiHelpers.IsFriendly(kingdom, lockedTarget.MapFaction))
        {
            if (lockedTarget.IsUnderSiege || StrategicAiHelpers.IsLowGarrisonFortification(lockedTarget))
            {
                leader.SetMoveDefendSettlement(lockedTarget, false, MobileParty.NavigationType.Default);
            }
            else
            {
                leader.SetMovePatrolAroundSettlement(lockedTarget, MobileParty.NavigationType.Default, false);
            }

            return true;
        }

        return false;
    }

    private static void MoveToDefensiveTarget(Kingdom kingdom, Army army, MobileParty leader)
    {
        Settlement? raidedVillage = StrategicAiState.GetRaidedVillageSettlement(kingdom);
        if (raidedVillage != null && StrategicAiHelpers.Distance(leader, raidedVillage) <= StrategicAiTuning.VillageDefenseRadius * 2f)
        {
            StrategicAiState.SetTargetLock(army, raidedVillage);
            leader.SetMovePatrolAroundSettlement(raidedVillage, MobileParty.NavigationType.Default, false);
            return;
        }

        Settlement? target = StrategicAiHelpers.FindBestDefensiveSettlement(kingdom);
        if (target == null)
        {
            MobileParty? shadowTarget = StrategicAiHelpers.FindEnemyArmyToShadow(kingdom, leader);
            if (shadowTarget != null)
            {
                StrategicAiState.SetTargetLock(army, shadowTarget.TargetSettlement);
                leader.SetMoveGoAroundParty(shadowTarget, MobileParty.NavigationType.Default);
                return;
            }

            MoveToReservePosition(kingdom, army, leader);
            return;
        }

        StrategicAiState.SetTargetLock(army, target);
        leader.SetMoveDefendSettlement(target, false, MobileParty.NavigationType.Default);
    }

    private static void MoveToInterceptorTarget(Kingdom kingdom, Army army, MobileParty leader)
    {
        if (StrategicAiHelpers.IsMercenaryLed(leader))
        {
            MoveToAggressiveTarget(kingdom, army, leader);
            return;
        }

        MobileParty? target = StrategicAiHelpers.FindBestInterceptorTarget(kingdom, leader);
        if (target != null)
        {
            StrategicAiState.SetTargetLock(army, target.CurrentSettlement);
            leader.SetMoveEngageParty(target, MobileParty.NavigationType.Default);
            return;
        }

        MobileParty? shadowTarget = StrategicAiHelpers.FindEnemyArmyToShadow(kingdom, leader);
        if (shadowTarget != null)
        {
            StrategicAiState.SetTargetLock(army, shadowTarget.TargetSettlement);
            leader.SetMoveGoAroundParty(shadowTarget, MobileParty.NavigationType.Default);
            return;
        }

        MoveToDefensiveTarget(kingdom, army, leader);
    }

    private static bool TryStageForSharedOperation(Kingdom kingdom, Army army, MobileParty leader)
    {
        if (StrategicAiState.GetRole(army) == StrategicArmyRole.Aggressor)
        {
            return false;
        }

        Settlement? operationTarget = StrategicAiState.GetOperationTarget(kingdom);
        if (operationTarget == null || operationTarget.MapFaction == kingdom || StrategicAiState.IsTargetOnCooldown(operationTarget))
        {
            return false;
        }

        float distanceToTarget = StrategicAiHelpers.Distance(leader, operationTarget);
        if (distanceToTarget <= StrategicAiTuning.StagingDistance || distanceToTarget > StrategicAiTuning.StagingTargetDistance)
        {
            return false;
        }

        Settlement? staging = StrategicAiHelpers.FindBestStagingSettlement(kingdom, operationTarget);
        if (staging == null)
        {
            return false;
        }

        StrategicAiState.SetTargetLock(army, staging);
        leader.SetMoveGoToSettlement(staging, MobileParty.NavigationType.Default, false);
        return true;
    }

    private static void MoveToReservePosition(Kingdom kingdom, Army army, MobileParty leader)
    {
        Settlement? reserveTarget = kingdom.InitialHomeSettlement ??
                                    StrategicAiHelpers.FindBestDefensiveSettlement(kingdom) ??
                                    StrategicAiHelpers.FindNearestFriendlyFortification(leader);

        if (reserveTarget == null)
        {
            return;
        }

        StrategicAiState.SetTargetLock(army, reserveTarget);
        leader.SetMovePatrolAroundSettlement(reserveTarget, MobileParty.NavigationType.Default, false);
    }

    private static void TryRetreatFromBadSiege(Army army)
    {
        MobileParty? leader = army.LeaderParty;
        Settlement? besiegedSettlement = leader?.BesiegedSettlement ?? leader?.TargetSettlement;

        if (leader == null ||
            leader.IsMainParty ||
            leader.MapFaction == null ||
            besiegedSettlement == null ||
            !besiegedSettlement.IsUnderSiege ||
            !StrategicAiHelpers.IsEnemy(leader.MapFaction, besiegedSettlement.MapFaction))
        {
            return;
        }

        float enemyPower = StrategicAiHelpers.EnemyLordPartiesNear(leader, StrategicAiTuning.SiegeRadarRadius)
            .Sum(party => party.GetTotalLandStrengthWithFollowers(false));

        if (enemyPower <= 1f)
        {
            return;
        }

        float friendlyPower = army.EstimatedStrength + StrategicAiHelpers.FriendlyLordPartiesNear(leader, StrategicAiTuning.LocalAllyRadius)
            .Sum(party => party.GetTotalLandStrengthWithFollowers(false));

        if (friendlyPower / enemyPower >= StrategicAiTuning.RetreatPowerRatio)
        {
            return;
        }

        LiftSiege(leader, besiegedSettlement);

        Settlement? fallback = StrategicAiHelpers.FindBestRetreatSettlement(leader) ??
                               StrategicAiHelpers.FindNearestFriendlyFortification(leader);
        if (fallback != null)
        {
            StrategicAiState.MarkFailedTarget(besiegedSettlement);
            leader.SetMoveGoToSettlement(fallback, MobileParty.NavigationType.Default, false);
        }
        else
        {
            StrategicAiState.MarkFailedTarget(besiegedSettlement);
            DisbandArmyAction.ApplyByUnknownReason(army);
        }
    }

    private static void TryReinforceFragileGarrison(Army army)
    {
        MobileParty? leader = army.LeaderParty;
        Settlement? target = leader?.CurrentSettlement ?? leader?.TargetSettlement;

        if (leader == null ||
            target == null ||
            !StrategicAiHelpers.IsFortification(target) ||
            !StrategicAiHelpers.IsFriendly(leader.MapFaction, target.MapFaction) ||
            !StrategicAiHelpers.IsLowGarrisonFortification(target) ||
            StrategicAiHelpers.Distance(leader, target) > 8f ||
            leader.MemberRoster.TotalHealthyCount <= StrategicAiTuning.MinimumLeaderPartyTroopsAfterDonation + StrategicAiTuning.ReinforcementTroopDonation)
        {
            return;
        }

        TroopRoster donatedRoster = leader.MemberRoster.RemoveNumberOfNonHeroTroopsRandomly(StrategicAiTuning.ReinforcementTroopDonation);
        if (donatedRoster.TotalManCount <= 0)
        {
            return;
        }

        target.Party.MemberRoster.Add(donatedRoster);
    }

    private static void LiftSiege(MobileParty leader, Settlement settlement)
    {
        MethodInfo? applyInternal = typeof(LiftSiegeAction).GetMethod(
            "ApplyInternal",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        applyInternal?.Invoke(null, new object[] { leader, settlement });
    }

    private static void OnSettlementOwnerChanged(
        Settlement settlement,
        bool openToClaim,
        Hero newOwner,
        Hero oldOwner,
        Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        if (StrategicAiHelpers.IsFortification(settlement))
        {
            StrategicAiState.MarkRecentlyCaptured(settlement);
            if (oldOwner?.MapFaction is Kingdom oldKingdom)
            {
                StrategicAiState.MarkLostClaim(oldKingdom, settlement);
            }
        }
    }

    private static void OnVillageBeingRaided(Village village)
    {
        if (village.Bound?.MapFaction is Kingdom kingdom)
        {
            StrategicAiState.SetRaidedVillage(kingdom, village);
        }
    }
}
