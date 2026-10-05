using System.Linq.Expressions;

namespace ReportPoC.Application.Reporting;

public enum ColumnWidthKind
{
    Auto,
    Relative,
    Fixed
}

public readonly record struct ColumnWidth
{
    private ColumnWidth(ColumnWidthKind kind, float value)
    {
        Kind = kind;
        Value = value;
    }

    public ColumnWidthKind Kind { get; }
    public float Value { get; }

    public static ColumnWidth Auto => new(ColumnWidthKind.Auto, 1);

    public static ColumnWidth Fixed(float points)
    {
        if (!float.IsFinite(points) || points <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "Fixed column width must be a positive finite number of points.");
        }

        return new ColumnWidth(ColumnWidthKind.Fixed, points);
    }

    public static ColumnWidth Relative(float weight)
    {
        if (!float.IsFinite(weight) || weight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "Relative column width must be a positive finite weight.");
        }

        return new ColumnWidth(ColumnWidthKind.Relative, weight);
    }
}

public enum CellAlignment
{
    Auto,
    Left,
    Center,
    Right
}

public enum AggregateKind
{
    None,
    Sum
}

public readonly record struct ReportColor
{
    private ReportColor(string hex)
    {
        Hex = hex;
    }

    public string Hex { get; }

    public static ReportColor FromHex(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        if (hex.Length != 7 || hex[0] != '#' || !hex.AsSpan(1).ToString().All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Color must use the #RRGGBB format.", nameof(hex));
        }

        return new ReportColor(hex.ToUpperInvariant());
    }
}

public static class ReportPalette
{
    public static ReportColor Danger { get; } = ReportColor.FromHex("#F8D7DA");
    public static ReportColor Draft { get; } = ReportColor.FromHex("#FFF3CD");
    public static ReportColor TotalRow { get; } = ReportColor.FromHex("#E2E3E5");
    public static ReportColor ZebraEven { get; } = ReportColor.FromHex("#F0EDED");
    public static ReportColor ZebraOdd { get; } = ReportColor.FromHex("#FFFFFF");
}

public sealed class ConditionalStyle<TRow>
    where TRow : class
{
    internal ConditionalStyle(ReportColor color, Func<TRow, bool> matches)
    {
        Color = color;
        Matches = matches;
    }

    public ReportColor Color { get; }
    public Func<TRow, bool> Matches { get; }
}

public sealed class PdfTotalsRowDefinition
{
    public PdfTotalsRowDefinition(string label, string? labelColumnName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        Label = label;
        LabelColumnName = labelColumnName;
    }

    public string Label { get; }
    public string? LabelColumnName { get; }
}

public sealed class PdfTotalsRowBuilder<TRow>
    where TRow : class
{
    private string _label = "Totals";

    internal string? LabelColumnName { get; private set; }

    public PdfTotalsRowBuilder<TRow> Label(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        _label = label;
        return this;
    }

    public PdfTotalsRowBuilder<TRow> In<TValue>(Expression<Func<TRow, TValue>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        LabelColumnName = GetMemberName(selector);
        return this;
    }

    internal PdfTotalsRowDefinition Build()
    {
        return new PdfTotalsRowDefinition(_label, LabelColumnName);
    }

    private static string GetMemberName<TValue>(Expression<Func<TRow, TValue>> selector)
    {
        Expression body = selector.Body;
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } conversion)
        {
            body = conversion.Operand;
        }

        if (body is not MemberExpression { Expression: ParameterExpression } member)
        {
            throw new ArgumentException("A totals label must select a direct row property or field.", nameof(selector));
        }

        return member.Member.Name;
    }
}

public sealed class PdfSummaryDefinition<TRow>
    where TRow : class
{
    internal PdfSummaryDefinition(string totalColumnName, Func<TRow, string?> getCurrencyCode)
    {
        TotalColumnName = totalColumnName;
        GetCurrencyCode = getCurrencyCode;
    }

    public string TotalColumnName { get; }
    public Func<TRow, string?> GetCurrencyCode { get; }
}

public sealed record PdfSummaryLine(string CurrencyCode, decimal Amount, string SpelledOutText);

public sealed class PdfSummaryBuilder<TRow>
    where TRow : class
{
    private string? _totalColumnName;
    private Func<TRow, string?>? _getCurrencyCode;

    public PdfSummaryBuilder<TRow> Total<TValue>(Expression<Func<TRow, TValue>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var type = Nullable.GetUnderlyingType(typeof(TValue)) ?? typeof(TValue);
        if (type != typeof(decimal) && type != typeof(double) && type != typeof(float) &&
            type != typeof(int) && type != typeof(long) && type != typeof(short))
        {
            throw new InvalidOperationException($"Summary total type '{typeof(TValue).Name}' is not supported.");
        }

        _totalColumnName = PdfExpression.GetMemberName(selector);
        return this;
    }

    public PdfSummaryBuilder<TRow> SpellOut(Expression<Func<TRow, string?>> currencyCode)
    {
        ArgumentNullException.ThrowIfNull(currencyCode);
        _getCurrencyCode = currencyCode.Compile();
        return this;
    }

    internal PdfSummaryDefinition<TRow> Build()
    {
        if (_totalColumnName is null || _getCurrencyCode is null)
        {
            throw new InvalidOperationException("Summary requires both a total column and a currency selector for spell-out.");
        }

        return new PdfSummaryDefinition<TRow>(_totalColumnName, _getCurrencyCode);
    }
}

internal static class PdfExpression
{
    public static string GetMemberName<TRow, TValue>(Expression<Func<TRow, TValue>> selector)
    {
        Expression body = selector.Body;
        while (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } conversion)
        {
            body = conversion.Operand;
        }

        if (body is not MemberExpression { Expression: ParameterExpression } member)
        {
            throw new ArgumentException("Expression must select a direct row property or field.", nameof(selector));
        }

        return member.Member.Name;
    }
}