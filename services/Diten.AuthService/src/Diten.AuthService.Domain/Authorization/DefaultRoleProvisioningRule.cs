using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// WP-0018-ROLE1 (FU-0022-SEED-02; owner decision 2026-10-10, binding): "the default role template is applied to a tenant only
/// when it is first set up; afterwards only an administrator decides a role's content — the system adds no permission by itself".
/// <para>
/// THE ONE decision of "is this new": a default role receives the template's permissions <b>if and only if the same call created
/// the role</b>. "Created" is never inferred from the role's content (an empty or a partial role is not "new") — it is the outcome
/// of the atomic insert of the role against the unique index <c>uq_roles_tenant_name_active</c> (one active role per tenant and
/// name): of two concurrent provisioners exactly one inserts, the other one finds the role and grants nothing. So the template
/// reaches a role at most once, an existing role is never completed, extended or trimmed, and a tenant that lacks one default role
/// (an old tenant) gets that role — and only that role — from the template. No marker, no new collection: the role's existence
/// is the marker.
/// </para>
/// Used by the runtime provisioning (<c>RoleProvisioningService</c>: <c>tenant-activated</c>, <c>tenant-admin-invited</c>,
/// self-registration) and by the seed tenant's start-up (<c>DataSeeder</c>), so both apply the same rule.
/// </summary>
public static class DefaultRoleProvisioningRule
{
    /// <summary>Audit event of a default role created from the template (<c>authAuditLogs</c>): tenant, role, permission count, trigger.</summary>
    public const string AuditEvent = "default_role_provisioned";

    /// <summary>The template permissions the role gets now: the whole template for a role this call created, nothing otherwise.</summary>
    public static IReadOnlyList<Permission> GrantsFor(string roleName, IEnumerable<Permission> catalog, bool roleCreatedByThisCall)
        => roleCreatedByThisCall ? DefaultRolePermissionTemplate.SelectFor(roleName, catalog) : [];
}

/// <summary>What asked for the default roles (written to the audit row).</summary>
public static class DefaultRoleProvisioningTriggers
{
    public const string TenantActivated = "tenant-activated";
    public const string TenantAdminInvited = "tenant-admin-invited";
    public const string Register = "register";
    public const string Seed = "seed";
}
