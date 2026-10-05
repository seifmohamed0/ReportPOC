using ReportPoC.Application.Reporting;
using ReportPoC.Infrastructure.Reporting;
using ReportPoC.WebApi.Endpoints;
using ReportPoC.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddScoped<ISampleSalesReportService, SampleSalesReportService>();
builder.Services.AddScoped<IPdfReportService, PdfReportService>();
builder.Services.AddSingleton<ICurrencySpellOutService, DemoCurrencySpellOutService>();
builder.Services.AddReportInfrastructure();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapReportEndpoints();

app.Run();
