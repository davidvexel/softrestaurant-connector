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

    internal const string HistoricalClosedSales = """
        SELECT
            COALESCE(ch.foliotempcheques, ch.folio) AS folio_temporal,
            ch.folio AS folio_historico,
            ch.numcheque,
            ch.fecha,
            ch.cierre,
            ch.idcliente,
            c.nombre AS cliente,
            ch.mesa,
            ch.estacion,
            ch.subtotal,
            ch.totalimpuesto1,
            ch.total,
            ch.propina,
            ch.totalconpropina,
            ch.usuarioapertura,
            ch.usuariopago
        FROM dbo.cheques ch
        LEFT JOIN dbo.clientes c
            ON c.idcliente = ch.idcliente
        WHERE
            ch.numcheque > 0
            AND ch.cierre IS NOT NULL
            AND ISNULL(ch.cancelado, 0) = 0
            AND ch.cierre >= @since
        ORDER BY ch.numcheque;
        """;

    internal const string HistoricalPayments = """
        SELECT
            idformadepago,
            importe,
            propina,
            referencia,
            cardBrand
        FROM dbo.chequespagos
        WHERE folio = @folio;
        """;

    internal const string HistoricalItems = """
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
        FROM dbo.vwrepproductosvendidoscheques
        WHERE folio = @folio
          AND numcheque > 0
          AND cierre IS NOT NULL
          AND ISNULL(cancelado, 0) = 0
        ORDER BY foliodet, idproducto;
        """;
}
