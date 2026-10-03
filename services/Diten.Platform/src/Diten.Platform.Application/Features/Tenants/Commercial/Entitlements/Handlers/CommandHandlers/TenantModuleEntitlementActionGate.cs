using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Handlers.CommandHandlers;

/// <summary>
/// BL-500 — the door every Modules-tab command handler passes before it writes: it reads the facts the list query reads
/// (<see cref="TenantModuleEntitlementRowFacts"/>, from the same records) and asks the one rule
/// (<see cref="TenantModuleEntitlementRowActions.Refuse"/>). An action the list does not offer is refused here, with
/// the code the screen says it from.
/// </summary>
internal static class TenantModuleEntitlementActionGate
{
    /// <summary>Null when <paramref name="action"/> is offered on the stored <paramref name="row"/>; otherwise the refusal.</summary>
    public static async Task<Response<NoContent>?> RefuseStoredAsync(
        IModuleCatalogRepository modules,
        TenantModuleEntitlement row,
        string action,
        CancellationToken ct)
    {
        var module = await modules.GetByCodeAsync(row.ModuleCode, ct);
        var refusal = TenantModuleEntitlementRowActions.Refuse(
            action, TenantModuleEntitlementRowFacts.Stored(row, module, DateTimeOffset.UtcNow));
        return refusal is null ? null : Response<NoContent>.Fail(refusal.Message, refusal.StatusCode, refusal.Code);
    }

    /// <summary>
    /// Null when the plan's own line for <paramref name="moduleCode"/> offers Disable; otherwise the refusal. A module
    /// the tenant's plan does not include has no plan line: unknown to the catalogue it is not found, known it offers
    /// nothing here.
    /// </summary>
    public static async Task<Response<NoContent>?> RefuseProjectionDisableAsync(
        IModuleCatalogRepository modules,
        ITenantModuleEntitlementRepository entitlements,
        ITenantSubscriptionRepository subscriptions,
        ISubscriptionPlanRepository plans,
        Guid tenantId,
        string moduleCode,
        CancellationToken ct)
    {
        var module = await modules.GetByCodeAsync(moduleCode, ct);
        var planModuleCodes = await PlanModuleCodesAsync(subscriptions, plans, tenantId, ct);
        if (!planModuleCodes.Contains(moduleCode, StringComparer.OrdinalIgnoreCase))
        {
            return module is null
                ? Response<NoContent>.Fail("Module was not found.", 404, TenantModuleEntitlementRefusalCodes.ModuleNotFound)
                : Response<NoContent>.Fail("This module has no plan line to suspend.", 409, TenantModuleEntitlementRefusalCodes.ActionNotOffered);
        }

        var rows = await entitlements.GetByTenantAndModuleAsync(tenantId, moduleCode, ct);
        var access = TenantModuleEntitlementAccessEvaluator.Evaluate(
            tenantId, moduleCode, module?.DisplayName ?? moduleCode, hasPlanAccess: true, module?.IsCoreModule == true, rows, DateTimeOffset.UtcNow);
        var refusal = TenantModuleEntitlementRowActions.Refuse(
            TenantModuleEntitlementRowActions.Disable, TenantModuleEntitlementRowFacts.Projection(access, module));
        return refusal is null ? null : Response<NoContent>.Fail(refusal.Message, refusal.StatusCode, refusal.Code);
    }

    /// <summary>The module codes the tenant's current plan includes — the list query reads them the same way.</summary>
    public static async Task<IReadOnlyList<string>> PlanModuleCodesAsync(
        ITenantSubscriptionRepository subscriptions,
        ISubscriptionPlanRepository plans,
        Guid tenantId,
        CancellationToken ct)
    {
        var subscription = await subscriptions.GetCurrentByTenantIdAsync(tenantId, ct);
        if (subscription is null)
        {
            return [];
        }

        var plan = await plans.GetByIdAsync(subscription.PlanId, ct);
        return plan?.IncludedModuleKeys ?? [];
    }
}
