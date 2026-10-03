using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Application.Features.ModuleRegistration;
using Diten.Platform.Application.Features.SubscriptionPlans.Commands;
using Diten.Platform.Application.Features.Tenants.Commercial.Entitlements.Commands;
using Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions.Commands;

namespace Diten.Platform.Application.Contracts.Audit;

/// <summary>
/// The commands allowed to skip the central audit pipeline because their handler writes the audit record in its own
/// transaction (<see cref="ITransactionOwnedAuditCommand"/>). <c>AuditBehavior</c> refuses a marked command that is
/// not named here — putting the marker on a command is not, by itself, a way around the pipeline (AUD-001 §4.4:
/// adding a name is a Control Tower decision).
///
/// <para>WP-PLATFORM-AUDIT-INTX-01 (CT decision 2026-10-02, option A): the eight tenant-subscription commands were
/// marked on 2026-08-31 (261f99105) but never added to this list, so every one of them ended in
/// "Transaction-owned audit is not authorized" before its handler ran. They are named now.
/// <c>TransactionOwnedAuditCommandGuardTests</c> holds this list equal to the marked commands, and requires each
/// marked command's handler to actually reach the in-transaction writer.</para>
/// </summary>
public static class TransactionOwnedAuditCommands
{
    public static readonly IReadOnlySet<Type> Authorized = new HashSet<Type>
    {
        // tenant module entitlements
        typeof(AddTenantModuleEntitlementCommand),
        typeof(EnableTenantModuleEntitlementCommand),
        typeof(DisableTenantModuleEntitlementCommand),
        typeof(UpdateTenantModuleEntitlementExpiryCommand),
        typeof(RemoveTenantManualModuleOverrideCommand),
        // tenant subscriptions
        typeof(CreateTenantSubscriptionCommand),
        typeof(AssignPlanToTenantCommand),
        typeof(ActivateTenantSubscriptionCommand),
        typeof(SuspendTenantSubscriptionCommand),
        typeof(ReactivateTenantSubscriptionCommand),
        typeof(RenewTenantSubscriptionCommand),
        typeof(CancelTenantSubscriptionCommand),
        typeof(ExpireTenantSubscriptionCommand),
        // subscription plans
        typeof(CreateSubscriptionPlanCommand),
        typeof(UpdateSubscriptionPlanCommand),
        typeof(ActivateSubscriptionPlanCommand),
        typeof(DeactivateSubscriptionPlanCommand),
        typeof(SeedDefaultSubscriptionPlansCommand),
        // module catalogue
        typeof(CreateModuleCatalogItemCommand),
        typeof(UpdateModuleCatalogItemCommand),
        typeof(ActivateModuleCatalogItemCommand),
        typeof(DeactivateModuleCatalogItemCommand),
        typeof(DeleteModuleCatalogItemCommand),
        typeof(BulkDeleteModuleCatalogItemsCommand),
        typeof(RegisterModuleManifestCommand)
    };
}
