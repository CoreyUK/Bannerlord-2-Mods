using System;
using System.IO;
using System.Reflection;

namespace WeaponDurability;

internal static class WeaponDurabilityDebugLog
{
    private static bool Enabled => false;

    public static void Write(string message)
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            string path = Path.Combine(directory, "WeaponDurability.debug.log");
            File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
