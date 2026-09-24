using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.StrategyTemplate.Queries;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Handlers.QueryHandlers;

/// <summary>WP-ST-DETAIL-1 — the version-history read for the Detay "Sürüm geçmişi" panel. It resolves the requested play
/// (a cross-tenant id answers 404, so another tenant's lineage is never observable), then lists EVERY version of its
/// lineage (archived included) newest-first via the EXISTING <see cref="IStrategyTemplateRepository.ListByLineageAsync"/>.
/// Read-only: no write, no new repository method, nothing persisted. IsCurrent flags the version the caller asked about.</summary>
public sealed class GetStrategyTemplateVersionsHandler
    : IRequestHandler<GetStrategyTemplateVersionsQuery, Response<StrategyTemplateVersionsDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IStrategyTemplateRepository _templates;

    public GetStrategyTemplateVersionsHandler(ITenantContext tenant, IStrategyTemplateRepository templates)
    {
        _tenant = tenant;
        _templates = templates;
    }

    public async Task<Response<StrategyTemplateVersionsDto>> Handle(
        GetStrategyTemplateVersionsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<StrategyTemplateVersionsDto>.Fail("Tenant context is required.", 400);
        }

        var template = await _templates.GetByIdAsync(tenantId, request.TemplateId, cancellationToken);
        if (template is null)
        {
            return Response<StrategyTemplateVersionsDto>.Fail("Strategy template not found.", 404);
        }

        var lineage = await _templates.ListByLineageAsync(tenantId, template.VersionLineageId, cancellationToken);
        var versions = lineage
            .OrderByDescending(v => v.TemplateVersion)
            .Select(v => new StrategyTemplateVersionDto(
                v.Id,
                v.TemplateVersion,
                v.TemplateStatus,
                v.ActivatedAt,
                v.ArchivedAt,
                v.CreatedAt,
                v.Id == request.TemplateId))
            .ToList();

        return Response<StrategyTemplateVersionsDto>.Success(new StrategyTemplateVersionsDto(versions));
    }
}
