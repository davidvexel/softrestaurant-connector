using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Origen.SRConnector.Configuration;
using Origen.SRConnector.Domain;
using Origen.SRConnector.Infrastructure.Api;
using Origen.SRConnector.Infrastructure.Persistence;
using Origen.SRConnector.Services;

namespace Origen.SRConnector.Tests;

public sealed class OutboxDispatchServiceTests
{
    [Fact]
    public async Task FailedSend_IsScheduledForRetry()
    {
        var outbox = new InMemoryOutbox();
        var service = new OutboxDispatchService(
            outbox,
            new FailingApiClient(),
            Options.Create(new ConnectorOptions { LocationId = "origen-playa", DispatchBatchSize = 20 }),
            NullLogger<OutboxDispatchService>.Instance);

        var processed = await service.DispatchDueAsync(CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Equal(1, outbox.FailedId);
        Assert.Equal(1, outbox.FailedAttempts);
        Assert.Contains("simulated", outbox.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 30)]
    [InlineData(5, 60)]
    [InlineData(20, 60)]
    public void RetrySchedule_UsesConfiguredBackoff(int attempt, int expectedMinutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), RetrySchedule.ForAttempt(attempt));
    }

    private sealed class FailingApiClient : ILoyaltyApiClient
    {
        public Task<ApiResult> SendSaleAsync(Sale sale, CancellationToken cancellationToken) =>
            Task.FromResult(ApiResult.Failed("Simulated API failure"));
    }

    private sealed class InMemoryOutbox : ISaleOutboxRepository
    {
        public long? FailedId { get; private set; }
        public int FailedAttempts { get; private set; }
        public string Error { get; private set; } = string.Empty;

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> EnqueueAsync(Sale sale, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<IReadOnlyList<OutboxSale>> ClaimDueAsync(int limit, CancellationToken cancellationToken)
        {
            IReadOnlyList<OutboxSale> result =
            [
                new OutboxSale(
                    1, "softrestaurant", "origen-playa", 1735,
                    SaleJsonSerializer.Serialize(TestSaleFactory.Create()),
                    "sending", 1, DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, null, null)
            ];
            return Task.FromResult(result);
        }

        public Task MarkSentAsync(long id, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task MarkFailedAsync(long id, int attempts, string error, CancellationToken cancellationToken)
        {
            FailedId = id;
            FailedAttempts = attempts;
            Error = error;
            return Task.CompletedTask;
        }

        public Task<OutboxCounts> GetCountsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new OutboxCounts(0, 0, 0, 1));
    }
}

