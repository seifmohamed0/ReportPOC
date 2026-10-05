using ReportPoC.Application.Reporting;
using Xunit;

namespace ReportPoC.Application.Tests.Reporting;

public sealed class SampleSalesReportServiceTests
{
    [Fact]
    public async Task GenerateAsync_UsesPdfReportServiceWithoutQuestPdfDependency()
    {
        var reportService = new FakePdfReportService();
        var service = new SampleSalesReportService(reportService);

        var result = await service.GenerateAsync();

        Assert.Equal(new byte[] { 1, 2, 3 }, result);
        Assert.Equal("Sample Sales Report", reportService.Title);
        Assert.Equal(new[] { "Customer", "Product", "Quantity", "Amount" }, reportService.Headers);
        Assert.Equal(ReportOrientation.Landscape, reportService.Orientation);
        Assert.Equal(new[] { "North Region", "South Region" }, reportService.GroupHeaders);
        Assert.Equal(3, reportService.RowCount);
        Assert.True(reportService.HasTotalsRow);
        Assert.True(reportService.HasGrandTotalRow);
    }

    private sealed class FakePdfReportService : IPdfReportService
    {
        public string? Title { get; private set; }
        public IReadOnlyList<string> Headers { get; private set; } = [];
        public IReadOnlyList<string> GroupHeaders { get; private set; } = [];
        public ReportOrientation Orientation { get; private set; }
        public int RowCount { get; private set; }
        public bool HasTotalsRow { get; private set; }
        public bool HasGrandTotalRow { get; private set; }

        public Task<byte[]> GenerateAsync<TRow>(
            PdfReportDefinition<TRow> report,
            CancellationToken cancellationToken = default)
            where TRow : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            Title = report.Title;
            Headers = report.Columns.Select(column => column.Header).ToArray();
            Orientation = report.Orientation;
            RowCount = report.Rows.Count;
            return Task.FromResult(new byte[] { 1, 2, 3 });
        }

        public Task<byte[]> GenerateAsync<TGroup, TItem>(
            GroupedPdfReportDefinition<TGroup, TItem> report,
            CancellationToken cancellationToken = default)
            where TGroup : class
            where TItem : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            Title = report.Title;
            Headers = report.Columns.Select(column => column.Header).ToArray();
            GroupHeaders = report.Groups.Select(group => group.Header).ToArray();
            Orientation = report.Orientation;
            RowCount = report.Groups.Sum(group => group.Items.Count);
            HasTotalsRow = report.TotalsRow is not null;
            HasGrandTotalRow = report.IncludeGrandTotal;
            return Task.FromResult(new byte[] { 1, 2, 3 });
        }
    }
}