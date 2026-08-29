using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record GetInitiativeByIdQuery(Guid Id) : IRequest<Response<InitiativeDto>>;
