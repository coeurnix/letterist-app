using Letterist.Model;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Letterist.Persistence;

internal sealed class BalloonTemplatePackFile
{
    public int Version { get; set; } = 1;
    public List<BalloonTemplateFile> Templates { get; set; } = new();

    public static BalloonTemplatePackFile FromTemplates(IEnumerable<BalloonTemplate> templates)
    {
        return new BalloonTemplatePackFile
        {
            Templates = templates.Select(BalloonTemplateFile.FromTemplate).ToList()
        };
    }
}

internal static class BalloonTemplatePackStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task SaveAsync(IEnumerable<BalloonTemplate> templates, string filePath)
    {
        var pack = BalloonTemplatePackFile.FromTemplates(templates);
        var json = JsonSerializer.Serialize(pack, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
    }

    public static async Task<List<BalloonTemplate>> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Balloon template pack file not found.", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<BalloonTemplate>();
        }

        var trimmed = json.TrimStart();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            var list = JsonSerializer.Deserialize<List<BalloonTemplateFile>>(json, JsonOptions) ?? new List<BalloonTemplateFile>();
            return list.Select(item => item.ToTemplate()).ToList();
        }

        var pack = JsonSerializer.Deserialize<BalloonTemplatePackFile>(json, JsonOptions);
        if (pack?.Templates != null && pack.Templates.Count > 0)
        {
            return pack.Templates.Select(item => item.ToTemplate()).ToList();
        }

        var single = JsonSerializer.Deserialize<BalloonTemplateFile>(json, JsonOptions);
        return single != null ? new List<BalloonTemplate> { single.ToTemplate() } : new List<BalloonTemplate>();
    }
}
