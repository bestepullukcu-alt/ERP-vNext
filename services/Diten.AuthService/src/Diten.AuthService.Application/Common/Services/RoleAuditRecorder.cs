using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Common.Services;

/// <summary>WP-ROLES-CLOSE-01 — see <see cref="IRoleAuditRecorder"/>. The shape of <see cref="UserAuditRecorder"/>. Fail-safe: never throws.</summary>
public sealed class RoleAuditRecorder : IRoleAuditRecorder
{
    private readonly IRbacAuditRecorder _localAudit;
    private readonly IPlatformAuditForwarder _forwarder;
    private readonly ILogger<RoleAuditRecorder> _logger;

    public RoleAuditRecorder(
        IRbacAuditRecorder localAudit,
        IPlatformAuditForwarder forwarder,
        ILogger<RoleAuditRecorder> logger)
    {
        _localAudit = localAudit;
        _forwarder = forwarder;
        _logger = logger;
    }

    public async Task RecordAsync(
        string eventName,
        Guid tenantId,
        Guid roleId,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken ct = default)
    {
        // The local row keeps the shape it always had: roleId first, then the event's own fields.
        var local = new Dictionary<string, object?>(StringComparer.Ordinal) { ["roleId"] = roleId };
        foreach (var (key, value) in metadata) local[key] = value;

        // The local row first: it is the one that must exist whatever Platform does.
        await _localAudit.RecordAsync(eventName, tenantId, local, ct);

        if (!RoleAuditEvents.Operations.TryGetValue(eventName, out var operation))
        {
            // A name outside the vocabulary is a coding error: keep the local row, invent no Platform operation, say so.
            _logger.LogError("Role audit event {EventName} has no Platform operation; not forwarded.", eventName);
            return;
        }

        // An update's before/after go where Platform's audit log keeps them (BeforeState / AfterState) — the screen
        // shows those as the change; left inside Metadata they are just two more keys. The local row is untouched.
        var before = metadata.GetValueOrDefault("before") as IReadOnlyDictionary<string, object?>;
        var after = metadata.GetValueOrDefault("after") as IReadOnlyDictionary<string, object?>;
        var forwarded = before is null && after is null
            ? metadata
            : metadata.Where(kv => kv.Key is not ("before" or "after")).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        try
        {
            await _forwarder.ForwardAsync(
                new PlatformAuditEvent(
                    eventName,
                    tenantId,
                    RoleAuditEvents.EntityType,
                    roleId,
                    operation,
                    RoleAuditEvents.OutcomeSucceeded,
                    forwarded,
                    before,
                    after),
                ct);
        }
        catch (Exception ex)
        {
            // The forwarder never throws by contract; this is the belt to that brace — the mutation already stands.
            _logger.LogWarning(ex, "Role audit forwarding failed for {EventName}. The local audit row stands.", eventName);
        }
    }
}
