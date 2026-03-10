namespace Diten.MdmService.Application.Features.Countries.Requests;

public sealed record CreateCountryRequest(
    string Name,
    string? NativeName,
    string Iso2Code,
    string Iso3Code,
    string? NumericCode,
    string? PhoneCode,
    string? CurrencyCode,
    string? CurrencyName,
    string? CurrencySymbol,
    string? Region,
    string? SubRegion,
    string? Capital,
    string? FlagEmoji,
    double? Latitude,
    double? Longitude,
    bool IsActive = true
);