using Microsoft.Extensions.Options;
using Origen.SRConnector.Configuration;
using Origen.SRConnector.Infrastructure.Persistence;
using Origen.SRConnector.Services;

namespace Origen.SRConnector.Workers;

public sealed class OutboxDispatchWorker(
    ISaleOutboxRepository outboxRepository,
    IOutboxDispatchService dispatchService,
    IOptions<ConnectorOptions> options,
    ILogger<OutboxDispatchWorker> logger) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await outboxRepository.InitializeAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.DispatchIntervalSeconds);
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await dispatchService.DispatchDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox dispatch failed; it will retry on the next cycle");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

