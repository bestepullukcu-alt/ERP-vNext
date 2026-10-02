using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles;
using Diten.AuthService.Application.Tests.Testing;

namespace Diten.AuthService.Application.Tests.Roles;

/// <summary>
/// WP-ROLES-CLOSE-01 (B) — the production <c>RoleAuditRecorder</c> on its own: the local row keeps its shape, the
/// forwarded event carries the role as its entity, and neither a failing forwarder nor a name outside the vocabulary
/// can break the role mutation that already happened.
/// </summary>
public sealed class RoleAuditRecorderTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid RoleId = Guid.NewGuid();

    public static IEnumerable<object[]> Events() =>
    [
        [RoleAuditEvents.Created, 1],
        [RoleAuditEvents.Updated, 2],
        [RoleAuditEvents.Deleted, 3],
        [RoleAuditEvents.PermissionGranted, 8],
        [RoleAuditEvents.PermissionRevoked, 9]
    ];

    [Theory]
    [MemberData(nameof(Events))]
    public async Task Each_role_event_is_written_locally_and_forwarded_with_the_role_as_its_entity(string eventName, int operation)
    {
        var local = new RecordingLocal();
        var forwarder = new RecordingPlatformAuditForwarder();
        var recorder = RoleAuditForTests.Over(local, forwarder);

        await recorder.RecordAsync(eventName, Tenant, RoleId, new Dictionary<string, object?> { ["roleName"] = "Auditors" });

        var row = Assert.Single(local.Rows);
        Assert.Equal(eventName, row.EventName);
        Assert.Equal(Tenant, row.TenantId);
        var metadata = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(row.Metadata);
        Assert.Equal(RoleId, metadata["roleId"]);            // the local row still names the role first
        Assert.Equal("Auditors", metadata["roleName"]);

        var sent = Assert.Single(forwarder.Events);
        Assert.Equal(eventName, sent.RequestType);
        Assert.Equal(Tenant, sent.TenantId);
        Assert.Equal("Role", sent.EntityType);
        Assert.Equal(RoleId, sent.EntityId);
        Assert.Equal(operation, sent.Operation);
        Assert.Equal(1, sent.Outcome);
        Assert.Equal("Auditors", sent.Metadata["roleName"]);
    }

    [Fact]
    public async Task A_forwarder_that_throws_does_not_break_the_caller_and_the_local_row_stands()
    {
        var local = new RecordingLocal();
        var recorder = RoleAuditForTests.Over(local, new ThrowingForwarder());

        await recorder.RecordAsync(RoleAuditEvents.Deleted, Tenant, RoleId, new Dictionary<string, object?>());

        Assert.Single(local.Rows);
    }

    [Fact]
    public async Task A_name_outside_the_vocabulary_is_written_locally_and_never_forwarded()
    {
        var local = new RecordingLocal();
        var forwarder = new RecordingPlatformAuditForwarder();

        await RoleAuditForTests.Over(local, forwarder).RecordAsync("role_something_new", Tenant, RoleId, new Dictionary<string, object?>());

        Assert.Single(local.Rows);
        Assert.Empty(forwarder.Events);
    }

    private sealed class RecordingLocal : IRbacAuditRecorder
    {
        public List<(string EventName, Guid TenantId, object Metadata)> Rows { get; } = [];

        public Task RecordAsync(string eventName, Guid tenantId, object metadata, CancellationToken ct = default)
        {
            Rows.Add((eventName, tenantId, metadata));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingForwarder : IPlatformAuditForwarder
    {
        public Task ForwardAsync(PlatformAuditEvent auditEvent, CancellationToken ct = default)
            => throw new HttpRequestException("Platform is away.");
    }
}
