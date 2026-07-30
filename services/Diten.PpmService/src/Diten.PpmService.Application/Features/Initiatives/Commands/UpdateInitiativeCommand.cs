using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record UpdateInitiativeCommand(Guid Id, string Code, string Name, string? Description, Guid? PortfolioId, string? VisibilityPolicyKey, int ExpectedVersion) : IRequest<Response<InitiativeDto>>;
