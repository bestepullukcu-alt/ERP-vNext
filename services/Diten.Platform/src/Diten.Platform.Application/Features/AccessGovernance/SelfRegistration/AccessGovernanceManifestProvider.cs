using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.AccessGovernance.SelfRegistration;

/// <summary>
/// FEAT-BASELINE-MODULES-S1 — the Access Governance module self-registration manifest, the pilot BASELINE module:
/// <c>IsBaseline = true</c> means it is entitlement-free — every tenant automatically has access (the tenant
/// entitlement wall is bypassed). The per-user permission gate is preserved: each page carries the verbatim
/// <c>auth.*</c> <c>[HasPermission]</c> key the frontend UX guard and backend enforce, so a user only sees a link
/// when they hold it (identical to the previous hardcoded tenant-shell block, now rendered data-driven).
/// <para>The five pages mirror the REAL tenant-shell routes: /Users, /Roles, /Permissions, /RoleAssignments,
/// /UserRoleAssignments. They are co-equal top-level nav entries (no parent), each IsNavigationVisible.</para>
/// </summary>
public sealed class AccessGovernanceManifestProvider : IModuleManifestProvider
{
    // Verbatim auth.* permission keys (same keys the previous hardcoded _LayoutTenantShell block gated on).
    private const string UsersRead = "auth.users.read";
    private const string RolesRead = "auth.roles.read";
    private const string RolesAssignPermission = "auth.roles.assign-permission";
    private const string UsersAssignRole = "auth.users.assign-role";

    // BL-458 / BL-452 package 3 — the page ACTIONS, verbatim from AuthService's seed (DataSeeder.BuildCanonicalPermissions)
    // and the [HasPermission] on the endpoints they reach. AccessGovernanceManifestSeedParityTests (AuthService tests)
    // holds this file and the seed equal: every key here is seeded, every seeded auth.users/auth.roles key is here except
    // the two API-only ones with no screen action (auth.users.lookup, auth.users.lookup-validation).
    private const string UsersCreate = "auth.users.create";
    private const string UsersUpdate = "auth.users.update";
    private const string UsersDelete = "auth.users.delete";
    private const string UsersExport = "auth.users.export";
    private const string UsersAccountKindManage = "auth.users.account-kind.manage";
    private const string RolesCreate = "auth.roles.create";
    private const string RolesUpdate = "auth.roles.update";
    private const string RolesDelete = "auth.roles.delete";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "access-governance",
            ModuleName: "Access Governance",
            DisplayName: "Access Governance",
            Domain: "Administration", // FEAT-ADMIN-DOMAIN — grouped with Tenant Settings under one Administration domain.
            Service: "DitenPlatform",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 50,
            Icon: "bx-shield-quarter",
            IsBaseline: true, // FEAT-BASELINE-MODULES — entitlement-free; every tenant gets it.
            Pages:
            [
                new ModuleManifestPage("USERS", "Users", "/Users", UsersRead, null, true, "List", 10,
                [
                    new ModuleManifestAction("CREATE", "Invite User", UsersCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                    new ModuleManifestAction("EXPORT", "Export", UsersExport, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                    new ModuleManifestAction("UPDATE", "Edit", UsersUpdate, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                    new ModuleManifestAction("ACCOUNT_KIND", "Set Account Kind", UsersAccountKindManage, "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                    new ModuleManifestAction("DELETE", "Delete", UsersDelete, "RowAction", 50, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                ]),
                new ModuleManifestPage("ROLES", "Roles", "/Roles", RolesRead, null, true, "List", 20,
                [
                    new ModuleManifestAction("CREATE", "Create Role", RolesCreate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                    new ModuleManifestAction("UPDATE", "Edit", RolesUpdate, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                    new ModuleManifestAction("DELETE", "Delete", RolesDelete, "RowAction", 30, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                ]),
                // Permissions is a READ-ONLY catalog view (permissions are code/module-owned, synced from
                // the module catalog — tenants can't create/edit them). Hidden from tenant nav to declutter;
                // the actual permission-picking happens on the Role Permissions screen. Route still reachable.
                new ModuleManifestPage("PERMISSIONS", "Permissions", "/Permissions", RolesRead, null, false, "List", 30, []),
                new ModuleManifestPage("ROLE_PERMISSIONS", "Role Permissions", "/RoleAssignments", RolesAssignPermission, null, true, "List", 40,
                [
                    new ModuleManifestAction("ASSIGN_PERMISSION", "Assign Permission", RolesAssignPermission, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                ]),
                new ModuleManifestPage("USER_ROLES", "User Roles", "/UserRoleAssignments", UsersAssignRole, null, true, "List", 50,
                [
                    new ModuleManifestAction("ASSIGN_ROLE", "Assign Role", UsersAssignRole, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                ])
            ]);
}
