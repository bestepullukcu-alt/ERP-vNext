using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.Countries.Commands;

public sealed record CreateCountryCommand(
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
) : IRequest<Country>;