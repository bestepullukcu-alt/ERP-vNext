using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.AuthService.Application.Tests.Audit;

// FEAT-AUDIT-RBAC — the shared recorder is the single write path every RBAC mutation handler uses. These verify it
// (1) writes the correct EventName + actor-stamped metadata, and (2) never breaks the operation if the write fails.
public sealed class RbacAuditRecorderTests
{
    [Fact]
    public async Task RecordAsync_writes_the_event_with_the_actor_and_flattened_metadata()
    {
        var actor = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var target = Guid.NewGuid();
        var role = Guid.NewGuid();
        var audit = new CapturingAuditService();
        var recorder = new RbacAuditRecorder(audit, new FakeActor(actor), NullLogger<RbacAuditRecorder>.Instance);

        await recorder.RecordAsync("user_role_assigned", tenant,
            new { targetUserId = target, roleId = role, roleName = "Administrator" });

        Assert.Equal("user_role_assigned", audit.EventName);
        Assert.Equal(actor, audit.UserId!.Value); // the log row's UserId is the ACTOR (who performed the change)
        Assert.Equal(tenant, audit.TenantId);

        using var doc = JsonDocument.Parse(audit.Metadata!);
        var root = doc.RootElement;
        Assert.Equal(actor.ToString(), root.GetProperty("actorId").GetString());
        Assert.Equal(target.ToString(), root.GetProperty("targetUserId").GetString());
        Assert.Equal(role.ToString(), root.GetProperty("roleId").GetString());
        Assert.Equal("Administrator", root.GetProperty("roleName").GetString());
    }

    [Fact]
    public async Task RecordAsync_stamps_a_null_actorId_when_unauthenticated()
    {
        var audit = new CapturingAuditService();
        var recorder = new RbacAuditRecorder(audit, new FakeActor(null), NullLogger<RbacAuditRecorder>.Instance);

        await recorder.RecordAsync("role_deleted", Guid.NewGuid(), new { roleId = Guid.NewGuid(), roleName = "Temp" });

        using var doc = JsonDocument.Parse(audit.Metadata!);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("actorId").ValueKind);
    }

    [Fact]
    public async Task RecordAsync_never_throws_when_the_audit_write_fails()
    {
        var recorder = new RbacAuditRecorder(new ThrowingAuditService(), new FakeActor(Guid.NewGuid()), NullLogger<RbacAuditRecorder>.Instance);

        // Must NOT throw — a failed audit write is logged and the already-committed mutation stands.
        await recorder.RecordAsync("role_permission_revoked", Guid.NewGuid(), new { roleId = Guid.NewGuid() });
    }

    private sealed class FakeActor(Guid? id) : ICurrentUserAccessor
    {
        public Guid? UserId { get; } = id;
    }

    private sealed class CapturingAuditService : IAuthAuditService
    {
        public string? EventName { get; private set; }
        public Guid? UserId { get; private set; }
        public Guid TenantId { get; private set; }
        public string? Metadata { get; private set; }

        public Task WriteAsync(string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default)
        {
            EventName = eventName;
            UserId = userId;
            TenantId = tenantId;
            Metadata = metadata;
            return Task.CompletedTask;
        }

        public Task WriteEmptyRoleLoginAsync(Guid userId, Guid tenantId, string email, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class ThrowingAuditService : IAuthAuditService
    {
        public Task WriteAsync(string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default)
            => throw new InvalidOperationException("audit store unavailable");

        public Task WriteEmptyRoleLoginAsync(Guid userId, Guid tenantId, string email, CancellationToken ct = default) => Task.CompletedTask;
    }
}
