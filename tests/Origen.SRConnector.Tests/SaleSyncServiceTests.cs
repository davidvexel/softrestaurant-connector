using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Origen.SRConnector.Configuration;
using Origen.SRConnector.Domain;
using Origen.SRConnector.Infrastructure.Persistence;
using Origen.SRConnector.Infrastructure.SoftRestaurant;
using Origen.SRConnector.Services;

namespace Origen.SRConnector.Tests;

public sealed class SaleSyncServiceTests
{
    [Fact]
    public async Task ClosedSaleWithoutPayments_IsNotQueued()
    {
        var outbox = new RecordingOutbox();
        var sale = TestSaleFactory.Create() with { Payments = [] };
        var service = CreateService(sale, outbox);

        await service.DetectAndQueueSalesAsync(CancellationToken.None);

        Assert.Empty(outbox.EnqueuedSales);
    }

    [Fact]
    public async Task ClosedSaleWithoutItems_IsNotQueued()
    {
        var outbox = new RecordingOutbox();
        var sale = TestSaleFactory.Create() with { Items = [] };
        var service = CreateService(sale, outbox);

        await service.DetectAndQueueSalesAsync(CancellationToken.None);

        Assert.Empty(outbox.EnqueuedSales);
    }

    [Fact]
    public async Task CompleteClosedSale_IsQueued()
    {
        var outbox = new RecordingOutbox();
        var sale = TestSaleFactory.Create();
        var service = CreateService(sale, outbox);

        await service.DetectAndQueueSalesAsync(CancellationToken.None);

        Assert.Same(sale, Assert.Single(outbox.EnqueuedSales));
    }

    private static SaleSyncService CreateService(Sale sale, ISaleOutboxRepository outbox) => new(
        new StubSoftRestaurantRepository(sale),
        outbox,
        Options.Create(new SoftRestaurantOptions
        {
            ConnectionString = "Server=test",
            LookbackHours = 1
        }),
        NullLogger<SaleSyncService>.Instance);

    private sealed class StubSoftRestaurantRepository(Sale sale) : ISoftRestaurantRepository
    {
        public Task TestConnectionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<Sale>> GetClosedSalesAsync(
            DateTime since,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Sale>>([sale]);
    }

    private sealed class RecordingOutbox : ISaleOutboxRepository
    {
        public List<Sale> EnqueuedSales { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> EnqueueAsync(Sale sale, CancellationToken cancellationToken)
        {
            EnqueuedSales.Add(sale);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<OutboxSale>> ClaimDueAsync(int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OutboxSale>>([]);

        public Task MarkSentAsync(long id, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task MarkFailedAsync(
            long id,
            int attempts,
            string error,
            bool retryable,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<OutboxStatus> GetStatusAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new OutboxStatus(new OutboxCounts(0, 0, 0, 0), null, null));
    }
}
