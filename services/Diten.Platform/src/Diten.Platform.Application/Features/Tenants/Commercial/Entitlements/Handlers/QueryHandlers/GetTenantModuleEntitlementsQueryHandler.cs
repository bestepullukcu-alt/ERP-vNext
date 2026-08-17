using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Queries;
using Diten.Platform.Common.Catalog;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.QueryHandlers;

public sealed class GetTenantModuleEntitlementsQueryHandler
    : IRequestHandler<GetTenantModuleEntitlementsQuery, Response<IReadOnlyList<TenantModuleEntitlementRowDto>>>
{
    private readonly ITenantModuleEntitlementRepository _entitlementRepository;
    private readonly IPlatformCatalogContract _catalogContract;
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionPlanRepository _planRepository;

    public GetTenantModuleEntitlementsQueryHandler(
        ITenantModuleEntitlementRepository entitlementRepository,
        IPlatformCatalogContract catalogContract,
        ITenantSubscriptionRepository subscriptionRepository,
        ISubscriptionPlanRepository planRepository)
    {
        _entitlementRepository = entitlementRepository;
        _catalogContract = catalogContract;
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
    }

    public async Task<Response<IReadOnlyList<TenantModuleEntitlementRowDto>>> Handle(GetTenantModuleEntitlementsQuery request, CancellationToken ct)
    {
        var physicalRows = await _entitlementRepository.GetByTenantIdAsync(request.TenantId, ct);
        var modules = await _catalogContract.GetAssignableModulesAsync(ct);
        var moduleMap = modules.ToDictionary(x => x.ModuleCode, StringComparer.OrdinalIgnoreCase);
        var planModuleCodes = await GetPlanModuleCodesAsync(request.TenantId, ct);
        var allCodes = planModuleCodes
            .Concat(physicalRows.Select(x => x.ModuleCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var rows = new List<TenantModuleEntitlementRowDto>();
        var now = DateTimeOffset.UtcNow;
        foreach (var code in allCodes)
        {
            moduleMap.TryGetValue(code, out var module);
            var moduleName = module?.DisplayName ?? module?.ModuleName ?? code;
            var moduleRows = physicalRows.Where(x => string.Equals(x.ModuleCode, code, StringComparison.OrdinalIgnoreCase)).ToList();
            var access = TenantModuleEntitlementAccessEvaluator.Evaluate(
                request.TenantId,
                code,
                moduleName,
                planModuleCodes.Contains(code, StringComparer.OrdinalIgnoreCase),
                module?.IsCoreModule == true,
                moduleRows,
                now);
            var hasManualOverride = moduleRows.Any(x => x.Source == EntitlementSource.ManualOverride);

            if (planModuleCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new TenantModuleEntitlementRowDto(
                    request.TenantId,
                    code,
                    moduleName,
                    "Plan",
                    null,
                    access.HasAccess && access.Source == "Plan",
                    null,
                    access.EffectiveAccess.ToString(),
                    access.Source == "Plan" ? null : access.Reason,
                    true,
                    hasManualOverride,
                    null,
                    null));
            }

            rows.AddRange(moduleRows.Select(row => new TenantModuleEntitlementRowDto(
                request.TenantId,
                row.ModuleCode,
                moduleName,
                row.Source.ToString(),
                row.Id,
                row.IsEnabled && !TenantModuleEntitlementAccessEvaluator.IsExpired(row, now),
                row.ExpiryDateUtc,
                access.EffectiveAccess.ToString(),
                row.Reason,
                false,
                hasManualOverride,
                row.UpdatedAt ?? row.CreatedAt,
                row.RowVersion)));
        }

        return Response<IReadOnlyList<TenantModuleEntitlementRowDto>>.Success(rows);
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
