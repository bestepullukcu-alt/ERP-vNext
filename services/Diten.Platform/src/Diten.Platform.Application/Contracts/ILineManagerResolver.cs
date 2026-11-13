namespace Diten.Platform.Application.Contracts;

/// <summary>How the line managers in <see cref="LineManagers"/> were found.</summary>
public enum LineManagerResolution
{
    /// <summary>Nobody: no primary seat, no reporting line, or vacant to the top (or a cycle in the org chart).</summary>
    None = 0,

    /// <summary>The holders of the seat the person's primary seat reports to.</summary>
    LineManager = 1,

    /// <summary>That seat was vacant (or held only by the person themselves): the holders of a seat further up.</summary>
    ManagerChain = 2
}

/// <summary>
/// The people a person reports to, and where they sit.
/// </summary>
/// <param name="UserIds">Never the person themselves; empty exactly when <paramref name="Resolution"/> is
/// <see cref="LineManagerResolution.None"/>.</param>
/// <param name="PositionId">The seat whose holders these are; null when nobody was found.</param>
public sealed record LineManagers(IReadOnlyList<Guid> UserIds, LineManagerResolution Resolution, Guid? PositionId)
{
    public static readonly LineManagers Nobody = new([], LineManagerResolution.None, null);
}

/// <summary>
/// WP-VW-W5 PLT-NOTIFY (K-N2, K-W9) — "the line manager of X", in ONE place: the primary seat →
/// <c>ReportsToPositionId</c> → whoever holds that seat now, the person excluded; a vacant seat moves one seat up,
/// a cycle or the top of the chart ends the walk (<see cref="LineManagerResolution.None"/>).
///
/// <para>Extracted from the timesheet approver (MOD-0280-FU01 D6), which still calls it and keeps its OWN fallback —
/// the tenant's time-admin pool — on top. A notification has no fallback: nobody found means nobody is written to.</para>
///
/// <para><b>Tenant.</b> The org reads are tenant-scoped by the ambient tenant context; <paramref name="tenantId"/> must
/// be that tenant. Asked about any other tenant (or with no tenant resolved), the answer is
/// <see cref="LineManagers.Nobody"/> — a caller that pinned the wrong tenant gets nobody, never another tenant's people.</para>
/// </summary>
public interface ILineManagerResolver
{
    Task<LineManagers> ResolveAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
