using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// The pure decision core of the ONE-WAY export backfill, shared by every "{module}.export" key that used to be covered
/// by its "{module}.read" key (BL-452: <c>auth.users.export</c>; WP-ROLES-CLOSE-01: <c>auth.roles.export</c>).
///
/// <para><b>What it does.</b> Before an export key existed, exporting a list needed only its read key. On the upgrade
/// every role of an OLD tenant that holds read also receives export — nobody loses a capability they had yesterday —
/// once per tenant and key.</para>
///
/// <para><b>Old or born — a STORED FACT, never a guess.</b> A tenant with a <c>permissionReconciliations</c> row for
/// the key is settled and is not looked at. For a tenant WITHOUT one, the decision is read off two timestamps the
/// database already holds: the <c>CreatedAt</c> of the tenant's OLDEST role document (deleted or not — a deleted role
/// still proves the tenant was there) and the <c>CreatedAt</c> of the export permission's catalog row:
/// <list type="bullet">
/// <item>oldest role STRICTLY BEFORE the key — the tenant was configured when the key did not exist: OLD. Its reading
/// roles receive export, whatever any of its roles already holds (an Admin that got the key from the template or an
/// entitlement sync does not make the tenant "done").</item>
/// <item>oldest role at or after the key — the tenant was set up with the key in the catalog: BORN. It never receives
/// a backfill, so a reading role opened without export stays that way.</item>
/// <item>either timestamp missing or default — UNKNOWN, treated as BORN.</item>
/// </list>
/// ⚠ Every doubt resolves to NOT GRANTING. A wrongly withheld export costs an administrator one click on the Role
/// Permissions screen; a wrongly granted one is an authority nobody decided to give (<c>auth.users.export</c> is
/// enforced on the server). "Older" therefore means older than the key by more than <see cref="ClockSkewAllowance"/>:
/// the key and the tenant may be written by two instances whose clocks disagree, and a tenant set up within that
/// window is BORN. The same instant is born too.</para>
///
/// <para><b>And the ROLE must be old too.</b> An old tenant that stayed unmarked (a failed write, an interrupted run)
/// can have roles its administrator opened AFTER the key existed — with read and, on purpose, without export — or a
/// role whose export was given by hand and taken away again. Such a role was configured with the key in the catalog;
/// it is not a candidate. A role receives the backfill only if it, and (when stored) its read grant, were created
/// before the key by more than the same allowance; a role without a date is not a candidate.</para>
///
/// <para><b>Each key decides for itself</b>: the two export permissions were created at different times, so a tenant
/// can be born for one and old for the other.</para>
///
/// <para>⚠ This plans AUTHORITY WRITES (role → permission grants made by the system, not by a person).</para>
/// </summary>
public static class ExportGrantBackfill
{
    public const string OriginBorn = "born";
    public const string OriginBackfilled = "backfilled";

    /// <summary>The audit event a backfilled grant is written as — the same name a person's grant carries.</summary>
    public const string AuditEventName = "role_permission_granted";

    /// <summary>The audit event of a grant whose SOURCE was corrected (nothing granted, nothing removed).</summary>
    public const string SourceCorrectedEventName = "role_permission_source_corrected";

    /// <summary>
    /// The audit event that withdraws a <see cref="AuditEventName"/> row the run KNOWS did not take effect: its audit
    /// row was written, then the grant insert failed with an error the run saw (not a crash). Without it the audit log
    /// would keep saying a permission was granted that no role holds.
    /// </summary>
    public const string GrantNotAppliedEventName = "role_permission_grant_not_applied";

    /// <summary>
    /// How much older than the key a tenant or a role must be to count as OLD. Two instances write the key and the
    /// tenant; their clocks may disagree. Five minutes is well beyond what NTP-synchronised hosts drift (milliseconds to
    /// seconds) and well below the age gap that matters here — an export key is a release, tenants and roles that predate
    /// it are days to months older. A tenant inside the window is BORN: no grant.
    /// </summary>
    public static readonly TimeSpan ClockSkewAllowance = TimeSpan.FromMinutes(5);

    /// <summary>One read → export pair and what its audit rows name as their source.</summary>
    public sealed record KeyPair(string ReadKey, string ExportKey, string AuditSource)
    {
        /// <summary>The audit source of a source correction of this key's grants.</summary>
        public string CorrectionAuditSource => AuditSource + "-source-correction";
    }

    public static readonly KeyPair Users = new("auth.users.read", "auth.users.export", "users-export-backfill");
    public static readonly KeyPair Roles = new("auth.roles.read", "auth.roles.export", "roles-export-backfill");

    /// <summary>Every pair the backfill covers. A new "{module}.export" key is one line here.</summary>
    public static readonly IReadOnlyList<KeyPair> Keys = [Users, Roles];

    /// <summary>A role document as the decision needs it. <paramref name="IsDeleted"/> roles prove the tenant's age and receive nothing.</summary>
    public sealed record RoleState(Guid RoleId, string Name, Guid TenantId, bool IsSystem, DateTimeOffset CreatedAt = default, bool IsDeleted = false);

    public sealed record PlannedGrant(Guid RoleId, string RoleName, Guid TenantId, Guid PermissionId, GrantSource Source);

    /// <summary>One unmarked tenant: what it is (<see cref="OriginBorn"/> / <see cref="OriginBackfilled"/>) and, for an
    /// old tenant, the grants it still lacks (possibly none). Either way it is marked afterwards.</summary>
    public sealed record TenantPlan(Guid TenantId, string Origin, IReadOnlyList<PlannedGrant> Grants);

    /// <summary>
    /// Old, or born? See the type's remarks. <paramref name="oldestRoleCreatedAt"/> is the earliest
    /// <c>CreatedAt</c> among ALL the tenant's role documents, deleted ones included.
    /// </summary>
    public static string Classify(DateTimeOffset oldestRoleCreatedAt, DateTimeOffset exportKeyCreatedAt)
        => IsOlderThanKey(oldestRoleCreatedAt, exportKeyCreatedAt) ? OriginBackfilled : OriginBorn;

    /// <summary>
    /// True only when both dates are known and <paramref name="createdAt"/> lies STRICTLY before
    /// <c><paramref name="exportKeyCreatedAt"/> − <see cref="ClockSkewAllowance"/></c>. Anything else — unknown, equal,
    /// inside the window, after — is false: do not grant.
    /// </summary>
    public static bool IsOlderThanKey(DateTimeOffset createdAt, DateTimeOffset exportKeyCreatedAt)
    {
        if (createdAt == default || exportKeyCreatedAt == default) return false; // unknown → do not grant
        if (exportKeyCreatedAt.UtcTicks <= ClockSkewAllowance.Ticks) return false;
        return createdAt.UtcTicks < exportKeyCreatedAt.UtcTicks - ClockSkewAllowance.Ticks;
    }

    /// <summary>
    /// The tenants to settle, in a stable order. A tenant in <paramref name="markedTenants"/> is not looked at; neither
    /// is a role without a tenant (<see cref="Guid.Empty"/>). Additive only, deterministic, idempotent: a live role
    /// that already holds export — from a template, an entitlement sync, a person or an earlier, interrupted run —
    /// plans no grant; a deleted role plans none either.
    /// </summary>
    /// <param name="templateGrantsExport">True when the default role template itself gives export to this (system) role.</param>
    /// <param name="readGrantCreatedAt">When each role's read grant was created, if stored. A read grant given after
    /// the key makes its role no candidate; one without a stored date leaves the decision to the role's own age.</param>
    public static IReadOnlyList<TenantPlan> Plan(
        IEnumerable<RoleState> roles,
        ISet<(Guid RoleId, Guid PermissionId)> grants,
        Guid readPermissionId,
        Guid exportPermissionId,
        DateTimeOffset exportKeyCreatedAt,
        IReadOnlySet<Guid> markedTenants,
        Func<RoleState, bool> templateGrantsExport,
        IReadOnlyDictionary<(Guid RoleId, Guid PermissionId), DateTimeOffset>? readGrantCreatedAt = null)
    {
        bool ReadGrantIsOld(RoleState r) =>
            readGrantCreatedAt is null
            || !readGrantCreatedAt.TryGetValue((r.RoleId, readPermissionId), out var at)
            || at == default
            || IsOlderThanKey(at, exportKeyCreatedAt);

        return roles
            .Where(r => r.TenantId != Guid.Empty && !markedTenants.Contains(r.TenantId))
            .GroupBy(r => r.TenantId)
            .OrderBy(g => g.Key)
            .Select(tenant =>
            {
                // The oldest role that HAS a date decides; a role without one says nothing about the tenant's age.
                var dated = tenant.Where(r => r.CreatedAt != default).Select(r => r.CreatedAt).ToList();
                var origin = Classify(dated.Count == 0 ? default : dated.Min(), exportKeyCreatedAt);
                IReadOnlyList<PlannedGrant> planned = origin == OriginBorn
                    ? []
                    : tenant
                        .Where(r => !r.IsDeleted
                                    && grants.Contains((r.RoleId, readPermissionId))
                                    && !grants.Contains((r.RoleId, exportPermissionId))
                                    && IsOlderThanKey(r.CreatedAt, exportKeyCreatedAt) // the ROLE predates the key …
                                    && ReadGrantIsOld(r))                             // … and so does its read grant
                        .OrderBy(r => r.RoleId)
                        .Select(r => new PlannedGrant(r.RoleId, r.Name, r.TenantId, exportPermissionId, SourceFor(r, templateGrantsExport)))
                        .ToList();
                return new TenantPlan(tenant.Key, origin, planned);
            })
            .ToList();
    }

    /// <summary>
    /// How a backfilled grant is filed. A SYSTEM role the default template itself gives export to (Admin) keeps a
    /// template-managed <see cref="GrantSource.System"/> grant, like the rest of its baseline. Everything else — a
    /// custom role, and a system role the template does NOT give export to (Viewer) — gets a
    /// <see cref="GrantSource.Manual"/> grant: nothing re-provisions it, so the tenant's administrator must be able to
    /// take it away on the Role Permissions screen (a System grant answers 409 there and can never be removed).
    /// </summary>
    public static GrantSource SourceFor(RoleState role, Func<RoleState, bool> templateGrantsExport)
        => role.IsSystem && templateGrantsExport(role) ? GrantSource.System : GrantSource.Manual;

    /// <summary>
    /// A grant an EARLIER build's backfill filed as <see cref="GrantSource.System"/> on a role the template does not
    /// give export to: its source is wrong (it locks the grant on the screen) and is corrected to Manual. A System
    /// grant on a role the template DOES give export to is right and is left alone; so is every Module / Manual grant.
    /// </summary>
    public static bool NeedsSourceCorrection(RoleState role, GrantSource current, Func<RoleState, bool> templateGrantsExport)
        => current == GrantSource.System && SourceFor(role, templateGrantsExport) == GrantSource.Manual;

    /// <summary>The id of THE mark of a tenant and key: two writers produce the same document.</summary>
    public static Guid MarkId(Guid tenantId, string exportKey) => Deterministic($"export-backfill-mark|{tenantId:N}|{exportKey}");

    /// <summary>The id of THE audit row of a backfilled grant: written once however often the run is repeated.</summary>
    public static Guid AuditId(Guid tenantId, Guid roleId, string exportKey) => Deterministic($"export-backfill-audit|{tenantId:N}|{roleId:N}|{exportKey}");

    /// <summary>The id of THE audit row that withdraws a grant the run knows was not applied.</summary>
    public static Guid NotAppliedAuditId(Guid tenantId, Guid roleId, string exportKey) => Deterministic($"export-backfill-not-applied|{tenantId:N}|{roleId:N}|{exportKey}");

    /// <summary>The id of THE audit row of a source correction.</summary>
    public static Guid CorrectionAuditId(Guid tenantId, Guid roleId, string exportKey) => Deterministic($"export-backfill-source-correction|{tenantId:N}|{roleId:N}|{exportKey}");

    private static Guid Deterministic(string name) => new(MD5.HashData(Encoding.UTF8.GetBytes(name)));
}
