using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// BL-023 PART B — is this assignment going UP?
///
/// <para>Downward and sideways work is an ORDER and stays one. Upward work is a REQUEST: a subordinate cannot
/// instruct their own manager, and the manager has to be able to refuse. SAP and Oracle both model it that way
/// for the same reason.</para>
///
/// <para><b>No new chain walk.</b> BL-057's resolver already emits
/// <see cref="Common.Authorization.EntitlementDataScopeKind.ManagerChain"/> — the positions ABOVE the actor.
/// That direction was the wrong one for assignability (which needed the descent, derived there); it is exactly
/// the right one here, so this reads the scope as it stands. A second ascent over
/// <c>Position.ReportsToPositionId</c> would put two truths on one column and they would drift.</para>
///
/// <para><b>Absence of a chain is not evidence of one.</b> Somebody who is neither above nor below the actor —
/// another department, another company — is NOT upward. Treating "not a subordinate" as "a superior" would turn
/// every ordinary cross-team assignment into a request nobody asked for.</para>
/// </summary>
public interface ITaskAssignmentDirection
{
    Task<bool> IsUpwardAsync(Guid targetUserId, CancellationToken ct);
}

/// <inheritdoc cref="ITaskAssignmentDirection"/>
public sealed class TaskAssignmentDirection : ITaskAssignmentDirection
{
    private readonly ITaskAssignmentScopeResolver _scopes;
    private readonly IPositionAssignmentRepository _assignments;
    private readonly ICurrentUserContext _currentUser;

    public TaskAssignmentDirection(
        ITaskAssignmentScopeResolver scopes,
        IPositionAssignmentRepository assignments,
        ICurrentUserContext currentUser)
    {
        _scopes = scopes;
        _assignments = assignments;
        _currentUser = currentUser;
    }

    public async Task<bool> IsUpwardAsync(Guid targetUserId, CancellationToken ct)
    {
        // Assigning to yourself is never upward, whatever the chart says about the position you hold.
        if (targetUserId == Guid.Empty || targetUserId == _currentUser.UserId)
        {
            return false;
        }

        var scope = await _scopes.ResolveAsync(ct);
        var managerPositions = scope.ManagerChainPositionIds;
        if (managerPositions.Count == 0)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;

        // Upward exactly when the target currently HOLDS one of the positions above me. Half-open interval, the
        // same one every other reader of this table uses.
        return (await _assignments.GetAllAsync(ct))
            .Any(a => a.UserId == targetUserId
                      && !a.IsCancelled
                      && a.EffectiveFrom <= now
                      && (a.EffectiveTo is null || a.EffectiveTo > now)
                      && managerPositions.Contains(a.PositionId));
    }
}
