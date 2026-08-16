using Origen.SRConnector.Domain;

namespace Origen.SRConnector.Infrastructure.Api;

public interface ILoyaltyApiClient
{
    string Name { get; }
    Task<ApiResult> TestConnectionAsync(CancellationToken cancellationToken);
    Task<ApiResult> SendSaleAsync(Sale sale, CancellationToken cancellationToken);
}
