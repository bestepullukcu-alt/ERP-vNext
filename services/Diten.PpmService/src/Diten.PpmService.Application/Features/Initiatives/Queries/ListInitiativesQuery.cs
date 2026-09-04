using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record ListInitiativesQuery : IRequest<Response<IReadOnlyList<InitiativeV2Dto>>>;
