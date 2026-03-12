using MediatR;

namespace Diten.MdmService.Application.Features.Countries.Commands;

public sealed record SeedCountriesCommand() : IRequest<SeedCountriesResult>;

public sealed record SeedCountriesResult(
    int InsertedCount,
    long UpdatedCount,
    int Total
);

