using MediatR;

namespace Diten.MdmService.Application.Features.Countries.Commands;

public sealed record BulkDeleteCountriesCommand(IReadOnlyList<Guid> Ids) : IRequest<int>;

