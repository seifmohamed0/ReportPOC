using ReportPoC.Application.Reporting;
using Xunit;

namespace ReportPoC.Application.Tests.Reporting;

public sealed class PdfReportBuilderTests
{
    [Fact]
    public void Build_OrdersConfiguredColumnsAndKeepsUnorderedColumnsStable()
    {
        var report = PdfReport
            .For(new[] { new SampleRow("Northwind", 25m, 3) })
            .Column(row => row.Amount, column => column.Header("Total").Order(2))
            .Column(row => row.Customer, column => column.Header("Customer").Order(1))
            .Column(row => row.Quantity)
            .Build();

        Assert.Equal(new[] { "Customer", "Total", "Quantity" }, report.Columns.Select(column => column.Header));
        Assert.Equal(new object?[] { "Northwind", 25m, 3 }, report.Columns
            .Select(column => column.GetValue(report.Rows[0])));
        Assert.Equal(ReportOrientation.Portrait, report.Orientation);
    }

    [Fact]
    public void Build_UsesConfiguredTitleAndLandscapeOrientation()
    {
        var report = PdfReport
            .For(new[] { new SampleRow("Northwind", 25m, 3) })
            .Title("Sales")
            .Landscape()
            .Column(row => row.Customer)
            .Build();

        Assert.Equal("Sales", report.Title);
        Assert.Equal(ReportOrientation.Landscape, report.Orientation);
    }

    [Fact]
    public void Build_RejectsReportsWithoutColumns()
    {
        var builder = PdfReport.For(new[] { new SampleRow("Northwind", 25m, 3) });

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Column_RejectsComputedExpressions()
    {
        var builder = PdfReport.For(new[] { new SampleRow("Northwind", 25m, 3) });

        Assert.Throws<ArgumentException>(() => builder.Column(row => row.Quantity + 1));
    }

    [Fact]
    public void GroupedBuild_OrdersItemColumnsAndKeepsGroupBoundaries()
    {
        var groups = new[]
        {
            new PdfReportGroup<SampleGroup, SampleRow>(
                "North",
                new SampleGroup("North"),
                new[] { new SampleRow("Northwind", 25m, 3) }),
            new PdfReportGroup<SampleGroup, SampleRow>(
                "South",
                new SampleGroup("South"),
                new[] { new SampleRow("Contoso", 10m, 1) })
        };

        var report = PdfReport
            .ForGroups(groups)
            .Title("Grouped Sales")
            .Column(row => row.Amount, column => column.Header("Total").Order(2))
            .Column(row => row.Customer, column => column.Header("Customer").Order(1))
            .Build();

        Assert.Equal("Grouped Sales", report.Title);
        Assert.Equal(new[] { "Customer", "Total" }, report.Columns.Select(column => column.Header));
        Assert.Equal(new[] { "North", "South" }, report.Groups.Select(group => group.Header));
        Assert.Equal(new[] { "Northwind", "Contoso" }, report.Groups
            .Select(group => (string)report.Columns[0].GetValue(group.Items[0])!));
    }

    [Fact]
    public void Build_SupportsTargetLedgerReportConfiguration()
    {
        var groups = new[]
        {
            new PdfReportGroup<SampleGroup, LedgerRow>(
                "North",
                new SampleGroup("North"),
                new[] { new LedgerRow(12, "J-12", DateOnly.FromDateTime(DateTime.Today), "Rent", 10m, 0m, 10m, "USD", 1, 120) })
        };

        var report = PdfReport
            .ForGroups(groups)
            .Title("General Ledger Accounts")
            .Landscape()
            .Column(x => x.EntryNo, c => c.Order(1).Width(ColumnWidth.Fixed(70))
                .Link(x => $"https://erp.example/journals/{x.JournalEntryId}"))
            .Column(x => x.EntryDate, c => c.Order(2).Format("dd-MMM-yyyy"))
            .Column(x => x.Statement, c => c.Order(3).Width(ColumnWidth.Relative(3)))
            .Column(x => x.Debit, c => c.Order(4).AlignRight().Sum())
            .Column(x => x.Credit, c => c.Order(5).AlignRight().Sum())
            .Column(x => x.Balance, c => c.Order(6).AlignRight().Sum()
                .Highlight(ReportPalette.Danger, x => x.Balance < 0))
            .Column(x => x.CurrencyCode)
            .Column(x => x.StatusId)
            .Hide("UnknownClientColumn")
            .HideWhen(x => x.CurrencyCode, true)
            .HighlightRow(ReportPalette.Draft, x => x.StatusId == 1)
            .TotalsRow(t => t.Label("Totals").In(x => x.EntryDate))
            .GrandTotalRow()
            .Summary(s => s.Total(x => x.Balance).SpellOut(x => x.CurrencyCode))
            .Build();

        Assert.Equal("General Ledger Accounts", report.Title);
        Assert.Equal(
            new[] { "EntryNo", "EntryDate", "Statement", "Debit", "Credit", "Balance", "StatusId" },
            report.Columns.Select(column => column.Name));
        Assert.Equal(70, report.Columns[0].Width.Value);
        Assert.Equal("dd-MMM-yyyy", report.Columns[1].Format);
        Assert.Equal(AggregateKind.Sum, report.Columns[5].Aggregate);
        Assert.Single(report.Columns[0].GetLink is null ? [] : new[] { report.Columns[0].GetLink });
        Assert.Contains("UnknownClientColumn", report.Warnings);
        Assert.Equal("EntryDate", report.TotalsRow?.LabelColumnName);
        Assert.True(report.IncludeGrandTotal);
        Assert.Equal("Balance", report.Summary?.TotalColumnName);
    }

    private sealed record SampleRow(string Customer, decimal Amount, int Quantity);
    private sealed record SampleGroup(string Name);
    private sealed record LedgerRow(
        int EntryNo,
        string JournalEntryId,
        DateOnly EntryDate,
        string Statement,
        decimal Debit,
        decimal Credit,
        decimal Balance,
        string CurrencyCode,
        int StatusId,
        int InternalId);
}