namespace ReportPoC.Application.Reporting;

public interface ISampleSalesReportService
{
    Task<byte[]> GenerateAsync(CancellationToken cancellationToken = default);
    Task<byte[]> GenerateFlatAsync(CancellationToken cancellationToken = default);
    Task<byte[]> GenerateGroupedAsync(CancellationToken cancellationToken = default);
}

public sealed class SampleSalesReportService(IPdfReportService pdfReportService) : ISampleSalesReportService
{
    public Task<byte[]> GenerateAsync(CancellationToken cancellationToken = default)
    {
        return GenerateGroupedReportAsync("Sample Sales Report", cancellationToken);
    }

    public Task<byte[]> GenerateFlatAsync(CancellationToken cancellationToken = default)
    {
        var report = PdfReport
            .For(CreateRows())
            .Title("Flat Sales Report")
            .Column(row => row.SaleDate, column => column.Header("Sale Date").Order(1).Format("dd-MMM-yyyy"))
            .Column(row => row.Customer, column => column.Header("Customer").Order(2))
            .Column(row => row.Product, column => column.Header("Product").Order(3))
            .Column(row => row.Quantity, column => column.Header("Quantity").Order(4).AlignRight().Sum())
            .Column(row => row.Amount, column => column.Header("Amount").Order(5).Format("C").AlignRight().Sum())
            .TotalsRow(totals => totals.Label("All sales").In(row => row.Customer))
            .Build();

        return pdfReportService.GenerateAsync(report, cancellationToken);
    }

    public Task<byte[]> GenerateGroupedAsync(CancellationToken cancellationToken = default)
    {
        return GenerateGroupedReportAsync("Grouped Sales Report", cancellationToken);
    }

    private Task<byte[]> GenerateGroupedReportAsync(string title, CancellationToken cancellationToken)
    {
        var report = PdfReport
            .ForGroups(CreateGroups())
            .Title(title)
            .Landscape()
            .Column(row => row.Customer, column => column.Header("Customer").Order(1))
            .Column(row => row.Product, column => column.Header("Product").Order(2))
            .Column(row => row.Quantity, column => column.Header("Quantity").Order(3).AlignRight().Sum())
            .Column(row => row.Amount, column => column.Header("Amount").Order(4)
                .Width(ColumnWidth.Fixed(85)).Format("C").AlignRight().Sum())
            .Column(row => row.CurrencyCode)
            .HideWhen(row => row.CurrencyCode, shouldHide: true)
            .TotalsRow(totals => totals.Label("Totals").In(row => row.Customer))
            .GrandTotalRow()
            .Build();

        return pdfReportService.GenerateAsync(report, cancellationToken);
    }

    private static IReadOnlyList<SalesReportRow> CreateRows()
    {
        return Array.AsReadOnly(new[]
        {
            new SalesReportRow(1001, new DateOnly(2026, 9, 2), "Northwind", "Desk lamp", 4, 49.95m, "USD", "Paid", "INT-1001"),
            new SalesReportRow(1002, new DateOnly(2026, 9, 5), "Fabrikam", "Office chair", 2, 189.00m, "USD", "Draft", "INT-1002"),
            new SalesReportRow(1003, new DateOnly(2026, 9, 12), "Contoso", "Notebook", 12, 3.50m, "USD", "Paid", "INT-1003")
        });
    }

    private static IReadOnlyList<PdfReportGroup<SalesRegion, SalesReportRow>> CreateGroups()
    {
        var rows = CreateRows();
        return Array.AsReadOnly(new[]
        {
            new PdfReportGroup<SalesRegion, SalesReportRow>(
                "North Region",
                new SalesRegion("North"),
                rows.Where(row => row.Customer != "Contoso")),
            new PdfReportGroup<SalesRegion, SalesReportRow>(
                "South Region",
                new SalesRegion("South"),
                rows.Where(row => row.Customer == "Contoso"))
        });
    }

    private sealed record SalesRegion(string Name);

    private sealed record SalesReportRow(
        int TransactionId,
        DateOnly SaleDate,
        string Customer,
        string Product,
        int Quantity,
        decimal Amount,
        string CurrencyCode,
        string Status,
        string InternalCode);
}