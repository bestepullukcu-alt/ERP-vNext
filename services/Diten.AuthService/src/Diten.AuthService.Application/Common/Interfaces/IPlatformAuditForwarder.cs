namespace Diten.AuthService.Application.Common.Interfaces;

/// <summary>
/// BL-456 — one AuthService audit event as Platform's central audit store receives it (<c>POST /api/internal/audit/append</c>).
/// <see cref="Operation"/> and <see cref="Outcome"/> carry the integer values of Platform's <c>AuditOperation</c> /
/// <c>AuditOutcome</c> enums (mirrored in <c>UserAuditEvents</c>) — AuthService does not reference the Platform domain.
/// The actor is NOT part of the event: the forwarder reads it from the current request, like MDM's forwarder.
/// </summary>
public sealed record PlatformAuditEvent(
    string RequestType,
    Guid TenantId,
    string EntityType,
    Guid? EntityId,
    int Operation,
    int Outcome,
    IReadOnlyDictionary<string, object?> Metadata);

/// <summary>
/// BL-456 — forwards an AuthService audit event to Platform's central audit store, S2S, with the internal key.
/// BEST-EFFORT by contract (the MDM <c>PlatformAuditForwarder</c> pattern): NEVER throws, never blocks the caller's
/// operation — Platform down, slow or refusing is logged and swallowed. The local <c>authAuditLogs</c> row is written
/// before this is called, so the event is never lost only because Platform is away.
/// </summary>
public interface IPlatformAuditForwarder
{
    Task ForwardAsync(PlatformAuditEvent auditEvent, CancellationToken ct = default);
}
