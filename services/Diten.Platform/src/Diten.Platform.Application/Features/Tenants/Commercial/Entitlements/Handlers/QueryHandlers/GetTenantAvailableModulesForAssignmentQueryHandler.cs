using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using Diten.Platform.Common.Catalog;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.QueryHandlers;

/// <summary>
/// FIX-ENTITLEMENT-DUP (Fix 1) — the Add-Module picker must offer ONLY modules the tenant is not already
/// entitled to, otherwise an already-entitled module can be re-selected and a duplicate row written. We subtract
/// the tenant's effective entitled set — plan-derived (computed) module codes ∪ active manual entitlement rows
/// (IsEnabled &amp; !IsDeleted) — from the assignable catalog (case-insensitive on ModuleCode). Plan-derived
/// modules are excluded too: the tenant already has them via the plan.
/// </summary>
public sealed class GetTenantAvailableModulesForAssignmentQueryHandler
    : IRequestHandler<GetTenantAvailableModulesForAssignmentQuery, Response<IReadOnlyList<TenantAvailableModuleDto>>>
{
    private readonly IPlatformCatalogContract _catalogContract;
    private readonly ITenantModuleEntitlementRepository _entitlementRepository;
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionPlanRepository _planRepository;

    public GetTenantAvailableModulesForAssignmentQueryHandler(
        IPlatformCatalogContract catalogContract,
        ITenantModuleEntitlementRepository entitlementRepository,
        ITenantSubscriptionRepository subscriptionRepository,
        ISubscriptionPlanRepository planRepository)
    {
        _catalogContract = catalogContract;
        _entitlementRepository = entitlementRepository;
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
    }

    public async Task<Response<IReadOnlyList<TenantAvailableModuleDto>>> Handle(GetTenantAvailableModulesForAssignmentQuery request, CancellationToken ct)
    {
        var modules = await _catalogContract.GetAssignableModulesAsync(ct);

        // Already-entitled module codes the picker must hide.
        var entitledCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Active manual entitlement rows. GetByTenantIdAsync already filters soft-deleted (IsDeleted=false), so
        // here we only drop the disabled rows — a disabled/blocked module stays selectable (re-enable path).
        var physicalRows = await _entitlementRepository.GetByTenantIdAsync(request.TenantId, ct);
        foreach (var row in physicalRows)
        {
            if (row.IsEnabled && !string.IsNullOrWhiteSpace(row.ModuleCode))
            {
                entitledCodes.Add(row.ModuleCode);
            }
        }

        // Plan-derived (computed) module codes — already in the tenant's access via the subscription plan.
        foreach (var code in await GetPlanModuleCodesAsync(request.TenantId, ct))
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                entitledCodes.Add(code);
            }
        }

        var rows = modules
            .Where(x => !entitledCodes.Contains(x.ModuleCode))
            .Select(x => new TenantAvailableModuleDto(x.ModuleCode, x.ModuleName, x.DisplayName))
            .ToList();

        return Response<IReadOnlyList<TenantAvailableModuleDto>>.Success(rows);
    }

    private async Task<IReadOnlyList<string>> GetPlanModuleCodesAsync(Guid tenantId, CancellationToken ct)
    {
        var subscription = await _subscriptionRepository.GetCurrentByTenantIdAsync(tenantId, ct);
        if (subscription is null)
        {
            return [];
        }

        var plan = await _planRepository.GetByIdAsync(subscription.PlanId, ct);
        return plan?.IncludedModuleKeys ?? [];
    }
}
