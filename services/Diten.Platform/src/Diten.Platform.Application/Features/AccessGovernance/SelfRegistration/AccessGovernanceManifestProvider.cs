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
                new ModuleManifestPage("USERS", "Users", "/Users", UsersRead, null, true, "List", 10, []),
                new ModuleManifestPage("ROLES", "Roles", "/Roles", RolesRead, null, true, "List", 20, []),
                new ModuleManifestPage("PERMISSIONS", "Permissions", "/Permissions", RolesRead, null, true, "List", 30, []),
                new ModuleManifestPage("ROLE_PERMISSIONS", "Role Permissions", "/RoleAssignments", RolesAssignPermission, null, true, "List", 40, []),
                new ModuleManifestPage("USER_ROLES", "User Roles", "/UserRoleAssignments", UsersAssignRole, null, true, "List", 50, [])
            ]);
}
