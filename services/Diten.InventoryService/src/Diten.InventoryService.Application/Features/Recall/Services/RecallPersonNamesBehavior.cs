using System.Reflection;
using Diten.InventoryService.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Application.Features.Recall.Services;

/// <summary>
/// WP-MVP1-NAMES1 (RECALL 1.8.0) — the ONE place every recall answer gets the names of its people (<see cref="RecallActorNames"/>), queries and
/// commands alike, so no handler can forget it and no answer reads per row. Only a SUCCESSFUL <c>Response&lt;T&gt;</c> of a recall answer type
/// that carries people is touched (decided once per closed type); every other request passes through without a lookup. A failure of the name
/// read never fails the answer: the people stay unnamed (the screen says "unknown user") and the failure is logged.
/// </summary>
public sealed class RecallPersonNamesBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private static readonly MethodInfo? Namer = Build();

    private readonly IServiceProvider _services;
    private readonly ILogger<RecallPersonNamesBehavior<TRequest, TResponse>> _logger;

    public RecallPersonNamesBehavior(IServiceProvider services, ILogger<RecallPersonNamesBehavior<TRequest, TResponse>> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();
        if (Namer is null || response is null) return response;
        try
        {
            return await (Task<TResponse>)Namer.Invoke(null, [response, _services.GetRequiredService<RecallActorNames>(), cancellationToken])!;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "recall.names.unavailable {RequestName}", typeof(TRequest).Name);
            return response;
        }
    }

    private static MethodInfo? Build()
    {
        var type = typeof(TResponse);
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Response<>)) return null;
        var data = type.GetGenericArguments()[0];
        return RecallActorNames.Names(data)
            ? typeof(RecallPersonNamesBehavior<TRequest, TResponse>).GetMethod(nameof(NameAsync), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(data)
            : null;
    }

    private static async Task<Response<T>> NameAsync<T>(Response<T> response, RecallActorNames names, CancellationToken cancellationToken)
        => response is { IsSuccessful: true, Data: { } data }
            ? Response<T>.Success((T)await names.NameAsync(data, cancellationToken), response.StatusCode)
            : response;
}
