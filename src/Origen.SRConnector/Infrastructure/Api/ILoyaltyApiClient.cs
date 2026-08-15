using Origen.SRConnector.Domain;

namespace Origen.SRConnector.Infrastructure.Api;

public interface ILoyaltyApiClient
{
    Task<ApiResult> TestConnectionAsync(CancellationToken cancellationToken);
    Task<ApiResult> SendSaleAsync(Sale sale, CancellationToken cancellationToken);
}
