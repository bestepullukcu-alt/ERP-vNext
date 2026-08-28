using Diten.Platform.Application.Features.TenantOrganization;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// WHO SITS IN WHICH SEAT — the ONE place MOD-0024 asks that question.
///
/// <para><b>What it replaced.</b> Nine files under Features/Tasks injected
/// <see cref="IPositionAssignmentRepository"/> and every one of them called <c>GetAllAsync</c>: nobody asked a
/// narrow question, everybody pulled the whole table and filtered it in memory. The active-window rule was
/// hand-written ten times, while its canonical form already existed as
/// <see cref="TenantOrganizationMapper.IsActiveNow"/> with no reader on this side.</para>
///
/// <para><b>Why one surface, and why now.</b> BL-071 hands seat ownership to HCM, where the same fact is
/// <c>EmploymentRecord.StartDate/EndDate</c> as <c>DateOnly</c> rather than <c>DateTimeOffset</c>. Ten sites
/// would each need changing, and a missed one does not fail loudly — it keeps answering from stale seat data,
/// so somebody who has left keeps being handed work. The type change is not innocent either: a half-open
/// interval over <c>DateOnly</c> and over <c>DateTimeOffset</c> disagree at the day boundary. THIS FILE is the
/// only place that has to learn any of it.</para>
///
/// <para><b>The rule is consumed, not copied.</b> <see cref="TenantOrganizationMapper.IsActiveNow"/> is
/// MOD-0288's own contract for MOD-0288's own entity, already read by that module and by the assignment DTO's
/// derived status. Absorbing it here would have produced the second copy this class exists to delete — moved
/// rather than removed. Its semantics were compared with the ten hand-written predicates and are identical:
/// cancelled ⇒ Ended, <c>EffectiveFrom &gt; now</c> ⇒ Planned, <c>EffectiveTo &lt;= now</c> ⇒ Ended.</para>
///
/// <para><b>The members are DERIVED from the call sites</b>, not invented. Reading the nine consumers produced
/// four distinct questions plus one that only BL-072 asks; each member below is one of them, and there is no
/// sixth "just in case".</para>
///
/// <para><b>Not addressed here, deliberately.</b> Every read is still <c>GetAllAsync</c> + an in-memory filter,
/// exactly as before — this round changes no behaviour. Turning these into indexed queries is now POSSIBLE for
/// the first time (there is one caller of the repository instead of nine), and is recorded as separate work.</para>
/// </summary>
public interface ITaskSeatDirectory
{
    /// <summary>
    /// Every seat occupied RIGHT NOW. Asked by the two assignment pickers (to enumerate candidates and count
    /// holders per position) and by the reassign guard, which hands them to <see cref="TaskAssigneeEligibility"/>.
    /// </summary>
    Task<IReadOnlyList<PositionAssignment>> ActiveAsync(CancellationToken ct);

    /// <summary>
    /// The seats one user occupies right now, as ROWS. Only the create handler needs the rows rather than the
    /// ids: it orders by <c>AssignmentType</c> so a person's PRIMARY seat decides their home unit.
    /// </summary>
    Task<IReadOnlyList<PositionAssignment>> ActiveForUserAsync(Guid userId, CancellationToken ct);

    /// <summary>The position ids one user occupies right now — the work-item provider and the task list.</summary>
    Task<IReadOnlyList<Guid>> PositionIdsForUserAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Does this user occupy any of these seats? The claim guard asks it of one position; the direction test
    /// asks it of the whole manager chain.
    /// </summary>
    Task<bool> HoldsAnyAsync(Guid userId, IReadOnlySet<Guid> positionIds, CancellationToken ct);

    /// <summary>
    /// Who occupies these seats right now? The notification service asks it of one position (a pool's holders);
    /// the team resolver asks it of every subordinate position at once.
    /// </summary>
    Task<IReadOnlyList<Guid>> HoldersOfAsync(IReadOnlySet<Guid> positionIds, CancellationToken ct);

    /// <summary>
    /// Everyone who has EVER held a seat, live or not.
    ///
    /// <para>A different question from the four above, and it has its own member rather than leaking inactive
    /// rows: BL-072's exclusion count needs the candidates the active list drops, so that "nobody holds a
    /// position" and "their assignment ended" stop being the same silent empty answer.</para>
    /// </summary>
    Task<IReadOnlySet<Guid>> EverAssignedUserIdsAsync(CancellationToken ct);
}

/// <inheritdoc cref="ITaskSeatDirectory"/>
public sealed class TaskSeatDirectory : ITaskSeatDirectory
{
    private readonly IPositionAssignmentRepository _assignments;

    public TaskSeatDirectory(IPositionAssignmentRepository assignments) => _assignments = assignments;

    public async Task<IReadOnlyList<PositionAssignment>> ActiveAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        // The repository read is already tenant-scoped and non-deleted; the window is MOD-0288's own rule.
        return (await _assignments.GetAllAsync(ct))
            .Where(assignment => TenantOrganizationMapper.IsActiveNow(assignment, now))
            .ToList();
    }

    public async Task<IReadOnlyList<PositionAssignment>> ActiveForUserAsync(Guid userId, CancellationToken ct)
        => (await ActiveAsync(ct))
            .Where(assignment => assignment.UserId == userId)
            // PRIMARY first: a person holding several seats has one "home", and the create handler resolves the
            // task's organization unit from it. The ordering was in the call site before and moves with it.
            .OrderBy(assignment => assignment.AssignmentType)
            .ToList();

    public async Task<IReadOnlyList<Guid>> PositionIdsForUserAsync(Guid userId, CancellationToken ct)
        => (await ActiveForUserAsync(userId, ct))
            .Select(assignment => assignment.PositionId)
            .Distinct()
            .ToList();

    public async Task<bool> HoldsAnyAsync(Guid userId, IReadOnlySet<Guid> positionIds, CancellationToken ct)
    {
        if (positionIds is null || positionIds.Count == 0) { return false; }

        return (await ActiveAsync(ct))
            .Any(assignment => assignment.UserId == userId && positionIds.Contains(assignment.PositionId));
    }

    public async Task<IReadOnlyList<Guid>> HoldersOfAsync(IReadOnlySet<Guid> positionIds, CancellationToken ct)
    {
        if (positionIds is null || positionIds.Count == 0) { return []; }

        return (await ActiveAsync(ct))
            .Where(assignment => positionIds.Contains(assignment.PositionId))
            .Select(assignment => assignment.UserId)
            .Distinct()
            .ToList();
    }

    public async Task<IReadOnlySet<Guid>> EverAssignedUserIdsAsync(CancellationToken ct)
        => (await _assignments.GetAllAsync(ct)).Select(assignment => assignment.UserId).ToHashSet();
}
