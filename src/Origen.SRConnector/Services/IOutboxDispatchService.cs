namespace Origen.SRConnector.Services;

public interface IOutboxDispatchService
{
    Task<int> DispatchDueAsync(CancellationToken cancellationToken);
}

