using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Objects.Usables;

namespace FieldFortifications;

/// <summary>
/// The ranged siege weapon AI assumes a siege: its threat seeker reads castle positions that do not exist in a field
/// battle and throws a NullReferenceException the moment an AI pilot mounts an engine. In field battles the threat
/// list is built here instead, from the enemy formations, and the AI's tick methods get finalizers so any further
/// siege-only assumption is logged rather than ending the game.
/// </summary>
internal static class SiegeAiPatches
{
    private static readonly Type? ThreatSeekerType = AccessTools.Inner(typeof(RangedSiegeWeaponAi), "ThreatSeeker");
    private static readonly FieldInfo? WeaponField = ThreatSeekerType == null ? null : AccessTools.Field(ThreatSeekerType, "Weapon");
    private static readonly FieldInfo? SingleUnitField = ThreatSeekerType == null ? null : AccessTools.Field(ThreatSeekerType, "SingleUnitThreatValue");
    private static readonly HashSet<string> _logged = new();

    /// <summary>Standing points of the arrow barrels this mod spawned in the current battle.</summary>
    public static readonly HashSet<StandingPoint> BarrelPoints = new();

    public static void Apply(Harmony harmony)
    {
        if (ThreatSeekerType == null) throw new InvalidOperationException("RangedSiegeWeaponAi.ThreatSeeker not found");

        MethodInfo getAll = AccessTools.Method(ThreatSeekerType, "GetAllThreats") ?? throw new InvalidOperationException("GetAllThreats not found");
        harmony.Patch(getAll,
            prefix: new HarmonyMethod(typeof(SiegeAiPatches), nameof(GetAllThreatsPrefix)),
            finalizer: new HarmonyMethod(typeof(SiegeAiPatches), nameof(GetAllThreatsFinalizer)));

        MethodInfo? getMax = AccessTools.Method(ThreatSeekerType, "GetMaxThreat");
        if (getMax != null)
            harmony.Patch(getMax, finalizer: new HarmonyMethod(typeof(SiegeAiPatches), nameof(GetMaxThreatFinalizer)));

        foreach (string name in new[] { "OnTick", "AfterTick", "FindNextTarget", "SetTargetFromThreatSeeker" })
        {
            MethodInfo? method = AccessTools.Method(typeof(RangedSiegeWeaponAi), name);
            if (method != null) harmony.Patch(method, finalizer: new HarmonyMethod(typeof(SiegeAiPatches), nameof(SwallowFinalizer)));
        }
        // Crew assignment onto any of our machines: a failure here should cost one assignment, not the battle.
        MethodInfo? addAgent = AccessTools.Method(typeof(UsableMachine), "TaleWorlds.MountAndBlade.IDetachment.AddAgent");
        if (addAgent != null) harmony.Patch(addAgent, finalizer: new HarmonyMethod(typeof(SiegeAiPatches), nameof(SwallowFinalizer)));

        // Our arrow barrels: AI archers are only eligible once their quiver is actually low.
        MethodInfo? disabledFor = AccessTools.Method(typeof(StandingPointWithWeaponRequirement), "IsDisabledForAgent");
        if (disabledFor != null) harmony.Patch(disabledFor, postfix: new HarmonyMethod(typeof(SiegeAiPatches), nameof(BarrelEligibilityPostfix)));
    }

    private static void BarrelEligibilityPostfix(StandingPointWithWeaponRequirement __instance, Agent agent, ref bool __result)
    {
        try
        {
            if (__result || agent == null || !agent.IsAIControlled || BarrelPoints.Count == 0 || !BarrelPoints.Contains(__instance)) return;
            if (AmmoFraction(agent) > FortificationSettings.Current.RefillBelow) __result = true;
        }
        catch (Exception ex)
        {
            LogOnce("BarrelEligibility", ex);
        }
    }

    /// <summary>Arrows and bolts carried, as a fraction of what the agent's quivers hold when full.</summary>
    private static float AmmoFraction(Agent agent)
    {
        int amount = 0, max = 0;
        MissionEquipment equipment = agent.Equipment;
        for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
        {
            MissionWeapon weapon = equipment[slot];
            if (weapon.IsEmpty) continue;
            WeaponComponentData? usage = weapon.CurrentUsageItem;
            if (usage == null || !usage.IsConsumable) continue;
            if (usage.WeaponClass != WeaponClass.Arrow && usage.WeaponClass != WeaponClass.Bolt) continue;
            amount += weapon.Amount;
            max += weapon.ModifiedMaxAmount;
        }
        return max > 0 ? (float)amount / max : 1f;
    }

    /// <summary>Field battles only: enemy formations are the threats, weighted by size and nearness.</summary>
    private static bool GetAllThreatsPrefix(object __instance, ref List<Threat> __result)
    {
        Mission? mission = Mission.Current;
        if (mission == null || !mission.IsFieldBattle) return true;
        var threats = new List<Threat>();
        __result = threats;
        if (WeaponField?.GetValue(__instance) is not RangedSiegeWeapon weapon) return false;
        float unitValue = SingleUnitField?.GetValue(__instance) is float v && v > 0f ? v : 1f;
        Vec2 origin = weapon.GameEntity.GlobalPosition.AsVec2;
        foreach (Team team in mission.Teams)
        {
            if (team.Side == weapon.Side || team.Side == BattleSideEnum.None) continue;
            foreach (Formation formation in team.FormationsIncludingEmpty)
            {
                if (formation.CountOfUnits == 0) continue;
                float distance = formation.CurrentPosition.Distance(origin);
                float value = formation.CountOfUnits * unitValue / (1f + distance / 100f);
                threats.Add(new Threat { Formation = formation, ThreatValue = value });
            }
        }
        return false;
    }

    private static Exception? GetAllThreatsFinalizer(Exception? __exception, ref List<Threat> __result)
    {
        if (__exception == null) return null;
        LogOnce("GetAllThreats", __exception);
        __result ??= new List<Threat>();
        return null;
    }

    private static Exception? GetMaxThreatFinalizer(Exception? __exception, ref Threat? __result)
    {
        if (__exception == null) return null;
        LogOnce("GetMaxThreat", __exception);
        __result = null;
        return null;
    }

    private static Exception? SwallowFinalizer(Exception? __exception, MethodBase __originalMethod)
    {
        if (__exception != null) LogOnce(__originalMethod.Name, __exception);
        return null;
    }

    private static void LogOnce(string where, Exception ex)
    {
        string key = where + ":" + ex.GetType().Name;
        if (!_logged.Add(key)) return;
        ErrorLog.Write($"Siege AI {where} threw {ex.GetType().Name}: {ex.Message} (swallowed; further identical errors not logged)\n{ex.StackTrace}");
    }
}
