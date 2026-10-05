using System.Reflection;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>WP-WCN-TASK-REQUEST-01 FIX2 (A5) — what a test wants to happen right before the next task write.</summary>
public sealed class TaskRepositoryHooks
{
    /// <summary>Runs ONCE, before the next <see cref="ITaskItemRepository.UpdateAsync"/> it matches, then disarms itself.</summary>
    public Func<TaskItem, Task>? BeforeUpdate { get; set; }

    /// <summary>Which update the hook waits for (null = the next one).</summary>
    public Func<TaskItem, bool>? When { get; set; }
}

/// <summary>
/// The real task store with a seam: every call is forwarded unchanged, except that an armed
/// <see cref="TaskRepositoryHooks.BeforeUpdate"/> runs before the matching update — the deterministic way to put one
/// request's write between another request's read and its write.
/// </summary>
public class HookedTaskRepository : DispatchProxy
{
    private ITaskItemRepository _inner = null!;
    private TaskRepositoryHooks _hooks = null!;

    public static ITaskItemRepository Wrap(ITaskItemRepository inner, TaskRepositoryHooks hooks)
    {
        var proxy = Create<ITaskItemRepository, HookedTaskRepository>();
        var self = (HookedTaskRepository)(object)proxy;
        self._inner = inner;
        self._hooks = hooks;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod!.Name == nameof(ITaskItemRepository.UpdateAsync)
            && args is [TaskItem task, int expectedVersion, CancellationToken ct]
            && _hooks.BeforeUpdate is { } hook
            && (_hooks.When?.Invoke(task) ?? true))
        {
            _hooks.BeforeUpdate = null;
            _hooks.When = null;
            return HookedUpdateAsync(hook, task, expectedVersion, ct);
        }

        try
        {
            return targetMethod.Invoke(_inner, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private async Task<bool> HookedUpdateAsync(Func<TaskItem, Task> hook, TaskItem task, int expectedVersion, CancellationToken ct)
    {
        await hook(task);
        return await _inner.UpdateAsync(task, expectedVersion, ct);
    }
}
