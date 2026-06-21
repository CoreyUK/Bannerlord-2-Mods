using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;

namespace WeaponDurability;

[HarmonyPatch]
internal static class InventoryWeaponTooltipDurabilityPatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo? method = AccessTools.DeclaredMethod(
            typeof(ItemMenuVM),
            "SetWeaponComponentTooltip",
            new[]
            {
                typeof(EquipmentElement).MakeByRefType(),
                typeof(int),
                typeof(EquipmentElement),
                typeof(int)
            });
        WeaponDurabilityDebugLog.Write($"TargetMethod ItemMenuVM.SetWeaponComponentTooltip found={method != null}");
        return method ?? throw new MissingMethodException(nameof(ItemMenuVM), "SetWeaponComponentTooltip");
    }

    [HarmonyPostfix]
    private static void Postfix(ItemMenuVM __instance, ref EquipmentElement __0)
    {
        WeaponDurabilityDebugLog.Write(
            $"SetWeaponComponentTooltip item={__0.Item?.StringId ?? "<null>"} modifier={__0.ItemModifier?.StringId ?? "<null>"}");

        if (__0.Item == null)
        {
            return;
        }

        int? durability = WeaponDurabilityBehavior.GetDurabilityForEquipmentElement(__0);
        WeaponDurabilityDebugLog.Write($"SetWeaponComponentTooltip durability={durability?.ToString() ?? "<none>"}");
        if (durability.HasValue)
        {
            InventoryTooltipDurabilityPatch.AddDurabilityProperty(__instance, durability.Value);
        }
    }
}
