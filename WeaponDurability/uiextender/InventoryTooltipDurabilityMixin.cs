using TaleWorlds.Core;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using System.ComponentModel;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Library;

namespace WeaponDurability.UiExtender;

[ViewModelMixin]
public sealed class InventoryTooltipDurabilityMixin : BaseViewModelMixin<ItemMenuVM>
{
    public InventoryTooltipDurabilityMixin(ItemMenuVM viewModel) : base(viewModel)
    {
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public override void OnRefresh()
    {
        base.OnRefresh();
        OnPropertyChanged(nameof(WeaponDurabilityText));
        OnPropertyChanged(nameof(HasWeaponDurability));
    }

    [DataSourceProperty]
    public string WeaponDurabilityText
    {
        get
        {
            int? durability = GetTargetDurability();
            return durability.HasValue ? WeaponDurabilityBehavior.GetConditionLabel(durability.Value) : string.Empty;
        }
    }

    [DataSourceProperty]
    public bool HasWeaponDurability => !string.IsNullOrWhiteSpace(WeaponDurabilityText);

    private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ItemMenuVM.ItemName) ||
            args.PropertyName == nameof(ItemMenuVM.TargetItemProperties))
        {
            OnPropertyChanged(nameof(WeaponDurabilityText));
            OnPropertyChanged(nameof(HasWeaponDurability));
        }
    }

    private int? GetTargetDurability()
    {
        ItemVM? targetItem = GetPrivate<ItemVM>("_targetItem");
        if (targetItem == null)
        {
            return null;
        }

        ItemRosterElement rosterElement = targetItem.ItemRosterElement;
        if (rosterElement.EquipmentElement.Item == null)
        {
            return null;
        }

        return WeaponDurabilityBehavior.GetDurabilityForRosterElement(rosterElement);
    }
}
