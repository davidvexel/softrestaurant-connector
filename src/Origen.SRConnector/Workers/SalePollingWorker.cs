using Microsoft.Extensions.Options;
using Origen.SRConnector.Configuration;
using Origen.SRConnector.Services;

namespace Origen.SRConnector.Workers;

public sealed class SalePollingWorker(
    ISaleSyncService syncService,
    IOptions<SoftRestaurantOptions> options,
    ILogger<SalePollingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds);
        logger.LogInformation("Origen SR Connector started; polling every {PollingSeconds} seconds", interval.TotalSeconds);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await syncService.DetectAndLogSalesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "SQL polling failed; the connector will retry on the next cycle");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));

        logger.LogInformation("Origen SR Connector stopped");
    }
}

