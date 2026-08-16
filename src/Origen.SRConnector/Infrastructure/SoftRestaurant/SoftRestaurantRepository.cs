using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Origen.SRConnector.Configuration;
using Origen.SRConnector.Domain;

namespace Origen.SRConnector.Infrastructure.SoftRestaurant;

public sealed class SoftRestaurantRepository(
    IOptions<SoftRestaurantOptions> options,
    ILogger<SoftRestaurantRepository> logger) : ISoftRestaurantRepository
{
    private readonly SoftRestaurantOptions _options = options.Value;

    public async Task TestConnectionAsync(CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateReadCommand(connection, SoftRestaurantQueries.TestConnection);
        await command.ExecuteScalarAsync(cancellationToken);
        logger.LogInformation("SQL connection successful");
    }

    public async Task<IReadOnlyList<Sale>> GetClosedSalesAsync(
        DateTime since,
        CancellationToken cancellationToken)
    {
        var headers = new List<SaleHeader>();

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await ReadHeadersAsync(
            connection,
            SoftRestaurantQueries.ClosedSales,
            since,
            historical: false,
            headers,
            cancellationToken);
        await ReadHeadersAsync(
            connection,
            SoftRestaurantQueries.HistoricalClosedSales,
            since,
            historical: true,
            headers,
            cancellationToken);

        // Una venta puede estar visible brevemente en ambas fuentes durante el corte.
        // El ticket global es la identidad estable; se prefiere tempcheques si aparece en ambas.
        var uniqueHeaders = headers
            .GroupBy(header => header.TicketNumber)
            .Select(group => group.OrderBy(header => header.IsHistorical).First())
            .OrderBy(header => header.TicketNumber)
            .ToList();

        var sales = new List<Sale>(uniqueHeaders.Count);
        foreach (var header in uniqueHeaders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payments = await ReadPaymentsAsync(
                connection,
                header.RelatedFolio,
                header.IsHistorical,
                cancellationToken);
            var items = await ReadItemsAsync(
                connection,
                header.RelatedFolio,
                header.IsHistorical,
                cancellationToken);
            sales.Add(header.ToSale(items, payments));
        }

        return sales;
    }

    private async Task ReadHeadersAsync(
        SqlConnection connection,
        string query,
        DateTime since,
        bool historical,
        ICollection<SaleHeader> headers,
        CancellationToken cancellationToken)
    {
        await using var command = CreateReadCommand(connection, query);
        command.Parameters.Add(new SqlParameter("@since", SqlDbType.DateTime) { Value = since });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            headers.Add(ReadHeader(reader, historical));
        }
    }

    private async Task<IReadOnlyList<SalePayment>> ReadPaymentsAsync(
        SqlConnection connection,
        long folio,
        bool historical,
        CancellationToken cancellationToken)
    {
        var payments = new List<SalePayment>();
        var query = historical
            ? SoftRestaurantQueries.HistoricalPayments
            : SoftRestaurantQueries.Payments;
        await using var command = CreateReadCommand(connection, query);
        command.Parameters.Add(new SqlParameter("@folio", SqlDbType.BigInt) { Value = folio });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            payments.Add(new SalePayment(
                reader.GetString(0),
                reader.GetDecimal(1),
                GetDecimalOrZero(reader, 2),
                GetNullableString(reader, 3),
                GetNullableString(reader, 4)));
        }

        return payments;
    }

    private async Task<IReadOnlyList<SaleItem>> ReadItemsAsync(
        SqlConnection connection,
        long folio,
        bool historical,
        CancellationToken cancellationToken)
    {
        var items = new List<SaleItem>();
        var query = historical
            ? SoftRestaurantQueries.HistoricalItems
            : SoftRestaurantQueries.Items;
        await using var command = CreateReadCommand(connection, query);
        command.Parameters.Add(new SqlParameter("@folio", SqlDbType.BigInt) { Value = folio });
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new SaleItem(
                Convert.ToInt64(reader.GetValue(0)),
                reader.GetString(1),
                GetNullableString(reader, 2),
                reader.GetDecimal(3),
                GetDecimalOrZero(reader, 4),
                GetDecimalOrZero(reader, 5),
                GetNullableDecimal(reader, 6),
                GetDecimalOrZero(reader, 7),
                GetBoolean(reader, 8),
                GetNullableString(reader, 9),
                GetBoolean(reader, 10)));
        }

        return items;
    }

    private SqlConnection CreateConnection() => new(_options.ConnectionString);

    private SqlCommand CreateReadCommand(SqlConnection connection, string commandText)
    {
        // Defensa adicional: este repositorio sólo acepta comandos que comienzan con SELECT.
        if (!commandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SoftRestaurant repository only permits SELECT commands.");
        }

        return new SqlCommand(commandText, connection)
        {
            CommandType = CommandType.Text,
            CommandTimeout = _options.CommandTimeoutSeconds
        };
    }

    private static SaleHeader ReadHeader(SqlDataReader reader, bool historical)
    {
        var offset = historical ? 1 : 0;
        var customerId = GetNullableString(reader, 4 + offset);
        return new SaleHeader(
            reader.GetInt64(0),
            historical ? reader.GetInt64(1) : reader.GetInt64(0),
            Convert.ToInt64(reader.GetValue(1 + offset)),
            reader.GetDateTime(2 + offset),
            reader.GetDateTime(3 + offset),
            customerId is null ? null : new CustomerReference(customerId, GetNullableString(reader, 5 + offset)),
            GetNullableString(reader, 6 + offset),
            GetNullableString(reader, 7 + offset),
            GetDecimalOrZero(reader, 8 + offset),
            GetDecimalOrZero(reader, 9 + offset),
            GetDecimalOrZero(reader, 10 + offset),
            GetDecimalOrZero(reader, 11 + offset),
            GetDecimalOrZero(reader, 12 + offset),
            GetNullableString(reader, 13 + offset),
            GetNullableString(reader, 14 + offset),
            historical);
    }

    private static string? GetNullableString(SqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetString(ordinal).Trim();
        return value.Length == 0 ? null : value;
    }

    private static decimal GetDecimalOrZero(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? 0m : reader.GetDecimal(ordinal);

    private static decimal? GetNullableDecimal(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDecimal(ordinal);

    private static bool GetBoolean(SqlDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);

    private sealed record SaleHeader(
        long SrFolio,
        long RelatedFolio,
        long TicketNumber,
        DateTime OpenedAt,
        DateTime ClosedAt,
        CustomerReference? Customer,
        string? Table,
        string? Station,
        decimal Subtotal,
        decimal Tax,
        decimal Total,
        decimal Tip,
        decimal TotalWithTip,
        string? OpeningUser,
        string? PaymentUser,
        bool IsHistorical)
    {
        internal Sale ToSale(IReadOnlyList<SaleItem> items, IReadOnlyList<SalePayment> payments) => new()
        {
            SrFolio = SrFolio,
            TicketNumber = TicketNumber,
            OpenedAt = OpenedAt,
            ClosedAt = ClosedAt,
            Station = Station,
            Customer = Customer,
            Totals = new SaleTotals(Subtotal, Tax, Total, Tip, TotalWithTip),
            Items = items,
            Payments = payments,
            Table = Table,
            OpeningUser = OpeningUser,
            PaymentUser = PaymentUser
        };
    }
}
