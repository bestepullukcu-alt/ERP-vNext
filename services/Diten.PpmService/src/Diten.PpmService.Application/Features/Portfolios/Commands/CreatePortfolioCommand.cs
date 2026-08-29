using Diten.PpmService.Application.Common;
using Diten.PpmService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.PpmService.Application.Features.Portfolios;

public sealed record CreatePortfolioCommand(string Code, string Name, string? Description, string? VisibilityPolicyKey) : IRequest<Response<PortfolioDto>>;
