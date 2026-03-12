using MediatR;

namespace Diten.MdmService.Application.Features.Countries.Commands;

public sealed record CreateCountryCommand(
    string Name,
    string Iso2Code,
    string Iso3Code,
    string? PhoneCode
) : IRequest<CreateCountryResult>;

public sealed record CreateCountryResult(
    Guid Id,
    string Name,
    string Iso2Code,
    string Iso3Code,
    Guid TenantId,
    DateTimeOffset CreatedAt
);

