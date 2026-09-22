using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Queries;

/// <summary>WP-ST-DETAIL-1 — every version sharing the requested play's lineage (archived included), newest first, for
/// the Detay "Sürüm geçmişi" panel. A cross-tenant id answers 404, never a partial lineage.</summary>
public sealed record GetStrategyTemplateVersionsQuery(Guid TemplateId)
    : IRequest<Response<StrategyTemplateVersionsDto>>;
