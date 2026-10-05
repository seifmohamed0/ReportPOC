namespace ReportPoC.Application.Reporting;

public interface IPdfReportRenderer
{
    Task<byte[]> RenderAsync<TRow>(
        PdfReportDefinition<TRow> report,
        CancellationToken cancellationToken = default)
        where TRow : class;

    Task<byte[]> RenderAsync<TGroup, TItem>(
        GroupedPdfReportDefinition<TGroup, TItem> report,
        CancellationToken cancellationToken = default)
        where TGroup : class
        where TItem : class;
}