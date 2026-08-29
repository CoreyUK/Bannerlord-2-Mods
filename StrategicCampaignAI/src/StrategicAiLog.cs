using System;
using System.IO;
using System.Reflection;
using TaleWorlds.CampaignSystem;

namespace StrategicCampaignAI;

/// <summary>
/// Diagnostic log for the strategic layer.
///
/// This deliberately does not use TaleWorlds.Library.Debug.Print: in
/// Win64_Shipping_Client that call is effectively inert, so nothing written
/// through it reaches rgl_log.txt or ButterLib's capture. Writing our own file
/// also gives users a single small artefact to attach to a bug report.
///
/// Written to Documents\Mount and Blade II Bannerlord\StrategicCampaignAI.log,
/// falling back to the module folder if Documents is not writable. Every failure
/// disables logging rather than propagating -- diagnostics must never be able to
/// break a campaign.
/// </summary>
internal static class StrategicAiLog
{
    private static StreamWriter? _writer;
    private static bool _initialised;
    private static bool _disabled;

    public static string? FilePath { get; private set; }

    /// <summary>Diagnostic detail. Only written when VerboseLogging is on.</summary>
    public static void Write(string message)
    {
        if (!StrategicAiTuning.VerboseLogging)
        {
            return;
        }

        WriteCore(message);
    }

    /// <summary>
    /// A genuine fault. Always recorded, regardless of VerboseLogging, so that a
    /// user reporting a problem has something to attach even with the default
    /// settings.
    /// </summary>
    public static void WriteError(string message)
    {
        WriteCore("ERROR: " + message);
    }

    private static void WriteCore(string message)
    {
        if (_disabled)
        {
            return;
        }

        try
        {
            EnsureOpen();
            if (_writer == null)
            {
                return;
            }

            _writer.WriteLine(Timestamp() + " " + message);
        }
        catch (Exception)
        {
            Disable();
        }
    }

    private static string Timestamp()
    {
        try
        {
            // The in-game date, not ToDays: that counts from an internal epoch
            // and renders as a five-digit day number that means nothing to a
            // reader trying to line the log up against their campaign.
            CampaignTime now = CampaignTime.Now;
            return "[" + now.GetYear + " " + now.GetSeasonOfYear + " " + now.GetDayOfSeason +
                   " " + now.GetHourOfDay.ToString("00") + ":00]";
        }
        catch (Exception)
        {
            return "[--]";
        }
    }

    private static void EnsureOpen()
    {
        if (_initialised)
        {
            return;
        }

        _initialised = true;

        foreach (string? candidate in new[] { DocumentsPath(), ModuleFolderPath() })
        {
            if (candidate == null)
            {
                continue;
            }

            try
            {
                string? directory = Path.GetDirectoryName(candidate);
                if (directory != null && !Directory.Exists(directory))
                {
                    continue;
                }

                // Truncate per session so the file cannot grow without bound.
                _writer = new StreamWriter(candidate, append: false) { AutoFlush = true };
                FilePath = candidate;
                _writer.WriteLine("Strategic Campaign AI " + ModuleVersion() + " - session log");
                _writer.WriteLine("Started " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                _writer.WriteLine();
                return;
            }
            catch (Exception)
            {
                _writer = null;
            }
        }

        Disable();
    }

    private static string ModuleVersion()
    {
        try
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        }
        catch (Exception)
        {
            return "unknown";
        }
    }

    private static string? DocumentsPath()
    {
        try
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return string.IsNullOrEmpty(documents)
                ? null
                : Path.Combine(documents, "Mount and Blade II Bannerlord", "StrategicCampaignAI.log");
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? ModuleFolderPath()
    {
        try
        {
            string? binDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string? moduleRoot = Path.GetDirectoryName(Path.GetDirectoryName(binDirectory));
            return moduleRoot == null ? null : Path.Combine(moduleRoot, "StrategicCampaignAI.log");
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void Disable()
    {
        _disabled = true;
        try
        {
            _writer?.Dispose();
        }
        catch (Exception)
        {
            // Nothing further to do.
        }

        _writer = null;
    }
}
