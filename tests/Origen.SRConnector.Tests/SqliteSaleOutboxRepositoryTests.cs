using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Origen.SRConnector.Configuration;
using Origen.SRConnector.Infrastructure.Persistence;

namespace Origen.SRConnector.Tests;

public sealed class SqliteSaleOutboxRepositoryTests
{
    [Fact]
    public async Task Enqueue_DeduplicatesBySourceLocationAndTicket()
    {
        await using var database = new TemporaryDatabase();
        var repository = CreateRepository(database.Path, "location-one");

        Assert.True(await repository.EnqueueAsync(TestSaleFactory.Create(), CancellationToken.None));
        Assert.False(await repository.EnqueueAsync(TestSaleFactory.Create(), CancellationToken.None));

        var counts = await repository.GetCountsAsync(CancellationToken.None);
        Assert.Equal(1, counts.Pending);
    }

    [Fact]
    public async Task PendingSale_PersistsAcrossRepositoryRestart()
    {
        await using var database = new TemporaryDatabase();
        var firstProcess = CreateRepository(database.Path);
        await firstProcess.EnqueueAsync(TestSaleFactory.Create(), CancellationToken.None);

        var restartedProcess = CreateRepository(database.Path);
        var claimed = await restartedProcess.ClaimDueAsync(10, CancellationToken.None);

        Assert.Single(claimed);
        Assert.Equal(1735, claimed[0].TicketNumber);
        Assert.Equal("sending", claimed[0].Status);
    }

    [Fact]
    public async Task Initialize_RecoversSendingSaleAfterProcessRestart()
    {
        await using var database = new TemporaryDatabase();
        var firstProcess = CreateRepository(database.Path);
        await firstProcess.EnqueueAsync(TestSaleFactory.Create(), CancellationToken.None);
        Assert.Single(await firstProcess.ClaimDueAsync(10, CancellationToken.None));

        var restartedProcess = CreateRepository(database.Path);
        await restartedProcess.InitializeAsync(CancellationToken.None);
        var recovered = await restartedProcess.ClaimDueAsync(10, CancellationToken.None);

        Assert.Single(recovered);
        Assert.Equal(2, recovered[0].Attempts);
    }

    [Fact]
    public async Task SameTicket_CanExistAtDifferentLocations()
    {
        await using var database = new TemporaryDatabase();
        var firstLocation = CreateRepository(database.Path, "location-one");
        var secondLocation = CreateRepository(database.Path, "location-two");

        Assert.True(await firstLocation.EnqueueAsync(TestSaleFactory.Create(), CancellationToken.None));
        Assert.True(await secondLocation.EnqueueAsync(TestSaleFactory.Create(), CancellationToken.None));

        var counts = await firstLocation.GetCountsAsync(CancellationToken.None);
        Assert.Equal(2, counts.Pending);
    }

    private static SqliteSaleOutboxRepository CreateRepository(
        string path,
        string locationId = "origen-playa") => new(
        Options.Create(new ConnectorOptions { DatabasePath = path, LocationId = locationId }),
        TimeProvider.System,
        NullLogger<SqliteSaleOutboxRepository>.Instance);

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"origen-outbox-{Guid.NewGuid():N}.db");

        public ValueTask DisposeAsync()
        {
            // Microsoft.Data.Sqlite conserva conexiones físicas en el pool. Windows no permite
            // eliminar el archivo temporal hasta liberar esos handles.
            SqliteConnection.ClearAllPools();
            File.Delete(Path);
            File.Delete(Path + "-shm");
            File.Delete(Path + "-wal");
            return ValueTask.CompletedTask;
        }
    }
}
