using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// BL-023 PART A — WHO is in my team, for the "Ekibim" scope selector.
///
/// <para><b>Three concepts that must not blur.</b> Work assigned TO me is <i>İşlerim</i>; work I handed to
/// somebody else is the Outbox (BL-016); my subordinates' OWN tasks — the ones I never assigned — are what this
/// resolves. A manager could not see the third at all.</para>
///
/// <para><b>It walks nothing.</b> The descent over <c>Position.ReportsToPositionId</c> is already done, once,
/// cycle-safe and depth-bounded, by <see cref="ITaskAssignmentScopeResolver"/> (BL-057). This turns those
/// position ids into the USER ids that hold them, and applies the SAME
/// <see cref="TaskAssignmentScope.Allows"/> the assignment pickers use — so a subordinate in another company is
/// in my team exactly when the chain reaches them, and somebody outside my scope never is.</para>
/// </summary>
public interface ITaskTeamResolver
{
    Task<TaskTeamScope> ResolveTeamAsync(CancellationToken ct);
}

/// <summary>
/// My team, and whether I HAVE one.
///
/// <para><see cref="HasTeam"/> exists so the caller can tell "nobody reports to you" from "your team's work is
/// all done". A silent empty list is the defect this project has corrected five times; the UI disables the
/// selector and says why rather than rendering an unexplained blank.</para>
/// </summary>
public sealed record TaskTeamScope(bool HasTeam, IReadOnlyCollection<Guid> UserIds)
{
    public static TaskTeamScope None { get; } = new(false, []);
}

/// <inheritdoc cref="ITaskTeamResolver"/>
public sealed class TaskTeamResolver : ITaskTeamResolver
{
    private readonly ITaskAssignmentScopeResolver _scopes;
    private readonly IPositionAssignmentRepository _assignments;

    public TaskTeamResolver(
        ITaskAssignmentScopeResolver scopes,
        IPositionAssignmentRepository assignments)
    {
        _scopes = scopes;
        _assignments = assignments;
    }

    public async Task<TaskTeamScope> ResolveTeamAsync(CancellationToken ct)
    {
        var scope = await _scopes.ResolveAsync(ct);
        var subordinatePositions = scope.SubordinatePositionIds;

        // "You have no reports" is a real answer and a different one from "your team has no open work".
        if (subordinatePositions.Count == 0)
        {
            return TaskTeamScope.None;
        }

        var now = DateTimeOffset.UtcNow;
        var userIds = (await _assignments.GetAllAsync(ct))
            // Half-open interval, the same one every other reader of this table uses.
            .Where(a => !a.IsCancelled
                        && a.EffectiveFrom <= now
                        && (a.EffectiveTo is null || a.EffectiveTo > now))
            .Where(a => subordinatePositions.Contains(a.PositionId))
            .Select(a => a.UserId)
            .Distinct()
            .ToList();

        // HasTeam reports the ORG CHART, not the headcount: a manager whose subordinate position is currently
        // vacant still has a team, and telling them "you have no team" would be wrong about their org.
        return new TaskTeamScope(HasTeam: true, UserIds: userIds);
    }
}
