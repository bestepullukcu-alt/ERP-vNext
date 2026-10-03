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
    /// May the CALLER send a request to this person? Null when yes. Asks the sending key (403), then self (400),
    /// then the addressee (400, the one answer).
    /// </summary>
    Task<(TaskAssignmentRefusal? Refusal, TaskRequestRecipient? Recipient)> CheckSendAsync(
        Guid offeredToUserId, CancellationToken ct);

    /// <summary>The addressee as they stand NOW, or null when they cannot receive a request.</summary>
    Task<TaskRequestRecipient?> ResolveRecipientAsync(Guid userId, CancellationToken ct);

    /// <summary>The legal entity of a person's home seat, or null when they hold none.</summary>
    Task<Guid?> LegalEntityOfAsync(Guid userId, CancellationToken ct);
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

    public async Task<(TaskAssignmentRefusal? Refusal, TaskRequestRecipient? Recipient)> CheckSendAsync(
        Guid offeredToUserId, CancellationToken ct)
    {
        // Asked HERE as well as on the controller: creating a task needs only Create, and the request rides the
        // create endpoint — the same reason TaskAssignmentGuard asks Assign itself.
        if (!_permissions.Has(TaskPermissions.RequestsCreate))
        {
            return (new TaskAssignmentRefusal(
                403, DocumentManagementReasonCodes.PermissionDenied, "Permission denied."), null);
        }

        if (offeredToUserId == _currentUser.UserId)
        {
            return (new TaskAssignmentRefusal(
                400, TaskReasonCodes.RequestToSelf, "A request cannot be sent to yourself."), null);
        }

        if (TaskRequestPolicy.SenderNeedsActiveSeat
            && (await _seats.ActiveForUserAsync(_currentUser.UserId, ct)).Count == 0)
        {
            return (new TaskAssignmentRefusal(
                403, DocumentManagementReasonCodes.PermissionDenied, "Permission denied."), null);
        }

        var recipient = await ResolveRecipientAsync(offeredToUserId, ct);
        return recipient is null ? (RecipientUnavailable, null) : (null, recipient);
    }

    public async Task<TaskRequestRecipient?> ResolveRecipientAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        var home = await ResolveHomeUnitAsync(userId, ct);
        if (home is null && TaskRequestPolicy.RecipientNeedsActiveSeat)
        {
            return null;
        }

        /*
         * The account itself. AuthService's contact read is tenant-first and omits a deactivated user, so "not in
         * the answer" covers switched off, unknown and another tenant's user at once. It is best-effort by
         * contract: AuthService being unreachable answers nobody, and a request is then REFUSED — the safe side.
         */
        var reachable = await _accounts.ResolveAsync([userId], ct);
        if (reachable.All(account => account.UserId != userId))
        {
            return null;
        }

        return new TaskRequestRecipient(userId, home?.Id, home?.LegalEntityId);
    }

    public async Task<Guid?> LegalEntityOfAsync(Guid userId, CancellationToken ct)
        => (await ResolveHomeUnitAsync(userId, ct))?.LegalEntityId;

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
