using System.Collections.Concurrent;
using System.Reflection;
using MediatR;

namespace Diten.BuildingBlocks.CentralAudit;

/// <summary>
/// BL-501 — the shared pipeline behavior. A request that carries <see cref="ICentralAuditDeclaration"/> and SUCCEEDS
/// leaves exactly one event in the service's outbox; a request that throws or answers a failed envelope leaves none.
///
/// <para>"Succeeded" = no exception AND, when the response has a public <c>bool IsSuccessful</c> (the repo's
/// <c>Response&lt;T&gt;</c> envelope — Inventory, MDM, Procurement, Manufacturing), that flag is true. A response without
/// the flag is judged by the exception alone.</para>
///
/// <para>Registered by <see cref="CentralAuditServiceCollectionExtensions.AddCentralAudit{TScope}"/>, after the service's own
/// behaviors (innermost: it wraps the handler only, and sees the handler's own exception).</para>
/// </summary>
public sealed class CentralAuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> SuccessFlags = new();
    private readonly CentralAuditRecorder _recorder;

    public CentralAuditBehavior(CentralAuditRecorder recorder)
    {
        _recorder = recorder;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ICentralAuditDeclaration declaration)
        {
            return await next();
        }

        var pending = _recorder.Begin(declaration, typeof(TRequest).Name);
        if (pending is null)
        {
            return await next();
        }

        TResponse response;
        try
        {
            response = await next();
        }
        catch
        {
            await _recorder.AbandonAsync(pending);
            throw;
        }

        if (!Succeeded(response))
        {
            await _recorder.AbandonAsync(pending);
            return response;
        }

        await _recorder.CompleteAsync(pending, declaration.CentralAuditEntityId(response), cancellationToken);
        return response;
    }

    internal static bool Succeeded(object? response)
    {
        if (response is null)
        {
            return true;
        }

        var flag = SuccessFlags.GetOrAdd(
            response.GetType(),
            type => type.GetProperty("IsSuccessful", BindingFlags.Public | BindingFlags.Instance) is { PropertyType: var t } property
                    && t == typeof(bool)
                ? property
                : null);

        return flag is null || (bool)flag.GetValue(response)!;
    }
}
