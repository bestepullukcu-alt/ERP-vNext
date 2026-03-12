using MediatR;

namespace Diten.MdmService.Application.Features.Countries.Commands;

public sealed record DeleteCountryCommand(Guid Id) : IRequest<Unit>;

