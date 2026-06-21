using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;

namespace WeaponDurability;

[HarmonyPatch]
internal static class InventorySetItemTooltipDurabilityPatch
{
    private static MethodBase TargetMethod()
    {
        MethodInfo? method = AccessTools.DeclaredMethod(
            typeof(ItemMenuVM),
            "SetItem",
            new[]
            {
                typeof(SPItemVM),
                typeof(InventoryLogic.InventorySide),
                typeof(ItemVM),
                typeof(BasicCharacterObject),
                typeof(int)
            });
        WeaponDurabilityDebugLog.Write($"TargetMethod ItemMenuVM.SetItem found={method != null}");
        return method ?? throw new MissingMethodException(nameof(ItemMenuVM), "SetItem");
    }

    [HarmonyPostfix]
    private static void Postfix(
        ItemMenuVM __instance,
        SPItemVM item,
        InventoryLogic.InventorySide currentEquipmentMode,
        ItemVM comparedItem,
        BasicCharacterObject character,
        int alternativeUsageIndex)
    {
        WeaponDurabilityDebugLog.Write(
            $"SetItem item={(item?.ItemDescription ?? "<null>")} stringId={(item?.StringId ?? "<null>")} compared={(comparedItem?.StringId ?? "<null>")} mode={currentEquipmentMode}");

        if (item != null)
        {
            WeaponDurabilityDebugLog.Write($"SetItem durability={InventoryTooltipDurabilityPatch.GetDurability(item)?.ToString() ?? "<none>"}");
            InventoryTooltipDurabilityPatch.AddDurabilityProperty(__instance, item);
        }

        if (comparedItem != null)
        {
            WeaponDurabilityDebugLog.Write($"SetItem compared durability={InventoryTooltipDurabilityPatch.GetDurability(comparedItem)?.ToString() ?? "<none>"}");
            InventoryTooltipDurabilityPatch.AddDurabilityProperty(__instance, comparedItem);
        }

        __instance.OnPropertyChanged(nameof(ItemMenuVM.TargetItemProperties));
        __instance.OnPropertyChanged(nameof(ItemMenuVM.ItemName));
        __instance.OnPropertyChanged("WeaponDurabilityText");
        __instance.OnPropertyChanged("HasWeaponDurability");
    }
}
