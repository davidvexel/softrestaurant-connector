namespace Origen.SRConnector.Configuration;

public sealed class ApiOptions
{
    public const string SectionName = "Api";
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 20;
}

