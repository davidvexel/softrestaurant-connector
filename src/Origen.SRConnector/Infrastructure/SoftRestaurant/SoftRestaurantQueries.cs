namespace Origen.SRConnector.Infrastructure.SoftRestaurant;

internal static class SoftRestaurantQueries
{
    internal const string TestConnection = "SELECT 1;";

    internal const string ClosedSales = """
        SELECT
            tc.folio,
            tc.numcheque,
            tc.fecha,
            tc.cierre,
            tc.idcliente,
            c.nombre AS cliente,
            tc.mesa,
            tc.estacion,
            tc.subtotal,
            tc.totalimpuesto1,
            tc.total,
            tc.propina,
            tc.totalconpropina,
            tc.usuarioapertura,
            tc.usuariopago
        FROM dbo.tempcheques tc
        LEFT JOIN dbo.clientes c
            ON c.idcliente = tc.idcliente
        WHERE
            tc.numcheque > 0
            AND tc.cierre IS NOT NULL
            AND ISNULL(tc.cancelado, 0) = 0
            AND tc.cierre >= @since
        ORDER BY tc.numcheque;
        """;

    internal const string Payments = """
        SELECT
            idformadepago,
            importe,
            propina,
            referencia,
            cardBrand
        FROM dbo.tempchequespagos
        WHERE folio = @folio;
        """;

    internal const string Items = """
        SELECT
            foliodet,
            idproducto,
            descripcion,
            cantidad,
            preciocatalogo,
            descuento,
            calcpreciomenosdescuento,
            iva,
            modificador,
            idproductocompuesto,
            productocompuestoprincipal
        FROM dbo.vwrepproductosvendidostempcheques
        WHERE folio = @folio
          AND numcheque > 0
          AND cierre IS NOT NULL
          AND ISNULL(cancelado, 0) = 0
        ORDER BY foliodet;
        """;
}
