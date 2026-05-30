using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace StrategicCampaignAI;

internal static class StrategicAiState
{
    private static Dictionary<string, int> EnemyTerritoryDaysByArmy = new();
    private static Dictionary<string, int> RolesByArmy = new();
    private static Dictionary<string, string> TargetLocksByArmy = new();
    private static Dictionary<string, double> TargetLockDaysByArmy = new();
    private static Dictionary<string, double> RecentCaptureDaysBySettlement = new();
    private static Dictionary<string, StrategicFactionStatus> FactionStatuses = new();
    private static Dictionary<string, string> RaidedVillageByFaction = new();
    private static Dictionary<string, double> FailedTargetCooldownDays = new();
    private static Dictionary<string, string> LostSettlementClaimByFaction = new();
    private static Dictionary<string, int> WarGoalsByFaction = new();
    private static Dictionary<string, double> WarGoalUpdatedDaysByFaction = new();
    private static Dictionary<string, string> OperationTargetByFaction = new();

    public static void SyncData(IDataStore dataStore)
    {
        // Runtime-only memory. Persisting these dictionaries can make Bannerlord fail save creation.
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
        return RolesByArmy.TryGetValue(GetArmyKey(army), out int role) &&
               role >= (int)StrategicArmyRole.Aggressor &&
               role <= (int)StrategicArmyRole.Reserve
            ? (StrategicArmyRole)role
            : StrategicArmyRole.Aggressor;
    }

    public static void SetRole(Army army, StrategicArmyRole role)
    {
        RolesByArmy[GetArmyKey(army)] = (int)role;
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
        return WarGoalsByFaction.TryGetValue(kingdom.StringId, out int goal) &&
               goal >= (int)StrategicWarGoal.BorderWar &&
               goal <= (int)StrategicWarGoal.ForcePeace
            ? (StrategicWarGoal)goal
            : StrategicWarGoal.BorderWar;
    }

    public static bool ShouldReevaluateWarGoal(Kingdom kingdom)
    {
        return !WarGoalUpdatedDaysByFaction.TryGetValue(kingdom.StringId, out double updatedDays) ||
               CampaignTime.Now.ToDays - updatedDays >= StrategicAiTuning.WarGoalReevaluationDays;
    }

    public static void SetWarGoal(Kingdom kingdom, StrategicWarGoal goal)
    {
        WarGoalsByFaction[kingdom.StringId] = (int)goal;
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
