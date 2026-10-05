using Microsoft.AspNetCore.Http.HttpResults;
using ReportPoC.Application.Reporting;

namespace ReportPoC.WebApi.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/reports/sample", async Task<FileContentHttpResult> (
            ISampleSalesReportService reportService,
            CancellationToken cancellationToken) =>
        {
            var pdf = await reportService.GenerateAsync(cancellationToken);
            return TypedResults.File(pdf, "application/pdf", "sample-sales-report.pdf");
        })
            .WithName("DownloadSampleSalesReport")
            .WithSummary("Downloads a sample tabular sales report as a PDF.")
            .WithDescription("Builds a report through the Application fluent API and renders it through the configured PDF adapter.")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        endpoints.MapGet("/reports/flat", async Task<FileContentHttpResult> (
            ISampleSalesReportService reportService,
            CancellationToken cancellationToken) =>
        {
            var pdf = await reportService.GenerateFlatAsync(cancellationToken);
            return TypedResults.File(pdf, "application/pdf", "flat-sales-report.pdf");
        })
            .WithName("DownloadFlatSalesReport")
            .WithSummary("Downloads a flat sales report as a PDF.")
            .WithDescription("Demonstrates a portrait table with explicit column order, date/currency formatting, and totals.")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        endpoints.MapGet("/reports/grouped", async Task<FileContentHttpResult> (
            ISampleSalesReportService reportService,
            CancellationToken cancellationToken) =>
        {
            var pdf = await reportService.GenerateGroupedAsync(cancellationToken);
            return TypedResults.File(pdf, "application/pdf", "grouped-sales-report.pdf");
        })
            .WithName("DownloadGroupedSalesReport")
            .WithSummary("Downloads a grouped sales report as a PDF.")
            .WithDescription("Demonstrates landscape layout, region groups, per-group totals, and a grand total.")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf");

        return endpoints;
    }
}