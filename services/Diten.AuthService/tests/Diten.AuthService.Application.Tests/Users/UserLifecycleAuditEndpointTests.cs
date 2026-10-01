using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// BL-456 — every user-lifecycle write leaves a row in BOTH logs: AuthService's own <c>authAuditLogs</c> and Platform's
/// central audit log (through the fake Platform's <c>POST /api/internal/audit/append</c>, category IdentityAccess, entity
/// "User", entity id = the user, tenant = the caller's tenant, actor = the caller). HTTP round trips through the real Api
/// on a test-owned mongod; only the network under the forwarder's typed client is fake. A disposable tenant per class.
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class UserLifecycleAuditEndpointTests : IClassFixture<PlatformEdgeTestHost>
{
    private const string AppendPath = "/api/internal/audit/append";

    // Platform AuditOperation values, written out by hand — independent of UserAuditEvents.Operations.
    private const int Create = 1, Update = 2, Delete = 3, Activate = 4, Deactivate = 5, Assign = 8, Revoke = 9, Execute = 15;

    private readonly PlatformEdgeTestHost _host;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId;
    private readonly string _token;

    public UserLifecycleAuditEndpointTests(PlatformEdgeTestHost host)
    {
        _host = host;
        (_actorId, _token) = SeedActorAsync().GetAwaiter().GetResult();
    }

    // ── the seven events this work package adds ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Inviting_a_user_is_audited_in_both_logs()
    {
        using var client = _host.Client(_token, _tenantId);
        var response = await client.PostAsJsonAsync("api/users", new { email = $"inv.{Guid.NewGuid():N}@audit.test", firstName = "In", lastName = "Vited" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = await ReadIdAsync(response);
        await AssertAuditedAsync(UserAuditEvents.Invited, id, Create);
    }

    [Fact]
    public async Task Deleting_a_user_is_audited_in_both_logs()
    {
        var target = await NewUserAsync(active: true);
        using var client = _host.Client(_token, _tenantId);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"api/users/{target}")).StatusCode);
        await AssertAuditedAsync(UserAuditEvents.Deleted, target, Delete);
    }

    [Fact]
    public async Task Deactivating_then_activating_is_audited_as_two_events()
    {
        var target = await NewUserAsync(active: true);
        using var client = _host.Client(_token, _tenantId);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"api/users/{target}/disable", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"api/users/{target}/enable", null)).StatusCode);

        await AssertAuditedAsync(UserAuditEvents.Deactivated, target, Deactivate);
        await AssertAuditedAsync(UserAuditEvents.Activated, target, Activate);
    }

    [Fact]
    public async Task Re_sending_the_current_state_writes_no_row()
    {
        var target = await NewUserAsync(active: true);
        using var client = _host.Client(_token, _tenantId);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"api/users/{target}/enable", null)).StatusCode);

        Assert.Empty(await LocalRowsAsync(UserAuditEvents.Activated, target));
        Assert.Empty(PlatformRows(UserAuditEvents.Activated, target));
    }

    [Fact]
    public async Task Admin_password_reset_is_audited_in_both_logs()
    {
        var target = await NewUserAsync(active: true);
        using var client = _host.Client(_token, _tenantId);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"api/users/{target}/reset-password", null)).StatusCode);
        await AssertAuditedAsync(UserAuditEvents.PasswordResetByAdmin, target, Execute);

        // Never the link, never the token.
        var row = Assert.Single(await LocalRowsAsync(UserAuditEvents.PasswordResetByAdmin, target));
        Assert.DoesNotContain("token", row.Metadata, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http", row.Metadata, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resending_an_invitation_is_audited_in_both_logs()
    {
        var target = await NewUserAsync(active: false, invited: true);
        using var client = _host.Client(_token, _tenantId);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"api/users/resend-invite/{target}", null)).StatusCode);
        await AssertAuditedAsync(UserAuditEvents.InvitationResent, target, Execute);
    }

    [Fact]
    public async Task Updating_fields_is_audited_with_the_changed_field_names_and_no_values()
    {
        var target = await NewUserAsync(active: true);
        using var client = _host.Client(_token, _tenantId);

        var response = await client.PutAsJsonAsync($"api/users/{target}", new { firstName = "Renamed", lastName = "Ject", isActive = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertAuditedAsync(UserAuditEvents.Updated, target, Update);
        var row = Assert.Single(await LocalRowsAsync(UserAuditEvents.Updated, target));
        Assert.Contains("firstName", row.Metadata);
        Assert.DoesNotContain("lastName", row.Metadata); // unchanged → not listed
        Assert.DoesNotContain("Renamed", row.Metadata);  // the value is personal data → never in the row
    }

    // ── the three events that already existed now reach Platform too ───────────────────────────────────

    [Fact]
    public async Task Assigning_and_removing_a_role_reach_Platform()
    {
        var target = await NewUserAsync(active: true);
        var roleId = await NewRoleAsync("Auditors-" + Guid.NewGuid().ToString("N")[..6]);
        using var client = _host.Client(_token, _tenantId);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"api/users/{target}/roles", new { roleId })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"api/users/{target}/roles/{roleId}")).StatusCode);

        await AssertAuditedAsync(UserAuditEvents.RoleAssigned, target, Assign);
        await AssertAuditedAsync(UserAuditEvents.RoleRemoved, target, Revoke);
    }

    [Fact]
    public async Task Changing_the_account_kind_reaches_Platform()
    {
        var target = await NewUserAsync(active: true);
        using var client = _host.Client(_token, _tenantId);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"api/users/{target}/account-kind", new { kind = "Service" })).StatusCode);
        await AssertAuditedAsync(UserAuditEvents.AccountKindChanged, target, Update);
    }

    // ── Platform down: the operation stands, the local row is written ─────────────────────────────────

    [Fact]
    public async Task With_Platform_down_the_operation_succeeds_and_the_local_row_is_written()
    {
        var target = await NewUserAsync(active: true);
        using var client = _host.Client(_token, _tenantId);
        var before = _host.Platform.Calls.Count;

        _host.Platform.Down = true;
        try
        {
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"api/users/{target}/disable", null)).StatusCode);
            var invite = await client.PostAsJsonAsync("api/users", new { email = $"down.{Guid.NewGuid():N}@audit.test", firstName = "Down", lastName = "Time" });
            Assert.Equal(HttpStatusCode.Created, invite.StatusCode);
            var invited = await ReadIdAsync(invite);

            Assert.Single(await LocalRowsAsync(UserAuditEvents.Deactivated, target));
            Assert.Single(await LocalRowsAsync(UserAuditEvents.Invited, invited));
            Assert.Equal(before, _host.Platform.Calls.Count); // nothing reached a Platform that is not there
        }
        finally
        {
            _host.Platform.Down = false;
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private async Task AssertAuditedAsync(string eventName, Guid target, int operation)
    {
        var local = Assert.Single(await LocalRowsAsync(eventName, target));
        Assert.Equal(_tenantId, local.TenantId);
        Assert.Contains(_actorId.ToString(), local.Metadata); // the recorder stamps the actor

        var call = Assert.Single(PlatformRows(eventName, target));
        Assert.True(call.HasInternalKey);
        Assert.Equal("POST", call.Method);
        var body = call.Body;
        Assert.Equal(2, body.GetProperty("category").GetInt32()); // AuditCategory.IdentityAccess
        Assert.Equal("User", body.GetProperty("entityType").GetString());
        Assert.Equal(operation, body.GetProperty("operation").GetInt32());
        Assert.Equal(1, body.GetProperty("outcome").GetInt32());
        Assert.Equal(_tenantId, body.GetProperty("targetTenantId").GetGuid());
        Assert.Equal(_actorId, body.GetProperty("actorId").GetGuid());
        Assert.Equal(3, body.GetProperty("actorType").GetInt32()); // TenantUser
        // The PERSON, from the token's given/family name — not Platform's internal caller ("system"). Owner, 2026-09-25.
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("actorDisplayName").GetString()), "the forwarded row names its actor");
        Assert.NotEqual("system", body.GetProperty("actorDisplayName").GetString(), StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Diten.AuthService", body.GetProperty("sourceService").GetString());
        Assert.False(body.GetProperty("isPlatformGlobal").GetBoolean());
    }

    private IReadOnlyList<FakePlatformEdge.Call> PlatformRows(string eventName, Guid target)
        => _host.Platform.CallsTo(AppendPath)
            .Where(c => c.Body.GetProperty("requestType").GetString() == eventName
                        && c.Body.TryGetProperty("entityId", out var id) && id.ValueKind == JsonValueKind.String && id.GetGuid() == target)
            .ToList();

    private async Task<List<AuthAuditLog>> LocalRowsAsync(string eventName, Guid target)
    {
        var rows = await _host.Database.GetCollection<AuthAuditLog>("authAuditLogs")
            .Find(r => r.EventName == eventName && r.TenantId == _tenantId)
            .ToListAsync();
        return rows.Where(r => r.Metadata.Contains(target.ToString(), StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private async Task<Guid> NewUserAsync(bool active, bool invited = false)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(_tenantId);
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var user = new User($"u.{Guid.NewGuid():N}@audit.test", "hash:x", "Sub", "Ject", _tenantId);
        user.SetAccountKind(AccountKind.Human);
        if (invited)
        {
            user.RequirePasswordChange(null);
            user.Deactivate();
        }
        else
        {
            user.ConfirmEmail();
            if (!active) user.Deactivate();
        }

        return (await users.CreateAsync(user, CancellationToken.None)).Id;
    }

    private async Task<Guid> NewRoleAsync(string name)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(_tenantId);
        var roles = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        return (await roles.CreateAsync(new Role(name, name, "audit fixture", _tenantId), CancellationToken.None)).Id;
    }

    /// <summary>
    /// The caller: a live user holding a role that carries <c>auth.users.create</c> — the delete guard refuses to remove the
    /// last person who can create users, so the tenant needs one — and a token with the write keys the tests exercise.
    /// </summary>
    private async Task<(Guid ActorId, string Token)> SeedActorAsync()
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(_tenantId);
        var users = sp.GetRequiredService<IUserRepository>();
        var roles = sp.GetRequiredService<IRoleRepository>();
        var userRoles = sp.GetRequiredService<IUserRoleRepository>();
        var tokens = sp.GetRequiredService<ITokenService>();

        var actor = new User($"actor.{Guid.NewGuid():N}@audit.test", "hash:x", "Audit", "Actor", _tenantId);
        actor.ConfirmEmail();
        actor = await users.CreateAsync(actor, CancellationToken.None);

        var stewards = await roles.CreateAsync(new Role("Stewards", "Stewards", "audit fixture", _tenantId), CancellationToken.None);
        var create = await _host.Database.GetCollection<Permission>("permissions")
            .Find(p => p.Key == "auth.users.create").SingleAsync();
        await _host.Database.GetCollection<RolePermission>("rolePermissions")
            .InsertOneAsync(RolePermission.ManualGrant(stewards.Id, create.Id, _tenantId, "audit-fixture"));
        await userRoles.AssignAsync(new UserRole(actor.Id, stewards.Id, _tenantId, "audit-fixture"), CancellationToken.None);

        var token = tokens.GenerateAccessToken(actor, ["Stewards"],
        [
            "auth.users.read", "auth.users.create", "auth.users.update", "auth.users.delete", "auth.users.assign-role",
            "auth.users.account-kind.manage"
        ], expiresInMinutes: 60);
        return (actor.Id, token);
    }
}
