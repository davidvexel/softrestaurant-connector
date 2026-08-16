using Microsoft.Extensions.Options;
using Origen.SRConnector.Configuration;
using Origen.SRConnector.Infrastructure.Persistence;
using Origen.SRConnector.Infrastructure.SoftRestaurant;

namespace Origen.SRConnector.Services;

public sealed class SaleSyncService(
    ISoftRestaurantRepository repository,
    ISaleOutboxRepository outboxRepository,
    IOptions<SoftRestaurantOptions> options,
    ILogger<SaleSyncService> logger) : ISaleSyncService
{
    public async Task DetectAndQueueSalesAsync(CancellationToken cancellationToken)
    {
        var since = DateTime.Now.AddHours(-options.Value.LookbackHours);
        var sales = await repository.GetClosedSalesAsync(since, cancellationToken);
        logger.LogDebug("Found {SaleCount} closed sales since {Since}", sales.Count, since);

        foreach (var sale in sales)
        {
            if (sale.Items.Count == 0 || sale.Payments.Count == 0)
            {
                logger.LogDebug(
                    "Sale {TicketNumber} is closed but incomplete ({ItemCount} items, {PaymentCount} payments); it will be checked again",
                    sale.TicketNumber,
                    sale.Items.Count,
                    sale.Payments.Count);
                continue;
            }

            if (await outboxRepository.EnqueueAsync(sale, cancellationToken))
            {
                logger.LogInformation("Sale {TicketNumber} queued", sale.TicketNumber);
            }
        }
    }
}
