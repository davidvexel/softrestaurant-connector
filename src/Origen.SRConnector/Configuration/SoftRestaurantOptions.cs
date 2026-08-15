using System.ComponentModel.DataAnnotations;

namespace Origen.SRConnector.Configuration;

public sealed class SoftRestaurantOptions
{
    public const string SectionName = "SoftRestaurant";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Range(5, 3600)]
    public int PollingIntervalSeconds { get; init; } = 10;

    [Range(1, 720)]
    public int LookbackHours { get; init; } = 48;

    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; init; } = 30;
}

