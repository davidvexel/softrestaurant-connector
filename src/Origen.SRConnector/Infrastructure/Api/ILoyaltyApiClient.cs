using Origen.SRConnector.Domain;

namespace Origen.SRConnector.Infrastructure.Api;

public interface ILoyaltyApiClient
{
    Task<ApiResult> SendSaleAsync(Sale sale, CancellationToken cancellationToken);
}

