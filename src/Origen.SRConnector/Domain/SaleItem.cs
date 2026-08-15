using System.Text.Json.Serialization;

namespace Origen.SRConnector.Domain;

public sealed record SaleItem(
    [property: JsonIgnore] long DetailFolio,
    [property: JsonPropertyName("product_id")] string ProductId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("quantity")] decimal Quantity,
    [property: JsonPropertyName("unit_price")] decimal UnitPrice,
    [property: JsonPropertyName("discount")] decimal Discount,
    [property: JsonIgnore] decimal? PriceAfterDiscount,
    [property: JsonIgnore] decimal Tax,
    [property: JsonPropertyName("modifier")] bool Modifier,
    [property: JsonPropertyName("compound_id")] string? CompoundId,
    [property: JsonPropertyName("compound_main")] bool CompoundMain);

