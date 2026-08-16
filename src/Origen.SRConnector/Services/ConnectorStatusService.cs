using Origen.SRConnector.Infrastructure.Api;
using Origen.SRConnector.Infrastructure.Persistence;
using Origen.SRConnector.Infrastructure.SoftRestaurant;

namespace Origen.SRConnector.Services;

public sealed class ConnectorStatusService(
    ISoftRestaurantRepository softRestaurantRepository,
    ISaleOutboxRepository outboxRepository,
    ILoyaltyApiClient apiClient)
{
    public async Task<ConnectorStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        string sqlStatus;
        try
        {
            await softRestaurantRepository.TestConnectionAsync(cancellationToken);
            sqlStatus = "Connected";
        }
        catch
        {
            sqlStatus = "Failed";
        }

        var apiResult = await apiClient.TestConnectionAsync(cancellationToken);
        var outbox = await outboxRepository.GetStatusAsync(cancellationToken);
        return new ConnectorStatus(
            sqlStatus,
            apiResult.Success ? $"{apiClient.Name}: Connected" : $"{apiClient.Name}: Failed",
            apiResult.Success,
            outbox);
    }
}

public sealed record ConnectorStatus(
    string SqlServer,
    string Api,
    bool ApiConnected,
    OutboxStatus Outbox);
