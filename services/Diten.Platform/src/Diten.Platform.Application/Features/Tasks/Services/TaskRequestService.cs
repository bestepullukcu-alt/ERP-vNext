using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementContract;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// BL-509 — the OWNER-DECISION points of the task request, in one place. Each is one line to change, and each has a
/// test named after it (<c>TaskRequestPolicyTests</c>).
///
/// <para>The fifth decision — the sending key is granted by hand only — lives where every such key lives:
/// AuthService's <c>ExplicitGrantOnlyPermissions</c>.</para>
/// </summary>
public static class TaskRequestPolicy
{
    /// <summary>A request can only be RECEIVED by somebody who holds an active seat in a live unit (decision 6).</summary>
    public static readonly bool RecipientNeedsActiveSeat = true;

    /// <summary>A request can be SENT by anybody holding the key, seat or no seat (decision 6).</summary>
    public static readonly bool SenderNeedsActiveSeat = false;

    /// <summary>Once accepted, the sender may no longer hand the work to somebody else (decision 3).</summary>
    public static readonly bool SenderMayReassignAfterAcceptance = false;

    /// <summary>Once accepted, the sender may no longer edit the task; they watch and comment (decision 3).</summary>
    public static readonly bool SenderMayEditAfterAcceptance = false;

    /// <summary>The recipient's manager does not see an offer before it is accepted (decision 4).</summary>
    public static readonly bool RecipientManagerSeesPendingOffer = false;

    /// <summary>A request nobody has taken on — waiting, declined or withdrawn. Nobody holds it.</summary>
    public static bool IsUnanswered(TaskItem task)
        => task.AssignmentTarget == TaskAssignmentTarget.Offered && task.AssigneeUserId is null;

    /// <summary>
    /// WHO a request was put to, and anything that names them, is for the two people it concerns: the sender and
    /// the person asked while it waits. Everybody else who may read the task — the sender's colleagues through the
    /// unit scope, a read-all holder, a manager once it is accepted — gets no such field at all (not an empty one).
    /// This is the cross-company visibility BL-057 closed; a request must not reopen it.
    /// </summary>
    public static bool MaySeeAddressee(TaskItem task, Guid userId)
        => userId != Guid.Empty && (task.CreatedByUserId == userId || IsOfferedTo(task, userId));

    /// <summary>
    /// A DECLINE — who said no, and why — is the sender's alone, before and after somebody else accepts, and (FIX2 A4)
    /// after the work is handed back or reassigned too: the sender keeps their own request's history. The person who
    /// declined no longer reads the task at all; nobody else ever sees it.
    /// </summary>
    public static bool MaySeeDecline(TaskItem task, Guid userId)
        => WasRequest(task) && userId != Guid.Empty && task.CreatedByUserId == userId;

    /// <summary>
    /// FIX2 (A4) — WAS this task ever a request? <see cref="TaskItem.IsRequest"/> is cleared when the work is handed
    /// back or reassigned (the task is its sender's own again, and the sender may direct it); the unit it was sent
    /// from is recorded once, at creation, on every request and never cleared — the persistent fact.
    /// </summary>
    public static bool WasRequest(TaskItem task)
        => task.IsRequest || task.RequestOriginUnitId is not null;

    /// <summary>
    /// FIX2 (A3) — the rows of WHO WAS ASKED (offered), WHO SAID NO (declined) and WHO WAS TAKEN BACK (withdrawn) are the
    /// sender's alone in the activity feed. Anybody else reading the task — the sender's colleagues, a manager once it
    /// is accepted — sees the work from the acceptance on; "unaddressed, and no withdrawal row" would otherwise tell
    /// them that somebody declined.
    /// </summary>
    public static bool MaySeeRequestRow(TaskItem task, TaskTransitionKind kind, Guid userId)
        => kind is not (TaskTransitionKind.Offered or TaskTransitionKind.Declined or TaskTransitionKind.Withdrawn)
           || MaySeeDecline(task, userId);

    /// <summary>
    /// Decision 3 at EVERY write gate (edit, reassign, cancel, checklist, subtasks, attachments, dependencies): the
    /// sender of an accepted request watches and comments. Null when the caller may write; the refusal otherwise —
    /// one wording and one code for all of them.
    /// </summary>
    public static TaskAssignmentRefusal? RefuseSenderOfAcceptedRequest(TaskItem task, Guid userId)
        => !SenderMayEditAfterAcceptance && IsSenderOfAcceptedRequest(task, userId)
            ? new TaskAssignmentRefusal(403, TaskReasonCodes.RequestSenderCannotDirect,
                "This task was accepted as a request; it is its holder's to change. You can follow it and comment.")
            : null;

    /// <summary>An offer that is still waiting for its answer: no holder yet, and somebody it is addressed to.</summary>
    public static bool IsPendingOffer(TaskItem task)
        => task.AssignmentTarget == TaskAssignmentTarget.Offered
           && task.AssigneeUserId is null
           && task.OfferedToUserId is not null
           && task.Lifecycle is not (TaskLifecycle.Done or TaskLifecycle.Cancelled);

    /// <summary>Is this offer waiting for THIS person's answer?</summary>
    public static bool IsOfferedTo(TaskItem task, Guid userId)
        => userId != Guid.Empty && IsPendingOffer(task) && task.OfferedToUserId == userId;

    /// <summary>
    /// Decision 4, for the team list and the read rule alike: a pending offer addressed to one of the caller's
    /// subordinates. One predicate so the two cannot answer differently.
    /// </summary>
    public static bool TeamSeesPendingOffer(TaskItem task, TaskTeamScope team)
        => RecipientManagerSeesPendingOffer
           && IsPendingOffer(task)
           && team.UserIds.Contains(task.OfferedToUserId!.Value);

    /// <summary>
    /// Decision 3 — the task came from a request, somebody else now holds it, and the caller is the one who asked.
    /// </summary>
    public static bool IsSenderOfAcceptedRequest(TaskItem task, Guid userId)
        => task.IsRequest
           && task.CreatedByUserId == userId
           && task.AssigneeUserId is { } holder
           && holder != userId;
}

/// <summary>A person's home seat: the unit and the legal entity, always read from one and the same seat.</summary>
public sealed record TaskRequestHome(Guid OrganizationUnitId, Guid LegalEntityId);

/// <summary>Who a request may be addressed to, with the two facts the handlers need about them.</summary>
/// <param name="OrganizationUnitId">The unit of their home seat — the task moves there when they accept.</param>
/// <param name="LegalEntityId">That unit's legal entity, recorded on the assignment row.</param>
public sealed record TaskRequestRecipient(Guid UserId, Guid? OrganizationUnitId, Guid? LegalEntityId);

/// <summary>
/// BL-509 — the TASK REQUEST: asking somebody outside the assignment scope to take work on.
///
/// <para><b>Beside BL-057, never through it.</b> A request does not ask <see cref="ITaskAssignmentScopeResolver"/>
/// anything and does not change who may be ASSIGNED work. What it asks is whether the addressee can receive work at
/// all — the eligibility half of <see cref="TaskAssigneeEligibility.Judge"/> (active position, live unit), with no
/// scope — and whether their account is switched on.</para>
///
/// <para><b>One answer for every "no".</b> Not eligible, deactivated, unknown, in another tenant: the sender gets
/// <see cref="TaskReasonCodes.RequestRecipientUnavailable"/> and nothing else, so the answer cannot be used to find
/// out who exists.</para>
/// </summary>
public interface ITaskRequestService
{
    /// <summary>
    /// May the CALLER send a request at all, to this id? The sending key (403) and "not yourself" (400). Nothing
    /// about the addressee is asked here — that is <see cref="CheckRecipientAsync"/>, and it comes LAST.
    /// </summary>
    Task<TaskAssignmentRefusal?> CheckSenderAsync(Guid offeredToUserId, CancellationToken ct);

    /// <summary>
    /// Can this person be asked? The one answer for every "no". Callers ask it as the LAST step before they write,
    /// after every other check on the request: asked earlier, the difference between "refused for this field" and
    /// "refused for this person" would tell the caller whether somebody can be reached, without leaving a trace.
    /// </summary>
    Task<(TaskAssignmentRefusal? Refusal, TaskRequestRecipient? Recipient)> CheckRecipientAsync(
        Guid offeredToUserId, CancellationToken ct);

    /// <summary>A person's home seat — unit and legal entity from the SAME seat — or null when they hold none.</summary>
    Task<TaskRequestHome?> HomeOfAsync(Guid userId, CancellationToken ct);

    /// <summary>The addressee as they stand NOW, or null when they cannot receive a request.</summary>
    Task<TaskRequestRecipient?> ResolveRecipientAsync(Guid userId, CancellationToken ct);

    /// <summary>The legal entity of a person's home seat, or null when they hold none.</summary>
    Task<Guid?> LegalEntityOfAsync(Guid userId, CancellationToken ct);

    /// <summary>FIX2 — the legal entities of several people's home seats, the seats, positions and units read ONCE.</summary>
    Task<IReadOnlyDictionary<Guid, Guid?>> LegalEntitiesOfAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct);

    /// <summary>FIX2 — can work still be filed in this unit (it exists and is not archived)?</summary>
    Task<bool> IsLiveUnitAsync(Guid organizationUnitId, CancellationToken ct);
}

/// <inheritdoc cref="ITaskRequestService"/>
public sealed class TaskRequestService : ITaskRequestService
{
    private readonly ITaskSeatDirectory _seats;
    private readonly IPositionRepository _positions;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly ITaskNotificationRecipientResolver _accounts;
    private readonly IActorPermissionContext _permissions;
    private readonly ICurrentUserContext _currentUser;

    public TaskRequestService(
        ITaskSeatDirectory seats,
        IPositionRepository positions,
        IOrganizationUnitRepository organizationUnits,
        ITaskNotificationRecipientResolver accounts,
        IActorPermissionContext permissions,
        ICurrentUserContext currentUser)
    {
        _seats = seats;
        _positions = positions;
        _organizationUnits = organizationUnits;
        _accounts = accounts;
        _permissions = permissions;
        _currentUser = currentUser;
    }

    public static TaskAssignmentRefusal RecipientUnavailable { get; } = new(
        400, TaskReasonCodes.RequestRecipientUnavailable, "That person cannot be sent a request.");

    public async Task<TaskAssignmentRefusal?> CheckSenderAsync(Guid offeredToUserId, CancellationToken ct)
    {
        // Asked HERE as well as on the controller: creating a task needs only Create, and the request rides the
        // create endpoint — the same reason TaskAssignmentGuard asks Assign itself.
        if (!_permissions.Has(TaskPermissions.RequestsCreate))
        {
            return new TaskAssignmentRefusal(
                403, DocumentManagementReasonCodes.PermissionDenied, "Permission denied.");
        }

        if (offeredToUserId == _currentUser.UserId)
        {
            return new TaskAssignmentRefusal(
                400, TaskReasonCodes.RequestToSelf, "A request cannot be sent to yourself.");
        }

        if (TaskRequestPolicy.SenderNeedsActiveSeat && await HomeOfAsync(_currentUser.UserId, ct) is null)
        {
            return new TaskAssignmentRefusal(
                403, DocumentManagementReasonCodes.PermissionDenied, "Permission denied.");
        }

        return null;
    }

    public async Task<(TaskAssignmentRefusal? Refusal, TaskRequestRecipient? Recipient)> CheckRecipientAsync(
        Guid offeredToUserId, CancellationToken ct)
    {
        var recipient = await ResolveRecipientAsync(offeredToUserId, ct);
        return recipient is null ? (RecipientUnavailable, null) : (null, recipient);
    }

    public async Task<TaskRequestHome?> HomeOfAsync(Guid userId, CancellationToken ct)
        => await ResolveHomeUnitAsync(userId, ct) is { } unit ? new TaskRequestHome(unit.Id, unit.LegalEntityId) : null;

    public async Task<TaskRequestRecipient?> ResolveRecipientAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        /*
         * FIX3 (A6, CT decision) — EVERY "NO" DOES THE SAME WORK. This used to stop at the first no: a person with no
         * seat cost one query, a person with a seat and a switched-off account cost the seat, position and unit reads
         * plus a call to AuthService — and the difference in time told the sender whether somebody holds an active
         * seat in this tenant. So the seats, the positions, the units and the account are read for EVERYBODY, and the
         * answers are combined only after all four are in. No artificial delay: the same calls, not the same clock.
         */
        var seats = (await _seats.ActiveForUserAsync(userId, ct)).OrderBy(seat => seat.AssignmentType).ToList();
        var positionById = (await _positions.GetAllAsync(ct)).ToDictionary(position => position.Id);
        var unitById = (await _organizationUnits.GetAllAsync(ct)).ToDictionary(unit => unit.Id);

        /*
         * The account itself. AuthService's contact read is tenant-first and omits a deactivated user, so "not in
         * the answer" covers switched off, unknown and another tenant's user at once. It is best-effort by
         * contract: AuthService being unreachable answers nobody, and a request is then REFUSED — the safe side.
         */
        var reachable = await _accounts.ResolveAsync([userId], ct);

        var home = Judge(seats, positionById, unitById);
        if (home is null && TaskRequestPolicy.RecipientNeedsActiveSeat)
        {
            return null;
        }

        if (reachable.All(account => account.UserId != userId))
        {
            return null;
        }

        return new TaskRequestRecipient(userId, home?.Id, home?.LegalEntityId);
    }

    public async Task<Guid?> LegalEntityOfAsync(Guid userId, CancellationToken ct)
        => (await ResolveHomeUnitAsync(userId, ct))?.LegalEntityId;

    public async Task<IReadOnlyDictionary<Guid, Guid?>> LegalEntitiesOfAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var result = new Dictionary<Guid, Guid?>();
        var distinct = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0)
        {
            return result;
        }

        // The whole position and unit tables once for everybody, not once per person.
        var positionById = (await _positions.GetAllAsync(ct)).ToDictionary(position => position.Id);
        var unitById = (await _organizationUnits.GetAllAsync(ct)).ToDictionary(unit => unit.Id);
        foreach (var userId in distinct)
        {
            result[userId] = (await HomeUnitAsync(userId, positionById, unitById, ct))?.LegalEntityId;
        }

        return result;
    }

    public async Task<bool> IsLiveUnitAsync(Guid organizationUnitId, CancellationToken ct)
        => await _organizationUnits.GetByIdAsync(organizationUnitId, ct) is { IsArchived: false };

    /// <summary>
    /// The unit of the person's best usable seat, PRIMARY first — the eligibility half of the shared rule, with
    /// no scope (see the class summary).
    /// </summary>
    private async Task<Domain.Entities.Organization.OrganizationUnit?> ResolveHomeUnitAsync(
        Guid userId, CancellationToken ct)
    {
        var seats = (await _seats.ActiveForUserAsync(userId, ct)).OrderBy(seat => seat.AssignmentType).ToList();
        if (seats.Count == 0)
        {
            return null;
        }

        var positionById = (await _positions.GetAllAsync(ct)).ToDictionary(position => position.Id);
        var unitById = (await _organizationUnits.GetAllAsync(ct)).ToDictionary(unit => unit.Id);
        return Judge(seats, positionById, unitById);
    }

    private async Task<Domain.Entities.Organization.OrganizationUnit?> HomeUnitAsync(
        Guid userId,
        Dictionary<Guid, Domain.Entities.Organization.Position> positionById,
        Dictionary<Guid, Domain.Entities.Organization.OrganizationUnit> unitById,
        CancellationToken ct)
        => Judge((await _seats.ActiveForUserAsync(userId, ct)).OrderBy(seat => seat.AssignmentType).ToList(),
            positionById, unitById);

    private static Domain.Entities.Organization.OrganizationUnit? Judge(
        IReadOnlyList<Domain.Entities.Organization.PositionAssignment> seats,
        Dictionary<Guid, Domain.Entities.Organization.Position> positionById,
        Dictionary<Guid, Domain.Entities.Organization.OrganizationUnit> unitById)
    {
        foreach (var seat in seats)
        {
            if (TaskAssigneeEligibility.Judge(
                    positionById.GetValueOrDefault(seat.PositionId), unitById, scope: null, out var unit)
                == TaskAssigneeVerdict.Assignable)
            {
                return unit;
            }
        }

        return null;
    }
}
