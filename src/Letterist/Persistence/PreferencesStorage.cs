using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Letterist.Persistence;

public static class PreferencesStorage
{
    private const string PreferencesFileName = "preferences.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static AppPreferences Load()
    {
        return LoadFromPath(GetPreferencesPath());
    }

    public static AppPreferences LoadFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return AppPreferences.CreateDefault();
        }

        if (!File.Exists(path))
        {
            var defaults = AppPreferences.CreateDefault();
            defaults.Normalize();
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var preferences = JsonSerializer.Deserialize<AppPreferences>(json, JsonOptions) ?? AppPreferences.CreateDefault();
            preferences.Normalize();
            return preferences;
        }
        catch
        {
            var defaults = AppPreferences.CreateDefault();
            defaults.Normalize();
            return defaults;
        }
    }

    public static void Save(AppPreferences preferences)
    {
        SaveToPath(preferences, GetPreferencesPath());
    }

    public static void SaveToPath(AppPreferences preferences, string path)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        preferences.Normalize();
        var folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var json = JsonSerializer.Serialize(preferences, JsonOptions);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    public static bool TryImport(string importPath, out AppPreferences preferences, out string errorMessage)
    {
        preferences = AppPreferences.CreateDefault();
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(importPath))
        {
            errorMessage = "Import path is empty.";
            return false;
        }

        if (!File.Exists(importPath))
        {
            errorMessage = "Import file does not exist.";
            return false;
        }

        try
        {
            var json = File.ReadAllText(importPath, Encoding.UTF8);
            var imported = JsonSerializer.Deserialize<AppPreferences>(json, JsonOptions);
            if (imported == null)
            {
                errorMessage = "Import file is empty.";
                return false;
            }

            imported.Normalize();
            preferences = imported;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static bool TryExport(AppPreferences preferences, string exportPath, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            SaveToPath(preferences, exportPath);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static string GetPreferencesPath()
    {
        return Path.Combine(GetAppDataFolder(), PreferencesFileName);
    }

    public static string GetAppDataFolder()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(basePath, "Letterist");
    }
}
