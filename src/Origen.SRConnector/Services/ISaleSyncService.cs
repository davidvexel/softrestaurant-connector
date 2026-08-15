namespace Origen.SRConnector.Services;

public interface ISaleSyncService
{
    Task DetectAndQueueSalesAsync(CancellationToken cancellationToken);
}
