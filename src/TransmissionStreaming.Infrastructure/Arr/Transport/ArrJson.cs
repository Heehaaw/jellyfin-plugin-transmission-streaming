namespace TransmissionStreaming.Infrastructure.Arr.Transport;

using System.Text.Json;

internal static class ArrJson
{
    public static string Basename(string path)
    {
        return Path.GetFileName(path.Replace('\\', '/'));
    }

    public static IEnumerable<JsonElement> Array(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Array ? element.EnumerateArray() : [];
    }

    public static JsonElement Object(JsonElement element, string name)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;
    }

    public static string Text(JsonElement element, string name)
    {
        return Object(element, name).ValueKind is JsonValueKind.String ? Object(element, name).GetString() ?? string.Empty : string.Empty;
    }

    public static int Number(JsonElement element, string name)
    {
        return Object(element, name).ValueKind == JsonValueKind.Number && Object(element, name).TryGetInt32(out var value) ? value : 0;
    }

    public static long Long(JsonElement element, string name)
    {
        return Object(element, name).ValueKind == JsonValueKind.Number && Object(element, name).TryGetInt64(out var value) ? value : 0;
    }

    public static string Data(JsonElement row, string name)
    {
        var data = Object(row, "data");
        if (data.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        foreach (var property in data.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() ?? string.Empty : property.Value.ToString();
            }
        }

        return string.Empty;
    }
}
