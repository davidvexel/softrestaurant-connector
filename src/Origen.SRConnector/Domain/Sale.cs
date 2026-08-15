using System.Text.Json.Serialization;

namespace Origen.SRConnector.Domain;

public sealed record Sale
{
    [JsonPropertyName("source")]
    public string Source { get; init; } = "softrestaurant";

    [JsonPropertyName("folio")]
    public required long SrFolio { get; init; }

    [JsonPropertyName("ticket")]
    public required long TicketNumber { get; init; }

    [JsonPropertyName("opened_at")]
    public required DateTime OpenedAt { get; init; }

    [JsonPropertyName("closed_at")]
    public required DateTime ClosedAt { get; init; }

    [JsonPropertyName("station")]
    public string? Station { get; init; }

    [JsonPropertyName("customer")]
    public CustomerReference? Customer { get; init; }

    [JsonPropertyName("totals")]
    public required SaleTotals Totals { get; init; }

    [JsonPropertyName("items")]
    public IReadOnlyList<SaleItem> Items { get; init; } = [];

    [JsonPropertyName("payments")]
    public IReadOnlyList<SalePayment> Payments { get; init; } = [];

    [JsonIgnore]
    public string? Table { get; init; }

    [JsonIgnore]
    public string? OpeningUser { get; init; }

    [JsonIgnore]
    public string? PaymentUser { get; init; }
}
