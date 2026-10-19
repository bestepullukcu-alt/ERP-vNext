using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;
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
    private readonly IModuleCatalogRepository _moduleRepository;

    public GetTenantModuleEntitlementsQueryHandler(
        ITenantModuleEntitlementRepository entitlementRepository,
        IPlatformCatalogContract catalogContract,
        ITenantSubscriptionRepository subscriptionRepository,
        ISubscriptionPlanRepository planRepository,
        IModuleCatalogRepository moduleRepository)
    {
        _moduleRepository = moduleRepository;
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
        var planModuleCodes = await TenantModuleEntitlementActionGate.PlanModuleCodesAsync(_subscriptionRepository, _planRepository, request.TenantId, ct);
        var allCodes = planModuleCodes
            .Concat(physicalRows.Select(x => x.ModuleCode))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
        // BL-500 — the catalogue records the command handlers refuse on, read ONCE for the whole list. This query also
        // serves the internal S2S authorization and permission-sync reads; it used to make one catalogue round trip
        // per module.
        var catalogue = await _moduleRepository.GetByCodesAsync(allCodes, ct);

        var rows = new List<TenantModuleEntitlementRowDto>();
        var now = DateTimeOffset.UtcNow;
        foreach (var code in allCodes)
        {
            moduleMap.TryGetValue(code, out var module);
            catalogue.TryGetValue(code, out var catalogItem);
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
                var planFacts = TenantModuleEntitlementRowFacts.Projection(access, catalogItem);
                rows.Add(new TenantModuleEntitlementRowDto(
                    request.TenantId,
                    code,
                    moduleName,
                    "Plan",
                    null,
                    planFacts.GrantsAccess,
                    null,
                    access.EffectiveAccess.ToString(),
                    access.Source == "Plan" ? null : access.Reason,
                    true,
                    hasManualOverride,
                    null,
                    null,
                    TenantModuleEntitlementRowActions.For(planFacts)));
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
                row.RowVersion,
                TenantModuleEntitlementRowActions.For(TenantModuleEntitlementRowFacts.Stored(row, catalogItem, now)))));
        }

        return Response<IReadOnlyList<TenantModuleEntitlementRowDto>>.Success(rows);
    }
}
