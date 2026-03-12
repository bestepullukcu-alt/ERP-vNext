namespace Diten.MdmService.Application.Features.Countries.Requests;

public sealed record UpdateCountryRequest(
    string Name,
    string Iso2Code,
    string Iso3Code,
    string? PhoneCode,
    bool IsActive
);

