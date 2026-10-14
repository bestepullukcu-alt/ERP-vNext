using System.Collections.Concurrent;
using System.Reflection;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// BL-533 — a deterministic interleaving point for WRITE races, the twin of <see cref="ReadBarrier"/>. Wraps a repository
/// interface so a test can run its own action at an exact point of a handler's run: right BEFORE one named call (the
/// competing write lands between the handler's read and its write), right AFTER it (it lands after the handler's own
/// write), or INSTEAD of it (the store's answer is dictated, e.g. a refused write). Every rule names the method, a
/// predicate over the call's arguments, and whether it fires once or every time; every call that matches no rule passes
/// straight through to the real repository. Overloads share a name: a predicate tells them apart by their arguments.
/// </summary>
public sealed class CallHook
{
    private readonly object _gate = new();
    private readonly List<Rule> _rules = [];
    private readonly ConcurrentDictionary<string, int> _calls = new(StringComparer.Ordinal);

    internal enum Moment
    {
        Before,
        After,
        Instead
    }

    internal sealed class Rule
    {
        public required string Method { get; init; }
        public required Moment When { get; init; }
        public required Func<object?[], bool> Matches { get; init; }
        public Func<Task>? Action { get; init; }
        public object? Answer { get; init; }
        public required bool Once { get; init; }
        public bool Spent { get; set; }
    }

    /// <summary>Runs <paramref name="action"/> before the matching call goes to the real repository.</summary>
    public void Before(string method, Func<object?[], bool> matches, Func<Task> action, bool once = true)
        => Add(new Rule { Method = method, When = Moment.Before, Matches = matches, Action = action, Once = once });

    /// <summary>Runs <paramref name="action"/> after the matching call returned from the real repository.</summary>
    public void After(string method, Func<object?[], bool> matches, Func<Task> action, bool once = true)
        => Add(new Rule { Method = method, When = Moment.After, Matches = matches, Action = action, Once = once });

    /// <summary>The matching call never reaches the real repository: it answers <paramref name="answer"/>.</summary>
    public void Instead(string method, Func<object?[], bool> matches, object? answer, bool once = true)
        => Add(new Rule { Method = method, When = Moment.Instead, Matches = matches, Answer = answer, Once = once });

    /// <summary>How many calls of <paramref name="method"/> (any overload) the wrapped repositories received.</summary>
    public int CallsTo(string method) => _calls.GetValueOrDefault(method);

    /// <summary>The first argument of type <typeparamref name="TArg"/> — how a predicate finds the entity in either overload.</summary>
    public static TArg? Arg<TArg>(object?[] args) where TArg : class => args.OfType<TArg>().FirstOrDefault();

    public T Wrap<T>(T inner) where T : class
    {
        var proxy = DispatchProxy.Create<T, CallHookProxy<T>>();
        var state = (CallHookProxy<T>)(object)proxy;
        state.Inner = inner;
        state.Hook = this;
        return proxy;
    }

    private void Add(Rule rule)
    {
        lock (_gate)
        {
            _rules.Add(rule);
        }
    }

    internal void Count(string method) => _calls.AddOrUpdate(method, 1, (_, n) => n + 1);

    internal Rule? Take(string method, Moment when, object?[] args)
    {
        lock (_gate)
        {
            var rule = _rules.FirstOrDefault(r => !r.Spent && r.When == when && r.Method == method && r.Matches(args));
            if (rule is { Once: true })
            {
                rule.Spent = true;
            }

            return rule;
        }
    }
}

public class CallHookProxy<T> : DispatchProxy where T : class
{
    private static readonly MethodInfo RunTyped = typeof(CallHookProxy<T>).GetMethod(
        nameof(RunTypedAsync), BindingFlags.Instance | BindingFlags.NonPublic)!;

    internal T Inner { get; set; } = null!;
    internal CallHook Hook { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;
        var arguments = args ?? [];
        Hook.Count(method.Name);

        var returnType = method.ReturnType;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            return RunTyped.MakeGenericMethod(returnType.GetGenericArguments()[0]).Invoke(this, [method, arguments]);
        }

        if (returnType == typeof(Task))
        {
            return RunUntypedAsync(method, arguments);
        }

        return Call(method, arguments);
    }

    private async Task<TResult> RunTypedAsync<TResult>(MethodInfo method, object?[] args)
    {
        var instead = Hook.Take(method.Name, CallHook.Moment.Instead, args);
        if (instead is not null)
        {
            return (TResult)instead.Answer!;
        }

        var before = Hook.Take(method.Name, CallHook.Moment.Before, args);
        if (before?.Action is not null)
        {
            await before.Action();
        }

        var result = await (Task<TResult>)Call(method, args)!;

        var after = Hook.Take(method.Name, CallHook.Moment.After, args);
        if (after?.Action is not null)
        {
            await after.Action();
        }

        return result;
    }

    private async Task RunUntypedAsync(MethodInfo method, object?[] args)
    {
        if (Hook.Take(method.Name, CallHook.Moment.Instead, args) is not null)
        {
            return;
        }

        var before = Hook.Take(method.Name, CallHook.Moment.Before, args);
        if (before?.Action is not null)
        {
            await before.Action();
        }

        await (Task)Call(method, args)!;

        var after = Hook.Take(method.Name, CallHook.Moment.After, args);
        if (after?.Action is not null)
        {
            await after.Action();
        }
    }

    private object? Call(MethodInfo method, object?[] args)
    {
        try
        {
            return method.Invoke(Inner, args);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }
}
