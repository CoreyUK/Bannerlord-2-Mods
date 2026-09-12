using System;
using System.Globalization;
using System.IO;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;

namespace FieldFortifications;

/// <summary>
/// Player-tunable values read from Modules/FieldFortifications/settings.txt. Read when the campaign loads (for the
/// prices) and again at the start of every battle. A missing file or key means the default below.
/// </summary>
public sealed class FortificationSettings
{
    /// <summary>Denars charged for each work.</summary>
    public int BarricadeCost = 6000;
    public int BallistaCost = 10000;
    public int MangonelCost = 15000;

    /// <summary>Let the siege AI assign archers to crew the engines. Off means the player mans them.</summary>
    public bool CrewAi = true;

    /// <summary>Bolts or stones loaded on each engine at the start.</summary>
    public int EngineAmmo = 30;

    /// <summary>Picks up the next item (or the first) during deployment.</summary>
    public InputKey PlaceKey = InputKey.P;

    /// <summary>Fixes the held item where its ghost is.</summary>
    public InputKey ConfirmKey = InputKey.Enter;

    /// <summary>Drops the held item back where it was.</summary>
    public InputKey CancelKey = InputKey.BackSpace;

    public InputKey RotateLeftKey = InputKey.Left;
    public InputKey RotateRightKey = InputKey.Right;

    public static string Path => System.IO.Path.Combine(BasePath.Name, "Modules", "FieldFortifications", "settings.txt");

    public static FortificationSettings Load()
    {
        var settings = new FortificationSettings();
        try
        {
            if (!File.Exists(Path)) return settings;
            foreach (string raw in File.ReadAllLines(Path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string value = line.Substring(eq + 1).Trim();
                bool Flag() => value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                int Int(int fallback) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 0 ? n : fallback;
                InputKey Key(InputKey fallback) => Enum.TryParse(value, true, out InputKey k) ? k : fallback;
                switch (key)
                {
                    case "barricade_cost": settings.BarricadeCost = Int(settings.BarricadeCost); break;
                    case "ballista_cost": settings.BallistaCost = Int(settings.BallistaCost); break;
                    case "catapult_cost": settings.MangonelCost = Int(settings.MangonelCost); break;
                    case "crew_ai": settings.CrewAi = Flag(); break;
                    case "engine_ammo": settings.EngineAmmo = Int(settings.EngineAmmo); break;
                    case "place_key": settings.PlaceKey = Key(settings.PlaceKey); break;
                    case "confirm_key": settings.ConfirmKey = Key(settings.ConfirmKey); break;
                    case "cancel_key": settings.CancelKey = Key(settings.CancelKey); break;
                    case "rotate_left_key": settings.RotateLeftKey = Key(settings.RotateLeftKey); break;
                    case "rotate_right_key": settings.RotateRightKey = Key(settings.RotateRightKey); break;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("settings.txt could not be read, using defaults: " + ex.Message);
        }
        return settings;
    }
}

/// <summary>Writes problems to Modules/FieldFortifications/errors.txt. Nothing is written when all is well.</summary>
public static class ErrorLog
{
    private static string Path => System.IO.Path.Combine(BasePath.Name, "Modules", "FieldFortifications", "errors.txt");

    public static void Write(string text)
    {
        try { File.AppendAllText(Path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + text + Environment.NewLine); }
        catch { /* logging must never break the game */ }
    }
}
