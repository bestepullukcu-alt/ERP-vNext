using System.Diagnostics.Metrics;
namespace Diten.SupplyChainService.Application.Common;

/// <summary>
/// MOD-0183 pack §8.1 signals O-2, O-3 and O-4, on <see cref="System.Diagnostics.Metrics"/> only. O-1 lives elsewhere.
/// </summary>
/// <remarks>
/// Counters are added once per completed operation and never inside a retried block, so a retry is not counted
/// as a second business event (pack §8.1). O-2 is a histogram; its p95 is read by a metrics consumer, for example
/// <c>dotnet-counters monitor --counters Diten.SupplyChainService.Shipments</c>. ASSUMPTION A9: the 500 ms warning in
/// <c>PerformanceBehavior</c> stays an observation threshold; no SLA or failing condition is derived from it here.
/// </remarks>
public static class ShipmentTelemetry
{
    public const string MeterName = "Diten.SupplyChainService.Shipments";
    public const string OperationTag = "operation";
    private static readonly Meter _meter = new(MeterName, "1.0");

    // O-2 operation latency.
    public static readonly Histogram<double> OperationDuration = _meter.CreateHistogram<double>("shipment.operation.duration", "ms",
        "Duration of one MediatR operation, tagged by request type.");

    // O-3, each bound to a pack §13 failure path.
    public static readonly Counter<long> IntakeBlocked = _meter.CreateCounter<long>("shipment.intake.blocked", "{intake}",
        "Automatic intake kept blocked: unavailable Warehouse detail or incompatible correlation.");
    public static readonly Counter<long> IdempotentReplays = _meter.CreateCounter<long>("shipment.idempotency.replays", "{request}",
        "Duplicate idempotency key answered with the first result and no second lifecycle or outbox record.");
    public static readonly Counter<long> ScopeDenials = _meter.CreateCounter<long>("shipment.scope.denials", "{request}",
        "Cross-tenant or cross-legal-entity scope answered 404 without existence leakage.");
    public static readonly Counter<long> SourceDrifts = _meter.CreateCounter<long>("shipment.intake.drift", "{detection}",
        "Warehouse source whose snapshot differs from the hash recorded for its identity; every detection counts.");

    // O-4 outbox states.
    public static readonly Counter<long> OutboxPending = _meter.CreateCounter<long>("shipment.outbox.pending", "{event}",
        "Events written as Pending by a committed shipment transaction; a publish retry does not add here.");
    public static readonly Counter<long> OutboxRetries = _meter.CreateCounter<long>("shipment.outbox.retries", "{attempt}",
        "Failed publish attempts returned to Pending for another attempt.");
    public static readonly Counter<long> OutboxDeadLetters = _meter.CreateCounter<long>("shipment.outbox.dead_letters", "{event}",
        "Events moved to DeadLettered, by attempt exhaustion or a terminal failure.");
}
