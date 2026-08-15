using Origen.SRConnector.Infrastructure.SoftRestaurant;

namespace Origen.SRConnector.Tests;

public sealed class SoftRestaurantQueriesTests
{
    [Theory]
    [MemberData(nameof(AllQueries))]
    public void EveryQuery_IsSelectOnly(string query)
    {
        Assert.StartsWith("SELECT", query.TrimStart(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TRUNCATE", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClosedSales_ExcludesOpenAndCancelledSalesAndUsesLookbackParameter()
    {
        Assert.Contains("tc.numcheque > 0", SoftRestaurantQueries.ClosedSales);
        Assert.Contains("tc.cierre IS NOT NULL", SoftRestaurantQueries.ClosedSales);
        Assert.Contains("ISNULL(tc.cancelado, 0) = 0", SoftRestaurantQueries.ClosedSales);
        Assert.Contains("tc.cierre >= @since", SoftRestaurantQueries.ClosedSales);
    }

    [Fact]
    public void ChildQueries_FilterByParameterizedFolio()
    {
        Assert.Contains("folio = @folio", SoftRestaurantQueries.Payments);
        Assert.Contains("folio = @folio", SoftRestaurantQueries.Items);
    }

    public static TheoryData<string> AllQueries => new()
    {
        SoftRestaurantQueries.TestConnection,
        SoftRestaurantQueries.ClosedSales,
        SoftRestaurantQueries.Payments,
        SoftRestaurantQueries.Items
    };
}

