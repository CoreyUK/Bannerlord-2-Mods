using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace StrategicCampaignAI;

internal static class StrategicAiState
{
    private static Dictionary<string, int> EnemyTerritoryDaysByArmy = new();
    private static Dictionary<string, StrategicArmyRole> RolesByArmy = new();
    private static Dictionary<string, string> TargetLocksByArmy = new();
    private static Dictionary<string, double> TargetLockDaysByArmy = new();
    private static Dictionary<string, double> RecentCaptureDaysBySettlement = new();
    private static Dictionary<string, StrategicFactionStatus> FactionStatuses = new();
    private static Dictionary<string, string> RaidedVillageByFaction = new();
    private static Dictionary<string, double> FailedTargetCooldownDays = new();
    private static Dictionary<string, string> LostSettlementClaimByFaction = new();
    private static Dictionary<string, StrategicWarGoal> WarGoalsByFaction = new();
    private static Dictionary<string, double> WarGoalUpdatedDaysByFaction = new();
    private static Dictionary<string, string> OperationTargetByFaction = new();

    public static void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("scai_enemy_territory_days_by_army", ref EnemyTerritoryDaysByArmy);
        dataStore.SyncData("scai_roles_by_army", ref RolesByArmy);
        dataStore.SyncData("scai_target_locks_by_army", ref TargetLocksByArmy);
        dataStore.SyncData("scai_target_lock_days_by_army", ref TargetLockDaysByArmy);
        dataStore.SyncData("scai_recent_capture_days_by_settlement", ref RecentCaptureDaysBySettlement);
        dataStore.SyncData("scai_raided_village_by_faction", ref RaidedVillageByFaction);
        dataStore.SyncData("scai_failed_target_cooldown_days", ref FailedTargetCooldownDays);
        dataStore.SyncData("scai_lost_settlement_claim_by_faction", ref LostSettlementClaimByFaction);
        dataStore.SyncData("scai_war_goals_by_faction", ref WarGoalsByFaction);
        dataStore.SyncData("scai_war_goal_updated_days_by_faction", ref WarGoalUpdatedDaysByFaction);
        dataStore.SyncData("scai_operation_target_by_faction", ref OperationTargetByFaction);

        EnemyTerritoryDaysByArmy ??= new Dictionary<string, int>();
        RolesByArmy ??= new Dictionary<string, StrategicArmyRole>();
        TargetLocksByArmy ??= new Dictionary<string, string>();
        TargetLockDaysByArmy ??= new Dictionary<string, double>();
        RecentCaptureDaysBySettlement ??= new Dictionary<string, double>();
        FactionStatuses ??= new Dictionary<string, StrategicFactionStatus>();
        RaidedVillageByFaction ??= new Dictionary<string, string>();
        FailedTargetCooldownDays ??= new Dictionary<string, double>();
        LostSettlementClaimByFaction ??= new Dictionary<string, string>();
        WarGoalsByFaction ??= new Dictionary<string, StrategicWarGoal>();
        WarGoalUpdatedDaysByFaction ??= new Dictionary<string, double>();
        OperationTargetByFaction ??= new Dictionary<string, string>();
    }

    public static int GetEnemyTerritoryDays(Army army)
    {
        return EnemyTerritoryDaysByArmy.TryGetValue(GetArmyKey(army), out int days) ? days : 0;
    }

    public static void SetEnemyTerritoryDays(Army army, int days)
    {
        string key = GetArmyKey(army);
        if (days <= 0)
        {
            EnemyTerritoryDaysByArmy.Remove(key);
            return;
        }

        EnemyTerritoryDaysByArmy[key] = days;
    }

    public static StrategicArmyRole GetRole(Army army)
    {
        return RolesByArmy.TryGetValue(GetArmyKey(army), out StrategicArmyRole role) ? role : StrategicArmyRole.Aggressor;
    }

    public static void SetRole(Army army, StrategicArmyRole role)
    {
        RolesByArmy[GetArmyKey(army)] = role;
    }

    public static void SetTargetLock(Army army, Settlement? settlement)
    {
        string key = GetArmyKey(army);
        if (settlement == null)
        {
            TargetLocksByArmy.Remove(key);
            TargetLockDaysByArmy.Remove(key);
            return;
        }

        if (!TargetLocksByArmy.TryGetValue(key, out string existingTarget) || existingTarget != settlement.StringId)
        {
            TargetLockDaysByArmy[key] = CampaignTime.Now.ToDays;
        }

        TargetLocksByArmy[key] = settlement.StringId;
    }

    public static Settlement? GetTargetLock(Army army)
    {
        return TargetLocksByArmy.TryGetValue(GetArmyKey(army), out string settlementId)
            ? FindSettlement(settlementId)
            : null;
    }

    public static bool IsCommittedToCurrentObjective(Army army)
    {
        return TargetLockDaysByArmy.TryGetValue(GetArmyKey(army), out double lockDays) &&
               (CampaignTime.Now.ToDays - lockDays) * 24d < StrategicAiTuning.ObjectiveCommitmentHours;
    }

    public static bool IsTargetLockedByAnotherArmy(Army army, Settlement settlement)
    {
        string armyKey = GetArmyKey(army);
        foreach (KeyValuePair<string, string> targetLock in TargetLocksByArmy)
        {
            if (targetLock.Key != armyKey && targetLock.Value == settlement.StringId)
            {
                return true;
            }
        }

        return false;
    }

    public static void MarkRecentlyCaptured(Settlement settlement)
    {
        RecentCaptureDaysBySettlement[settlement.StringId] = CampaignTime.Now.ToDays;
    }

    public static bool WasRecentlyCaptured(Settlement settlement)
    {
        return RecentCaptureDaysBySettlement.TryGetValue(settlement.StringId, out double captureDays) &&
               (CampaignTime.Now.ToDays - captureDays) * 24d <= StrategicAiTuning.RecentCaptureConsolidationHours;
    }

    public static void SetFactionStatus(Kingdom kingdom, StrategicFactionStatus status)
    {
        status.WarGoal = GetWarGoal(kingdom);
        status.WantsPeace = status.IsExhausted && status.ActiveWars > 0;
        FactionStatuses[kingdom.StringId] = status;
    }

    public static StrategicFactionStatus GetFactionStatus(IFaction faction)
    {
        if (faction is Kingdom kingdom && FactionStatuses.TryGetValue(kingdom.StringId, out StrategicFactionStatus status))
        {
            return status;
        }

        return new StrategicFactionStatus();
    }

    public static void SetRaidedVillage(Kingdom kingdom, Village village)
    {
        RaidedVillageByFaction[kingdom.StringId] = village.Settlement.StringId;
    }

    public static Settlement? GetRaidedVillageSettlement(Kingdom kingdom)
    {
        if (!RaidedVillageByFaction.TryGetValue(kingdom.StringId, out string settlementId))
        {
            return null;
        }

        foreach (Settlement settlement in Settlement.All)
        {
            if (settlement.StringId == settlementId)
            {
                return settlement;
            }
        }

        return null;
    }

    public static void MarkFailedTarget(Settlement settlement)
    {
        FailedTargetCooldownDays[settlement.StringId] = CampaignTime.Now.ToDays;
    }

    public static bool IsTargetOnCooldown(Settlement settlement)
    {
        return FailedTargetCooldownDays.TryGetValue(settlement.StringId, out double failedDays) &&
               (CampaignTime.Now.ToDays - failedDays) * 24d <= StrategicAiTuning.TargetFailureCooldownHours;
    }

    public static void MarkLostClaim(Kingdom kingdom, Settlement settlement)
    {
        LostSettlementClaimByFaction[kingdom.StringId] = settlement.StringId;
    }

    public static Settlement? GetLostClaim(Kingdom kingdom)
    {
        if (!LostSettlementClaimByFaction.TryGetValue(kingdom.StringId, out string settlementId))
        {
            return null;
        }

        return FindSettlement(settlementId);
    }

    public static StrategicWarGoal GetWarGoal(Kingdom kingdom)
    {
        return WarGoalsByFaction.TryGetValue(kingdom.StringId, out StrategicWarGoal goal)
            ? goal
            : StrategicWarGoal.BorderWar;
    }

    public static bool ShouldReevaluateWarGoal(Kingdom kingdom)
    {
        return !WarGoalUpdatedDaysByFaction.TryGetValue(kingdom.StringId, out double updatedDays) ||
               CampaignTime.Now.ToDays - updatedDays >= StrategicAiTuning.WarGoalReevaluationDays;
    }

    public static void SetWarGoal(Kingdom kingdom, StrategicWarGoal goal)
    {
        WarGoalsByFaction[kingdom.StringId] = goal;
        WarGoalUpdatedDaysByFaction[kingdom.StringId] = CampaignTime.Now.ToDays;
    }

    public static void SetOperationTarget(Kingdom kingdom, Settlement? settlement)
    {
        if (settlement == null)
        {
            OperationTargetByFaction.Remove(kingdom.StringId);
            return;
        }

        OperationTargetByFaction[kingdom.StringId] = settlement.StringId;
    }

    public static Settlement? GetOperationTarget(Kingdom kingdom)
    {
        return OperationTargetByFaction.TryGetValue(kingdom.StringId, out string settlementId)
            ? FindSettlement(settlementId)
            : null;
    }

    private static Settlement? FindSettlement(string settlementId)
    {
        foreach (Settlement settlement in Settlement.All)
        {
            if (settlement.StringId == settlementId)
            {
                return settlement;
            }
        }

        return null;
    }

    private static string GetArmyKey(Army army)
    {
        return army.LeaderParty?.Party?.Id ?? army.GetHashCode().ToString();
    }
}
