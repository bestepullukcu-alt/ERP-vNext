using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Queries;

/// <summary>Template detail including all four binding lists. Cross-tenant ids answer 404, never a partial row.</summary>
public sealed record GetStrategyTemplateByIdQuery(Guid TemplateId)
    : IRequest<Response<StrategyTemplateDetailDto>>;
