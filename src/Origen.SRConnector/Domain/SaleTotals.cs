using System.Text.Json.Serialization;

namespace Origen.SRConnector.Domain;

public sealed record SaleTotals(
    [property: JsonPropertyName("subtotal")] decimal Subtotal,
    [property: JsonPropertyName("tax")] decimal Tax,
    [property: JsonPropertyName("total")] decimal Total,
    [property: JsonPropertyName("tip")] decimal Tip,
    [property: JsonPropertyName("total_with_tip")] decimal TotalWithTip);

