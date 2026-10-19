using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WCN-TASK-REQUEST-01 FIX3 — the PRODUCTION read rule (<see cref="TaskReadAccessPolicy"/>) over in-memory stores, for
/// suites outside the rule's own (meetings, the write gate). Only the data legs answer unless a test hands in more: no
/// scope, no team, no read-all — so "who reads this task" is exactly the task's own people, and a test that needs a
/// task to be UNREADABLE gets one by naming somebody else as its holder and creator.
/// </summary>
internal static class RealTaskReadRule
{
    internal static TaskReadAccessPolicy Over(
        ITaskItemRepository tasks,
        Guid caller,
        FakeTaskWatcherRepository? watchers = null,
        FakeActorPermissions? permissions = null,
        ITaskTeamResolver? team = null)
        => new(
            tasks,
            watchers ?? new FakeTaskWatcherRepository(),
            new FakeTaskNotificationService(),
            new FakeOrganizationUnitRepository(),
            new NoScope(),
            team ?? new FakeTaskTeamResolver(),
            permissions ?? TaskActors.None(),
            new FakeCurrentUserContext(caller));

    private sealed class NoScope : ITaskAssignmentScopeResolver
    {
        public Task<TaskAssignmentScope> ResolveAsync(CancellationToken ct) => Task.FromResult(TaskAssignmentScope.Empty);
    }
}
