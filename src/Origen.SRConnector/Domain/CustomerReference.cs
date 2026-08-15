using System.Text.Json.Serialization;

namespace Origen.SRConnector.Domain;

public sealed record CustomerReference(
    [property: JsonPropertyName("external_id")] string ExternalId,
    [property: JsonPropertyName("name")] string? Name);

