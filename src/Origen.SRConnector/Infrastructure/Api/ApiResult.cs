namespace Origen.SRConnector.Infrastructure.Api;

public enum ApiFailureKind
{
    None,
    Retryable,
    Permanent
}

public sealed record ApiResult(
    bool Success,
    ApiFailureKind FailureKind = ApiFailureKind.None,
    string? Error = null)
{
    public static ApiResult Ok() => new(true);
    public static ApiResult Retryable(string error) => new(false, ApiFailureKind.Retryable, error);
    public static ApiResult Permanent(string error) => new(false, ApiFailureKind.Permanent, error);
}
