using System.ComponentModel.DataAnnotations;

namespace Origen.SRConnector.Configuration;

public sealed class ConnectorOptions
{
    public const string SectionName = "Connector";
    [Required]
    public string LocationId { get; init; } = string.Empty;

    [Required]
    public string DatabasePath { get; init; } = "connector.db";

    [Range(1, 300)]
    public int DispatchIntervalSeconds { get; init; } = 5;

    [Range(1, 100)]
    public int DispatchBatchSize { get; init; } = 20;
}
