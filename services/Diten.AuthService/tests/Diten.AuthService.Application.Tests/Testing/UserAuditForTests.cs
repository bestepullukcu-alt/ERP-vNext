using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Testing;

/// <summary>
/// BL-456 — the production <see cref="UserAuditRecorder"/> for handler unit tests: the local row goes to the test's own
/// <see cref="IRbacAuditRecorder"/> fake (so existing authAuditLogs assertions keep reading the same rows), the Platform
/// edge to a recording forwarder that never touches a network. The HTTP-level tests prove the real forwarder.
/// </summary>
internal static class UserAuditForTests
{
    public static UserAuditRecorder Over(IRbacAuditRecorder local, RecordingPlatformAuditForwarder? forwarder = null)
        => new(local, forwarder ?? new RecordingPlatformAuditForwarder(), NullLogger<UserAuditRecorder>.Instance);

    public static UserAuditRecorder None() => Over(new NoLocalAudit());

    private sealed class NoLocalAudit : IRbacAuditRecorder
    {
        public Task RecordAsync(string eventName, Guid tenantId, object metadata, CancellationToken ct = default) => Task.CompletedTask;
    }
}

internal sealed class RecordingPlatformAuditForwarder : IPlatformAuditForwarder
{
    public List<PlatformAuditEvent> Events { get; } = [];

    public Task ForwardAsync(PlatformAuditEvent auditEvent, CancellationToken ct = default)
    {
        Events.Add(auditEvent);
        return Task.CompletedTask;
    }
}

/// <summary>BL-459 — a quota edge for handler unit tests: answers <see cref="Decision"/> and records every call.</summary>
internal sealed class RecordingUserQuotaClient : IUserQuotaClient
{
    public UserQuotaDecision Decision { get; set; } = new(UserQuotaOutcome.Consumed);
    public List<string> Consumed { get; } = [];

    public Task<UserQuotaDecision> TryConsumeUserSeatAsync(Guid tenantId, string operationReference, CancellationToken ct)
    {
        Consumed.Add(operationReference);
        return Task.FromResult(Decision);
    }
}
