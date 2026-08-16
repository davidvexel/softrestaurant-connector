using System.ComponentModel.DataAnnotations;

namespace Origen.SRConnector.Configuration;

public sealed class ApiOptions
{
    public const string SectionName = "Api";

    [Required]
    public string Mode { get; init; } = "Mock";

    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;

    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 20;
}
