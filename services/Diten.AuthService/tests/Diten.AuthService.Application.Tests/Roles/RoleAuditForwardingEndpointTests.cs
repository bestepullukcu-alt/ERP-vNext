using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Features.Roles;
using Diten.AuthService.Application.Tests.Testing;

namespace Diten.AuthService.Application.Tests.Roles;

/// <summary>
/// WP-ROLES-CLOSE-01 (B) — every role and role-permission write leaves a row in BOTH logs: AuthService's own
/// <c>authAuditLogs</c> (unchanged) and Platform's central audit log (the fake Platform's
/// <c>POST /api/internal/audit/append</c>: category IdentityAccess, entity "Role", entity id = the role, tenant = the
/// caller's tenant, actor = the caller BY NAME). HTTP round trips through the real Api on a test-owned mongod; only the
/// network under the forwarder's typed client is fake. Two disposable tenants per class.
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class RoleAuditForwardingEndpointTests : IClassFixture<PlatformEdgeTestHost>
{
    private const string AppendPath = "/api/internal/audit/append";

    // Platform AuditOperation values, written out by hand — independent of RoleAuditEvents.Operations.
    private const int Create = 1, Update = 2, Delete = 3, Assign = 8, Revoke = 9;

    private readonly PlatformEdgeTestHost _host;
    private readonly RoleEndpointWorld _world;

    public RoleAuditForwardingEndpointTests(PlatformEdgeTestHost host)
    {
        _host = host;
        _world = RoleEndpointWorld.Create(host, "Rana", "Steward");
    }

    [Fact]
    public async Task Creating_a_role_is_audited_in_both_logs_with_the_role_name()
    {
        using var client = _world.Client();
        var name = "QA-" + Guid.NewGuid().ToString("N")[..8];

        var response = await client.PostAsJsonAsync("api/roles", new { name, displayName = "Quality Reviewers", description = "created" });

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var roleId = await ReadIdAsync(response);
        var body = await AssertAuditedAsync(_world, RoleAuditEvents.Created, roleId, Create);
        Assert.Equal(name, body.GetProperty("metadata").GetProperty("roleName").GetString());
        Assert.Equal("Quality Reviewers", body.GetProperty("metadata").GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Updating_a_role_is_audited_in_both_logs_with_before_and_after()
    {
        var role = await _world.NewRoleAsync("upd");
        using var client = _world.Client();

        var response = await client.PutAsJsonAsync($"api/roles/{role.Id}", new { displayName = "Renamed role", description = "after" });

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var body = await AssertAuditedAsync(_world, RoleAuditEvents.Updated, role.Id, Update);
        var metadata = body.GetProperty("metadata");
        Assert.Equal(role.DisplayName, metadata.GetProperty("before").GetProperty("displayName").GetString());
        Assert.Equal("Renamed role", metadata.GetProperty("after").GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Deleting_a_role_is_audited_in_both_logs()
    {
        var role = await _world.NewRoleAsync("del");
        using var client = _world.Client();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"api/roles/{role.Id}")).StatusCode);

        var body = await AssertAuditedAsync(_world, RoleAuditEvents.Deleted, role.Id, Delete);
        Assert.Equal(role.Name, body.GetProperty("metadata").GetProperty("roleName").GetString());
    }

    [Fact]
    public async Task Granting_and_revoking_a_permission_are_audited_in_both_logs_with_the_permission_key()
    {
        var role = await _world.NewRoleAsync("perm");
        var permission = await _world.PermissionAsync("auth.users.read");
        using var client = _world.Client();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"api/roles/{role.Id}/permissions", new { permissionId = permission.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"api/roles/{role.Id}/permissions/{permission.Id}")).StatusCode);

        var granted = await AssertAuditedAsync(_world, RoleAuditEvents.PermissionGranted, role.Id, Assign);
        Assert.Equal("auth.users.read", granted.GetProperty("metadata").GetProperty("permissionKey").GetString());
        Assert.Equal(role.Name, granted.GetProperty("metadata").GetProperty("roleName").GetString());
        var revoked = await AssertAuditedAsync(_world, RoleAuditEvents.PermissionRevoked, role.Id, Revoke);
        Assert.Equal("auth.users.read", revoked.GetProperty("metadata").GetProperty("permissionKey").GetString());
    }

    [Fact]
    public async Task A_refused_write_is_not_audited_anywhere()
    {
        var system = await _world.NewRoleAsync("sys", system: true);
        using var client = _world.Client();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"api/roles/{system.Id}")).StatusCode);

        Assert.Empty(await _world.LocalRowsAsync(RoleAuditEvents.Deleted, system.Id));
        Assert.Empty(PlatformRows(RoleAuditEvents.Deleted, system.Id));
    }

    [Fact]
    public async Task With_Platform_down_the_role_write_succeeds_and_the_local_row_is_written()
    {
        var role = await _world.NewRoleAsync("down");
        using var client = _world.Client();
        var before = _host.Platform.Calls.Count;

        _host.Platform.Down = true;
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"api/roles/{role.Id}")).StatusCode);

            Assert.Single(await _world.LocalRowsAsync(RoleAuditEvents.Deleted, role.Id));
            Assert.Equal(before, _host.Platform.Calls.Count); // nothing reached a Platform that is not there
        }
        finally
        {
            _host.Platform.Down = false;
        }
    }

    [Fact]
    public async Task Each_tenants_event_carries_its_own_tenant_and_its_own_actor()
    {
        var other = RoleEndpointWorld.Create(_host, "Omer", "Other");
        var mine = await _world.NewRoleAsync("t1");
        var theirs = await other.NewRoleAsync("t2");
        using var myClient = _world.Client();
        using var theirClient = other.Client();

        Assert.Equal(HttpStatusCode.NoContent, (await myClient.DeleteAsync($"api/roles/{mine.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await theirClient.DeleteAsync($"api/roles/{theirs.Id}")).StatusCode);
        // The other tenant's role is not there for me: no deletion, no event under my tenant.
        var foreign = await other.NewRoleAsync("t3");
        Assert.Equal(HttpStatusCode.NotFound, (await myClient.DeleteAsync($"api/roles/{foreign.Id}")).StatusCode);

        var myBody = await AssertAuditedAsync(_world, RoleAuditEvents.Deleted, mine.Id, Delete);
        var theirBody = await AssertAuditedAsync(other, RoleAuditEvents.Deleted, theirs.Id, Delete);
        Assert.Equal("Rana Steward", myBody.GetProperty("actorDisplayName").GetString());
        Assert.Equal("Omer Other", theirBody.GetProperty("actorDisplayName").GetString());
        Assert.Empty(PlatformRows(RoleAuditEvents.Deleted, foreign.Id));
        Assert.Empty(await _world.LocalRowsAsync(RoleAuditEvents.Deleted, theirs.Id));
        Assert.Empty(await other.LocalRowsAsync(RoleAuditEvents.Deleted, mine.Id));
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private async Task<JsonElement> AssertAuditedAsync(RoleEndpointWorld world, string eventName, Guid roleId, int operation)
    {
        var local = Assert.Single(await world.LocalRowsAsync(eventName, roleId));
        Assert.Equal(world.TenantId, local.TenantId);
        Assert.Contains(world.ActorId.ToString(), local.Metadata); // the recorder stamps the actor

        var call = Assert.Single(PlatformRows(eventName, roleId));
        Assert.True(call.HasInternalKey);
        Assert.Equal("POST", call.Method);
        var body = call.Body;
        Assert.Equal(2, body.GetProperty("category").GetInt32()); // AuditCategory.IdentityAccess — the user events' category
        Assert.Equal("Role", body.GetProperty("entityType").GetString());
        Assert.Equal(operation, body.GetProperty("operation").GetInt32());
        Assert.Equal(1, body.GetProperty("outcome").GetInt32());
        Assert.Equal(world.TenantId, body.GetProperty("targetTenantId").GetGuid());
        Assert.Equal(world.ActorId, body.GetProperty("actorId").GetGuid());
        Assert.Equal(3, body.GetProperty("actorType").GetInt32()); // TenantUser
        // The PERSON, from the token's given/family name — not Platform's internal caller ("system").
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("actorDisplayName").GetString()), "the forwarded role event names its actor");
        Assert.NotEqual("system", body.GetProperty("actorDisplayName").GetString(), StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Diten.AuthService", body.GetProperty("sourceService").GetString());
        Assert.Equal("access-governance", body.GetProperty("sourceModule").GetString());
        Assert.NotEqual(Guid.Empty, body.GetProperty("correlationId").GetGuid());
        Assert.False(body.GetProperty("isPlatformGlobal").GetBoolean());
        // No secret travels: the event carries ids, names and keys only.
        var raw = body.GetRawText();
        Assert.DoesNotContain("token", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        return body;
    }

    private IReadOnlyList<FakePlatformEdge.Call> PlatformRows(string eventName, Guid roleId)
        => _host.Platform.CallsTo(AppendPath)
            .Where(c => c.Body.GetProperty("requestType").GetString() == eventName
                        && c.Body.TryGetProperty("entityId", out var id) && id.ValueKind == JsonValueKind.String && id.GetGuid() == roleId)
            .ToList();

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }
}
