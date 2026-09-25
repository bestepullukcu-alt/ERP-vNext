using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// BL-459 — the plan's user limit (<c>users.max</c>) on the tenant administrator's own user create, over HTTP through
/// the real Api on a test-owned mongod, with Platform's internal quota endpoints faked (requests recorded). One
/// disposable tenant per test.
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class UserQuotaEndpointTests : IClassFixture<PlatformEdgeTestHost>
{
    private const string ConsumePath = "/api/internal/quotas/consume";
    private const string ReleasePath = "/api/internal/quotas/release";

    private readonly PlatformEdgeTestHost _host;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly string _token;

    public UserQuotaEndpointTests(PlatformEdgeTestHost host)
    {
        _host = host;
        _host.Platform.Responder = null;
        _host.Platform.Down = false;
        _token = SeedActorTokenAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Under_the_limit_one_seat_is_taken_and_the_user_is_invited()
    {
        var email = NewEmail();
        using var client = _host.Client(_token, _tenantId);

        var response = await client.PostAsJsonAsync("api/users", new { email, firstName = "Under", lastName = "Limit" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var consume = Assert.Single(TenantCalls(ConsumePath));
        Assert.True(consume.HasInternalKey);
        Assert.Equal("users.max", consume.Body.GetProperty("quotaKey").GetString());
        Assert.Equal(1m, consume.Body.GetProperty("amount").GetDecimal());
        Assert.Equal("Diten.AuthService", consume.Body.GetProperty("source").GetString());
        Assert.Empty(TenantCalls(ReleasePath));
        Assert.Single(_host.Emails.SentTo, email);
    }

    [Fact]
    public async Task At_the_limit_the_create_is_refused_with_the_numbers_and_nothing_is_written_or_sent()
    {
        var email = NewEmail();
        _host.Platform.Responder = call => call.Path == ConsumePath && TenantOf(call) == _tenantId
            ? FakePlatformEdge.Json(HttpStatusCode.Conflict, new
            {
                data = (object?)null,
                statusCode = 409,
                isSuccessful = false,
                errors = new[] { "QUOTA_LIMIT_EXCEEDED" },
                quota = new { quotaKey = "users.max", limitValue = 5m, currentValue = 5m }
            })
            : null;
        using var client = _host.Client(_token, _tenantId);

        var response = await client.PostAsJsonAsync("api/users", new { email, firstName = "Over", lastName = "Limit" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var code = Assert.Single(body.RootElement.GetProperty("errorCodes").EnumerateArray());
        Assert.Equal(UserLifecycle.QuotaExceededCode, code.GetProperty("code").GetString());
        Assert.Equal("5", code.GetProperty("params").GetProperty("max").GetString());
        Assert.Equal("5", code.GetProperty("params").GetProperty("current").GetString());

        Assert.Equal(0, await _host.Database.GetCollection<User>("users").CountDocumentsAsync(u => u.Email == email && u.TenantId == _tenantId));
        Assert.DoesNotContain(email, _host.Emails.SentTo);
        Assert.Empty(TenantCalls(ReleasePath));                 // no seat was taken, none is given back
        Assert.Empty(TenantCalls("/api/internal/audit/append")); // nothing happened, nothing is audited
    }

    // BL-459 F1 — users.max is Platform's live count; there is nothing to give back, and nothing is sent.
    [Fact]
    public async Task Deleting_a_user_sends_no_release()
    {
        using var client = _host.Client(_token, _tenantId);
        var created = await client.PostAsJsonAsync("api/users", new { email = NewEmail(), firstName = "Leav", lastName = "Ing" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = createdBody.RootElement.GetProperty("data").GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"api/users/{id}")).StatusCode);

        Assert.Empty(TenantCalls(ReleasePath));
        Assert.Single(TenantCalls(ConsumePath)); // the create asked once; the delete asked nothing
    }

    // BL-459 F1 — an Inactive account holds no seat, so switching it back on asks: at the limit it stays off.
    [Fact]
    public async Task Switching_an_inactive_user_back_on_at_the_limit_is_refused()
    {
        var target = await NewInactiveUserAsync();
        _host.Platform.Responder = call => call.Path == ConsumePath && TenantOf(call) == _tenantId
            ? FakePlatformEdge.Json(HttpStatusCode.Conflict, new
            {
                statusCode = 409, isSuccessful = false, errors = new[] { "QUOTA_LIMIT_EXCEEDED" },
                quota = new { quotaKey = "users.max", limitValue = 3m, currentValue = 3m }
            })
            : null;
        using var client = _host.Client(_token, _tenantId);

        var enable = await client.PostAsync($"api/users/{target}/enable", null);
        var edit = await client.PutAsJsonAsync($"api/users/{target}", new { firstName = "Off", lastName = "Line", isActive = true });

        Assert.Equal(HttpStatusCode.Conflict, enable.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        using var body = JsonDocument.Parse(await enable.Content.ReadAsStringAsync());
        Assert.Equal(UserLifecycle.QuotaExceededCode, body.RootElement.GetProperty("errorCodes")[0].GetProperty("code").GetString());
        var stored = await _host.Database.GetCollection<User>("users").Find(u => u.Id == target).SingleAsync();
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task With_the_quota_service_down_the_create_goes_ahead_and_says_so_in_the_log()
    {
        var email = NewEmail();
        using var client = _host.Client(_token, _tenantId);

        _host.Platform.Down = true;
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync("api/users", new { email, firstName = "Open", lastName = "Door" });
        }
        finally
        {
            _host.Platform.Down = false;
        }

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, await _host.Database.GetCollection<User>("users").CountDocumentsAsync(u => u.Email == email && u.TenantId == _tenantId));
        Assert.Contains(_host.Logs.Entries, e => e.Level == LogLevel.Warning
                                                 && e.Message.Contains("quota_unavailable", StringComparison.Ordinal)
                                                 && e.Message.Contains(_tenantId.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_quota_answer_that_is_not_the_limit_refusal_does_not_block_the_create()
    {
        var email = NewEmail();
        // Platform has no users.max row for this tenant (e.g. no subscription): 404 QUOTA_USAGE_NOT_FOUND.
        _host.Platform.Responder = call => call.Path == ConsumePath && TenantOf(call) == _tenantId
            ? FakePlatformEdge.Json(HttpStatusCode.NotFound, new { statusCode = 404, isSuccessful = false, errors = new[] { "QUOTA_USAGE_NOT_FOUND" } })
            : null;
        using var client = _host.Client(_token, _tenantId);

        var response = await client.PostAsJsonAsync("api/users", new { email, firstName = "No", lastName = "Plan" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains(_host.Logs.Entries, e => e.Message.Contains("quota_unavailable", StringComparison.Ordinal)
                                                 && e.Message.Contains(_tenantId.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private IReadOnlyList<FakePlatformEdge.Call> TenantCalls(string path)
        => _host.Platform.CallsTo(path).Where(c => TenantOf(c) == _tenantId || TargetTenantOf(c) == _tenantId).ToList();

    private static Guid? TenantOf(FakePlatformEdge.Call call)
        => call.Body.TryGetProperty("tenantId", out var t) && t.ValueKind == JsonValueKind.String ? t.GetGuid() : null;

    private static Guid? TargetTenantOf(FakePlatformEdge.Call call)
        => call.Body.TryGetProperty("targetTenantId", out var t) && t.ValueKind == JsonValueKind.String ? t.GetGuid() : null;

    private static string NewEmail() => $"q.{Guid.NewGuid():N}@quota.test";

    private async Task<Guid> NewInactiveUserAsync()
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(_tenantId);
        var user = new User(NewEmail(), "hash:x", "Off", "Line", _tenantId);
        user.ConfirmEmail();
        user.Deactivate();
        return (await scope.ServiceProvider.GetRequiredService<IUserRepository>().CreateAsync(user, CancellationToken.None)).Id;
    }

    /// <summary>A live caller holding a role with auth.users.create (the delete guard needs one) and the write keys.</summary>
    private async Task<string> SeedActorTokenAsync()
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(_tenantId);
        var users = sp.GetRequiredService<IUserRepository>();
        var roles = sp.GetRequiredService<IRoleRepository>();
        var userRoles = sp.GetRequiredService<IUserRoleRepository>();

        var actor = new User($"actor.{Guid.NewGuid():N}@quota.test", "hash:x", "Quota", "Actor", _tenantId);
        actor.ConfirmEmail();
        actor = await users.CreateAsync(actor, CancellationToken.None);

        var stewards = await roles.CreateAsync(new Role("Stewards", "Stewards", "quota fixture", _tenantId), CancellationToken.None);
        var create = await _host.Database.GetCollection<Permission>("permissions").Find(p => p.Key == "auth.users.create").SingleAsync();
        await _host.Database.GetCollection<RolePermission>("rolePermissions")
            .InsertOneAsync(RolePermission.ManualGrant(stewards.Id, create.Id, _tenantId, "quota-fixture"));
        await userRoles.AssignAsync(new UserRole(actor.Id, stewards.Id, _tenantId, "quota-fixture"), CancellationToken.None);

        return sp.GetRequiredService<ITokenService>().GenerateAccessToken(actor, ["Stewards"],
            ["auth.users.read", "auth.users.create", "auth.users.update", "auth.users.delete"], expiresInMinutes: 60);
    }
}
