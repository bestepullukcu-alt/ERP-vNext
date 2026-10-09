using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.StrategyTemplate.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Handlers.QueryHandlers;

/// <summary>Template detail with all four binding lists. A cross-tenant id answers 404 rather than an empty row, so the
/// existence of another tenant's template is never observable.</summary>
public sealed class GetStrategyTemplateByIdHandler
    : IRequestHandler<GetStrategyTemplateByIdQuery, Response<StrategyTemplateDetailDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IStrategyTemplateRepository _templates;
    private readonly StrategyTemplateLineJourneyReader? _journeys;

    /// <param name="journeys">WP-SB-3a — each product line's journey name / status / hints (read-only).</param>
    public GetStrategyTemplateByIdHandler(
        ITenantContext tenant, IStrategyTemplateRepository templates, StrategyTemplateLineJourneyReader? journeys = null)
    {
        _tenant = tenant;
        _templates = templates;
        _journeys = journeys;
    }

    public async Task<Response<StrategyTemplateDetailDto>> Handle(
        GetStrategyTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<StrategyTemplateDetailDto>.Fail("Tenant context is required.", 400);
        }

        var template = await _templates.GetByIdAsync(tenantId, request.TemplateId, cancellationToken);
        if (template is null)
        {
            return Response<StrategyTemplateDetailDto>.Fail("Strategy template not found.", 404);
        }

        var journeys = _journeys is null ? null : await _journeys.ReadAsync(tenantId, template, cancellationToken);
        var detail = StrategyTemplateMapper.ToDetail(template, journeys);

        // WP-E2E-FIX-3 (E5-B2) — "Yerini v{n} aldı": one read of the successor, only when there is one.
        if (template.SupersededByTemplateId is { } next
            && await _templates.GetByIdAsync(tenantId, next, cancellationToken) is { } successor)
        {
            detail = detail with { SupersededByTemplateVersion = successor.TemplateVersion };
        }

        return Response<StrategyTemplateDetailDto>.Success(detail);
    }
}
