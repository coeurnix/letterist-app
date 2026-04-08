using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using Letterist.Model;

namespace Letterist.Persistence;

internal static class DocumentStorage
{
    private const string DocumentFileName = "document.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task SaveAsync(
        Document document,
        string folderPath,
        IReadOnlyDictionary<Guid, string?> backgroundPaths,
        IReadOnlyDictionary<Guid, string?> panelImagePaths,
        IReadOnlyDictionary<Guid, string?> floatingImagePaths)
    {
        Directory.CreateDirectory(folderPath);
        var filePath = Path.Combine(folderPath, DocumentFileName);

        var file = DocumentFile.FromDocument(document, backgroundPaths, panelImagePaths, floatingImagePaths);
        var json = JsonSerializer.Serialize(file, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, Encoding.UTF8);
    }

    public static async Task<Document> LoadAsync(string folderPath)
    {
        var filePath = Path.Combine(folderPath, DocumentFileName);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Document file not found.", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        var file = JsonSerializer.Deserialize<DocumentFile>(json, JsonOptions)
                   ?? throw new InvalidOperationException("Failed to parse document file.");

        return file.ToDocument();
    }
}
