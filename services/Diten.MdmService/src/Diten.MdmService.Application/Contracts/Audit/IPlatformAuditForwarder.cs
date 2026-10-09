namespace Diten.MdmService.Application.Contracts.Audit;

/// <summary>
/// Forwards a completed auditable command to Platform's central audit store over S2S. Implementations MUST be
/// best-effort: a forwarding failure is logged and swallowed, never surfaced to the business command.
/// </summary>
public interface IPlatformAuditForwarder
{
    Task ForwardAsync(AuditForwardRequest request, CancellationToken ct = default);
}

/// <summary>
/// The audit facts the behavior has resolved for one command execution. Enum values are carried as their integer
/// codes (aligned with Platform's enums) so the forwarder just serializes them onto the wire contract.
/// </summary>
public sealed record AuditForwardRequest(
    string RequestType,
    int Category,
    int Operation,
    string EntityType,
    string SourceModule,
    Guid? EntityId,
    int Outcome,
    // WP-MDM-BRAND-ACCEPT-01 FIX2 (M2) — the request's normalised correlation id (see IRequestCorrelation); null = the
    // forwarder resolves it from the request as before.
    Guid? CorrelationId = null,
    // MOD-0290-FU04 S1 FIX1 item 2 (CT decision): what changed and why — Platform's AuditAppendRequest already carries
    // these three. Null = not sent (every command without IAuditDetailsProvider forwards exactly what it did before).
    IReadOnlyDictionary<string, object?>? BeforeState = null,
    IReadOnlyDictionary<string, object?>? AfterState = null,
    IReadOnlyDictionary<string, object?>? Metadata = null);
