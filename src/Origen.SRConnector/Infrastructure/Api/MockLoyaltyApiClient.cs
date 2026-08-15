using Origen.SRConnector.Domain;
using Origen.SRConnector.Services;

namespace Origen.SRConnector.Infrastructure.Api;

public sealed class MockLoyaltyApiClient(ILogger<MockLoyaltyApiClient> logger) : ILoyaltyApiClient
{
    public Task<ApiResult> TestConnectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ApiResult.Ok());
    }

    public Task<ApiResult> SendSaleAsync(Sale sale, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation(
            "Mock send for sale {TicketNumber}. Payload:{NewLine}{PayloadJson}",
            sale.TicketNumber,
            Environment.NewLine,
            SaleJsonSerializer.Serialize(sale));
        return Task.FromResult(ApiResult.Ok());
    }
}
