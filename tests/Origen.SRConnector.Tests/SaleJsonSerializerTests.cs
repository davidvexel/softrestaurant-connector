using System.Text.Json;
using Origen.SRConnector.Domain;
using Origen.SRConnector.Services;

namespace Origen.SRConnector.Tests;

public sealed class SaleJsonSerializerTests
{
    [Fact]
    public void Serialize_BuildsExpectedPayloadWithCustomerAndMultiplePayments()
    {
        var sale = CreateSale() with
        {
            Customer = new CustomerReference("DASDASDSAD2323", "PRUEBA 2"),
            Payments =
            [
                new SalePayment("MC", 20m, 0m, null, null),
                new SalePayment("VISA", 20m, 0m, "ABC", "VISA")
            ]
        };

        using var document = JsonDocument.Parse(SaleJsonSerializer.Serialize(sale));
        var root = document.RootElement;

        Assert.Equal("softrestaurant", root.GetProperty("source").GetString());
        Assert.False(root.TryGetProperty("workspace_id", out _));
        Assert.Equal(9, root.GetProperty("folio").GetInt64());
        Assert.Equal(1735, root.GetProperty("ticket").GetInt64());
        Assert.Equal("DASDASDSAD2323", root.GetProperty("customer").GetProperty("external_id").GetString());
        Assert.Equal(2, root.GetProperty("payments").GetArrayLength());
        Assert.Equal(40m, root.GetProperty("totals").GetProperty("total").GetDecimal());
    }

    [Fact]
    public void Serialize_SaleWithoutCustomer_OmitsCustomer()
    {
        using var document = JsonDocument.Parse(SaleJsonSerializer.Serialize(CreateSale()));
        Assert.False(document.RootElement.TryGetProperty("customer", out _));
    }

    [Fact]
    public void Serialize_PreservesCompoundMainAndModifierRows()
    {
        var sale = CreateSale() with
        {
            Items =
            [
                new SaleItem(1, "JUGO", "JUGO VERDE", 1m, 70m, 0m, 70m, 0m, false, "_G5GQ71BQN", true),
                new SaleItem(2, "GRANDE", "GRANDE", 1m, 30m, 0m, 30m, 0m, true, "_G5GQ71BQN", false)
            ]
        };

        using var document = JsonDocument.Parse(SaleJsonSerializer.Serialize(sale));
        var items = document.RootElement.GetProperty("items");

        Assert.Equal(2, items.GetArrayLength());
        Assert.True(items[0].GetProperty("compound_main").GetBoolean());
        Assert.True(items[1].GetProperty("modifier").GetBoolean());
        Assert.Equal("_G5GQ71BQN", items[1].GetProperty("compound_id").GetString());
    }

    private static Sale CreateSale() => new()
    {
        SrFolio = 9,
        TicketNumber = 1735,
        OpenedAt = new DateTime(2026, 8, 15, 13, 52, 16),
        ClosedAt = new DateTime(2026, 8, 15, 13, 59, 24),
        Station = "SERVIDOR",
        Customer = null,
        Totals = new SaleTotals(34.48m, 5.52m, 40m, 0m, 40m),
        Items = [new SaleItem(1, "02022", "ESPRESSO", 1m, 40m, 0m, 40m, 5.52m, false, null, false)],
        Payments = [new SalePayment("MC", 40m, 0m, null, null)]
    };
}
