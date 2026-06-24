namespace Diten.Platform.API.Security;

/// <summary>
/// AG-STEP-004B Slice 1B — Platform-only permission-key alias map (single source of truth for dual-read).
///
/// Keys are the <b>canonical</b> (lowercase, PKS-001) permission keys; values are the set of <b>legacy aliases</b>
/// that must also satisfy a check for that canonical key during the permission-key migration. The map is:
/// <list type="bullet">
///   <item><description><b>Directional</b> — canonical → aliases, never the reverse.</description></item>
///   <item><description><b>Non-transitive</b> — an alias is never itself a canonical key; a canonical key is never
///   listed as an alias; an alias is bound to exactly one canonical key.</description></item>
///   <item><description><b>Immutable</b> — a compile-time constant; there is no runtime/dynamic alias source.</description></item>
/// </list>
///
/// Scope: <b>Platform-enforced rows only</b>, generated from the migration plan §1
/// (<c>execution/portfolio/access-governance-permission-key-migration-plan.md</c>). MDM <c>mdm.legal-entities.*</c>
/// rows are excluded (Slice 1A already canonicalized them; no legacy grant exists) and EnterpriseStrategy rows are
/// out of Platform scope. The map structurally supports multiple aliases per canonical; today each canonical has one.
/// </summary>
public static class PermissionAliasMap
{
    /// <summary>
    /// Canonical (lowercase) permission key → set of legacy aliases that also satisfy it (dual-read).
    /// Looked up with <see cref="StringComparer.Ordinal"/> so a legacy-spelled requirement never matches a canonical
    /// key by case-folding (no auto-upgrade).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> CanonicalToAliases =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            // ── §1.2  Platform.* → platform.*  (32) ──
            ["platform.administrators.read"] = Aliases("Platform.Administrators.Read"),
            ["platform.administrators.create"] = Aliases("Platform.Administrators.Create"),
            ["platform.administrators.update"] = Aliases("Platform.Administrators.Update"),
            ["platform.administrators.suspend"] = Aliases("Platform.Administrators.Suspend"),
            ["platform.administrators.assign-roles"] = Aliases("Platform.Administrators.AssignRoles"),
            ["platform.audit.read"] = Aliases("Platform.Audit.Read"),
            ["platform.audit.export"] = Aliases("Platform.Audit.Export"),
            ["platform.audit.redact-actor"] = Aliases("Platform.Audit.RedactActor"),
            ["platform.audit.retention.update"] = Aliases("Platform.Audit.Retention.Update"),
            ["platform.interface-registry.read"] = Aliases("Platform.InterfaceRegistry.Read"),
            ["platform.interface-registry.import"] = Aliases("Platform.InterfaceRegistry.Import"),
            ["platform.interface-registry.review"] = Aliases("Platform.InterfaceRegistry.Review"),
            ["platform.interface-registry.deprecate"] = Aliases("Platform.InterfaceRegistry.Deprecate"),
            ["platform.lookups.read"] = Aliases("Platform.Lookups.Read"),
            ["platform.notifications.read"] = Aliases("Platform.Notifications.Read"),
            ["platform.notifications.configure"] = Aliases("Platform.Notifications.Configure"),
            ["platform.notifications.dispatches.read"] = Aliases("Platform.Notifications.Dispatches.Read"),
            ["platform.notifications.dispatches.queue"] = Aliases("Platform.Notifications.Dispatches.Queue"),
            ["platform.notifications.templates.read"] = Aliases("Platform.Notifications.Templates.Read"),
            ["platform.notifications.templates.create"] = Aliases("Platform.Notifications.Templates.Create"),
            ["platform.notifications.templates.update"] = Aliases("Platform.Notifications.Templates.Update"),
            ["platform.notifications.templates.archive"] = Aliases("Platform.Notifications.Templates.Archive"),
            ["platform.subscription-features.read"] = Aliases("Platform.SubscriptionFeatures.Read"),
            ["platform.subscription-features.create"] = Aliases("Platform.SubscriptionFeatures.Create"),
            ["platform.subscription-features.update"] = Aliases("Platform.SubscriptionFeatures.Update"),
            ["platform.subscription-features.archive"] = Aliases("Platform.SubscriptionFeatures.Archive"),
            ["platform.subscription-features.manage-mappings"] = Aliases("Platform.SubscriptionFeatures.ManageMappings"),
            ["platform.subscription-plans.read"] = Aliases("Platform.SubscriptionPlans.Read"),
            ["platform.subscription-plans.create"] = Aliases("Platform.SubscriptionPlans.Create"),
            ["platform.subscription-plans.update"] = Aliases("Platform.SubscriptionPlans.Update"),
            ["platform.subscription-plans.activate"] = Aliases("Platform.SubscriptionPlans.Activate"),
            ["platform.subscription-plans.deactivate"] = Aliases("Platform.SubscriptionPlans.Deactivate"),

            // ── §1.1  Modules.* (Platform-owned) → platform.*  (20) ──
            ["platform.module-catalog.read"] = Aliases("Modules.ModuleCatalog.Read"),
            ["platform.module-catalog.create"] = Aliases("Modules.ModuleCatalog.Create"),
            ["platform.module-catalog.update"] = Aliases("Modules.ModuleCatalog.Update"),
            ["platform.module-catalog.delete"] = Aliases("Modules.ModuleCatalog.Delete"),
            ["platform.module-catalog.bulk-delete"] = Aliases("Modules.ModuleCatalog.BulkDelete"),
            ["platform.organization-units.read"] = Aliases("Modules.OrganizationUnit.Read"),
            ["platform.organization-units.create"] = Aliases("Modules.OrganizationUnit.Create"),
            ["platform.organization-units.update"] = Aliases("Modules.OrganizationUnit.Update"),
            ["platform.organization-units.delete"] = Aliases("Modules.OrganizationUnit.Delete"),
            ["platform.organization-units.archive"] = Aliases("Modules.OrganizationUnit.Archive"),
            ["platform.positions.read"] = Aliases("Modules.Position.Read"),
            ["platform.positions.create"] = Aliases("Modules.Position.Create"),
            ["platform.positions.update"] = Aliases("Modules.Position.Update"),
            ["platform.positions.delete"] = Aliases("Modules.Position.Delete"),
            ["platform.positions.archive"] = Aliases("Modules.Position.Archive"),
            ["platform.position-assignments.read"] = Aliases("Modules.PositionAssignment.Read"),
            ["platform.position-assignments.create"] = Aliases("Modules.PositionAssignment.Create"),
            ["platform.position-assignments.update"] = Aliases("Modules.PositionAssignment.Update"),
            ["platform.position-assignments.delete"] = Aliases("Modules.PositionAssignment.Delete"),
            ["platform.organization.read-manager-chain"] = Aliases("Modules.Organization.ReadManagerChain"),

            // ── §1.3  D-4 verb aliases (platform.tenants.*.view → .read)  (3) ──
            ["platform.tenants.quotas.read"] = Aliases("platform.tenants.quotas.view"),
            ["platform.tenants.commercial.subscription.read"] = Aliases("platform.tenants.commercial.subscription.view"),
            ["platform.tenants.commercial.subscription.history.read"] = Aliases("platform.tenants.commercial.subscription.history.view"),

            // MOD-0028-FU01 spec keys -> effective lowercase runtime permissions (5).
            ["platform.document-management.collection-definitions.list"] = Aliases("MOD0028.COLLECTION_DEFINITION.LIST"),
            ["platform.document-management.collection-definitions.view"] = Aliases("MOD0028.COLLECTION_DEFINITION.VIEW"),
            ["platform.document-management.baseline-releases.list"] = Aliases("MOD0028.BASELINE_RELEASE.LIST"),
            ["platform.document-management.corporate-root.initialize"] = Aliases("MOD0028.CORPORATE_ROOT.INITIALIZE"),
            ["platform.document-management.collection-instances.view"] = Aliases("MOD0028.COLLECTION_INSTANCE.VIEW"),

            // MOD-0028-FU02 spec keys -> effective lowercase runtime permissions (1).
            // Only the unambiguous parent-spec match is mapped:
            //   qms-baselines.publish  -> MOD0028.BASELINE_RELEASE.PUBLISH (exact parent spec verb).
            // Deliberately NOT mapped, pending an explicit EA/MOD-0018 decision:
            //   qms-baselines.import  -> no parent spec key exists (FU02-native; mint MOD0028.QMS_BASELINE.IMPORT or leave native).
            //   qms-baselines.view    -> parent spec has only BASELINE_RELEASE.LIST, not a VIEW verb; view<->list is a semantic
            //                            widening that requires EA confirmation before a directional alias is added.
            ["platform.document-management.qms-baselines.publish"] = Aliases("MOD0028.BASELINE_RELEASE.PUBLISH"),
        };

    private static IReadOnlySet<string> Aliases(params string[] aliases) =>
        new HashSet<string>(aliases, StringComparer.Ordinal);
}
