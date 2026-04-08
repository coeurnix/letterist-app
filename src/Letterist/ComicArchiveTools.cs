using Microsoft.Win32;
using System.Diagnostics;

namespace Letterist;

internal sealed record ExternalToolResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut);

internal static class ComicArchiveTools
{
    public static string? ResolveRarExecutable(string? configuredPath = null)
    {
        foreach (var candidate in EnumerateRarCandidates(configuredPath))
        {
            if (TryResolveExecutable(candidate, out var resolved))
            {
                return resolved;
            }
        }

        return null;
    }

    public static IReadOnlyList<string> BuildRarCreateArguments(string archivePath, string listFilePath)
    {
        return
        [
            "a",
            "-ep1",
            "-idq",
            "-m5",
            "-ma5",
            archivePath,
            "@" + listFilePath
        ];
    }

    public static async Task<ExternalToolResult> RunProcessAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        int timeoutMilliseconds = 120000,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var waitTask = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = Task.Delay(timeoutMilliseconds, cancellationToken);
        var completedTask = await Task.WhenAny(waitTask, timeoutTask);
        if (!ReferenceEquals(completedTask, waitTask))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }

            var timedOutOutput = await outputTask;
            var timedOutError = await errorTask;
            return new ExternalToolResult(-1, timedOutOutput, timedOutError, TimedOut: true);
        }

        await waitTask;
        var output = await outputTask;
        var error = await errorTask;
        return new ExternalToolResult(process.ExitCode, output, error, TimedOut: false);
    }

    private static IEnumerable<string?> EnumerateRarCandidates(string? configuredPath)
    {
        yield return configuredPath;

        var envPath = Environment.GetEnvironmentVariable("LETTERIST_RAR_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            yield return envPath;
        }

        foreach (var candidate in EnumeratePathCandidates("rar.exe"))
        {
            yield return candidate;
        }

        foreach (var candidate in EnumeratePathCandidates("winrar.exe"))
        {
            yield return candidate;
        }

        yield return ReadRegistryPath(@"HKEY_CURRENT_USER\Software\WinRAR", "exe64");
        yield return ReadRegistryPath(@"HKEY_CURRENT_USER\Software\WinRAR", "exe32");
        yield return ReadRegistryPath(@"HKEY_CURRENT_USER\Software\WinRAR", "Path");
        yield return ReadRegistryPath(@"HKEY_CURRENT_USER\Software\WinRAR", "ExePath");
        yield return ReadRegistryPath(@"HKEY_LOCAL_MACHINE\SOFTWARE\WinRAR", "exe64");
        yield return ReadRegistryPath(@"HKEY_LOCAL_MACHINE\SOFTWARE\WinRAR", "exe32");
        yield return ReadRegistryPath(@"HKEY_LOCAL_MACHINE\SOFTWARE\WinRAR", "Path");
        yield return ReadRegistryPath(@"HKEY_LOCAL_MACHINE\SOFTWARE\WinRAR", "ExePath");
        yield return ReadRegistryPath(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\WinRAR", "exe64");
        yield return ReadRegistryPath(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\WinRAR", "exe32");
        yield return ReadRegistryPath(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\WinRAR", "Path");
        yield return ReadRegistryPath(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\WinRAR", "ExePath");

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            yield return Path.Combine(programFiles, "WinRAR", "rar.exe");
            yield return Path.Combine(programFiles, "WinRAR", "winrar.exe");
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            yield return Path.Combine(programFilesX86, "WinRAR", "rar.exe");
            yield return Path.Combine(programFilesX86, "WinRAR", "winrar.exe");
        }
    }

    private static IEnumerable<string> EnumeratePathCandidates(string executableName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            yield break;
        }

        var entries = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in entries)
        {
            yield return Path.Combine(entry, executableName);
        }
    }

    private static bool TryResolveExecutable(string? candidate, out string resolved)
    {
        resolved = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        try
        {
            var trimmed = candidate.Trim().Trim('"');
            if (File.Exists(trimmed))
            {
                resolved = trimmed;
                return true;
            }

            if (Directory.Exists(trimmed))
            {
                var rarExe = Path.Combine(trimmed, "rar.exe");
                if (File.Exists(rarExe))
                {
                    resolved = rarExe;
                    return true;
                }

                var winrarExe = Path.Combine(trimmed, "winrar.exe");
                if (File.Exists(winrarExe))
                {
                    resolved = winrarExe;
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }

    private static string? ReadRegistryPath(string keyName, string valueName)
    {
        try
        {
            var value = Registry.GetValue(keyName, valueName, defaultValue: null);
            return value?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
