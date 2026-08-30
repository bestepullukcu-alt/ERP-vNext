using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record ListInitiativesQuery : IRequest<Response<IReadOnlyList<InitiativeDto>>>;
