namespace ReportPoC.Application.Reporting;

public sealed class PdfReportGroup<TGroup, TItem>
    where TGroup : class
    where TItem : class
{
    public PdfReportGroup(string header, TGroup group, IEnumerable<TItem> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(header);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(items);

        Header = header;
        Group = group;
        Items = Array.AsReadOnly(items.ToArray());
    }

    public string Header { get; }
    public TGroup Group { get; }
    public IReadOnlyList<TItem> Items { get; }

    internal PdfReportGroup<TGroup, TItem> Snapshot()
    {
        return new PdfReportGroup<TGroup, TItem>(Header, Group, Items);
    }
}

public sealed class GroupedPdfReportBuilder<TGroup, TItem>
    where TGroup : class
    where TItem : class
{
    private readonly IReadOnlyList<PdfReportGroup<TGroup, TItem>> _groups;
    private readonly PdfReportBuilder<TItem> _itemBuilder;
    private bool _includeGrandTotal;

    internal GroupedPdfReportBuilder(IReadOnlyList<PdfReportGroup<TGroup, TItem>> groups)
    {
        _groups = Array.AsReadOnly(groups.ToArray());
        _itemBuilder = new PdfReportBuilder<TItem>(groups.SelectMany(group => group.Items).ToArray());
    }

    public GroupedPdfReportBuilder<TGroup, TItem> Title(string title)
    {
        _itemBuilder.Title(title);
        return this;
    }

    public GroupedPdfReportBuilder<TGroup, TItem> Landscape(bool enabled = true)
    {
        _itemBuilder.Landscape(enabled);
        return this;
    }

    public GroupedPdfReportBuilder<TGroup, TItem> Hide(params string[] names)
    {
        _itemBuilder.Hide(names);
        return this;
    }

    public GroupedPdfReportBuilder<TGroup, TItem> Hide<TValue>(
        System.Linq.Expressions.Expression<Func<TItem, TValue>> selector)
    {
        _itemBuilder.Hide(selector);
        return this;
    }

    public GroupedPdfReportBuilder<TGroup, TItem> HideWhen<TValue>(
        System.Linq.Expressions.Expression<Func<TItem, TValue>> selector,
        bool shouldHide)
    {
        _itemBuilder.HideWhen(selector, shouldHide);
        return this;
    }

    public GroupedPdfReportBuilder<TGroup, TItem> HighlightRow(
        ReportColor color,
        System.Linq.Expressions.Expression<Func<TItem, bool>> predicate)
    {
        _itemBuilder.HighlightRow(color, predicate);
        return this;
    }

    public GroupedPdfReportBuilder<TGroup, TItem> TotalsRow(
        Action<PdfTotalsRowBuilder<TItem>> configure)
    {
        _itemBuilder.TotalsRow(configure);
        return this;
    }

    public GroupedPdfReportBuilder<TGroup, TItem> GrandTotalRow()
    {
        _includeGrandTotal = true;
        return this;
    }

    public GroupedPdfReportBuilder<TGroup, TItem> Summary(
        Action<PdfSummaryBuilder<TItem>> configure)
    {
        _itemBuilder.Summary(configure);
        return this;
    }

    public GroupedPdfReportBuilder<TGroup, TItem> Column<TValue>(
        System.Linq.Expressions.Expression<Func<TItem, TValue>> selector,
        Action<PdfColumnBuilder<TItem, TValue>>? configure = null)
    {
        _itemBuilder.Column(selector, configure);
        return this;
    }

    public GroupedPdfReportDefinition<TGroup, TItem> Build()
    {
        var itemDefinition = _itemBuilder.Build();
        return new GroupedPdfReportDefinition<TGroup, TItem>(
            itemDefinition.Title,
            itemDefinition.Orientation,
            _groups,
            itemDefinition.Columns,
            itemDefinition.RowStyles,
            itemDefinition.Warnings,
            itemDefinition.TotalsRow,
            _includeGrandTotal,
            itemDefinition.Summary,
            itemDefinition.SummaryLines);
    }
}

public sealed class GroupedPdfReportDefinition<TGroup, TItem>
    where TGroup : class
    where TItem : class
{
    internal GroupedPdfReportDefinition(
        string title,
        ReportOrientation orientation,
        IReadOnlyList<PdfReportGroup<TGroup, TItem>> groups,
        IReadOnlyList<PdfColumnDefinition<TItem>> columns,
        IReadOnlyList<ConditionalStyle<TItem>> rowStyles,
        IReadOnlyList<string> warnings,
        PdfTotalsRowDefinition? totalsRow,
        bool includeGrandTotal,
        PdfSummaryDefinition<TItem>? summary,
        IReadOnlyList<PdfSummaryLine> summaryLines)
    {
        Title = title;
        Orientation = orientation;
        Groups = groups;
        Columns = columns;
        RowStyles = rowStyles;
        Warnings = warnings;
        TotalsRow = totalsRow;
        IncludeGrandTotal = includeGrandTotal;
        Summary = summary;
        SummaryLines = summaryLines;
    }

    public string Title { get; }
    public ReportOrientation Orientation { get; }
    public IReadOnlyList<PdfReportGroup<TGroup, TItem>> Groups { get; }
    public IReadOnlyList<PdfColumnDefinition<TItem>> Columns { get; }
    public IReadOnlyList<ConditionalStyle<TItem>> RowStyles { get; }
    public IReadOnlyList<string> Warnings { get; }
    public PdfTotalsRowDefinition? TotalsRow { get; }
    public bool IncludeGrandTotal { get; }
    public PdfSummaryDefinition<TItem>? Summary { get; }
    public IReadOnlyList<PdfSummaryLine> SummaryLines { get; }

    internal GroupedPdfReportDefinition<TGroup, TItem> WithSummaryLines(
        IReadOnlyList<PdfSummaryLine> summaryLines)
    {
        return new GroupedPdfReportDefinition<TGroup, TItem>(
            Title, Orientation, Groups, Columns, RowStyles, Warnings, TotalsRow,
            IncludeGrandTotal, Summary, summaryLines);
    }
}