using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;

public sealed class GetBusinessReferenceDataUsageFormOptionsQueryHandler
    : IRequestHandler<GetBusinessReferenceDataUsageFormOptionsQuery, Response<BusinessReferenceDataUsageFormOptionsModel>>
{
    private readonly IMediator _mediator;
    private readonly ITenantContext _tenantContext;
    private readonly IBusinessReferenceDataStewardshipRepository _repository;

    public GetBusinessReferenceDataUsageFormOptionsQueryHandler(
        IMediator mediator,
        ITenantContext tenantContext,
        IBusinessReferenceDataStewardshipRepository repository)
    {
        _mediator = mediator;
        _tenantContext = tenantContext;
        _repository = repository;
    }

    public async Task<Response<BusinessReferenceDataUsageFormOptionsModel>> Handle(
        GetBusinessReferenceDataUsageFormOptionsQuery request,
        CancellationToken ct)
    {
        // Consumer modules = the modules this tenant is effectively entitled to.
        // Use the commercial entitlement projection so plan/override rows match Tenant Management.
        var entitlementsResponse = await _mediator.Send(new GetTenantModuleEntitlementsQuery(_tenantContext.TenantId), ct);
        var consumerModules = (entitlementsResponse.Data ?? [])
            .Where(m => m.IsEnabled)
            .Select(m => new BusinessReferenceDataUsageFormOptionModel(
                m.ModuleCode,
                string.IsNullOrWhiteSpace(m.ModuleName) ? m.ModuleCode : m.ModuleName))
            .GroupBy(o => o.Value, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Scope types = the governed scope-type vocabulary (SSOT: BusinessReferenceDataScopeTypes).
        var scopeTypes = BusinessReferenceDataScopeTypes.Options
            .Select(o => new BusinessReferenceDataUsageFormOptionModel(o.Value, o.Name))
            .ToList();

        // Scope keys = the distinct scope keys the set actually has versions for, grouped by the set scope type.
        var scopeKeys = new List<string>();
        string? setScopeType = null;
        var scopeKeysByScopeType = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var set = await _repository.GetSetByCodeAsync(request.SetCode.Trim(), ct);
        if (set is not null)
        {
            setScopeType = set.ScopeType;
            var versions = await _repository.GetVersionsBySetIdAsync(set.BusinessReferenceDataSetId, ct);
            scopeKeys = versions
                .Where(v => !string.IsNullOrWhiteSpace(v.ScopeKey))
                .Select(v => v.ScopeKey!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            scopeKeysByScopeType[set.ScopeType] = scopeKeys;
        }

        return Response<BusinessReferenceDataUsageFormOptionsModel>.Success(
            new BusinessReferenceDataUsageFormOptionsModel(
                consumerModules,
                scopeTypes,
                scopeKeys,
                setScopeType,
                scopeKeysByScopeType));
    }
}
