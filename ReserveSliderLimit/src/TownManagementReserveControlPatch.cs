using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement;
using TaleWorlds.Library;

namespace ReserveSliderLimit;

[HarmonyPatch(typeof(TownManagementReserveControlVM))]
internal static class TownManagementReserveControlPatch
{
    private const int ReserveSliderLimit = 100000;

    [HarmonyTranspiler]
    [HarmonyPatch(MethodType.Constructor, typeof(TaleWorlds.CampaignSystem.Settlements.Settlement), typeof(System.Action))]
    private static IEnumerable<CodeInstruction> ConstructorTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return ReplaceVanillaLimit(instructions);
    }

    [HarmonyPostfix]
    [HarmonyPatch(MethodType.Constructor, typeof(TaleWorlds.CampaignSystem.Settlements.Settlement), typeof(System.Action))]
    private static void ConstructorPostfix(TownManagementReserveControlVM __instance)
    {
        ApplyLimit(__instance);
    }

    [HarmonyTranspiler]
    [HarmonyPatch("ExecuteConfirm")]
    private static IEnumerable<CodeInstruction> ExecuteConfirmTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return ReplaceVanillaLimit(instructions);
    }

    [HarmonyPostfix]
    [HarmonyPatch("ExecuteConfirm")]
    private static void ExecuteConfirmPostfix(TownManagementReserveControlVM __instance)
    {
        ApplyLimit(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch("RefreshValues")]
    private static void RefreshValuesPostfix(TownManagementReserveControlVM __instance)
    {
        ApplyLimit(__instance);
    }

    private static void ApplyLimit(TownManagementReserveControlVM reserveControl)
    {
        reserveControl.MaxReserveAmount = GetEffectiveLimit();
    }

    private static int GetEffectiveLimit()
    {
        return MathF.Min(Hero.MainHero.Gold, ReserveSliderLimit);
    }

    private static IEnumerable<CodeInstruction> ReplaceVanillaLimit(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.Select(instruction =>
        {
            if (instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int value && value == 10000)
            {
                instruction.operand = ReserveSliderLimit;
            }

            return instruction;
        });
    }
}
