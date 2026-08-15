using Origen.SRConnector.Domain;

namespace Origen.SRConnector.Infrastructure.Persistence;

public interface ISaleOutboxRepository
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task<bool> EnqueueAsync(Sale sale, CancellationToken cancellationToken);
    Task<IReadOnlyList<OutboxSale>> ClaimDueAsync(int limit, CancellationToken cancellationToken);
    Task MarkSentAsync(long id, CancellationToken cancellationToken);
    Task MarkFailedAsync(long id, int attempts, string error, CancellationToken cancellationToken);
    Task<OutboxCounts> GetCountsAsync(CancellationToken cancellationToken);
}

public sealed record OutboxCounts(int Pending, int Sending, int Sent, int Failed);

