using Diten.MdmService.Application.Contracts.Audit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.MdmService.Application.Behaviors;

/// <summary>
/// After an <see cref="IAuditableCommand"/> handler runs, forwards an audit event to Platform's central store (S2S,
/// via <see cref="IPlatformAuditForwarder"/>). BEST-EFFORT: registered innermost (closest to the handler) so it only
/// wraps real handler executions, and any forwarding failure is swallowed — the business command is never blocked.
/// </summary>
public sealed class AuditForwardingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    // Mirrors Diten.Platform.Domain.Enums.AuditOutcome.
    private const int OutcomeSucceeded = 1;
    private const int OutcomeFailed = 2;

    private readonly IPlatformAuditForwarder _forwarder;
    private readonly ILogger<AuditForwardingBehavior<TRequest, TResponse>> _logger;

    public AuditForwardingBehavior(
        IPlatformAuditForwarder forwarder,
        ILogger<AuditForwardingBehavior<TRequest, TResponse>> logger)
    {
        _forwarder = forwarder;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();

        if (request is IAuditableCommand && request is IAuditMetadataProvider provider)
        {
            try
            {
                var metadata = provider.GetAuditMetadata();
                var forwardRequest = new AuditForwardRequest(
                    RequestType: typeof(TRequest).Name,
                    Category: (int)metadata.Category,
                    Operation: (int)metadata.Operation,
                    EntityType: metadata.EntityType,
                    SourceModule: metadata.SourceModule,
                    EntityId: ResolveEntityId(request, response),
                    Outcome: ReadIsSuccessful(response) ? OutcomeSucceeded : OutcomeFailed);

                await _forwarder.ForwardAsync(forwardRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                // Best-effort: audit instrumentation must never break the business command.
                _logger.LogWarning(ex, "Audit forwarding failed for {RequestType}. Command result is preserved.", typeof(TRequest).Name);
            }
        }

        return response;
    }

    // Create commands carry no id — the new id comes back on the response (Response<Guid>.Data). Update/lifecycle
    // commands carry the id themselves (e.g. LegalEntityId). Prefer the response id, then fall back to a command id.
    private static Guid? ResolveEntityId(object command, object? response)
    {
        if (response?.GetType().GetProperty("Data")?.GetValue(response) is Guid dataGuid && dataGuid != Guid.Empty)
        {
            return dataGuid;
        }

        foreach (var property in command.GetType().GetProperties())
        {
            if (property.PropertyType == typeof(Guid)
                && property.Name.EndsWith("Id", StringComparison.Ordinal)
                && property.GetValue(command) is Guid commandGuid
                && commandGuid != Guid.Empty)
            {
                return commandGuid;
            }
        }

        return null;
    }

    private static bool ReadIsSuccessful(object? response)
    {
        return response?.GetType().GetProperty("IsSuccessful")?.GetValue(response) is not bool isSuccessful || isSuccessful;
    }
}
