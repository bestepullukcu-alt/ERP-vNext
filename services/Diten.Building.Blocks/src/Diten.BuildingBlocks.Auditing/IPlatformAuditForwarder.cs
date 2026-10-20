namespace Diten.BuildingBlocks.Auditing;

/// <summary>
/// Sends one audit record to Platform's central store. Best-effort and NOT silent (AUD-001 §4.3, K8): a record that is
/// not written is a warning in the log and a count in <see cref="AuditForwardingCounters"/> — never an exception into
/// the business command.
/// </summary>
public interface IPlatformAuditForwarder
{
    Task<AuditForwardResult> ForwardAsync(AuditForwardRequest request, CancellationToken cancellationToken = default);
}

public enum AuditForwardResult
{
    /// <summary>Platform accepted the record (2xx).</summary>
    Sent = 1,

    /// <summary>Not attempted: the forwarder is not configured, or the call has no tenant. Warned and counted.</summary>
    Skipped = 2,

    /// <summary>Attempted and refused / unreachable. Warned and counted.</summary>
    Failed = 3
}

/// <summary>The facts the behavior resolved for one command execution. The caller (tenant, actor, correlation) is
/// resolved by the forwarder from <see cref="IAuditCallContext"/>.</summary>
public sealed record AuditForwardRequest(
    string RequestType,
    AuditCategory Category,
    AuditOperation Operation,
    AuditOutcome Outcome,
    string EntityType,
    Guid? EntityId,
    string SourceModule,
    IReadOnlyDictionary<string, object?>? BeforeState = null,
    IReadOnlyDictionary<string, object?>? AfterState = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);
