using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Letterist.Model;

namespace Letterist.Persistence;

internal static class StyleLibraryStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task SaveAsync(Document document, string filePath)
    {
        var library = StyleLibraryFile.FromDocument(document);
        var json = JsonSerializer.Serialize(library, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
    }

    public static async Task<StyleLibraryFile> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Style library file not found.", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        return JsonSerializer.Deserialize<StyleLibraryFile>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to parse style library file.");
    }
}
