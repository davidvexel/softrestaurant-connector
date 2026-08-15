namespace Origen.SRConnector.Configuration;

public sealed class ConnectorOptions
{
    public const string SectionName = "Connector";
    public string LocationId { get; init; } = string.Empty;
    public string DatabasePath { get; init; } = "connector.db";
}

