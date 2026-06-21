using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;

namespace WeaponDurability;

[HarmonyPatch(typeof(SPItemVM))]
internal static class InventoryDurabilityPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (ConstructorInfo constructor in AccessTools.GetDeclaredConstructors(typeof(SPItemVM)))
        {
            yield return constructor;
        }

        foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(SPItemVM)).Where(method => method.Name == "RefreshWith"))
        {
            yield return method;
        }
    }

    [HarmonyPostfix]
    private static void Postfix(SPItemVM __instance)
    {
        ApplyMarker(__instance);
    }

    private static void ApplyMarker(SPItemVM itemVm)
    {
        ItemRosterElement rosterElement = GetRosterElement(itemVm);
        if (rosterElement.EquipmentElement.Item == null)
        {
            return;
        }

        itemVm.ItemDescription = WeaponDurabilityBehavior.StripInventoryMarker(itemVm.ItemDescription ?? string.Empty);
        itemVm.IsItemHighlightEnabled = WeaponDurabilityBehavior.IsCriticalDurability(rosterElement);
    }

    private static ItemRosterElement GetRosterElement(SPItemVM itemVm)
    {
        Traverse traverse = Traverse.Create(itemVm);
        ItemRosterElement rosterElement = traverse.Field<ItemRosterElement>("ItemRosterElement").Value;
        if (rosterElement.EquipmentElement.Item != null)
        {
            return rosterElement;
        }

        rosterElement = traverse.Field<ItemRosterElement>("_itemRosterElement").Value;
        return rosterElement;
    }
}
