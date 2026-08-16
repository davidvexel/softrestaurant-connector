using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Origen.SRConnector.Configuration;
using Origen.SRConnector.Domain;
using Origen.SRConnector.Services;

namespace Origen.SRConnector.Infrastructure.Persistence;

public sealed class SqliteSaleOutboxRepository(
    IOptions<ConnectorOptions> options,
    TimeProvider timeProvider,
    ILogger<SqliteSaleOutboxRepository> logger) : ISaleOutboxRepository
{
    private const string TimestampFormat = "O";
    private readonly ConnectorOptions _options = options.Value;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            var databasePath = ResolveDatabasePath(_options.DatabasePath);
            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var connection = CreateConnection(databasePath);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS outbox_sales (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    source TEXT NOT NULL,
                    location_id TEXT NOT NULL,
                    ticket_number INTEGER NOT NULL,
                    payload_json TEXT NOT NULL,
                    status TEXT NOT NULL CHECK (status IN ('pending', 'sending', 'sent', 'failed')),
                    attempts INTEGER NOT NULL DEFAULT 0,
                    last_attempt_at TEXT NULL,
                    next_attempt_at TEXT NULL,
                    created_at TEXT NOT NULL,
                    sent_at TEXT NULL,
                    last_error TEXT NULL,
                    UNIQUE (source, location_id, ticket_number)
                );
                CREATE INDEX IF NOT EXISTS ix_outbox_sales_due
                    ON outbox_sales (status, next_attempt_at);
                UPDATE outbox_sales
                    SET status = 'pending', next_attempt_at = NULL
                    WHERE status = 'sending';
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            _initialized = true;
            logger.LogInformation("Outbox database ready at {DatabasePath}", databasePath);
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<bool> EnqueueAsync(Sale sale, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO outbox_sales (
                source, location_id, ticket_number, payload_json, status, attempts, created_at)
            VALUES (@source, @locationId, @ticket, @payload, 'pending', 0, @createdAt)
            ON CONFLICT(source, location_id, ticket_number) DO UPDATE SET
                payload_json = excluded.payload_json,
                status = 'pending',
                attempts = 0,
                last_attempt_at = NULL,
                next_attempt_at = NULL,
                sent_at = NULL,
                last_error = NULL
            WHERE outbox_sales.status = 'failed'
              AND outbox_sales.next_attempt_at IS NULL
              AND outbox_sales.payload_json <> excluded.payload_json;
            """;
        command.Parameters.AddWithValue("@source", sale.Source);
        command.Parameters.AddWithValue("@locationId", _options.LocationId);
        command.Parameters.AddWithValue("@ticket", sale.TicketNumber);
        command.Parameters.AddWithValue("@payload", SaleJsonSerializer.Serialize(sale));
        command.Parameters.AddWithValue("@createdAt", Format(timeProvider.GetUtcNow()));
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public async Task<IReadOnlyList<OutboxSale>> ClaimDueAsync(int limit, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        var claimed = new List<OutboxSale>();
        var now = timeProvider.GetUtcNow();
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var candidates = new List<OutboxSale>();
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = (SqliteTransaction)transaction;
            select.CommandText = """
                SELECT id, source, location_id, ticket_number, payload_json, status, attempts,
                       last_attempt_at, next_attempt_at, created_at, sent_at, last_error
                FROM outbox_sales
                WHERE status = 'pending'
                   OR (status = 'failed' AND next_attempt_at IS NOT NULL AND next_attempt_at <= @now)
                ORDER BY created_at, id
                LIMIT @limit;
                """;
            select.Parameters.AddWithValue("@now", Format(now));
            select.Parameters.AddWithValue("@limit", limit);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                candidates.Add(ReadOutboxSale(reader));
            }
        }

        foreach (var candidate in candidates)
        {
            await using var update = connection.CreateCommand();
            update.Transaction = (SqliteTransaction)transaction;
            update.CommandText = """
                UPDATE outbox_sales
                SET status = 'sending', attempts = attempts + 1, last_attempt_at = @now,
                    next_attempt_at = NULL, last_error = NULL
                WHERE id = @id AND status IN ('pending', 'failed');
                """;
            update.Parameters.AddWithValue("@now", Format(now));
            update.Parameters.AddWithValue("@id", candidate.Id);
            if (await update.ExecuteNonQueryAsync(cancellationToken) == 1)
            {
                claimed.Add(candidate with
                {
                    Status = "sending",
                    Attempts = candidate.Attempts + 1,
                    LastAttemptAt = now,
                    NextAttemptAt = null,
                    LastError = null
                });
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    public async Task MarkSentAsync(long id, CancellationToken cancellationToken)
    {
        await UpdateStatusAsync(
            id,
            "sent",
            sentAt: timeProvider.GetUtcNow(),
            nextAttemptAt: null,
            error: null,
            cancellationToken);
    }

    public async Task MarkFailedAsync(
        long id,
        int attempts,
        string error,
        bool retryable,
        CancellationToken cancellationToken)
    {
        var nextAttempt = retryable
            ? timeProvider.GetUtcNow() + RetrySchedule.ForAttempt(attempts)
            : (DateTimeOffset?)null;
        await UpdateStatusAsync(id, "failed", null, nextAttempt, Truncate(error, 2000), cancellationToken);
    }

    public async Task<OutboxStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT status, COUNT(*) FROM outbox_sales GROUP BY status;
            SELECT MAX(ticket_number), MAX(sent_at) FROM outbox_sales;
            """;
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            counts[reader.GetString(0)] = reader.GetInt32(1);
        }

        var outboxCounts = new OutboxCounts(
            counts.GetValueOrDefault("pending"),
            counts.GetValueOrDefault("sending"),
            counts.GetValueOrDefault("sent"),
            counts.GetValueOrDefault("failed"));

        long? lastTicket = null;
        DateTimeOffset? lastSync = null;
        if (await reader.NextResultAsync(cancellationToken) && await reader.ReadAsync(cancellationToken))
        {
            lastTicket = reader.IsDBNull(0) ? null : reader.GetInt64(0);
            lastSync = ParseNullable(reader, 1);
        }

        return new OutboxStatus(outboxCounts, lastTicket, lastSync);
    }

    private async Task UpdateStatusAsync(
        long id,
        string status,
        DateTimeOffset? sentAt,
        DateTimeOffset? nextAttemptAt,
        string? error,
        CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE outbox_sales
            SET status = @status, sent_at = @sentAt, next_attempt_at = @nextAttemptAt,
                last_error = @error
            WHERE id = @id AND status = 'sending';
            """;
        command.Parameters.AddWithValue("@status", status);
        command.Parameters.AddWithValue("@sentAt", DbValue(sentAt));
        command.Parameters.AddWithValue("@nextAttemptAt", DbValue(nextAttemptAt));
        command.Parameters.AddWithValue("@error", (object?)error ?? DBNull.Value);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private SqliteConnection CreateConnection(string? resolvedPath = null)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = resolvedPath ?? ResolveDatabasePath(_options.DatabasePath),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        return new SqliteConnection(builder.ToString());
    }

    private static OutboxSale ReadOutboxSale(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetInt64(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.GetInt32(6),
        ParseNullable(reader, 7),
        ParseNullable(reader, 8),
        Parse(reader.GetString(9)),
        ParseNullable(reader, 10),
        reader.IsDBNull(11) ? null : reader.GetString(11));

    private static string ResolveDatabasePath(string configuredPath) =>
        Path.GetFullPath(configuredPath, AppContext.BaseDirectory);

    private static string Format(DateTimeOffset value) => value.ToString(TimestampFormat, CultureInfo.InvariantCulture);
    private static DateTimeOffset Parse(string value) => DateTimeOffset.ParseExact(value, TimestampFormat, CultureInfo.InvariantCulture);
    private static DateTimeOffset? ParseNullable(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Parse(reader.GetString(ordinal));
    private static object DbValue(DateTimeOffset? value) => value is null ? DBNull.Value : Format(value.Value);
    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
