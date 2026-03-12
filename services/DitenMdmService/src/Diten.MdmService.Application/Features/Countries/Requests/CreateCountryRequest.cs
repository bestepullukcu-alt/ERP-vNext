namespace Diten.MdmService.Application.Features.Countries.Requests;

public sealed record CreateCountryRequest(
    string Name,
    string Iso2Code,
    string Iso3Code,
    string? PhoneCode
);

