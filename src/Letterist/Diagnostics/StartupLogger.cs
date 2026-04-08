using System;
using System.IO;

namespace Letterist.Diagnostics;

internal static class StartupLogger
{
    private static readonly object SyncRoot = new();
    private static string? _logPath;
    private static bool _enabled = true;

    public static void Configure(bool enabled, string? logPath = null)
    {
        _enabled = enabled;
        if (!string.IsNullOrWhiteSpace(logPath))
        {
            _logPath = logPath.Trim();
        }
    }

    public static void Log(string message, Exception? ex = null)
    {
        if (!_enabled) return;

        try
        {
            var path = _logPath ??= GetLogPath();
            var line = $"{DateTime.UtcNow:O} {message}";
            if (ex != null)
            {
                line = $"{line} | {ex}";
            }

            lock (SyncRoot)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch
        {
        }
    }

    private static string GetLogPath()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(basePath, "Letterist", "startup.log");
    }
}
