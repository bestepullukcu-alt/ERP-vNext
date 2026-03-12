using MediatR;

namespace Diten.MdmService.Application.Features.Countries.Commands;

public sealed record UpdateCountryCommand(
    Guid Id,
    string Name,
    string Iso2Code,
    string Iso3Code,
    string? PhoneCode,
    bool IsActive
) : IRequest<bool>;

