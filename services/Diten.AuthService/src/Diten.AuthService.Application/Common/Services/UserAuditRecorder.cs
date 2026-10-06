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

    public Task RecordAsync(
        string eventName,
        Guid tenantId,
        Guid targetUserId,
        IReadOnlyDictionary<string, object?> metadata,
        CancellationToken ct = default)
        => RecordAsync(eventName, tenantId, targetUserId, metadata, succeeded: true, ct);

    public async Task RecordAsync(
        string eventName,
        Guid tenantId,
        Guid targetUserId,
        IReadOnlyDictionary<string, object?> metadata,
        bool succeeded,
        CancellationToken ct)
    {
        var local = new Dictionary<string, object?>(metadata, StringComparer.Ordinal) { ["targetUserId"] = targetUserId };

        // The local row first: it is the one that must exist whatever Platform does. BL-529 FIX3 — and it is inside the
        // recorder's "never throws" promise too: a local write failure after a completed mutation (an administrator's
        // reset, written in a finally) is logged, not turned into a 500 that says the reset failed.
        try
        {
            await _localAudit.RecordAsync(eventName, tenantId, local, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local user audit write failed for {EventName}. The mutation stands; forwarding continues.", eventName);
        }

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
                    succeeded ? UserAuditEvents.OutcomeSucceeded : UserAuditEvents.OutcomeFailed,
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
