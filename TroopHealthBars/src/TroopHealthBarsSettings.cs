using System;
using System.Linq;
using System.Reflection;

namespace TroopHealthBars;

public enum TroopHealthBarsLocation
{
    TopLeft,
    BottomRight,
    BottomLeft
}

public enum TroopHealthBarsColor
{
    BannerlordRed,
    DeepRed,
    Gold,
    Green,
    Blue
}

public sealed class TroopHealthBarsSettings
{
    public TroopHealthBarsLocation Location { get; set; } = TroopHealthBarsLocation.TopLeft;

    public TroopHealthBarsColor BarColor { get; set; } = TroopHealthBarsColor.BannerlordRed;

    public bool ShowInfantry { get; set; } = true;

    public bool ShowArchers { get; set; } = true;

    public bool ShowMounted { get; set; } = true;

    public bool ShowPercentages { get; set; } = true;

    public float Opacity { get; set; } = 1f;

    internal static TroopHealthBarsSettings Effective => TryReadMcmSettings() ?? new TroopHealthBarsSettings();

    private static TroopHealthBarsSettings? TryReadMcmSettings()
    {
        try
        {
            Type? settingsType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("TroopHealthBars.Mcm.TroopHealthBarsMcmSettings", false))
                .FirstOrDefault(type => type != null);

            object? instance = settingsType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetValue(null);
            if (instance == null)
            {
                return null;
            }

            return new TroopHealthBarsSettings
            {
                Location = ReadLocation(GetPropertyValue(instance, nameof(Location), TroopHealthBarsLocation.TopLeft)),
                BarColor = ReadColor(GetPropertyValue(instance, nameof(BarColor), TroopHealthBarsColor.BannerlordRed)),
                ShowInfantry = (bool)GetPropertyValue(instance, nameof(ShowInfantry), true),
                ShowArchers = (bool)GetPropertyValue(instance, nameof(ShowArchers), true),
                ShowMounted = (bool)GetPropertyValue(instance, nameof(ShowMounted), true),
                ShowPercentages = (bool)GetPropertyValue(instance, nameof(ShowPercentages), true),
                Opacity = (float)GetPropertyValue(instance, nameof(Opacity), 1f)
            };
        }
        catch
        {
            return null;
        }
    }

    private static object GetPropertyValue(object instance, string propertyName, object fallback)
    {
        return instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance) ?? fallback;
    }

    private static TroopHealthBarsLocation ReadLocation(object value)
    {
        if (value is TroopHealthBarsLocation location)
        {
            return location;
        }

        string selected = ReadSelectedText(value);
        return selected switch
        {
            "Bottom Right" => TroopHealthBarsLocation.BottomRight,
            "Bottom Left" => TroopHealthBarsLocation.BottomLeft,
            _ => TroopHealthBarsLocation.TopLeft
        };
    }

    private static TroopHealthBarsColor ReadColor(object value)
    {
        if (value is TroopHealthBarsColor color)
        {
            return color;
        }

        string selected = ReadSelectedText(value);
        return selected switch
        {
            "Deep Red" => TroopHealthBarsColor.DeepRed,
            "Gold" => TroopHealthBarsColor.Gold,
            "Green" => TroopHealthBarsColor.Green,
            "Blue" => TroopHealthBarsColor.Blue,
            _ => TroopHealthBarsColor.BannerlordRed
        };
    }

    private static string ReadSelectedText(object value)
    {
        return value.GetType().GetProperty("SelectedValue", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value)?.ToString() ?? string.Empty;
    }
}
