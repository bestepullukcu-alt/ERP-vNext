using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.StrategyTemplate.Contract;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Queries;

/// <summary>What this FU is, and — just as importantly — what it deliberately is not.</summary>
public sealed record GetStrategyTemplateContractQuery : IRequest<Response<StrategyTemplateContractDto>>;
