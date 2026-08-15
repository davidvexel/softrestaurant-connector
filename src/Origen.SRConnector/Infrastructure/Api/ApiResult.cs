namespace Origen.SRConnector.Infrastructure.Api;

public sealed record ApiResult(bool Success, string? Error = null)
{
    public static ApiResult Ok() => new(true);
    public static ApiResult Failed(string error) => new(false, error);
}

