using Microsoft.Extensions.Options;
using Origen.SRConnector.Configuration;
using Origen.SRConnector.Infrastructure.Api;
using Origen.SRConnector.Infrastructure.Persistence;

namespace Origen.SRConnector.Services;

public sealed class OutboxDispatchService(
    ISaleOutboxRepository outboxRepository,
    ILoyaltyApiClient apiClient,
    IOptions<ConnectorOptions> options,
    ILogger<OutboxDispatchService> logger) : IOutboxDispatchService
{
    public async Task<int> DispatchDueAsync(CancellationToken cancellationToken)
    {
        var entries = await outboxRepository.ClaimDueAsync(options.Value.DispatchBatchSize, cancellationToken);
        foreach (var entry in entries)
        {
            try
            {
                var sale = SaleJsonSerializer.Deserialize(entry.PayloadJson);
                var result = await apiClient.SendSaleAsync(sale, cancellationToken);
                if (result.Success)
                {
                    await outboxRepository.MarkSentAsync(entry.Id, cancellationToken);
                    logger.LogInformation("Sale {TicketNumber} sent successfully", entry.TicketNumber);
                }
                else
                {
                    await MarkFailedAsync(entry, result.Error ?? "Unknown API error", cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await MarkFailedAsync(entry, exception.Message, cancellationToken);
            }
        }

        return entries.Count;
    }

    private async Task MarkFailedAsync(
        OutboxSale entry,
        string error,
        CancellationToken cancellationToken)
    {
        await outboxRepository.MarkFailedAsync(entry.Id, entry.Attempts, error, cancellationToken);
        logger.LogWarning(
            "Mock API unavailable; sale {TicketNumber} queued for retry after attempt {Attempt}",
            entry.TicketNumber,
            entry.Attempts);
    }
}

