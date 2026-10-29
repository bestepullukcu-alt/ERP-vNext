using System.Reflection;
using System.Text.RegularExpressions;
using Diten.Platform.Application.Features.Lookups;
using Diten.Platform.Application.Features.Lookups.Services;
using Xunit;

namespace Diten.Platform.Application.Tests.Lookups;

/// <summary>
/// MOD-0048-FU01 §21 (TRM-17, XMC-001 B1) — the currencies lookup (<c>GET /api/lookups/reference/currencies</c>) returns
/// ISO 4217 alpha-3 codes only. ICU gives the macro regions (001 World, 150 Europe, 419 Latin America) the currency
/// placeholder "¤¤"; it is no currency and must not reach a picker or the Treasury precision check. Reads the production
/// builder <c>PlatformLookupProvider.BuildCurrencyOptions</c> — the one <c>GetCurrenciesAsync</c> caches.
/// </summary>
public sealed class CurrencyLookupIsoAlpha3Tests
{
    private static readonly Regex IsoAlpha3 = new("^[A-Z]{3}$", RegexOptions.CultureInvariant);

    private static IReadOnlyList<LookupOptionDto> Currencies()
    {
        var build = typeof(PlatformLookupProvider).GetMethod("BuildCurrencyOptions", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(PlatformLookupProvider), "BuildCurrencyOptions");
        return (IReadOnlyList<LookupOptionDto>)build.Invoke(null, null)!;
    }

    [Fact]
    public void Every_currency_code_is_iso_alpha3_and_no_icu_placeholder_is_returned()
    {
        var options = Currencies();

        Assert.NotEmpty(options);
        Assert.All(options, option =>
        {
            Assert.DoesNotContain("¤", option.Code, StringComparison.Ordinal);
            Assert.DoesNotContain("¤", option.Value, StringComparison.Ordinal);
            Assert.Matches(IsoAlpha3, option.Code);
            Assert.Matches(IsoAlpha3, option.Value);
        });
    }
}
