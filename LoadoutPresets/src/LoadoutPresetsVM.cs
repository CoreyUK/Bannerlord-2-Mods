using System;
using TaleWorlds.Library;

namespace LoadoutPresets;

public sealed class PresetSlotVM : ViewModel
{
    private readonly int _index;
    private readonly Action<int> _apply;
    private readonly Action<int> _save;
    private string _label = string.Empty;
    private string _summary = string.Empty;
    private bool _hasPreset;

    public PresetSlotVM(int index, Action<int> apply, Action<int> save)
    {
        _index = index;
        _apply = apply;
        _save = save;
        Label = (index + 1).ToString();
    }

    [DataSourceProperty]
    public string Label { get => _label; set => SetField(ref _label, value, nameof(Label)); }

    [DataSourceProperty]
    public string Summary { get => _summary; set => SetField(ref _summary, value, nameof(Summary)); }

    [DataSourceProperty]
    public bool HasPreset { get => _hasPreset; set => SetField(ref _hasPreset, value, nameof(HasPreset)); }

    public void ExecuteApply() => _apply(_index);

    public void ExecuteSave() => _save(_index);
}

public sealed class LoadoutPresetsVM : ViewModel
{
    private readonly LoadoutPresetService _service;
    private readonly Func<int> _getEquipmentMode;
    private readonly Action _refreshInventory;
    private MBBindingList<PresetSlotVM> _presets = new();
    private string _statusText = string.Empty;
    private bool _isSuppressed;

    internal LoadoutPresetsVM(LoadoutPresetService service, Func<int> getEquipmentMode, Action refreshInventory)
    {
        _service = service;
        _getEquipmentMode = getEquipmentMode;
        _refreshInventory = refreshInventory;
        for (int i = 0; i < LoadoutPresetService.PresetCount; i++)
        {
            _presets.Add(new PresetSlotVM(i, ApplyPreset, SavePreset));
        }
    }

    [DataSourceProperty]
    public MBBindingList<PresetSlotVM> Presets { get => _presets; set => SetField(ref _presets, value, nameof(Presets)); }

    [DataSourceProperty]
    public string StatusText { get => _statusText; set => SetField(ref _statusText, value, nameof(StatusText)); }

    [DataSourceProperty]
    public bool IsSuppressed { get => _isSuppressed; set => SetField(ref _isSuppressed, value, nameof(IsSuppressed)); }

    public void Refresh()
    {
        for (int i = 0; i < _presets.Count; i++)
        {
            PresetSlotVM preset = _presets[i];
            preset.HasPreset = _service.HasPreset(i);
            preset.Summary = _service.GetSummary(i);
        }
    }

    private void ApplyPreset(int index)
    {
        StatusText = _service.ApplyPreset(index, _getEquipmentMode());
        _refreshInventory();
        Refresh();
    }

    private void SavePreset(int index)
    {
        StatusText = _service.SavePreset(index, _getEquipmentMode());
        Refresh();
    }
}
