using Origen.SRConnector.Domain;

namespace Origen.SRConnector.Tests;

internal static class TestSaleFactory
{
    internal static Sale Create(long ticket = 1735) => new()
    {
        SrFolio = 9,
        TicketNumber = ticket,
        OpenedAt = new DateTime(2026, 8, 15, 13, 52, 16),
        ClosedAt = new DateTime(2026, 8, 15, 13, 59, 24),
        Station = "SERVIDOR",
        Totals = new SaleTotals(34.48m, 5.52m, 40m, 0m, 40m),
        Items = [new SaleItem(1, "02022", "ESPRESSO", 1m, 40m, 0m, 40m, 5.52m, false, null, false)],
        Payments = [new SalePayment("MC", 40m, 0m, null, null)]
    };
}

