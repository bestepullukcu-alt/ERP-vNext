using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

/// <summary>
/// WP-MDM-GSKU-ACCEPT-01 FIX3 item 1 (B13): what one recovery cycle did, counted the same way for every GSKU flow.
/// Claimed = the cycle held the operation's lease; advanced = it moved (or finished); quarantined = it ended this
/// cycle in manual reconciliation; waiting = everything else (busy elsewhere, out of scope, not yet due, retry set).
/// InManualReconciliation is the number of the flow's operations waiting for a person, read after the cycle.
/// </summary>
public sealed class ProductIdentityRecoveryCycle
{
    public int Tenants { get; set; }
    public int Seen { get; set; }
    public int Claimed { get; set; }
    public int Advanced { get; set; }
    public int Quarantined { get; set; }
    public int Waiting { get; set; }
    public int Failed { get; set; }
    public long InManualReconciliation { get; set; }

    /// <summary>
    /// Counts one operation. <paramref name="claimed"/> is false when the processor answered without holding the
    /// lease (another owner holds it, or the store did not answer the claim).
    /// </summary>
    public void Count(bool claimed, bool quarantinedNow, bool advanced)
    {
        Seen++;
        if (!claimed)
        {
            Waiting++;
            return;
        }
        Claimed++;
        if (quarantinedNow) Quarantined++;
        else if (advanced) Advanced++;
        else Waiting++;
    }

    public void NotClaimed()
    {
        Seen++;
        Waiting++;
    }

    /// <summary>One line per cycle: counts only — no tenant data, no secret.</summary>
    public void Log(ILogger logger, string flow)
    {
        // FIX4 item 8: a cycle that found nothing to do and nothing stuck is Debug (every few seconds otherwise).
        logger.Log(Quarantined > 0 || Failed > 0 ? LogLevel.Warning
            : Seen == 0 && InManualReconciliation == 0 ? LogLevel.Debug : LogLevel.Information,
            "{Flow} recovery cycle: tenants {Tenants}, operations {Seen}, claimed {Claimed}, advanced {Advanced}, quarantined {Quarantined}, waiting {Waiting}, failed {Failed}, in manual reconciliation {InManualReconciliation}.",
            flow, Tenants, Seen, Claimed, Advanced, Quarantined, Waiting, Failed, InManualReconciliation);
    }

    public static bool WasNotClaimed(string? errorCode) =>
        errorCode is not null && (errorCode.EndsWith("_BUSY", StringComparison.Ordinal)
                                  || errorCode == Application.Features.ProductItemSkuMaster.Workflow
                                      .ProductIdentityWorkflowClaims.StoreUnavailable);
}

/// <summary>
/// The one gauge for operations waiting in manual reconciliation, per flow (tag "flow"), as the last cycle counted
/// them. MDM has no health or metrics pattern of its own (only an anonymous /health that answers "Healthy"); this is
/// the standard .NET meter, readable with dotnet-counters or any OpenTelemetry exporter, plus the cycle log line.
/// </summary>
public sealed class ProductIdentityRecoveryMetrics : IDisposable
{
    public const string MeterName = "Diten.MdmService.ProductIdentityRecovery";
    public const string ManualReconciliationGauge = "mdm.product_identity.recovery.manual_reconciliation";

    private readonly Meter _meter = new(MeterName);
    private readonly ConcurrentDictionary<string, long> _manual = new(StringComparer.Ordinal);

    public ProductIdentityRecoveryMetrics()
    {
        _meter.CreateObservableGauge(ManualReconciliationGauge,
            () => _manual.Select(item => new Measurement<long>(item.Value, new KeyValuePair<string, object?>("flow", item.Key))),
            unit: "{operation}",
            description: "Product-identity operations waiting in manual reconciliation, per flow, at the last recovery cycle.");
    }

    public void Record(string flow, long inManualReconciliation) => _manual[flow] = inManualReconciliation;

    public long? Current(string flow) => _manual.TryGetValue(flow, out var value) ? value : null;

    public void Dispose() => _meter.Dispose();
}
