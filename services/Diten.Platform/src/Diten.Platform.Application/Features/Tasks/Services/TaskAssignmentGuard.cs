using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementContract;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>A write refused because of WHO it hands work to. Carries the wire shape so every caller answers
/// with the same status and code.</summary>
public sealed record TaskAssignmentRefusal(int StatusCode, string ReasonCode, string Message);

/// <summary>
/// BL-057 at the WRITE — the server says what the picker says, no more and no less.
///
/// <para><b>The defect this closes.</b> "Who may I hand work to" was enforced only by the two pickers. Nothing on
/// the write side asked it again, so a client posting straight to the API could create a task for — or pool work
/// to, or schedule recurring work for — somebody the picker would never have shown, including a person in another
/// company. The reassign endpoint checked the position and unit but not the scope; create checked nothing for a
/// person and only half of it for a pool; recurrence rules checked nothing.</para>
///
/// <para><b>One rule, not a second one.</b> Eligibility and scope are asked of
/// <see cref="TaskAssigneeEligibility.Judge"/> and <see cref="ITaskAssignmentScopeResolver"/> — the same two calls
/// the pickers make, over the same repositories. A candidate the picker lists is accepted here and a candidate it
/// does not list is refused here; <c>TaskAssignmentWriteGuardTests</c> measures that the two sets are equal.</para>
///
/// <para><b>What it deliberately does not say.</b> "Out of scope" and "not eligible" produce the SAME refusal. Telling
/// them apart would tell the caller that a person they cannot reach exists and holds a live position — exactly
/// what the scope withholds, and what the picker's exclusion summary is careful to report only as a count.</para>
/// </summary>
public interface ITaskAssignmentGuard
{
    /// <summary>
    /// Handing work to one person: the ASSIGN permission (403), then an active position in a live unit inside the
    /// actor's scope (400 <see cref="TaskReasonCodes.AssigneeNotAssignable"/>). No self exemption — the caller
    /// decides whether naming oneself is an assignment at all (see <see cref="CheckTargetAsync"/>).
    /// </summary>
    Task<TaskAssignmentRefusal?> CheckPersonAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Pooling work to a position: active, in a live unit, inside the actor's scope (400
    /// <see cref="TaskReasonCodes.PositionNotAssignable"/>). No ASSIGN permission — the pool picker does not ask for
    /// one either, and the server must not be narrower than the screen.
    /// </summary>
    Task<TaskAssignmentRefusal?> CheckPoolAsync(Guid positionId, CancellationToken ct);

    /// <summary>
    /// An assignment INTENT as a create form states it. SelfAssigned, and a Person that is the actor, are not an
    /// assignment to anybody else and are not checked — giving yourself work needs no authority over others.
    /// Call only after <see cref="TaskAssignmentIntentRules.Validate"/> has accepted the triple.
    /// </summary>
    Task<TaskAssignmentRefusal?> CheckTargetAsync(
        TaskAssignmentTarget target, Guid? assigneeUserId, Guid? poolPositionId, CancellationToken ct);
}

/// <inheritdoc cref="ITaskAssignmentGuard"/>
public sealed class TaskAssignmentGuard : ITaskAssignmentGuard
{
    private readonly ITaskSeatDirectory _seats;
    private readonly IPositionRepository _positions;
    private readonly IOrganizationUnitRepository _organizationUnits;
    private readonly ITaskAssignmentScopeResolver _scopes;
    private readonly IActorPermissionContext _permissions;
    private readonly ICurrentUserContext _currentUser;

    public TaskAssignmentGuard(
        ITaskSeatDirectory seats,
        IPositionRepository positions,
        IOrganizationUnitRepository organizationUnits,
        ITaskAssignmentScopeResolver scopes,
        IActorPermissionContext permissions,
        ICurrentUserContext currentUser)
    {
        _seats = seats;
        _positions = positions;
        _organizationUnits = organizationUnits;
        _scopes = scopes;
        _permissions = permissions;
        _currentUser = currentUser;
    }

    public async Task<TaskAssignmentRefusal?> CheckPersonAsync(Guid userId, CancellationToken ct)
    {
        /*
         * The permission the people picker is gated by. Asked HERE rather than only on the controller because two
         * routes reach an assignment and only one of them carries [HasPermission(Assign)]: creating a task needs
         * only Create, and without this a user who may create but not assign could name anybody through the body.
         *
         * The same code the [HasPermission] filter answers with, so a refusal from here and one from the filter
         * are the same response on the wire — the client has no reason to tell them apart.
         */
        if (!_permissions.Has(TaskPermissions.Assign))
        {
            return new TaskAssignmentRefusal(
                403, DocumentManagementReasonCodes.PermissionDenied, "Permission denied.");
        }

        var seats = (await _seats.ActiveAsync(ct)).Where(seat => seat.UserId == userId).ToList();
        if (seats.Count > 0)
        {
            var positionById = (await _positions.GetAllAsync(ct)).ToDictionary(position => position.Id);
            var unitById = (await _organizationUnits.GetAllAsync(ct)).ToDictionary(unit => unit.Id);
            var scope = await _scopes.ResolveAsync(ct);

            // ANY seat is enough, exactly as the picker lists a person by their best seat.
            if (seats.Any(seat => TaskAssigneeEligibility.Judge(
                    positionById.GetValueOrDefault(seat.PositionId), unitById, scope, out _)
                == TaskAssigneeVerdict.Assignable))
            {
                return null;
            }
        }

        return new TaskAssignmentRefusal(
            400, TaskReasonCodes.AssigneeNotAssignable, "That person cannot be assigned work.");
    }

    public async Task<TaskAssignmentRefusal?> CheckPoolAsync(Guid positionId, CancellationToken ct)
    {
        // GetAllAsync rather than GetByIdAsync: the SAME tenant-filtered read the pool picker enumerates, so a
        // position the picker cannot see cannot be accepted here through a different lookup.
        var position = (await _positions.GetAllAsync(ct)).FirstOrDefault(candidate => candidate.Id == positionId);
        var unitById = (await _organizationUnits.GetAllAsync(ct)).ToDictionary(unit => unit.Id);
        var scope = await _scopes.ResolveAsync(ct);

        return TaskAssigneeEligibility.Judge(position, unitById, scope, out _) == TaskAssigneeVerdict.Assignable
            ? null
            : new TaskAssignmentRefusal(
                400, TaskReasonCodes.PositionNotAssignable, "The selected position is not assignable.");
    }

    public Task<TaskAssignmentRefusal?> CheckTargetAsync(
        TaskAssignmentTarget target, Guid? assigneeUserId, Guid? poolPositionId, CancellationToken ct)
        => target switch
        {
            TaskAssignmentTarget.Person when assigneeUserId is { } person && person != _currentUser.UserId
                => CheckPersonAsync(person, ct),
            TaskAssignmentTarget.PositionPool when poolPositionId is { } pool
                => CheckPoolAsync(pool, ct),
            _ => Task.FromResult<TaskAssignmentRefusal?>(null)
        };
}
