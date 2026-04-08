using Letterist.Model;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Letterist.Commands;

public interface ICommand
{
    Guid Id { get; }

    string CommandType { get; }

    string Description { get; }

    void Execute(Document document);

    void Undo(Document document);

    CommandData Serialize();
}

public sealed class CommandData
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public Guid Id { get; init; }

    public required string Type { get; init; }

    public Dictionary<string, object?> Parameters { get; init; } = new();

    public T? Get<T>(string key)
    {
        if (Parameters.TryGetValue(key, out var value))
        {
            if (value is T typed) return typed;
            if (value is System.Text.Json.JsonElement element)
            {
                if (typeof(T) == typeof(Guid) && element.ValueKind == JsonValueKind.String)
                {
                    if (Guid.TryParse(element.GetString(), out var guid))
                    {
                        return (T)(object)guid;
                    }
                }
                if (element.ValueKind == JsonValueKind.Number)
                {
                    if (typeof(T) == typeof(float)) return (T)(object)element.GetSingle();
                    if (typeof(T) == typeof(double)) return (T)(object)element.GetDouble();
                    if (typeof(T) == typeof(int)) return (T)(object)element.GetInt32();
                    if (typeof(T) == typeof(long)) return (T)(object)element.GetInt64();
                }
                return JsonSerializer.Deserialize<T>(element.GetRawText(), JsonOptions);
            }
            if (typeof(T) == typeof(Guid) && value is string strValue)
            {
                if (Guid.TryParse(strValue, out var guid))
                {
                    return (T)(object)guid;
                }
            }
        }
        return default;
    }

    public T GetRequired<T>(string key)
    {
        var value = Get<T>(key);
        if (value is null)
        {
            throw new InvalidOperationException($"Required parameter '{key}' not found or null");
        }
        return value;
    }
}
