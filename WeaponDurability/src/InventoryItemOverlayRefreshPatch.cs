using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;

namespace WeaponDurability;

[HarmonyPatch(typeof(SPItemVM))]
internal static class InventoryItemOverlayRefreshPatch
{
    private static readonly string[] OverlayProperties =
    {
        "HasWeaponDurabilityOverlay",
        "WeaponDurabilityOverlayText",
        "ShowWeaponDurabilityGreen",
        "ShowWeaponDurabilityYellow",
        "ShowWeaponDurabilityAmber",
        "ShowWeaponDurabilityRed",
        "ShowWeaponDurabilityBlack"
    };

    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(SPItemVM))
                     .Where(method => method.Name is "RefreshValues" or "RefreshWith"))
        {
            yield return method;
        }
    }

    [HarmonyPostfix]
    private static void Postfix(SPItemVM __instance)
    {
        foreach (string property in OverlayProperties)
        {
            __instance.OnPropertyChanged(property);
        }
    }
}
