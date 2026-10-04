using System.Reflection;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// BL-533 — a deterministic interleaving point for race tests. Wraps a repository interface so that the FIRST call to
/// one named read returns its document and then stops: <see cref="Reached"/> completes, and the caller does not get the
/// document until the test calls <see cref="Release"/>. Between the two, the test runs the competing write to the end.
/// Every other call (and every later call to the same read) passes straight through to the real repository.
/// </summary>
public sealed class ReadBarrier
{
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _armed;

    /// <param name="armed">False when the wrapped repository also serves the test's own set-up calls — the test then
    /// calls <see cref="Arm"/> right before the request that must stop.</param>
    public ReadBarrier(bool armed = true) => _armed = armed ? 1 : 0;

    public Task Reached => _reached.Task;

    public void Arm() => Interlocked.Exchange(ref _armed, 1);

    public void Release() => _release.TrySetResult();

    public T Wrap<T>(T inner, string readMethod) where T : class
    {
        var proxy = DispatchProxy.Create<T, BarrierProxy<T>>();
        var state = (BarrierProxy<T>)(object)proxy;
        state.Inner = inner;
        state.ReadMethod = readMethod;
        state.Barrier = this;
        return proxy;
    }

    internal bool TryTake() => Interlocked.Exchange(ref _armed, 0) == 1;

    internal async Task<TResult> HoldAsync<TResult>(Task<TResult> read)
    {
        var document = await read;
        _reached.TrySetResult();
        await _release.Task.WaitAsync(TimeSpan.FromSeconds(30));
        return document;
    }
}

public class BarrierProxy<T> : DispatchProxy where T : class
{
    private static readonly MethodInfo Hold = typeof(ReadBarrier).GetMethod(
        nameof(ReadBarrier.HoldAsync), BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal T Inner { get; set; } = null!;
    internal string ReadMethod { get; set; } = string.Empty;
    internal ReadBarrier Barrier { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        object? result;
        try
        {
            result = targetMethod!.Invoke(Inner, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }

        if (targetMethod.Name != ReadMethod || result is null || !Barrier.TryTake())
        {
            return result;
        }

        var resultType = targetMethod.ReturnType.GetGenericArguments()[0];
        return Hold.MakeGenericMethod(resultType).Invoke(Barrier, [result]);
    }
}
