using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Services;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Common.Services;

/// <summary>BL-456 — see <see cref="IUserAuditRecorder"/>. Fail-safe: never throws.</summary>
public sealed class UserAuditRecorder : IUserAuditRecorder
{
    private readonly IRbacAuditRecorder _localAudit;
    private readonly IPlatformAuditForwarder _forwarder;
    private readonly ILogger<UserAuditRecorder> _logger;

    public UserAuditRecorder(
        IRbacAuditRecorder localAudit,
        IPlatformAuditForwarder forwarder,
        ILogger<UserAuditRecorder> logger)
    {
        _localAudit = localAudit;
        _forwarder = forwarder;
        _logger = logger;
    }

    public async Task RecordAsync(
        string eventName,
        Guid tenantId,
        Guid targetUserId,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken ct = default)
    {
        var local = new Dictionary<string, object?>(metadata, StringComparer.Ordinal) { ["targetUserId"] = targetUserId };

        // The local row first: it is the one that must exist whatever Platform does.
        await _localAudit.RecordAsync(eventName, tenantId, local, ct);

        if (!UserAuditEvents.Operations.TryGetValue(eventName, out var operation))
        {
            // A caller passed a name outside the vocabulary — a coding error, not a runtime condition. Keep the local
            // row, refuse to invent a Platform operation for it, and say so loudly.
            _logger.LogError("User audit event {EventName} has no Platform operation; not forwarded.", eventName);
            return;
        }

        try
        {
            await _forwarder.ForwardAsync(
                new PlatformAuditEvent(
                    eventName,
                    tenantId,
                    UserAuditEvents.EntityType,
                    targetUserId,
                    operation,
                    UserAuditEvents.OutcomeSucceeded,
                    metadata),
                ct);
        }
        catch (Exception ex)
        {
            // The forwarder never throws by contract; this is the belt to that brace — the mutation already stands.
            _logger.LogWarning(ex, "User audit forwarding failed for {EventName}. The local audit row stands.", eventName);
        }
    }
}
