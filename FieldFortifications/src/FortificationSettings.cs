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
    /// <summary>Denars charged for the first copy of each work, indexed by FortificationState.Work.</summary>
    public readonly int[] BaseCost = { 6000, 10000, 15000, 2500, 8000 };

    /// <summary>How many of each work may be bought for one battle, indexed by FortificationState.Work.</summary>
    public readonly int[] MaxCount = { 3, 2, 2, 4, 1 };

    /// <summary>Each further copy of a work costs this fraction of the base price more than the one before.</summary>
    public float PriceStep = 0.5f;

    /// <summary>Price of the next copy of a work, given how many are already bought. Rounded to the nearest 100.</summary>
    public int Price(FortificationState.Work work, int alreadyBought)
    {
        float raw = BaseCost[(int)work] * (1f + PriceStep * alreadyBought);
        return Math.Max(0, (int)Math.Round(raw / 100f) * 100);
    }

    public int Max(FortificationState.Work work) => Math.Max(0, MaxCount[(int)work]);

    /// <summary>Let the siege AI assign archers to crew the engines. Off means the player mans them.</summary>
    public bool CrewAi = true;

    /// <summary>Write the stack of any null-reference thrown during a battle to debug.txt.</summary>
    public bool Debug;

    /// <summary>AI archers go to an arrow barrel only when their quiver is below this fraction of full.</summary>
    public float RefillBelow = 0.3f;

    /// <summary>The settings in force for the current battle.</summary>
    public static FortificationSettings Current = new();

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
                    case "barricade_cost": settings.BaseCost[0] = Int(settings.BaseCost[0]); break;
                    case "ballista_cost": settings.BaseCost[1] = Int(settings.BaseCost[1]); break;
                    case "catapult_cost": settings.BaseCost[2] = Int(settings.BaseCost[2]); break;
                    case "arrows_cost": settings.BaseCost[3] = Int(settings.BaseCost[3]); break;
                    case "tower_cost": settings.BaseCost[4] = Int(settings.BaseCost[4]); break;
                    case "max_barricades": settings.MaxCount[0] = Int(settings.MaxCount[0]); break;
                    case "max_ballistas": settings.MaxCount[1] = Int(settings.MaxCount[1]); break;
                    case "max_catapults": settings.MaxCount[2] = Int(settings.MaxCount[2]); break;
                    case "max_arrows": settings.MaxCount[3] = Int(settings.MaxCount[3]); break;
                    case "max_platforms": settings.MaxCount[4] = Int(settings.MaxCount[4]); break;
                    case "price_step":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float step) && step >= 0f && step <= 10f) settings.PriceStep = step;
                        break;
                    case "crew_ai": settings.CrewAi = Flag(); break;
                    case "debug": settings.Debug = Flag(); break;
                    case "refill_below":
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && f >= 0f && f <= 1f) settings.RefillBelow = f;
                        break;
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
