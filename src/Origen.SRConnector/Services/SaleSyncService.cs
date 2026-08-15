using Microsoft.Extensions.Options;
using Origen.SRConnector.Configuration;
using Origen.SRConnector.Infrastructure.SoftRestaurant;

namespace Origen.SRConnector.Services;

public sealed class SaleSyncService(
    ISoftRestaurantRepository repository,
    IOptions<SoftRestaurantOptions> options,
    ILogger<SaleSyncService> logger) : ISaleSyncService
{
    public async Task DetectAndLogSalesAsync(CancellationToken cancellationToken)
    {
        var since = DateTime.Now.AddHours(-options.Value.LookbackHours);
        var sales = await repository.GetClosedSalesAsync(since, cancellationToken);
        logger.LogInformation("Found {SaleCount} closed sales since {Since}", sales.Count, since);

        foreach (var sale in sales)
        {
            logger.LogInformation(
                "Detected sale {TicketNumber} from station {Station}. Payload:{NewLine}{PayloadJson}",
                sale.TicketNumber,
                sale.Station,
                Environment.NewLine,
                SaleJsonSerializer.Serialize(sale));
        }
    }
}

