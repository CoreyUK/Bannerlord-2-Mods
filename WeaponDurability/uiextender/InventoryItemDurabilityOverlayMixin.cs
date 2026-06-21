using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace WeaponDurability.UiExtender;

[ViewModelMixin]
public sealed class InventoryItemDurabilityOverlayMixin : BaseViewModelMixin<SPItemVM>
{
    public InventoryItemDurabilityOverlayMixin(SPItemVM viewModel) : base(viewModel)
    {
    }

    public override void OnRefresh()
    {
        base.OnRefresh();
        RefreshDurabilityProperties();
    }

    [DataSourceProperty]
    public bool HasWeaponDurabilityOverlay => GetDurability().HasValue;

    [DataSourceProperty]
    public string WeaponDurabilityOverlayText
    {
        get
        {
            int? durability = GetDurability();
            return durability.HasValue ? $"{durability.Value}%" : string.Empty;
        }
    }

    [DataSourceProperty]
    public bool ShowWeaponDurabilityGreen => GetDurability() is int durability && durability < 100 && durability >= 80;

    [DataSourceProperty]
    public bool ShowWeaponDurabilityYellow => GetDurability() is int durability && durability < 80 && durability >= 50;

    [DataSourceProperty]
    public bool ShowWeaponDurabilityAmber => GetDurability() is int durability && durability < 50 && durability >= 30;

    [DataSourceProperty]
    public bool ShowWeaponDurabilityRed => GetDurability() is int durability && durability < 30 && durability > 0;

    [DataSourceProperty]
    public bool ShowWeaponDurabilityBlack => GetDurability() is 0;

    private void RefreshDurabilityProperties()
    {
        OnPropertyChanged(nameof(HasWeaponDurabilityOverlay));
        OnPropertyChanged(nameof(WeaponDurabilityOverlayText));
        OnPropertyChanged(nameof(ShowWeaponDurabilityGreen));
        OnPropertyChanged(nameof(ShowWeaponDurabilityYellow));
        OnPropertyChanged(nameof(ShowWeaponDurabilityAmber));
        OnPropertyChanged(nameof(ShowWeaponDurabilityRed));
        OnPropertyChanged(nameof(ShowWeaponDurabilityBlack));
    }

    private int? GetDurability()
    {
        ItemRosterElement rosterElement = ViewModel.ItemRosterElement;
        if (rosterElement.EquipmentElement.Item != null)
        {
            return WeaponDurabilityBehavior.GetDurabilityForRosterElement(rosterElement);
        }

        return GetEquippedSlotDurability();
    }

    private int? GetEquippedSlotDurability()
    {
        Equipment? battleEquipment = Hero.MainHero?.BattleEquipment;
        if (battleEquipment == null)
        {
            return null;
        }

        foreach (EquipmentIndex equipmentIndex in new[]
                 {
                     EquipmentIndex.Weapon0,
                     EquipmentIndex.Weapon1,
                     EquipmentIndex.Weapon2,
                     EquipmentIndex.Weapon3
                 })
        {
            EquipmentElement equipmentElement = battleEquipment[equipmentIndex];
            ItemObject item = equipmentElement.Item;
            if (item == null)
            {
                continue;
            }

            if (MatchesViewModelEquipment(equipmentElement))
            {
                return WeaponDurabilityBehavior.GetDurabilityForEquipmentElement(equipmentElement);
            }
        }

        return null;
    }

    private bool MatchesViewModelEquipment(EquipmentElement equipmentElement)
    {
        ItemObject item = equipmentElement.Item;
        if (!string.IsNullOrWhiteSpace(ViewModel.StringId) && ViewModel.StringId == item.StringId)
        {
            return true;
        }

        string description = ViewModel.ItemDescription ?? string.Empty;
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        string modifiedName = equipmentElement.GetModifiedItemName()?.ToString() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(modifiedName) && description.Contains(modifiedName))
        {
            return true;
        }

        string itemName = item.Name?.ToString() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(itemName) && description.Contains(itemName))
        {
            return true;
        }

        string modifierName = equipmentElement.ItemModifier?.Name?.ToString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(modifierName) && description.Contains(modifierName);
    }
}
