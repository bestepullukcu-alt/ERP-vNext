using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services.WorkAggregation;

/// <summary>
/// WC-D1 — THE WRITE HALF OF THE GENERAL BRIDGE, and deliberately the same shape as the read half.
///
/// <para><b>Why this is not designed separately.</b> DCP-004 recorded on 2026-08-26 that reading and writing a
/// remote module are ONE contract seen from two directions, and that designing them apart produces two identity
/// models, two error dictionaries and two retry policies for one conversation. So both halves are built from the
/// same configuration row, both go through <see cref="RemoteWorkItemGateway"/>, and both take the caller's own
/// bearer token and the same tenant header. The only thing this class adds to the read half is the budget: the
/// aggregation loop applies <c>WorkAggregation:Resilience:ProviderTimeout</c> for reads and there is no loop on
/// the write path, so the SAME option is applied here rather than a second number being invented.</para>
///
/// <para><b>FAIL-CLOSED, stated as a rule and pinned by a test.</b> If the module does not answer — refused
/// socket, exceeded budget, a body that is not an envelope — the action is REFUSED with
/// <see cref="WorkItemActionReasonCodes.RemoteUnavailable"/> and HTTP 504. It is never reported as success. The
/// write may in fact have landed on the far side, and that is exactly the reason: the honest thing to tell
/// somebody whose outcome is unknown is that it is unknown, and the board they re-read is the authority on what
/// actually happened.</para>
///
/// <para><b>No business rule lives here</b>, the same as every other dispatcher. It translates the one wire shape
/// into the module's own endpoint and returns what the module answered — status, reason code and errors intact —
/// so a refusal still reaches the reader as a sentence in their own language instead of "an error occurred".</para>
/// </summary>
public sealed class HttpWorkItemActionDispatcher : IWorkItemActionDispatcher
{
    private readonly RemoteWorkItemProviderOptions _row;
    private readonly RemoteWorkItemGateway _gateway;
    private readonly WorkAggregationResilienceOptions _resilience;
    private readonly ILogger<HttpWorkItemActionDispatcher> _logger;

    public HttpWorkItemActionDispatcher(
        RemoteWorkItemProviderOptions row,
        RemoteWorkItemGateway gateway,
        IOptions<WorkAggregationResilienceOptions> resilience,
        ILogger<HttpWorkItemActionDispatcher> logger)
    {
        _row = row;
        _gateway = gateway;
        _resilience = resilience.Value;
        _logger = logger;
    }

    public string ProviderCode => _row.ProviderCode;

    public IReadOnlyCollection<string> SupportedActionCodes => _row.Actions.Keys.ToArray();

    public bool CanDispatch(string actionCode)
        => !string.IsNullOrWhiteSpace(actionCode) && _row.Actions.ContainsKey(actionCode);

    /// <summary>
    /// The same map the provider publishes as <c>RequiredActionPermissions</c> — one list, so the containment the
    /// guard test asserts cannot be broken by editing one of two places.
    /// </summary>
    public string? RequiredPermission(string actionCode)
        => actionCode is not null && _row.Actions.TryGetValue(actionCode, out var key) ? key : null;

    public async Task<Response<WorkItemActionResultDto>> DispatchAsync(
        WorkItemActionDispatchRequest request,
        CancellationToken ct = default)
    {
        if (!CanDispatch(request.ActionCode))
        {
            // Reachable directly (a caller holding this dispatcher) even though the controller checks first, and
            // an unpublished verb must never be forwarded to a module on the strength of a stale screen.
            return WorkItemActionDispatchResults.ActionUnknown(request);
        }

        var path = _row.ActionPathTemplate
            .Replace("{itemId}", Uri.EscapeDataString(request.ItemId.ToString()), StringComparison.Ordinal)
            .Replace("{actionCode}", Uri.EscapeDataString(request.ActionCode), StringComparison.Ordinal);

        /*
         * THE ACTOR IS NOT IN THE BODY, and this is the one place it would be tempting to put it.
         *
         * The payload forwarded below is the caller's own — the fields the browser filled in. The identity is
         * carried by the BEARER TOKEN the gateway forwards, so the module resolves the actor from a signed claim
         * exactly as it does for a caller arriving at its own screens. Serialising `Actor.UserId` into the body
         * would hand a remote module a user id that has crossed a trust boundary as plain data, and any module
         * that trusted it would let one Platform caller act as anybody.
         */
        var body = new RemoteWorkItemActionRequest(_row.ProviderCode, request.Payload);

        // The write path's own budget, from the SAME option the aggregation loop uses for reads. Linked to the
        // caller's token so a reader who navigates away still cancels the call in flight.
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (_resilience.ProviderTimeout <= TimeSpan.Zero)
        {
            budget.Cancel();
        }
        else
        {
            budget.CancelAfter(_resilience.ProviderTimeout);
        }

        RemoteCallOutcome outcome;
        try
        {
            outcome = await _gateway.SendAsync(
                _row, HttpMethod.Post, path, body, request.CorrelationId, budget.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The CALLER went away. Not a refusal to report — there is nobody left to report it to.
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Work-item action {ActionCode} on provider {ProviderCode} exceeded its {Timeout} budget; refused.",
                request.ActionCode,
                _row.ProviderCode,
                _resilience.ProviderTimeout);
            return Unavailable(request);
        }

        if (!outcome.Reached)
        {
            _logger.LogError(
                "Work-item action {ActionCode} on provider {ProviderCode} could not be delivered: {Detail}",
                request.ActionCode,
                _row.ProviderCode,
                outcome.FailureDetail);
            return Unavailable(request);
        }

        if (!outcome.Succeeded)
        {
            // The module's OWN verdict, carried through unchanged — status, reason code and sentences. Flattening
            // a 409 TASK_CONCURRENCY_CONFLICT here would disconnect the seven-language error-code bridge and every
            // remote refusal would read "an error occurred".
            return Response<WorkItemActionResultDto>.Fail(
                outcome.Errors.Count > 0 ? outcome.Errors : ["The action was refused."],
                outcome.StatusCode is >= 400 and < 600 ? outcome.StatusCode : 400,
                outcome.ReasonCode,
                request.CorrelationId);
        }

        return Response<WorkItemActionResultDto>.Success(
            new WorkItemActionResultDto(
                request.ItemId.ToString(),
                _row.ProviderCode,
                request.ActionCode),
            200,
            request.CorrelationId);
    }

    /// <summary>
    /// 504, because the honest answer is "no answer arrived in time" and that is what a gateway timeout means. Not
    /// 500 (Platform did not fail) and never a 2xx.
    /// </summary>
    private Response<WorkItemActionResultDto> Unavailable(WorkItemActionDispatchRequest request)
        => Response<WorkItemActionResultDto>.Fail(
            $"The module behind '{_row.ProviderCode}' did not answer, so the action was not confirmed.",
            504,
            WorkItemActionReasonCodes.RemoteUnavailable,
            request.CorrelationId);
}

/// <summary>
/// The body Platform posts to a module — deliberately IDENTICAL to <see cref="WorkItemActionRequestDto"/>, the
/// body the browser posts to Platform.
///
/// <para>One wire shape for the whole chain: a module implementing the contract reads the same JSON Platform's own
/// endpoint reads, and there is no second payload vocabulary to keep in step. The <c>providerCode</c> is echoed so
/// a module serving more than one source can tell which it is being asked about.</para>
/// </summary>
public sealed record RemoteWorkItemActionRequest(
    string ProviderCode,
    WorkItemActionPayloadDto Payload);
