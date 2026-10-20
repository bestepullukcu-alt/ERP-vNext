using Diten.CrmService.Domain.Entities.Commercial;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.CrmService.Application.Features.Commercial.PreOrders;

/// <summary>
/// WP-VW-W6 W6-BE-a — MOD-0168 §12 (DESIGN-GAP §3, K-N8): the system step after an approval. The transmitter (depot PDF
/// + e-mails) is W6-BE-c; this service owns the state: sent → <c>transmitted</c>, failed → <c>transmission_failed</c>
/// (retried by sales operations; the approval is never taken back), deferred (no transmitter wired yet) → the order stays
/// <c>approved</c> and nothing is recorded. A transmitter that throws counts as failed.
/// </summary>
public sealed class PreOrderTransmissionService
{
    private readonly PreOrderContext _c;
    private readonly IPreOrderTransmitter _transmitter;
    private readonly ILogger<PreOrderTransmissionService> _logger;

    public PreOrderTransmissionService(PreOrderContext context, IPreOrderTransmitter transmitter, ILogger<PreOrderTransmissionService>? logger = null)
    {
        _c = context;
        _transmitter = transmitter;
        _logger = logger ?? NullLogger<PreOrderTransmissionService>.Instance;
    }

    /// <summary>False = nothing changed (deferred, not in a transmittable state, or a lost race).</summary>
    public async Task<bool> TransmitAsync(PreOrder order, string? by, CancellationToken ct)
    {
        if (order.Status is not (PreOrderStatuses.Approved or PreOrderStatuses.TransmissionFailed))
        {
            return false;
        }

        var attemptNo = (order.Transmission?.Attempts.Count ?? 0) + 1;
        PreOrderTransmissionResult result;
        try
        {
            result = await _transmitter.TransmitAsync(order, attemptNo, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "pre_order.transmission.threw PreOrderId={Id}", order.Id);
            result = new PreOrderTransmissionResult(PreOrderTransmissionResult.Failed, Array.Empty<string>(), null, null, "transmitter_error");
        }

        if (result.Outcome == PreOrderTransmissionResult.Deferred)
        {
            _logger.LogInformation("pre_order.transmission.deferred PreOrderId={Id} Reason={Reason}", order.Id, result.ReasonCode);
            return false;
        }

        var now = _c.Now;
        var expected = order.Version;
        var sent = result.Outcome == PreOrderTransmissionResult.Sent;
        order.Transmission ??= new PreOrderTransmission();
        order.Transmission.Attempts.Add(new PreOrderTransmissionAttempt
        {
            At = now, DispatchIds = result.DispatchIds.ToList(), Status = sent ? PreOrderTransmissionStatuses.Sent : PreOrderTransmissionStatuses.Failed,
            ReasonCode = result.ReasonCode, ByUserId = by
        });
        order.Transmission.LastStatus = sent ? PreOrderTransmissionStatuses.Sent : PreOrderTransmissionStatuses.Failed;
        PreOrderStateMachine.Append(order, PreOrderEventTypes.TransmissionAttempted, by, now,
            new Dictionary<string, string?> { ["attempt"] = attemptNo.ToString(), ["reason"] = result.ReasonCode });
        if (sent)
        {
            order.Transmission.TransmittedAt = now;
            order.Transmission.DepotDocumentRef = result.DepotDocumentRef ?? order.Transmission.DepotDocumentRef;
            order.Transmission.PharmacyCopyRef = result.PharmacyCopyRef ?? order.Transmission.PharmacyCopyRef;
            PreOrderStateMachine.TryApply(order, PreOrderStateMachine.Actions.TransmitOk, PreOrderEventTypes.Transmitted, by, now);
        }
        else
        {
            PreOrderStateMachine.TryApply(order, PreOrderStateMachine.Actions.TransmitFailed, PreOrderEventTypes.TransmissionFailed, by, now,
                new Dictionary<string, string?> { ["reason"] = result.ReasonCode });
        }

        return await _c.SaveAsync(order, expected, ct);
    }
}
