namespace Origen.SRConnector.Infrastructure.Persistence;

public sealed record OutboxSale(
    long Id,
    string Source,
    string LocationId,
    long TicketNumber,
    string PayloadJson,
    string Status,
    int Attempts,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    string? LastError);

