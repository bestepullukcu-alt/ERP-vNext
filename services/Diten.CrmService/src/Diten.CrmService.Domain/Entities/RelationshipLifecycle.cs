namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0149/MOD-0150 historical-lifecycle policy for AccountContactLink and AccountRelationship.
/// <para>
/// Contact↔Account and Account↔Account relationships are <b>historical business facts</b>. When a relationship
/// changes (a doctor moves hospitals, a pharmacy's affiliation changes) the old record is <b>not destroyed</b> — it is
/// <b>ended</b> with a validity/status transition (<c>Status</c> → ended/inactive, <c>ValidTo</c> set) so downstream
/// sales, visits, orders, forecasts and route history keep their original account/contact/relationship context.
/// </para>
/// <para>
/// Therefore duplicate/primary/active-pair uniqueness checks must count only records that are <b>still active</b>
/// (not closed): a closed (ended/inactive) record must never block opening a new active one, even for the same
/// natural key. The list projections keep all non-deleted records (active + closed) so history stays visible.
/// </para>
/// </summary>
public static class RelationshipLifecycle
{
    /// <summary>Lifecycle statuses that mark a relationship as historically closed (excluded from active uniqueness checks).</summary>
    public static readonly string[] ClosedStatuses = { "ended", "inactive" };

    /// <summary>True when the status marks the record as historically closed (ended/inactive). Null/empty = active.</summary>
    public static bool IsClosed(string? status)
        => !string.IsNullOrWhiteSpace(status)
           && ClosedStatuses.Contains(status.Trim().ToLowerInvariant());

    /// <summary>WP-VP-2B (R1/R2) — the store-side twin of <see cref="IsClosed"/>: a closed status, trimmed and
    /// case-insensitive (use with the <c>i</c> option). Built from <see cref="ClosedStatuses"/> so the two never drift.</summary>
    public static string ClosedStatusPattern
        => $"^\\s*({string.Join("|", ClosedStatuses.Select(System.Text.RegularExpressions.Regex.Escape))})\\s*$";

    /// <summary>WP-VP-2B (R1) — an account–contact link that counts as an ACTIVE contact of its account: not deleted and
    /// not closed. The contact itself must also still exist (not soft-deleted) — the same rule the account's
    /// <c>/contacts</c> projection applies when it skips links to a removed contact.</summary>
    public static bool IsActiveLink(AccountContactLink link, bool contactExists)
        => !link.IsDeleted && !IsClosed(link.Status) && contactExists;
}
