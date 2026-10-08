using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.PlannedVisit.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.PlannedVisit.Handlers.QueryHandlers;

/// <summary>Loads one plan's detail. A cross-tenant id resolves to nothing and returns 404 (no authorization leak).</summary>
public sealed class GetPlannedVisitByIdHandler
    : IRequestHandler<GetPlannedVisitByIdQuery, Response<PlannedVisitDetailDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IPlannedVisitRepository _repository;
    private readonly ICallerScope _caller;
    private readonly VisitTargetNameReader _names;
    private readonly IProductNameReader? _productNames;

    public GetPlannedVisitByIdHandler(
        ITenantContext tenant, IPlannedVisitRepository repository, ICallerScope caller, VisitTargetNameReader names,
        // WP-VP-4G (F4-4) — names for items an older plan stored without (one bulk MDM read, fail-open).
        IProductNameReader? productNames = null)
    {
        _productNames = productNames;
        _tenant = tenant;
        _repository = repository;
        _caller = caller;
        _names = names;
    }

    public async Task<Response<PlannedVisitDetailDto>> Handle(
        GetPlannedVisitByIdQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<PlannedVisitDetailDto>.Fail("Tenant context is required.", 400);
        }

        var plan = await _repository.GetByIdAsync(tenantId, request.PlannedVisitId, cancellationToken);
        // WP-VP-2 (B-1) — another rep's plan is as absent as a missing one (404, nothing leaks).
        if (plan is null || !_caller.MayAccess(PlannedVisitPermissions.ReadAll, plan.Resource.ResourceId))
        {
            return Response<PlannedVisitDetailDto>.Fail("Planned visit not found.", 404);
        }

        var names = await _names.ReadAsync(
            tenantId,
            new[] { plan.AccountId, plan.TargetType != PlannedVisitTargetType.Contact ? plan.TargetId : (Guid?)null },
            new[] { plan.ContactId },
            cancellationToken);
        var dto = PlannedVisitMapper.ToDetail(plan, names);
        var unnamed = PlannedVisitMapper.UnnamedProductIds(new[] { plan }).Distinct().ToList();
        if (_productNames is not null && unnamed.Count > 0)
        {
            dto = dto with
            {
                ContentItems = PlannedVisitMapper.WithProductNames(
                    dto.ContentItems, await _productNames.ReadNamesAsync(unnamed, cancellationToken))
            };
        }

        return Response<PlannedVisitDetailDto>.Success(dto);
    }
}
