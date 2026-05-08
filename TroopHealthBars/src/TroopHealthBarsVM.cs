using TaleWorlds.Library;

namespace TroopHealthBars;

public sealed class TroopHealthBarItemVM : ViewModel
{
    private readonly TroopCategory _category;
    private string _icon = string.Empty;
    private string _iconSprite = string.Empty;
    private string _label = string.Empty;
    private string _countText = string.Empty;
    private string _percentText = string.Empty;
    private int _aliveCount;
    private int _initialCount;
    private float _percentage;
    private bool _isVisible;
    private bool _useBannerlordRed;
    private bool _useDeepRed;
    private bool _useGold;
    private bool _useGreen;
    private bool _useBlue;
    private bool _showPercentages = true;

    internal TroopHealthBarItemVM(TroopCategory category)
    {
        _category = category;
    }

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

    [DataSourceProperty]
    public bool UseBannerlordRed { get => _useBannerlordRed; set => SetField(ref _useBannerlordRed, value, nameof(UseBannerlordRed)); }

    [DataSourceProperty]
    public bool UseDeepRed { get => _useDeepRed; set => SetField(ref _useDeepRed, value, nameof(UseDeepRed)); }

    [DataSourceProperty]
    public bool UseGold { get => _useGold; set => SetField(ref _useGold, value, nameof(UseGold)); }

    [DataSourceProperty]
    public bool UseGreen { get => _useGreen; set => SetField(ref _useGreen, value, nameof(UseGreen)); }

    [DataSourceProperty]
    public bool UseBlue { get => _useBlue; set => SetField(ref _useBlue, value, nameof(UseBlue)); }

    [DataSourceProperty]
    public bool ShowPercentages { get => _showPercentages; set => SetField(ref _showPercentages, value, nameof(ShowPercentages)); }

    internal void SetValues(int aliveCount, int initialCount)
    {
        AliveCount = aliveCount;
        InitialCount = initialCount;
        Percentage = initialCount > 0 ? MBMath.ClampFloat((float)aliveCount / initialCount, 0f, 1f) : 0f;
        CountText = $"{aliveCount}/{initialCount}";
        PercentText = $"{MathF.Round(Percentage * 100f)}%";
        RefreshSettings();
    }

    internal void RefreshSettings()
    {
        TroopHealthBarsSettings settings = TroopHealthBarsSettings.Effective;
        IsVisible = InitialCount > 0 && IsCategoryEnabled(settings);
        UseBannerlordRed = settings.BarColor == TroopHealthBarsColor.BannerlordRed;
        UseDeepRed = settings.BarColor == TroopHealthBarsColor.DeepRed;
        UseGold = settings.BarColor == TroopHealthBarsColor.Gold;
        UseGreen = settings.BarColor == TroopHealthBarsColor.Green;
        UseBlue = settings.BarColor == TroopHealthBarsColor.Blue;
        ShowPercentages = settings.ShowPercentages;
    }

    private bool IsCategoryEnabled(TroopHealthBarsSettings settings)
    {
        return _category switch
        {
            TroopCategory.Infantry => settings.ShowInfantry,
            TroopCategory.Archer => settings.ShowArchers,
            TroopCategory.Mounted => settings.ShowMounted,
            _ => true
        };
    }
}

public sealed class TroopHealthBarsVM : ViewModel
{
    private MBBindingList<TroopHealthBarItemVM> _bars = new();
    private bool _isTopLeft;
    private bool _isBottomRight;
    private bool _isBottomLeft;
    private float _opacity = 1f;

    public TroopHealthBarsVM()
    {
        Bars.Add(new TroopHealthBarItemVM(TroopCategory.Infantry) { Icon = "I", IconSprite = "Order\\FormationTypeIcons\\Infantry", Label = "Infantry" });
        Bars.Add(new TroopHealthBarItemVM(TroopCategory.Archer) { Icon = "A", IconSprite = "Order\\FormationTypeIcons\\Ranged", Label = "Archers" });
        Bars.Add(new TroopHealthBarItemVM(TroopCategory.Mounted) { Icon = "M", IconSprite = "Order\\FormationTypeIcons\\Cavalry", Label = "Mounted" });
        RefreshSettings();
    }

    [DataSourceProperty]
    public MBBindingList<TroopHealthBarItemVM> Bars { get => _bars; set => SetField(ref _bars, value, nameof(Bars)); }

    [DataSourceProperty]
    public bool IsTopLeft { get => _isTopLeft; set => SetField(ref _isTopLeft, value, nameof(IsTopLeft)); }

    [DataSourceProperty]
    public bool IsBottomRight { get => _isBottomRight; set => SetField(ref _isBottomRight, value, nameof(IsBottomRight)); }

    [DataSourceProperty]
    public bool IsBottomLeft { get => _isBottomLeft; set => SetField(ref _isBottomLeft, value, nameof(IsBottomLeft)); }

    [DataSourceProperty]
    public float Opacity { get => _opacity; set => SetField(ref _opacity, value, nameof(Opacity)); }

    internal void SetValues(TroopCategory category, int aliveCount, int initialCount)
    {
        Bars[(int)category].SetValues(aliveCount, initialCount);
    }

    internal void RefreshSettings()
    {
        TroopHealthBarsSettings settings = TroopHealthBarsSettings.Effective;
        IsTopLeft = settings.Location == TroopHealthBarsLocation.TopLeft;
        IsBottomRight = settings.Location == TroopHealthBarsLocation.BottomRight;
        IsBottomLeft = settings.Location == TroopHealthBarsLocation.BottomLeft;
        Opacity = MBMath.ClampFloat(settings.Opacity, 0.15f, 1f);

        foreach (TroopHealthBarItemVM bar in Bars)
        {
            bar.RefreshSettings();
        }
    }
}
