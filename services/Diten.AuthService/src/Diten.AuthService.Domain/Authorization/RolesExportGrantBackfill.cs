namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// WP-ROLES-CLOSE-01 — the keys of the one-way backfill that ships with <c>auth.roles.export</c>. The decision core is
/// <see cref="ExportGrantBackfill"/> (shared with <c>auth.users.export</c>); this type only names the pair.
/// </summary>
public static class RolesExportGrantBackfill
{
    public const string ReadKey = "auth.roles.read";
    public const string ExportKey = "auth.roles.export";

    /// <summary>What the audit row of a backfilled grant says about where it came from.</summary>
    public const string AuditSource = "roles-export-backfill";
}
