using ReportPoC.Application.Reporting;
using Xunit;

namespace ReportPoC.Application.Tests.Reporting;

public sealed class PdfReportServiceTests
{
    [Fact]
    public async Task GenerateAsync_ResolvesGroupedSummaryPerCurrencyBeforeRendering()
    {
        var groups = new[]
        {
            new PdfReportGroup<Region, LedgerRow>(
                "All regions",
                new Region("All"),
                new[]
                {
                    new LedgerRow(1.25m, "USD"),
                    new LedgerRow(2.50m, "USD"),
                    new LedgerRow(1m, "EUR")
                })
        };
        var report = PdfReport
            .ForGroups(groups)
            .Column(row => row.Balance, column => column.Sum())
            .Summary(summary => summary.Total(row => row.Balance).SpellOut(row => row.CurrencyCode))
            .Build();
        var renderer = new RecordingRenderer();
        var spellOut = new RecordingSpellOutService();
        var service = new PdfReportService(renderer, spellOut);

        var result = await service.GenerateAsync(report);

        Assert.Equal(new byte[] { 1, 2, 3 }, result);
        Assert.Equal(new[] { (1m, "EUR"), (3.75m, "USD") }, spellOut.Calls);
        Assert.Equal(new[] { "1.00 EUR in words", "3.75 USD in words" }, renderer.SummaryLines);
    }

    private sealed class RecordingSpellOutService : ICurrencySpellOutService
    {
        public List<(decimal Amount, string CurrencyCode)> Calls { get; } = [];

        public Task<string> SpellOutAsync(
            decimal amount,
            string currencyCode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add((amount, currencyCode));
            return Task.FromResult($"{amount:0.00} {currencyCode} in words");
        }
    }

    private sealed class RecordingRenderer : IPdfReportRenderer
    {
        public IReadOnlyList<string> SummaryLines { get; private set; } = [];

        public Task<byte[]> RenderAsync<TRow>(
            PdfReportDefinition<TRow> report,
            CancellationToken cancellationToken = default)
            where TRow : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            SummaryLines = report.SummaryLines.Select(line => line.SpelledOutText).ToArray();
            return Task.FromResult(new byte[] { 1, 2, 3 });
        }

        public Task<byte[]> RenderAsync<TGroup, TItem>(
            GroupedPdfReportDefinition<TGroup, TItem> report,
            CancellationToken cancellationToken = default)
            where TGroup : class
            where TItem : class
        {
            cancellationToken.ThrowIfCancellationRequested();
            SummaryLines = report.SummaryLines.Select(line => line.SpelledOutText).ToArray();
            return Task.FromResult(new byte[] { 1, 2, 3 });
        }
    }

    private sealed record Region(string Name);
    private sealed record LedgerRow(decimal Balance, string CurrencyCode);
}