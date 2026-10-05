using System.Linq.Expressions;
using System.Reflection;
using System.Globalization;

namespace ReportPoC.Application.Reporting;

public static class PdfReport
{
    public static PdfReportBuilder<TRow> For<TRow>(IEnumerable<TRow> rows)
        where TRow : class
    {
        ArgumentNullException.ThrowIfNull(rows);
        return new PdfReportBuilder<TRow>(rows.ToArray());
    }

    public static GroupedPdfReportBuilder<TGroup, TItem> ForGroups<TGroup, TItem>(
        IEnumerable<PdfReportGroup<TGroup, TItem>> groups)
        where TGroup : class
        where TItem : class
    {
        ArgumentNullException.ThrowIfNull(groups);
        return new GroupedPdfReportBuilder<TGroup, TItem>(groups.Select(group => group.Snapshot()).ToArray());
    }
}

public enum ReportOrientation
{
    Portrait,
    Landscape
}

public sealed class PdfReportBuilder<TRow>
    where TRow : class
{
    private readonly IReadOnlyList<TRow> _rows;
    private readonly List<PdfColumnDefinition<TRow>> _columns = [];
    private readonly List<ConditionalStyle<TRow>> _rowStyles = [];
    private readonly HashSet<string> _hiddenNames = new(StringComparer.Ordinal);
    private PdfTotalsRowDefinition? _totalsRow;
    private PdfSummaryDefinition<TRow>? _summary;
    private string _title = "";
    private ReportOrientation _orientation = ReportOrientation.Portrait;

    internal PdfReportBuilder(IReadOnlyList<TRow> rows)
    {
        _rows = rows;
    }

    public PdfReportBuilder<TRow> Title(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        _title = title;
        return this;
    }

    public PdfReportBuilder<TRow> Landscape(bool enabled = true)
    {
        _orientation = enabled ? ReportOrientation.Landscape : ReportOrientation.Portrait;
        return this;
    }

    public PdfReportBuilder<TRow> TotalsRow(Action<PdfTotalsRowBuilder<TRow>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new PdfTotalsRowBuilder<TRow>();
        configure(builder);
        _totalsRow = builder.Build();
        return this;
    }

    public PdfReportBuilder<TRow> Summary(Action<PdfSummaryBuilder<TRow>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new PdfSummaryBuilder<TRow>();
        configure(builder);
        _summary = builder.Build();
        return this;
    }

    public PdfReportBuilder<TRow> Hide(params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Hidden column names cannot be empty.", nameof(names));
            }

            _hiddenNames.Add(name);
        }

        return this;
    }

    public PdfReportBuilder<TRow> Hide<TValue>(Expression<Func<TRow, TValue>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        _hiddenNames.Add(GetSelectedMember(selector).Name);
        return this;
    }

    public PdfReportBuilder<TRow> HideWhen<TValue>(Expression<Func<TRow, TValue>> selector, bool shouldHide)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (shouldHide)
        {
            Hide(selector);
        }

        return this;
    }

    public PdfReportBuilder<TRow> HighlightRow(
        ReportColor color,
        Expression<Func<TRow, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _rowStyles.Add(new ConditionalStyle<TRow>(color, predicate.Compile()));
        return this;
    }

    public PdfReportBuilder<TRow> Column<TValue>(
        Expression<Func<TRow, TValue>> selector,
        Action<PdfColumnBuilder<TRow, TValue>>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(selector);

        var member = GetSelectedMember(selector);
        if (_columns.Any(column => column.Name == member.Name))
        {
            throw new ArgumentException($"Column '{member.Name}' has already been added.", nameof(selector));
        }

        var columnBuilder = new PdfColumnBuilder<TRow, TValue>(member.Name);
        configure?.Invoke(columnBuilder);

        var getter = selector.Compile();
        Func<TRow, decimal?>? numericGetter = columnBuilder.Aggregate == AggregateKind.Sum
            ? row => getter(row) is { } value ? Convert.ToDecimal(value, CultureInfo.InvariantCulture) : null
            : null;
        _columns.Add(new PdfColumnDefinition<TRow>(
            member.Name,
            columnBuilder.ResolvedHeader,
            columnBuilder.ConfiguredOrder,
            _columns.Count,
            row => getter(row),
            columnBuilder.ConfiguredWidth,
            columnBuilder.ConfiguredAlignment,
            columnBuilder.FormatString,
            columnBuilder.LinkResolver,
            Array.AsReadOnly(columnBuilder.CellStyles.ToArray()),
            columnBuilder.Aggregate,
            numericGetter));

        return this;
    }

    public PdfReportDefinition<TRow> Build()
    {
        if (_columns.Count == 0)
        {
            throw new InvalidOperationException("A report must contain at least one column.");
        }

        var unknownHiddenNames = _hiddenNames
            .Except(_columns.Select(column => column.Name), StringComparer.Ordinal)
            .ToArray();

        var orderedColumns = _columns
            .Where(column => !_hiddenNames.Contains(column.Name))
            .OrderBy(column => column.Order ?? int.MaxValue)
            .ThenBy(column => column.DiscoveryIndex)
            .ToArray();

        if (orderedColumns.Length == 0)
        {
            throw new InvalidOperationException("A report must contain at least one visible column.");
        }

        if (_summary is not null)
        {
            var summaryColumn = _columns.SingleOrDefault(column => column.Name == _summary.TotalColumnName);
            if (summaryColumn is null)
            {
                throw new InvalidOperationException($"Summary column '{_summary.TotalColumnName}' was not added to the report.");
            }

            if (summaryColumn.GetNumeric is null)
            {
                throw new InvalidOperationException($"Summary column '{_summary.TotalColumnName}' must configure Sum().");
            }
        }

        return new PdfReportDefinition<TRow>(
            _title,
            _orientation,
            Array.AsReadOnly(_rows.ToArray()),
            Array.AsReadOnly(orderedColumns),
            Array.AsReadOnly(_rowStyles.ToArray()),
            Array.AsReadOnly(unknownHiddenNames),
            _totalsRow,
            _summary,
            Array.Empty<PdfSummaryLine>());
    }

    private static MemberInfo GetSelectedMember<TValue>(Expression<Func<TRow, TValue>> selector)
    {
        Expression body = selector.Body;
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } conversion)
        {
            body = conversion.Operand;
        }

        if (body is not MemberExpression { Expression: ParameterExpression } member)
        {
            throw new ArgumentException("A report column must select a direct row property or field.", nameof(selector));
        }

        return member.Member;
    }
}

public sealed class PdfColumnBuilder<TRow, TValue>
    where TRow : class
{
    private readonly string _name;
    private string? _header;
    private readonly List<ConditionalStyle<TRow>> _cellStyles = [];

    internal PdfColumnBuilder(string name)
    {
        _name = name;
    }

    internal int? ConfiguredOrder { get; private set; }
    internal string ResolvedHeader => _header ?? _name;
    internal ColumnWidth ConfiguredWidth { get; private set; } = ColumnWidth.Auto;
    internal CellAlignment ConfiguredAlignment { get; private set; } = CellAlignment.Auto;
    internal string? FormatString { get; private set; }
    internal Func<TRow, string?>? LinkResolver { get; private set; }
    internal IReadOnlyList<ConditionalStyle<TRow>> CellStyles => _cellStyles;
    internal AggregateKind Aggregate { get; private set; }

    public PdfColumnBuilder<TRow, TValue> Header(string header)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(header);
        _header = header;
        return this;
    }

    public PdfColumnBuilder<TRow, TValue> Order(int order)
    {
        if (order < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(order), "Column order must be positive.");
        }

        ConfiguredOrder = order;
        return this;
    }

    public PdfColumnBuilder<TRow, TValue> Width(ColumnWidth width)
    {
        ConfiguredWidth = width;
        return this;
    }

    public PdfColumnBuilder<TRow, TValue> Format(string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        FormatString = format;
        return this;
    }

    public PdfColumnBuilder<TRow, TValue> AlignRight()
    {
        ConfiguredAlignment = CellAlignment.Right;
        return this;
    }

    public PdfColumnBuilder<TRow, TValue> Link(Expression<Func<TRow, string?>> resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        LinkResolver = resolver.Compile();
        return this;
    }

    public PdfColumnBuilder<TRow, TValue> Link(string? url)
    {
        LinkResolver = _ => url;
        return this;
    }

    public PdfColumnBuilder<TRow, TValue> Highlight(ReportColor color)
    {
        _cellStyles.Add(new ConditionalStyle<TRow>(color, _ => true));
        return this;
    }

    public PdfColumnBuilder<TRow, TValue> Highlight(
        ReportColor color,
        Expression<Func<TRow, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _cellStyles.Add(new ConditionalStyle<TRow>(color, predicate.Compile()));
        return this;
    }

    public PdfColumnBuilder<TRow, TValue> Sum()
    {
        var type = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
        if (type != typeof(decimal) && type != typeof(double) && type != typeof(float) &&
            type != typeof(int) && type != typeof(long) && type != typeof(short))
        {
            throw new InvalidOperationException($"Column '{_name}' has unsupported sum type '{typeof(TValue).Name}'.");
        }

        Aggregate = AggregateKind.Sum;
        return this;
    }
}

public sealed class PdfColumnDefinition<TRow>
    where TRow : class
{
    internal PdfColumnDefinition(
        string name,
        string header,
        int? order,
        int discoveryIndex,
        Func<TRow, object?> getValue,
        ColumnWidth width,
        CellAlignment alignment,
        string? format,
        Func<TRow, string?>? getLink,
        IReadOnlyList<ConditionalStyle<TRow>> cellStyles,
        AggregateKind aggregate,
        Func<TRow, decimal?>? getNumeric)
    {
        Name = name;
        Header = header;
        Order = order;
        DiscoveryIndex = discoveryIndex;
        GetValue = getValue;
        Width = width;
        Alignment = alignment;
        Format = format;
        GetLink = getLink;
        CellStyles = cellStyles;
        Aggregate = aggregate;
        GetNumeric = getNumeric;
    }

    public string Name { get; }
    public string Header { get; }
    public int? Order { get; }
    public int DiscoveryIndex { get; }
    public Func<TRow, object?> GetValue { get; }
    public ColumnWidth Width { get; }
    public CellAlignment Alignment { get; }
    public string? Format { get; }
    public Func<TRow, string?>? GetLink { get; }
    public IReadOnlyList<ConditionalStyle<TRow>> CellStyles { get; }
    public AggregateKind Aggregate { get; }
    public Func<TRow, decimal?>? GetNumeric { get; }
}

public sealed class PdfReportDefinition<TRow>
    where TRow : class
{
    internal PdfReportDefinition(
        string title,
        ReportOrientation orientation,
        IReadOnlyList<TRow> rows,
        IReadOnlyList<PdfColumnDefinition<TRow>> columns,
        IReadOnlyList<ConditionalStyle<TRow>> rowStyles,
        IReadOnlyList<string> warnings,
        PdfTotalsRowDefinition? totalsRow,
        PdfSummaryDefinition<TRow>? summary,
        IReadOnlyList<PdfSummaryLine> summaryLines)
    {
        Title = title;
        Orientation = orientation;
        Rows = rows;
        Columns = columns;
        RowStyles = rowStyles;
        Warnings = warnings;
        TotalsRow = totalsRow;
        Summary = summary;
        SummaryLines = summaryLines;
    }

    public string Title { get; }
    public ReportOrientation Orientation { get; }
    public IReadOnlyList<TRow> Rows { get; }
    public IReadOnlyList<PdfColumnDefinition<TRow>> Columns { get; }
    public IReadOnlyList<ConditionalStyle<TRow>> RowStyles { get; }
    public IReadOnlyList<string> Warnings { get; }
    public PdfTotalsRowDefinition? TotalsRow { get; }
    public PdfSummaryDefinition<TRow>? Summary { get; }
    public IReadOnlyList<PdfSummaryLine> SummaryLines { get; }

    internal PdfReportDefinition<TRow> WithSummaryLines(IReadOnlyList<PdfSummaryLine> summaryLines)
    {
        return new PdfReportDefinition<TRow>(
            Title, Orientation, Rows, Columns, RowStyles, Warnings, TotalsRow, Summary, summaryLines);
    }
}