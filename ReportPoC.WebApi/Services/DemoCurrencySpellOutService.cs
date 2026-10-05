using System.Globalization;
using ReportPoC.Application.Reporting;

namespace ReportPoC.WebApi.Services;

public sealed class DemoCurrencySpellOutService : ICurrencySpellOutService
{
    public Task<string> SpellOutAsync(
        decimal amount,
        string currencyCode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var demoText = $"DEMO ONLY: {amount.ToString("N2", CultureInfo.InvariantCulture)} {currencyCode} in words.";
        return Task.FromResult(demoText);
    }
}