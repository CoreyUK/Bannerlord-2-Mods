using System;
using TaleWorlds.Library;

namespace FieldFortifications;

/// <summary>Data behind the card panel shown during deployment.</summary>
public sealed class PlacementPanelVM : ViewModel
{
    private MBBindingList<PlacementCardVM> _cards = new();
    private string _hint = "";

    [DataSourceProperty]
    public MBBindingList<PlacementCardVM> Cards
    {
        get => _cards;
        set { if (value != _cards) { _cards = value; OnPropertyChangedWithValue(value, nameof(Cards)); } }
    }

    [DataSourceProperty]
    public string Hint
    {
        get => _hint;
        set { if (value != _hint) { _hint = value; OnPropertyChangedWithValue(value, nameof(Hint)); } }
    }
}

/// <summary>One card: an item the player bought and can position.</summary>
public sealed class PlacementCardVM : ViewModel
{
    private readonly Action<PlacementCardVM> _onSelect;
    private string _name, _status, _icon;
    private bool _isSelected;

    public int Index { get; }

    public PlacementCardVM(int index, string name, string icon, Action<PlacementCardVM> onSelect)
    {
        Index = index;
        _name = name;
        _icon = icon;
        _status = "";
        _onSelect = onSelect;
    }

    [DataSourceProperty]
    public string Name
    {
        get => _name;
        set { if (value != _name) { _name = value; OnPropertyChangedWithValue(value, nameof(Name)); } }
    }

    [DataSourceProperty]
    public string Status
    {
        get => _status;
        set { if (value != _status) { _status = value; OnPropertyChangedWithValue(value, nameof(Status)); } }
    }

    [DataSourceProperty]
    public string Icon
    {
        get => _icon;
        set { if (value != _icon) { _icon = value; OnPropertyChangedWithValue(value, nameof(Icon)); } }
    }

    [DataSourceProperty]
    public bool IsSelected
    {
        get => _isSelected;
        set { if (value != _isSelected) { _isSelected = value; OnPropertyChangedWithValue(value, nameof(IsSelected)); } }
    }

    public void ExecuteSelect() => _onSelect(this);
}
