using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Platform.API.Controllers;

[ApiController]
[Route("api/lookups")]
[AllowAnonymous]
public sealed class LookupsController : ControllerBase
{
    [HttpGet("countries")]
    public IActionResult GetCountries()
    {
        var countries = new[]
        {
            new { Code = "TR", Name = "Turkey" },
            new { Code = "US", Name = "United States" },
            new { Code = "GB", Name = "United Kingdom" },
            new { Code = "DE", Name = "Germany" },
            new { Code = "FR", Name = "France" },
            new { Code = "ES", Name = "Spain" },
            new { Code = "IT", Name = "Italy" },
            new { Code = "RU", Name = "Russia" },
            new { Code = "CN", Name = "China" },
            new { Code = "JP", Name = "Japan" },
            new { Code = "AE", Name = "United Arab Emirates" },
            new { Code = "SA", Name = "Saudi Arabia" },
            new { Code = "NL", Name = "Netherlands" },
            new { Code = "BE", Name = "Belgium" },
            new { Code = "CH", Name = "Switzerland" },
            new { Code = "AT", Name = "Austria" },
            new { Code = "PL", Name = "Poland" },
            new { Code = "SE", Name = "Sweden" },
            new { Code = "NO", Name = "Norway" },
            new { Code = "DK", Name = "Denmark" },
        }.OrderBy(x => x.Name);

        return Ok(countries);
    }

    [HttpGet("currencies")]
    public IActionResult GetCurrencies()
    {
        var currencies = new[]
        {
            new { Code = "TRY", Name = "Turkish Lira" },
            new { Code = "USD", Name = "US Dollar" },
            new { Code = "EUR", Name = "Euro" },
            new { Code = "GBP", Name = "British Pound" },
            new { Code = "CNY", Name = "Chinese Yuan" },
            new { Code = "JPY", Name = "Japanese Yen" },
            new { Code = "RUB", Name = "Russian Ruble" },
            new { Code = "AED", Name = "UAE Dirham" },
            new { Code = "SAR", Name = "Saudi Riyal" },
            new { Code = "CHF", Name = "Swiss Franc" },
            new { Code = "SEK", Name = "Swedish Krona" },
            new { Code = "NOK", Name = "Norwegian Krone" },
            new { Code = "DKK", Name = "Danish Krone" },
            new { Code = "PLN", Name = "Polish Zloty" },
        }.OrderBy(x => x.Name);

        return Ok(currencies);
    }

    [HttpGet("timezones")]
    public IActionResult GetTimezones()
    {
        var timezones = TimeZoneInfo.GetSystemTimeZones()
            .Select(tz => new { Id = tz.Id, Name = tz.DisplayName })
            .OrderBy(x => x.Name);

        return Ok(timezones);
    }
}
