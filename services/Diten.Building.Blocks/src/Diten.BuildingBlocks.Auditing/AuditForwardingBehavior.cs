using System.Collections;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.BuildingBlocks.Auditing;

/// <summary>
/// AUD-001 K4 (WP-VW-W6 F8-a) — the shared path-<c>b</c> pipeline behavior. For an <see cref="IAuditableCommand"/> it
/// forwards ONE record per execution, whatever happened:
/// <list type="bullet">
/// <item>the handler succeeded → <see cref="AuditOutcome.Succeeded"/>;</item>
/// <item>the handler (or a behavior inside it, e.g. validation) REFUSED — a response whose <c>IsSuccessful</c> is
/// false → <see cref="AuditOutcome.Failed"/>, or <see cref="AuditOutcome.Denied"/> for a 401 / 403; the first
/// code-shaped error goes in as <c>refusalCode</c> (a sentence never does) — AUD-001 §3: a refused command is an audit
/// fact too;</item>
/// <item>the handler THREW → <see cref="AuditOutcome.Failed"/> with the exception's type name, and the exception is
/// rethrown unchanged.</item>
/// </list>
/// <para>
/// Best-effort, not silent (§4.3 "other business records"): the record is written AFTER the handler, and a record that
/// cannot be written never changes the command's result — it is a warning and a count. Register it OUTERMOST (first) so
/// a refusal from an inner behavior is recorded as well. Commands of the K2 class (identity, permission, tenant state,
/// GxP, KVKK-special) need the fail-closed path (§4.4) and must not rely on this behavior alone.
/// </para>
/// </summary>
public sealed class AuditForwardingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IPlatformAuditForwarder _forwarder;
    private readonly AuditForwardingCounters _counters;
    private readonly ILogger<AuditForwardingBehavior<TRequest, TResponse>> _logger;

    public AuditForwardingBehavior(
        IPlatformAuditForwarder forwarder,
        AuditForwardingCounters counters,
        ILogger<AuditForwardingBehavior<TRequest, TResponse>> logger)
    {
        _forwarder = forwarder;
        _counters = counters;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IAuditableCommand auditable)
        {
            return await next();
        }

        TResponse response;
        try
        {
            response = await next();
        }
        catch (Exception ex)
        {
            // The command already failed; its record must not wait on the caller's cancelled token.
            await ForwardAsync(auditable, request, response: null, ex, CancellationToken.None);
            throw;
        }

        await ForwardAsync(auditable, request, response, exception: null, cancellationToken);
        return response;
    }

    private async Task ForwardAsync(IAuditableCommand auditable, TRequest request, object? response, Exception? exception,
        CancellationToken cancellationToken)
    {
        try
        {
            var descriptor = auditable.DescribeAudit();
            var (succeeded, statusCode, errors) = ReadResponse(response);
            var outcome = exception is not null || !succeeded
                ? statusCode is 401 or 403 ? AuditOutcome.Denied : AuditOutcome.Failed
                : AuditOutcome.Succeeded;

            var details = new AuditDetails();
            if (request is IAuditDetailsProvider provider)
            {
                provider.DescribeAuditDetails(details, response);
            }

            if (exception is not null)
            {
                details.Set(AuditDetails.ErrorTypeKey, exception.GetType().Name);
            }
            else if (!succeeded)
            {
                if (statusCode is { } status)
                {
                    details.Set(AuditDetails.StatusCodeKey, status);
                }

                if (AuditCodes.FirstCode(errors) is { } code)
                {
                    details.Set(AuditDetails.RefusalCodeKey, code);
                }
            }

            if (details.ObjectVersion is { } version)
            {
                details.Set(AuditDetails.ObjectVersionKey, version);
            }

            if (details.ChangedFields.Count > 0)
            {
                details.Set(AuditDetails.ChangedFieldsKey, details.ChangedFields.ToArray());
            }

            var forwardRequest = new AuditForwardRequest(
                RequestType: typeof(TRequest).Name,
                Category: descriptor.Category,
                Operation: descriptor.Operation,
                Outcome: outcome,
                EntityType: descriptor.EntityType,
                EntityId: descriptor.EntityId ?? details.EntityId,
                SourceModule: descriptor.SourceModule,
                BeforeState: details.BeforeState,
                AfterState: details.AfterState,
                Metadata: details.Metadata.Count > 0 ? details.Metadata : null);

            await _forwarder.ForwardAsync(forwardRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            // A broken descriptor / details provider / forwarder never breaks the command — but it is not silent.
            _counters.RecordFailed();
            _logger.LogWarning(ex,
                "AUD-001: audit record NOT written for {RequestType} (forwarding threw); the command's result is preserved.",
                typeof(TRequest).Name);
        }
    }

    // A response envelope is read by shape (IsSuccessful / StatusCode / Errors), not by type: every service has its own.
    private static (bool Succeeded, int? StatusCode, IEnumerable<string>? Errors) ReadResponse(object? response)
    {
        if (response is null)
        {
            return (true, null, null);
        }

        var type = response.GetType();
        var succeeded = type.GetProperty("IsSuccessful")?.GetValue(response) is not bool flag || flag;
        var statusCode = type.GetProperty("StatusCode")?.GetValue(response) as int?;
        var errors = type.GetProperty("Errors")?.GetValue(response) is IEnumerable list and not string
            ? list.OfType<string>()
            : null;
        return (succeeded, statusCode, errors);
    }
}
