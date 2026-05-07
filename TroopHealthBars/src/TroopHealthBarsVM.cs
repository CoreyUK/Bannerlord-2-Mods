using TaleWorlds.Library;

namespace TroopHealthBars;

public sealed class TroopHealthBarItemVM : ViewModel
{
    private string _icon = string.Empty;
    private string _iconSprite = string.Empty;
    private string _label = string.Empty;
    private string _countText = string.Empty;
    private string _percentText = string.Empty;
    private int _aliveCount;
    private int _initialCount;
    private float _percentage;
    private bool _isVisible;

    [DataSourceProperty]
    public string Icon { get => _icon; set => SetField(ref _icon, value, nameof(Icon)); }

    [DataSourceProperty]
    public string IconSprite { get => _iconSprite; set => SetField(ref _iconSprite, value, nameof(IconSprite)); }

    [DataSourceProperty]
    public string Label { get => _label; set => SetField(ref _label, value, nameof(Label)); }

    [DataSourceProperty]
    public string CountText { get => _countText; set => SetField(ref _countText, value, nameof(CountText)); }

    [DataSourceProperty]
    public string PercentText { get => _percentText; set => SetField(ref _percentText, value, nameof(PercentText)); }

    [DataSourceProperty]
    public int AliveCount { get => _aliveCount; set => SetField(ref _aliveCount, value, nameof(AliveCount)); }

    [DataSourceProperty]
    public int InitialCount { get => _initialCount; set => SetField(ref _initialCount, value, nameof(InitialCount)); }

    [DataSourceProperty]
    public float Percentage { get => _percentage; set => SetField(ref _percentage, value, nameof(Percentage)); }

    [DataSourceProperty]
    public bool IsVisible { get => _isVisible; set => SetField(ref _isVisible, value, nameof(IsVisible)); }

    internal void SetValues(int aliveCount, int initialCount)
    {
        AliveCount = aliveCount;
        InitialCount = initialCount;
        Percentage = initialCount > 0 ? MBMath.ClampFloat((float)aliveCount / initialCount, 0f, 1f) : 0f;
        CountText = $"{aliveCount}/{initialCount}";
        PercentText = $"{MathF.Round(Percentage * 100f)}%";
        IsVisible = initialCount > 0;
    }
}

public sealed class TroopHealthBarsVM : ViewModel
{
    private MBBindingList<TroopHealthBarItemVM> _bars = new();

    public TroopHealthBarsVM()
    {
        Bars.Add(new TroopHealthBarItemVM { Icon = "I", IconSprite = "Order\\FormationTypeIcons\\Infantry", Label = "Infantry" });
        Bars.Add(new TroopHealthBarItemVM { Icon = "A", IconSprite = "Order\\FormationTypeIcons\\Ranged", Label = "Archers" });
        Bars.Add(new TroopHealthBarItemVM { Icon = "M", IconSprite = "Order\\FormationTypeIcons\\Cavalry", Label = "Mounted" });
    }

    [DataSourceProperty]
    public MBBindingList<TroopHealthBarItemVM> Bars { get => _bars; set => SetField(ref _bars, value, nameof(Bars)); }

    internal void SetValues(TroopCategory category, int aliveCount, int initialCount)
    {
        Bars[(int)category].SetValues(aliveCount, initialCount);
    }
}
