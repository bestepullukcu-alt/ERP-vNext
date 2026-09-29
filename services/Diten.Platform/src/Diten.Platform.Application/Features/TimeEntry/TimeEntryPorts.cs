namespace Diten.Platform.Application.Features.TimeEntry;

// MOD-0280-FU01 (pack §2.4, §3.3, ADR-004 decision 3) — the ONLY doors from this module to MOD-0288 (org chart) and
// MOD-0024 (tasks). The v1 implementations live in Adapters/ and are the only files under Features/TimeEntry allowed to
// name another module's repository or entity; TimeEntrySourceGuardTests fails the build otherwise. On extraction these
// become HTTP clients and nothing else in the module changes.

/// <summary>A person's primary seat: the position that decides their line manager and their legal entity.</summary>
public sealed record TimeEntryPrimarySeat(Guid PositionId, Guid? LegalEntityId);

/// <summary>One node of the reporting chain.</summary>
public sealed record TimeEntryPosition(Guid PositionId, Guid? ReportsToPositionId, bool IsArchived);

/// <summary>MOD-0288, read only.</summary>
public interface ITimeEntryOrgGateway
{
    /// <summary>The person's primary active seat (primary assignment first, as MOD-0288 orders them), or null.</summary>
    Task<TimeEntryPrimarySeat?> PrimarySeatAsync(Guid userId, CancellationToken ct = default);

    /// <summary>One position, or null when it does not exist in this tenant.</summary>
    Task<TimeEntryPosition?> PositionAsync(Guid positionId, CancellationToken ct = default);

    /// <summary>Who holds this seat right now.</summary>
    Task<IReadOnlyList<Guid>> HoldersOfAsync(Guid positionId, CancellationToken ct = default);
}

/// <summary>MOD-0024, read only. T1a needs one fact; T1b widens it (lifecycle, holder, plan block).</summary>
public interface ITimeEntryTaskGateway
{
    /// <summary>Which of these task ids <paramref name="userId"/> may READ, by MOD-0024's own read-access rule (F5). A
    /// task that does not exist and a task the person cannot read are both simply absent — the caller cannot tell them
    /// apart, so a time row can never be used to probe for another team's task ids.</summary>
    Task<IReadOnlySet<Guid>> ReadableTaskIdsAsync(Guid userId, IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default);
}
