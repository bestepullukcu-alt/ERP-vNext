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
    IReadOnlyDictionary<string, object?>? Metadata = null,
    // MOD-0290-FU04 S2: the tenant of a command that runs outside a user's request (the approval worker applying a
    // decision). Null = the forwarder reads the tenant from the request's token as before. Never overrides a token's.
    Guid? TenantId = null,
    // S2 FIX1 item 5: the record's actor is the system, whoever's request carried the command (an approver's decision
    // applied during the maker's replay is not the maker's act; the decision's own actor travels in the metadata).
    bool SystemActor = false);

/// <summary>
/// MOD-0290-FU04 S2: an audited command that knows its tenant itself — run by a worker, with no user token to read it
/// from. The record's actor is then the system (the decision's own actor travels in the command's metadata).
/// </summary>
public interface IAuditTenantProvider
{
    Guid AuditTenantId { get; }
}

/// <summary>
/// S2 FIX1 item 5: an audited command whose act is never the caller's — the forwarder records the system as its actor even
/// when a user's request carried it (e.g. the maker's replay applying the approver's decision).
/// </summary>
public interface IAuditSystemActor
{
}

/// <summary>
/// S2 FIX1 item 5: a command retried by a worker whose repeated refusals are one fact — a failed outcome is forwarded only
/// when this says so (the operation's first failure, and each change of code), never once per attempt.
/// </summary>
public interface IAuditFailureRepeatFilter
{
    bool ShouldAuditFailure(string? refusalCode);
}
