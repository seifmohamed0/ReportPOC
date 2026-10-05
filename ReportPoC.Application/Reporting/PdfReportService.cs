namespace ReportPoC.Application.Reporting;

public interface IPdfReportService
{
    Task<byte[]> GenerateAsync<TRow>(
        PdfReportDefinition<TRow> report,
        CancellationToken cancellationToken = default)
        where TRow : class;

    Task<byte[]> GenerateAsync<TGroup, TItem>(
        GroupedPdfReportDefinition<TGroup, TItem> report,
        CancellationToken cancellationToken = default)
        where TGroup : class
        where TItem : class;
}

public interface ICurrencySpellOutService
{
    Task<string> SpellOutAsync(
        decimal amount,
        string currencyCode,
        CancellationToken cancellationToken = default);
}

public sealed class PdfReportService(
    IPdfReportRenderer renderer,
    ICurrencySpellOutService? spellOutService = null) : IPdfReportService
{
    public async Task<byte[]> GenerateAsync<TRow>(
        PdfReportDefinition<TRow> report,
        CancellationToken cancellationToken = default)
        where TRow : class
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Summary is null)
        {
            return await renderer.RenderAsync(report, cancellationToken);
        }

        var summaryLines = await ResolveSummaryAsync(
            report.Columns,
            report.Rows,
            report.Summary,
            cancellationToken);
        return await renderer.RenderAsync(report.WithSummaryLines(summaryLines), cancellationToken);
    }

    public async Task<byte[]> GenerateAsync<TGroup, TItem>(
        GroupedPdfReportDefinition<TGroup, TItem> report,
        CancellationToken cancellationToken = default)
        where TGroup : class
        where TItem : class
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Summary is null)
        {
            return await renderer.RenderAsync(report, cancellationToken);
        }

        var rows = report.Groups.SelectMany(group => group.Items).ToArray();
        var summaryLines = await ResolveSummaryAsync(
            report.Columns,
            rows,
            report.Summary,
            cancellationToken);
        return await renderer.RenderAsync(report.WithSummaryLines(summaryLines), cancellationToken);
    }

    private async Task<IReadOnlyList<PdfSummaryLine>> ResolveSummaryAsync<TRow>(
        IReadOnlyList<PdfColumnDefinition<TRow>> columns,
        IEnumerable<TRow> rows,
        PdfSummaryDefinition<TRow> summary,
        CancellationToken cancellationToken)
        where TRow : class
    {
        if (spellOutService is null)
        {
            throw new InvalidOperationException("A currency spell-out service is required for this report summary.");
        }

        var totalColumn = columns.Single(column => column.Name == summary.TotalColumnName);
        if (totalColumn.GetNumeric is null)
        {
            throw new InvalidOperationException($"Summary column '{summary.TotalColumnName}' must configure Sum().");
        }

        var summaryRows = rows
            .Select(row => (Row: row, CurrencyCode: summary.GetCurrencyCode(row)))
            .ToArray();
        if (summaryRows.Any(item => string.IsNullOrWhiteSpace(item.CurrencyCode)))
        {
            throw new InvalidOperationException("Every row in a spell-out summary must have a currency code.");
        }

        var totalsByCurrency = summaryRows
            .GroupBy(item => item.CurrencyCode!, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        var lines = new List<PdfSummaryLine>();
        foreach (var currencyGroup in totalsByCurrency)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var amount = currencyGroup.Sum(item => totalColumn.GetNumeric(item.Row) ?? 0m);
            var text = await spellOutService.SpellOutAsync(amount, currencyGroup.Key, cancellationToken);
            lines.Add(new PdfSummaryLine(currencyGroup.Key, amount, text));
        }

        return Array.AsReadOnly(lines.ToArray());
    }
}