using System.Text.Json.Serialization;

namespace Origen.SRConnector.Domain;

public sealed record SalePayment(
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("tip")] decimal Tip,
    [property: JsonPropertyName("reference")] string? Reference,
    [property: JsonIgnore] string? CardBrand);

