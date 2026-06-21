using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace WeaponDurability;

internal static class InventoryTooltipDurabilityPatch
{
    internal static void AddDurabilityProperty(ItemMenuVM itemMenu, ItemVM item)
    {
        int? durability = GetDurability(item);
        if (!durability.HasValue)
        {
            return;
        }

        AddDurabilityProperty(itemMenu, durability.Value);
    }

    internal static int? GetDurability(ItemVM item)
    {
        ItemRosterElement rosterElement = GetRosterElement(item);
        if (rosterElement.EquipmentElement.Item == null)
        {
            return null;
        }

        return WeaponDurabilityBehavior.GetDurabilityForRosterElement(rosterElement);
    }

    internal static void AddDurabilityProperty(
        ItemMenuVM itemMenu,
        int durability,
        MBBindingList<ItemMenuTooltipPropertyVM>? targetProperties = null,
        bool applyTitleMarker = true)
    {
        targetProperties ??= itemMenu.TargetItemProperties;
        if (HasDurabilityProperty(targetProperties))
        {
            WeaponDurabilityDebugLog.Write($"Tooltip add skipped existing durability={durability}");
            return;
        }

        WeaponDurabilityDebugLog.Write($"Tooltip adding durability={durability} propertiesBefore={targetProperties.Count}");

        targetProperties.Add(new ItemMenuTooltipPropertyVM(
            "Durability:",
            WeaponDurabilityBehavior.GetConditionLabel(durability),
            0,
            WeaponDurabilityBehavior.GetConditionColor(durability),
            false,
            null,
            TooltipProperty.TooltipPropertyFlags.None,
            string.Empty,
            false));

        itemMenu.OnPropertyChanged(nameof(ItemMenuVM.TargetItemProperties));
        itemMenu.OnPropertyChanged(nameof(ItemMenuVM.ItemName));
        itemMenu.OnPropertyChanged("WeaponDurabilityText");
        itemMenu.OnPropertyChanged("HasWeaponDurability");
    }

    private static void ApplyTitleMarker(ItemMenuVM itemMenu, int durability)
    {
        Traverse titleProperty = Traverse.Create(itemMenu).Property("ItemName");
        string title = titleProperty.GetValue<string>();
        if (string.IsNullOrWhiteSpace(title) || title.Contains("% "))
        {
            return;
        }

        titleProperty.SetValue($"{title} ({WeaponDurabilityBehavior.GetConditionLabel(durability)})");
    }

    private static ItemRosterElement GetRosterElement(ItemVM item)
    {
        Traverse traverse = Traverse.Create(item);
        ItemRosterElement rosterElement = traverse.Field<ItemRosterElement>("ItemRosterElement").Value;
        if (rosterElement.EquipmentElement.Item != null)
        {
            return rosterElement;
        }

        return traverse.Field<ItemRosterElement>("_itemRosterElement").Value;
    }

    private static bool HasDurabilityProperty(MBBindingList<ItemMenuTooltipPropertyVM> properties)
    {
        for (int i = 0; i < properties.Count; i++)
        {
            if (properties[i].DefinitionLabel == "Durability:")
            {
                return true;
            }
        }

        return false;
    }
}
