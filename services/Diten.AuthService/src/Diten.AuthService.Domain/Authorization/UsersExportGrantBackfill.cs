namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// BL-452 package 3 — the keys of the one-way backfill that shipped with <c>auth.users.export</c>. The decision core is
/// <see cref="ExportGrantBackfill"/> (shared with <c>auth.roles.export</c>); this type only names the pair.
/// </summary>
public static class UsersExportGrantBackfill
{
    public const string ReadKey = "auth.users.read";
    public const string ExportKey = "auth.users.export";
}
