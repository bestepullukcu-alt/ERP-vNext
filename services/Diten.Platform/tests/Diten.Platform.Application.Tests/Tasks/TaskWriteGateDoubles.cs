using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WCN-TASK-REQUEST-01 FIX2 — the write gate for hand-built handlers.
/// <para><see cref="Open"/> is for unit tests about something else (a lifecycle rule, a checklist verb): it passes every
/// task that exists, exactly like the handlers before the gate. The gate itself is tested over the production
/// <see cref="TaskWriteGate"/> (<see cref="Real"/>) — in the request HTTP suite and its own tests.</para>
/// </summary>
public static class TaskWriteGateDoubles
{
    public static ITaskWriteGate Open { get; } = new OpenGate();

    public static ITaskWriteGate Real(ITaskReadAccessPolicy readAccess, ICurrentUserContext currentUser)
        => new TaskWriteGate(readAccess, currentUser);

    /// <summary>The production gate over a read rule that admits everybody: the RELATIONSHIP rules of a request (sender of
    /// an accepted request 403, the person a request waits on 409) are real, the read base is not under test.</summary>
    public static ITaskWriteGate ReadableTo(ICurrentUserContext currentUser) => new TaskWriteGate(new EveryoneReads(), currentUser);

    private sealed class EveryoneReads : ITaskReadAccessPolicy
    {
        public Task<bool> CanReadAsync(TaskItem task, Guid actorUserId, CancellationToken ct) => Task.FromResult(true);

        public Task<IReadOnlySet<Guid>> ResolveDataLegCandidatesAsync(TaskItem task, CancellationToken ct)
            => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
    }

    private sealed class OpenGate : ITaskWriteGate
    {
        public Task<TaskAssignmentRefusal?> CheckAsync(TaskItem? task, TaskWriteKind kind, CancellationToken ct)
            => Task.FromResult(task is null ? TaskWriteGate.NotFound : null);
    }
}
