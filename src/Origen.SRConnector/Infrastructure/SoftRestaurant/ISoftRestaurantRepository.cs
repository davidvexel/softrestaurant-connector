using Origen.SRConnector.Domain;

namespace Origen.SRConnector.Infrastructure.SoftRestaurant;

public interface ISoftRestaurantRepository
{
    Task TestConnectionAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Sale>> GetClosedSalesAsync(DateTime since, CancellationToken cancellationToken);
}

