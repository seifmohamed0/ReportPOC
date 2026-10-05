using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ReportPoC.Application.Reporting;

namespace ReportPoC.Infrastructure.Reporting;

public sealed class QuestPdfReportRenderer : IPdfReportRenderer
{
    public Task<byte[]> RenderAsync<TRow>(
        PdfReportDefinition<TRow> report,
        CancellationToken cancellationToken = default)
        where TRow : class
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();

        var document = Document.Create(container => container.Page(page =>
        {
            page.Size(report.Orientation == ReportOrientation.Landscape
                ? PageSizes.A4.Landscape()
                : PageSizes.A4);
            page.Margin(32);
            page.Header()
                .PaddingBottom(12)
                .Text(report.Title)
                .SemiBold()
                .FontSize(18);

            page.Content().Column(content =>
            {
                content.Item().Table(table => ComposeTable(
                    table,
                    report.Columns,
                    report.Rows,
                    report.RowStyles,
                    report.TotalsRow));

                foreach (var summaryLine in report.SummaryLines)
                {
                    content.Item()
                        .PaddingTop(8)
                        .Text(summaryLine.SpelledOutText)
                        .SemiBold();
                }
            });
        }));

        return Task.FromResult(document.GeneratePdf());
    }

    public Task<byte[]> RenderAsync<TGroup, TItem>(
        GroupedPdfReportDefinition<TGroup, TItem> report,
        CancellationToken cancellationToken = default)
        where TGroup : class
        where TItem : class
    {
        ArgumentNullException.ThrowIfNull(report);
        cancellationToken.ThrowIfCancellationRequested();

        var document = Document.Create(container => container.Page(page =>
        {
            page.Size(report.Orientation == ReportOrientation.Landscape
                ? PageSizes.A4.Landscape()
                : PageSizes.A4);
            page.Margin(32);
            page.Header()
                .PaddingBottom(12)
                .Text(report.Title)
                .SemiBold()
                .FontSize(18);

            page.Content().Column(content =>
            {
                content.Spacing(14);
                foreach (var group in report.Groups)
                {
                    content.Item()
                        .Text(group.Header)
                        .SemiBold()
                        .FontSize(13);
                    content.Item().Table(table => ComposeTable(
                        table,
                        report.Columns,
                        group.Items,
                        report.RowStyles,
                        report.TotalsRow));
                }

                if (report.IncludeGrandTotal && report.Columns.Any(column => column.Aggregate == AggregateKind.Sum))
                {
                    var allItems = report.Groups.SelectMany(group => group.Items).ToArray();
                    var grandTotals = report.TotalsRow ?? new PdfTotalsRowDefinition("Grand Total");
                    content.Item()
                        .PaddingTop(8)
                        .Text("Grand total")
                        .SemiBold()
                        .FontSize(13);
                    content.Item().Table(table => ComposeTotalsTable(
                        table,
                        report.Columns,
                        allItems,
                        grandTotals));
                }

                foreach (var summaryLine in report.SummaryLines)
                {
                    content.Item()
                        .PaddingTop(8)
                        .Text(summaryLine.SpelledOutText)
                        .SemiBold();
                }
            });
        }));

        return Task.FromResult(document.GeneratePdf());
    }

    private static void ComposeTable<TRow>(
        TableDescriptor table,
        IReadOnlyList<PdfColumnDefinition<TRow>> columns,
        IEnumerable<TRow> rows,
        IReadOnlyList<ConditionalStyle<TRow>> rowStyles,
        PdfTotalsRowDefinition? totalsRow)
        where TRow : class
    {
        var rowList = rows as IReadOnlyList<TRow> ?? rows.ToArray();
        table.ColumnsDefinition(definitions =>
        {
            foreach (var column in columns)
            {
                switch (column.Width.Kind)
                {
                    case ColumnWidthKind.Fixed:
                        definitions.ConstantColumn(column.Width.Value);
                        break;
                    case ColumnWidthKind.Relative:
                        definitions.RelativeColumn(column.Width.Value);
                        break;
                    default:
                        definitions.RelativeColumn();
                        break;
                }
            }
        });

        table.Header(header =>
        {
            foreach (var column in columns)
            {
                header.Cell()
                    .Element(HeaderCell)
                    .Text(column.Header);
            }
        });

        var rowIndex = 0;
        foreach (var row in rowList)
        {
            foreach (var column in columns)
            {
                var background = ResolveBackground(column, row, rowStyles, rowIndex);
                var cell = table.Cell().Element(container => BodyCell(container, background, column.Alignment));
                var text = FormatValue(column.GetValue(row), column.Format);
                var url = column.GetLink?.Invoke(row);

                if (string.IsNullOrWhiteSpace(url))
                {
                    cell.Text(text);
                }
                else
                {
                    cell.Hyperlink(url)
                        .Text(text)
                        .FontColor(Colors.Blue.Medium)
                        .Underline();
                }
            }

            rowIndex++;
        }

        if (totalsRow is not null && columns.Any(column => column.Aggregate == AggregateKind.Sum))
        {
            ComposeTotalsCells(table, columns, rowList, totalsRow);
        }
    }

    private static void ComposeTotalsTable<TRow>(
        TableDescriptor table,
        IReadOnlyList<PdfColumnDefinition<TRow>> columns,
        IReadOnlyList<TRow> rows,
        PdfTotalsRowDefinition totalsRow)
        where TRow : class
    {
        table.ColumnsDefinition(definitions =>
        {
            foreach (var column in columns)
            {
                switch (column.Width.Kind)
                {
                    case ColumnWidthKind.Fixed:
                        definitions.ConstantColumn(column.Width.Value);
                        break;
                    case ColumnWidthKind.Relative:
                        definitions.RelativeColumn(column.Width.Value);
                        break;
                    default:
                        definitions.RelativeColumn();
                        break;
                }
            }
        });

        ComposeTotalsCells(table, columns, rows, totalsRow);
    }

    private static void ComposeTotalsCells<TRow>(
        TableDescriptor table,
        IReadOnlyList<PdfColumnDefinition<TRow>> columns,
        IReadOnlyList<TRow> rows,
        PdfTotalsRowDefinition totalsRow)
        where TRow : class
    {
        var labelColumnName = totalsRow.LabelColumnName;
        if (labelColumnName is not null && columns.Any(column =>
                column.Name == labelColumnName && column.Aggregate == AggregateKind.Sum))
        {
            labelColumnName = null;
        }

        labelColumnName ??= columns.FirstOrDefault(column => column.Aggregate != AggregateKind.Sum)?.Name;
        foreach (var column in columns)
        {
            var cell = table.Cell().Element(TotalCell);
            if (column.Aggregate == AggregateKind.Sum)
            {
                var total = rows.Sum(row => column.GetNumeric?.Invoke(row) ?? 0m);
                cell.Text(FormatValue(total, column.Format));
            }
            else if (column.Name == labelColumnName)
            {
                cell.Text(totalsRow.Label);
            }
            else
            {
                cell.Text(string.Empty);
            }
        }
    }

    private static ReportColor ResolveBackground<TRow>(
        PdfColumnDefinition<TRow> column,
        TRow row,
        IReadOnlyList<ConditionalStyle<TRow>> rowStyles,
        int rowIndex)
        where TRow : class
    {
        foreach (var style in column.CellStyles)
        {
            if (style.Matches(row))
            {
                return style.Color;
            }
        }

        foreach (var style in rowStyles)
        {
            if (style.Matches(row))
            {
                return style.Color;
            }
        }

        return rowIndex % 2 == 0 ? ReportPalette.ZebraEven : ReportPalette.ZebraOdd;
    }

    private static IContainer HeaderCell(IContainer container)
    {
        return container
            .Background(Colors.Grey.Lighten3)
            .BorderBottom(1)
            .Padding(6)
            .DefaultTextStyle(style => style.SemiBold());
    }

    private static IContainer BodyCell(
        IContainer container,
        ReportColor background,
        CellAlignment alignment)
    {
        container = container
            .Background(Color.FromHex(background.Hex))
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(6);

        return alignment switch
        {
            CellAlignment.Left => container.AlignLeft(),
            CellAlignment.Center => container.AlignCenter(),
            CellAlignment.Right => container.AlignRight(),
            _ => container
        };
    }

    private static IContainer TotalCell(IContainer container)
    {
        return container
            .Background(Color.FromHex(ReportPalette.TotalRow.Hex))
            .BorderTop(1)
            .Padding(6)
            .DefaultTextStyle(style => style.SemiBold());
    }

    private static string FormatValue(object? value, string? format)
    {
        return value switch
        {
            null => string.Empty,
            IFormattable formattable => formattable.ToString(format, CultureInfo.CurrentCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }
}

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddReportInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        QuestPDF.Settings.License = LicenseType.Community;
        services.AddSingleton<IPdfReportRenderer, QuestPdfReportRenderer>();
        return services;
    }
}