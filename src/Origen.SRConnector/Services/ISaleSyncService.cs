namespace Origen.SRConnector.Services;

public interface ISaleSyncService
{
    Task DetectAndLogSalesAsync(CancellationToken cancellationToken);
}

