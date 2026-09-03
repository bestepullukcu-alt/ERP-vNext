using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record GetInitiativeDetailLinksQuery(Guid Id) : IRequest<Response<InitiativeDetailLinks>>;
