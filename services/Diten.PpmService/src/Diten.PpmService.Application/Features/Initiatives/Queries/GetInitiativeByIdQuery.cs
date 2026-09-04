using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record GetInitiativeByIdQuery(Guid Id) : IRequest<Response<InitiativeV2Dto>>;
