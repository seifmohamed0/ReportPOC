using ReportPoC.Application.Reporting;
using Xunit;

namespace ReportPoC.Application.Tests.Reporting;

public sealed class ReportExamplesServiceTests
{
    [Fact]
    public async Task Examples_BuildDistinctReportDefinitionsForEachEndpoint()
    {
        var reportService = new RecordingPdfReportService();
        var examples = new SampleSalesReportService(reportService);

        await examples.GenerateFlatAsync();
        var flat = Assert.IsType<CapturedReport>(reportService.LastReport);
        Assert.False(flat.IsGrouped);
        Assert.Equal("Flat Sales Report", flat.Title);
        Assert.Equal(ReportOrientation.Portrait, flat.Orientation);
        Assert.Contains("SaleDate", flat.ColumnNames);

        await examples.GenerateGroupedAsync();
        var grouped = Assert.IsType<CapturedReport>(reportService.LastReport);
        Assert.True(grouped.IsGrouped);
        Assert.Equal("Grouped Sales Report", grouped.Title);
        Assert.Contains("North Region", grouped.GroupHeaders);
        Assert.True(grouped.HasTotalsRow);
        Assert.True(grouped.HasGrandTotalRow);

    }

    private sealed class RecordingPdfReportService : IPdfReportService
    {
        public object? LastReport { get; private set; }

        public Task<byte[]> GenerateAsync<TRow>(
            PdfReportDefinition<TRow> report,
            CancellationToken cancellationToken = default)
            where TRow : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastReport = Capture(report);
            return Task.FromResult(new byte[] { 1 });
        }

        public Task<byte[]> GenerateAsync<TGroup, TItem>(
            GroupedPdfReportDefinition<TGroup, TItem> report,
            CancellationToken cancellationToken = default)
            where TGroup : class
            where TItem : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastReport = Capture(report);
            return Task.FromResult(new byte[] { 1 });
        }

        private static CapturedReport Capture<TRow>(PdfReportDefinition<TRow> report)
            where TRow : class
        {
            return new CapturedReport(
                report.Title,
                report.Orientation,
                false,
                report.Columns.Select(CaptureColumn).ToArray(),
                report.Columns.Select(column => column.Name).ToArray(),
                report.Warnings,
                [],
                report.Rows.Count,
                report.TotalsRow is not null,
                false,
                report.RowStyles.Count > 0,
                report.Columns.Any(column => column.CellStyles.Count > 0),
                report.Summary?.TotalColumnName,
                report.Summary is not null,
                report.Columns.FirstOrDefault(column => column.GetLink is not null)?.GetLink?.Invoke(report.Rows[0]) ?? "");
        }

        private static CapturedReport Capture<TGroup, TItem>(GroupedPdfReportDefinition<TGroup, TItem> report)
            where TGroup : class
            where TItem : class
        {
            var firstItem = report.Groups.SelectMany(group => group.Items).First();
            return new CapturedReport(
                report.Title,
                report.Orientation,
                true,
                report.Columns.Select(CaptureColumn).ToArray(),
                report.Columns.Select(column => column.Name).ToArray(),
                report.Warnings,
                report.Groups.Select(group => group.Header).ToArray(),
                report.Groups.Sum(group => group.Items.Count),
                report.TotalsRow is not null,
                report.IncludeGrandTotal,
                report.RowStyles.Count > 0,
                report.Columns.Any(column => column.CellStyles.Count > 0),
                report.Summary?.TotalColumnName,
                report.Summary is not null,
                report.Columns.FirstOrDefault(column => column.GetLink is not null)?.GetLink?.Invoke(firstItem) ?? "");
        }

        private static CapturedColumn CaptureColumn<TRow>(PdfColumnDefinition<TRow> column)
            where TRow : class
        {
            return new CapturedColumn(column.Name, column.Width.Kind, column.Format, column.Aggregate);
        }
    }

    private sealed record CapturedReport(
        string Title,
        ReportOrientation Orientation,
        bool IsGrouped,
        IReadOnlyList<CapturedColumn> Columns,
        IReadOnlyList<string> ColumnNames,
        IReadOnlyList<string> Warnings,
        IReadOnlyList<string> GroupHeaders,
        int RowCount,
        bool HasTotalsRow,
        bool HasGrandTotalRow,
        bool HasRowHighlight,
        bool HasCellHighlight,
        string? SummaryTotalColumn,
        bool HasCurrencySelector,
        string FirstRowLink);

    private sealed record CapturedColumn(
        string Name,
        ColumnWidthKind WidthKind,
        string? Format,
        AggregateKind Aggregate);
}