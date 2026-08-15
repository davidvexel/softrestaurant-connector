using System.Text.Json;
using System.Text.Json.Serialization;
using Origen.SRConnector.Domain;

namespace Origen.SRConnector.Services;

public static class SaleJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static string Serialize(Sale sale) => JsonSerializer.Serialize(sale, Options);

    public static Sale Deserialize(string json) =>
        JsonSerializer.Deserialize<Sale>(json, Options)
        ?? throw new JsonException("The persisted sale payload is empty or invalid.");
}
