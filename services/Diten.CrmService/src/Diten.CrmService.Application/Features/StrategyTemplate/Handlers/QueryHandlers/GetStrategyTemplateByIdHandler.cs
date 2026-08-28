using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
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

    public GetStrategyTemplateByIdHandler(ITenantContext tenant, IStrategyTemplateRepository templates)
    {
        _tenant = tenant;
        _templates = templates;
    }

    public async Task<Response<StrategyTemplateDetailDto>> Handle(
        GetStrategyTemplateByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<StrategyTemplateDetailDto>.Fail("Tenant context is required.", 400);
        }

        var template = await _templates.GetByIdAsync(tenantId, request.TemplateId, cancellationToken);
        return template is null
            ? Response<StrategyTemplateDetailDto>.Fail("Strategy template not found.", 404)
            : Response<StrategyTemplateDetailDto>.Success(StrategyTemplateMapper.ToDetail(template));
    }
}
