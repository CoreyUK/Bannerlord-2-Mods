using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace StrategicCampaignAI;

/// <summary>
/// Optional user overrides for <see cref="StrategicAiTuning"/>, read from
/// <c>Modules/StrategicCampaignAI/ModuleData/settings.txt</c>.
///
/// Format is one <c>Key = Value</c> per line; <c>#</c> and <c>//</c> start a comment.
/// Keys are the field names on <see cref="StrategicAiTuning"/> and are matched
/// case-insensitively. Unknown keys and unparseable values are ignored, so a
/// stale settings file can never stop the mod from loading.
/// </summary>
internal static class StrategicAiSettings
{
    private static bool _loaded;

    /// <summary>Whether a settings file was found and read. Reported in the log.</summary>
    public static bool FileFound { get; private set; }

    /// <summary>Resolved settings path, whether or not it exists.</summary>
    public static string? ResolvedPath { get; private set; }

    public static void LoadOnce()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        try
        {
            string? path = ResolveSettingsPath();
            ResolvedPath = path;
            if (path == null || !File.Exists(path))
            {
                return;
            }

            FileFound = true;

            foreach (string rawLine in File.ReadAllLines(path))
            {
                ApplyLine(rawLine);
            }
        }
        catch (Exception)
        {
            // A malformed or unreadable settings file must never break startup.
        }
    }

    private static string? ResolveSettingsPath()
    {
        string? binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        // .../<Module>/bin/Win64_Shipping_Client -> .../<Module>
        string? moduleRoot = Path.GetDirectoryName(Path.GetDirectoryName(binDirectory));
        return moduleRoot == null ? null : Path.Combine(moduleRoot, "ModuleData", "settings.txt");
    }

    private static void ApplyLine(string rawLine)
    {
        string line = rawLine.Trim();

        int commentIndex = line.IndexOf('#');
        if (commentIndex >= 0)
        {
            line = line.Substring(0, commentIndex).Trim();
        }

        commentIndex = line.IndexOf("//", StringComparison.Ordinal);
        if (commentIndex >= 0)
        {
            line = line.Substring(0, commentIndex).Trim();
        }

        int separator = line.IndexOf('=');
        if (separator <= 0)
        {
            return;
        }

        string key = line.Substring(0, separator).Trim();
        string value = line.Substring(separator + 1).Trim();
        if (key.Length == 0 || value.Length == 0)
        {
            return;
        }

        FieldInfo? field = typeof(StrategicAiTuning).GetField(
            key,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.IgnoreCase);

        if (field == null || field.IsInitOnly || field.IsLiteral)
        {
            return;
        }

        try
        {
            if (field.FieldType == typeof(float) &&
                float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
            {
                field.SetValue(null, floatValue);
            }
            else if (field.FieldType == typeof(double) &&
                     double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double doubleValue))
            {
                field.SetValue(null, doubleValue);
            }
            else if (field.FieldType == typeof(int) &&
                     int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
            {
                field.SetValue(null, intValue);
            }
            else if (field.FieldType == typeof(bool) && TryParseBool(value, out bool boolValue))
            {
                field.SetValue(null, boolValue);
            }
        }
        catch (Exception)
        {
            // Ignore this key and keep the built-in default.
        }
    }

    private static bool TryParseBool(string value, out bool result)
    {
        if (bool.TryParse(value, out result))
        {
            return true;
        }

        if (value == "1" || value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (value == "0" || value.Equals("no", StringComparison.OrdinalIgnoreCase) || value.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        result = false;
        return false;
    }
}
