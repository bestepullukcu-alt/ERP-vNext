using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.ESignReauth;

/// <summary>
/// MOD-0022 E1 — e-signature re-authentication, end to end over HTTP on the test-owned mongod (never 27017): the real
/// controllers, pipeline, handlers, repositories and indexes. Platform's login-settings edge is replaced in-process
/// with the measured tenant defaults (5 failed attempts / 15 minutes).
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class ESignReauthEndpointTests : IClassFixture<ESignReauthEndpointTests.ESignReauthTestHost>
{
    private const string Reauth = "api/tenant-auth/esign/reauth";
    private const string WindowTicket = "api/tenant-auth/esign/ticket";
    private const string Consume = "internal/esign-tickets/consume";
    private const string BrowserA = "Mozilla/5.0 (FX E1 browser A)";

    private readonly ESignReauthTestHost _host;

    public ESignReauthEndpointTests(ESignReauthTestHost host) => _host = host;

    // ── §16 E1: correct password → ticket; no session, no token ───────────────────────────────────────
    [Fact]
    public async Task Correct_password_returns_a_ticket_and_opens_no_session()
    {
        var user = await NewUserAsync();
        var refreshBefore = await RefreshTokenCountAsync(user);
        using var client = _host.Client(user.Token);

        var response = await client.PostAsJsonAsync(Reauth, new { password = user.Password, intent = "sign" });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} {body}");
        Assert.False(response.Headers.Contains("Set-Cookie"));
        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        var ticket = data.GetProperty("ticket").GetString();
        Assert.False(string.IsNullOrWhiteSpace(ticket));
        Assert.Equal("Password", data.GetProperty("kind").GetString());
        var expiresIn = data.GetProperty("expiresAtUtc").GetDateTime().ToUniversalTime() - DateTime.UtcNow;
        Assert.InRange(expiresIn.TotalSeconds, 60, 125); // CT-D1: 120 s
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(refreshBefore, await RefreshTokenCountAsync(user));

        // Only the keyed hash is stored — never the ticket itself.
        var stored = Assert.Single(await Tickets().Find(t => t.UserId == user.Id && t.TenantId == user.TenantId).ToListAsync());
        Assert.NotEqual(ticket, stored.TicketHash);
        Assert.False(JsonSerializer.Serialize(stored).Contains(ticket!, StringComparison.Ordinal), "the stored ticket row contains the plain ticket");
        Assert.Equal("sign", stored.Intent);
        Assert.Null(stored.ConsumedAtUtc);

        Assert.Equal(1, await AuditCountAsync(user, "tenant_esign_reauth_succeeded"));
    }

    // ── §16 E1: the login counter, the tenant lockout (5 / 15), login refused afterwards ────────────────
    [Fact]
    public async Task Wrong_passwords_share_the_login_counter_and_the_fifth_locks_reauth_and_login()
    {
        var user = await NewUserAsync();
        using var client = _host.Client(user.Token);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var wrong = await client.PostAsJsonAsync(Reauth, new { password = "not-the-password-" + attempt, intent = "sign" });
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            var code = await FirstErrorAsync(wrong);
            Assert.Equal("ESIGN_REAUTH_FAILED", code.Code);
            Assert.Equal((5 - attempt).ToString(), code.Params!["remainingAttempts"]);
            Assert.Equal(attempt, (await LoadUserAsync(user)).FailedLoginAttempts);
        }

        var fifth = await client.PostAsJsonAsync(Reauth, new { password = "not-the-password-5", intent = "sign" });
        Assert.Equal((HttpStatusCode)423, fifth.StatusCode);
        var locked = await FirstErrorAsync(fifth);
        Assert.Equal("ESIGN_ACCOUNT_LOCKED", locked.Code);
        var lockoutEnd = DateTime.Parse(locked.Params!["lockoutEndUtc"], null, System.Globalization.DateTimeStyles.RoundtripKind);
        Assert.InRange((lockoutEnd.ToUniversalTime() - DateTime.UtcNow).TotalMinutes, 14, 15.1);
        Assert.NotNull((await LoadUserAsync(user)).LockoutEnd);

        // Correct password while locked → 423, no ticket.
        var whileLocked = await client.PostAsJsonAsync(Reauth, new { password = user.Password, intent = "sign" });
        Assert.Equal((HttpStatusCode)423, whileLocked.StatusCode);
        Assert.Empty(await Tickets().Find(t => t.UserId == user.Id).ToListAsync());

        // The same lock refuses login (one counter, KS-13).
        using var anonymous = _host.Client(tenantHeader: user.TenantId);
        var login = await anonymous.PostAsJsonAsync("api/tenant-auth/login", new { email = user.Email, password = user.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        // Main sync 2026-10-13: the lane's sign-in door gives ONE answer for every refusal (WP-USERS-ROLES-CLOSE-FIX1 item 2,
        // BL-496) — a locked account is not told apart from a wrong password. The lock is proven by the door's own audit row.
        Assert.Equal("AUTH_INVALID_CREDENTIALS", (await FirstErrorAsync(login)).Code);
        Assert.DoesNotContain("locked", await login.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await AuditCountAsync(user, "tenant_login_blocked_by_lockout"));

        // Every attempt is one audit event.
        Assert.Equal(5, await AuditCountAsync(user, "tenant_esign_reauth_password_failed"));
        Assert.Equal(1, await AuditCountAsync(user, "tenant_esign_reauth_lockout_started"));
        Assert.Equal(1, await AuditCountAsync(user, "tenant_esign_reauth_blocked_by_lockout"));
    }

    [Fact]
    public async Task A_correct_reauth_resets_the_shared_counter()
    {
        var user = await NewUserAsync();
        using var client = _host.Client(user.Token);
        await client.PostAsJsonAsync(Reauth, new { password = "wrong-once", intent = "sign" });
        Assert.Equal(1, (await LoadUserAsync(user)).FailedLoginAttempts);

        var ok = await client.PostAsJsonAsync(Reauth, new { password = user.Password, intent = "sign" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(0, (await LoadUserAsync(user)).FailedLoginAttempts);
    }

    // ── §16 E1: only tenant users ────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public async Task A_platform_or_partner_token_is_refused_with_403(string actorType)
    {
        var user = await NewUserAsync();
        using var scope = _host.Factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<ITokenService>().GeneratePlatformAccessToken(
            user.Id, user.Email, "Plat", "Form", user.TenantId, actorType, [], [], 30);
        using var client = _host.Client(token);

        foreach (var (route, body) in new (string, object)[]
                 {
                     (Reauth, new { password = user.Password, intent = "sign" }),
                     (WindowTicket, new { intent = "sign", userName = user.Email })
                 })
        {
            var response = await client.PostAsJsonAsync(route, body);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{route}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            Assert.Equal("ESIGN_ACTOR_NOT_TENANT_USER", (await FirstErrorAsync(response)).Code);
        }
    }

    [Fact]
    public async Task Without_a_token_the_endpoints_answer_401()
    {
        // With a tenant header but no token: the [Authorize] gate answers 401.
        using var client = _host.Client(tenantHeader: Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Reauth, new { password = "x", intent = "sign" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(WindowTicket, new { intent = "sign", userName = "x" })).StatusCode);

        // With neither: the existing tenant middleware refuses first (400 Missing Tenant), as on every protected route.
        using var bare = _host.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await bare.PostAsJsonAsync(Reauth, new { password = "x", intent = "sign" })).StatusCode);
    }

    // ── §16 E1: internal consume — bound, single use ─────────────────────────────────────────────────
    [Fact]
    public async Task Consume_is_bound_to_user_tenant_intent_and_works_once()
    {
        var user = await NewUserAsync();
        var other = await NewUserAsync();
        var ticket = await ReauthAsync(user, "sign");
        using var s2s = InternalClient();

        Assert.Equal(HttpStatusCode.Conflict, (await s2s.PostAsJsonAsync(Consume, new { ticket, tenantId = user.TenantId, userId = other.Id, intent = "sign" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await s2s.PostAsJsonAsync(Consume, new { ticket, tenantId = Guid.NewGuid(), userId = user.Id, intent = "sign" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await s2s.PostAsJsonAsync(Consume, new { ticket, tenantId = user.TenantId, userId = user.Id, intent = "invalidate" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await s2s.PostAsJsonAsync(Consume, new { ticket = ticket + "x", tenantId = user.TenantId, userId = user.Id, intent = "sign" })).StatusCode);

        var first = await s2s.PostAsJsonAsync(Consume, new { ticket, tenantId = user.TenantId, userId = user.Id, intent = "sign", consumedBy = "platform-test" });
        var firstBody = await first.Content.ReadAsStringAsync();
        Assert.True(first.StatusCode == HttpStatusCode.OK, $"{(int)first.StatusCode} {firstBody}");
        using (var json = JsonDocument.Parse(firstBody))
        {
            Assert.Equal("Password", json.RootElement.GetProperty("data").GetProperty("kind").GetString());
            Assert.Equal("sign", json.RootElement.GetProperty("data").GetProperty("intent").GetString());
        }

        var second = await s2s.PostAsJsonAsync(Consume, new { ticket, tenantId = user.TenantId, userId = user.Id, intent = "sign" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("ESIGN_TICKET_INVALID", (await FirstErrorAsync(second)).Code);

        var row = Assert.Single(await Tickets().Find(t => t.UserId == user.Id).ToListAsync());
        Assert.NotNull(row.ConsumedAtUtc);
        Assert.Equal("platform-test", row.ConsumedBy);
        Assert.Equal(1, await AuditCountAsync(user, "tenant_esign_ticket_consumed"));
    }

    [Fact]
    public async Task Parallel_consumption_of_one_ticket_has_exactly_one_winner()
    {
        var user = await NewUserAsync();
        var ticket = await ReauthAsync(user, "sign");

        var attempts = Enumerable.Range(0, 12).Select(async _ =>
        {
            using var s2s = InternalClient();
            var r = await s2s.PostAsJsonAsync(Consume, new { ticket, tenantId = user.TenantId, userId = user.Id, intent = "sign" });
            return r.StatusCode;
        });
        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(11, results.Count(s => s == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task An_expired_ticket_is_refused()
    {
        var user = await NewUserAsync();
        var ticket = await ReauthAsync(user, "sign");
        await Tickets().UpdateOneAsync(t => t.UserId == user.Id,
            Builders<ESignReauthTicket>.Update.Set(t => t.ExpiresAtUtc, DateTime.UtcNow.AddSeconds(-1)));

        using var s2s = InternalClient();
        var r = await s2s.PostAsJsonAsync(Consume, new { ticket, tenantId = user.TenantId, userId = user.Id, intent = "sign" });
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    [Fact]
    public async Task Consume_without_the_internal_key_is_401()
    {
        var user = await NewUserAsync();
        var ticket = await ReauthAsync(user, "sign");
        using var client = _host.Client(user.Token);
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", "wrong-key");
        var r = await client.PostAsJsonAsync(Consume, new { ticket, tenantId = user.TenantId, userId = user.Id, intent = "sign" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Null(Assert.Single(await Tickets().Find(t => t.UserId == user.Id).ToListAsync()).ConsumedAtUtc);
    }

    // ── §16 E1: KS-3 window, CT-D7 user name ─────────────────────────────────────────────────────────
    [Fact]
    public async Task An_open_window_gives_a_window_ticket_without_the_password_for_the_same_browser_only()
    {
        var user = await NewUserAsync();
        using var browser = Browser(user, BrowserA);
        var opened = await browser.PostAsJsonAsync(Reauth, new { password = user.Password, intent = "sign", openWindowMinutes = 15 });
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        using (var json = JsonDocument.Parse(await opened.Content.ReadAsStringAsync()))
        {
            var windowEnd = json.RootElement.GetProperty("data").GetProperty("windowExpiresAtUtc").GetDateTime().ToUniversalTime();
            Assert.InRange((windowEnd - DateTime.UtcNow).TotalMinutes, 14, 15.1);
        }

        var ticket = await browser.PostAsJsonAsync(WindowTicket, new { intent = "sign", userName = user.Email });
        var body = await ticket.Content.ReadAsStringAsync();
        Assert.True(ticket.StatusCode == HttpStatusCode.OK, $"{(int)ticket.StatusCode} {body}");
        using (var json = JsonDocument.Parse(body))
        {
            Assert.Equal("Window", json.RootElement.GetProperty("data").GetProperty("kind").GetString());
        }

        // Another browser (user-agent) gets nothing from that window.
        using var otherBrowser = Browser(user, "Mozilla/5.0 (another browser)");
        var foreign = await otherBrowser.PostAsJsonAsync(WindowTicket, new { intent = "sign", userName = user.Email });
        Assert.Equal(HttpStatusCode.Conflict, foreign.StatusCode);
        Assert.Equal("ESIGN_REAUTH_WINDOW_EXPIRED", (await FirstErrorAsync(foreign)).Code);

        Assert.Equal(1, await AuditCountAsync(user, "tenant_esign_window_ticket_issued"));
    }

    [Fact]
    public async Task Window_ticket_needs_the_session_user_name_and_never_serves_sample_or_invalidate()
    {
        var user = await NewUserAsync();
        using var browser = Browser(user, BrowserA);
        await browser.PostAsJsonAsync(Reauth, new { password = user.Password, intent = "sign", openWindowMinutes = 10 });

        var mismatch = await browser.PostAsJsonAsync(WindowTicket, new { intent = "sign", userName = "someone.else@acceptance.invalid" });
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Equal("ESIGN_USERNAME_MISMATCH", (await FirstErrorAsync(mismatch)).Code);

        foreach (var intent in new[] { "sample", "invalidate" })
        {
            var refused = await browser.PostAsJsonAsync(WindowTicket, new { intent, userName = user.Email });
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Equal("ESIGN_INTENT_REQUIRES_PASSWORD", (await FirstErrorAsync(refused)).Code);
        }

        // A window cannot be opened for a non-sign intent.
        var badOpen = await browser.PostAsJsonAsync(Reauth, new { password = user.Password, intent = "sample", openWindowMinutes = 10 });
        Assert.Equal(HttpStatusCode.BadRequest, badOpen.StatusCode);
        var outOfRange = await browser.PostAsJsonAsync(Reauth, new { password = user.Password, intent = "sign", openWindowMinutes = 61 });
        Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);
    }

    [Fact]
    public async Task An_expired_window_gives_409()
    {
        var user = await NewUserAsync();
        using var browser = Browser(user, BrowserA);
        await browser.PostAsJsonAsync(Reauth, new { password = user.Password, intent = "sign", openWindowMinutes = 5 });
        await Windows().UpdateOneAsync(w => w.UserId == user.Id,
            Builders<ESignReauthWindow>.Update.Set(w => w.ExpiresAtUtc, DateTime.UtcNow.AddSeconds(-1)));

        var r = await browser.PostAsJsonAsync(WindowTicket, new { intent = "sign", userName = user.Email });
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    [Fact]
    public async Task A_password_change_closes_the_window()
    {
        var user = await NewUserAsync();
        using var browser = Browser(user, BrowserA);
        await browser.PostAsJsonAsync(Reauth, new { password = user.Password, intent = "sign", openWindowMinutes = 30 });
        Assert.Equal(HttpStatusCode.OK, (await browser.PostAsJsonAsync(WindowTicket, new { intent = "sign", userName = user.Email })).StatusCode);

        var newPassword = "Fx" + Guid.NewGuid().ToString("N")[..10] + "9Z";
        var changed = await browser.PostAsJsonAsync("api/auth/change-password", new { currentPassword = user.Password, newPassword });
        Assert.True(changed.StatusCode == HttpStatusCode.NoContent, $"{(int)changed.StatusCode} {await changed.Content.ReadAsStringAsync()}");

        var after = await browser.PostAsJsonAsync(WindowTicket, new { intent = "sign", userName = user.Email });
        Assert.Equal(HttpStatusCode.Conflict, after.StatusCode);
        var window = Assert.Single(await Windows().Find(w => w.UserId == user.Id).ToListAsync());
        Assert.Equal("password_changed", window.RevokedReason);
    }

    [Fact]
    public async Task Logout_closes_the_window()
    {
        var user = await NewUserAsync();
        using var anonymous = _host.Client(tenantHeader: user.TenantId);
        anonymous.DefaultRequestHeaders.UserAgent.ParseAdd("FxE1-logout/1.0");
        var login = await anonymous.PostAsJsonAsync("api/tenant-auth/login", new { email = user.Email, password = user.Password });
        var loginBody = await login.Content.ReadAsStringAsync();
        Assert.True(login.StatusCode == HttpStatusCode.OK, $"{(int)login.StatusCode} {loginBody}");
        string accessToken, refreshToken;
        using (var json = JsonDocument.Parse(loginBody))
        {
            accessToken = json.RootElement.GetProperty("data").GetProperty("accessToken").GetString()!;
            refreshToken = json.RootElement.GetProperty("data").GetProperty("refreshToken").GetString()!;
        }

        using var session = _host.Client(accessToken);
        session.DefaultRequestHeaders.UserAgent.ParseAdd("FxE1-logout/1.0");
        Assert.Equal(HttpStatusCode.OK, (await session.PostAsJsonAsync(Reauth, new { password = user.Password, intent = "sign", openWindowMinutes = 30 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostAsJsonAsync(WindowTicket, new { intent = "sign", userName = user.Email })).StatusCode);

        var logout = await session.PostAsJsonAsync("api/auth/logout", new { accessToken, refreshToken });
        Assert.True(logout.IsSuccessStatusCode, $"{(int)logout.StatusCode} {await logout.Content.ReadAsStringAsync()}");

        var after = await session.PostAsJsonAsync(WindowTicket, new { intent = "sign", userName = user.Email });
        Assert.Equal(HttpStatusCode.Conflict, after.StatusCode);
        Assert.Equal("logout", Assert.Single(await Windows().Find(w => w.UserId == user.Id).ToListAsync()).RevokedReason);
    }

    // ── §16 E1: nothing logs the password or the plain ticket ────────────────────────────────────────
    [Fact]
    public async Task No_log_line_contains_the_password_or_the_plain_ticket()
    {
        var user = await NewUserAsync();
        var wrongPassword = "Wrong-" + Guid.NewGuid().ToString("N");
        string? ticket = null;
        string? windowTicket = null;

        var captured = await ConsoleTee.CaptureAsync(async () =>
        {
            using var browser = Browser(user, BrowserA);
            await browser.PostAsJsonAsync(Reauth, new { password = wrongPassword, intent = "sign" });
            var ok = await browser.PostAsJsonAsync(Reauth, new { password = user.Password, intent = "sign", openWindowMinutes = 5 });
            using (var json = JsonDocument.Parse(await ok.Content.ReadAsStringAsync()))
            {
                ticket = json.RootElement.GetProperty("data").GetProperty("ticket").GetString();
            }

            var w = await browser.PostAsJsonAsync(WindowTicket, new { intent = "sign", userName = user.Email });
            using (var json = JsonDocument.Parse(await w.Content.ReadAsStringAsync()))
            {
                windowTicket = json.RootElement.GetProperty("data").GetProperty("ticket").GetString();
            }

            using var s2s = InternalClient();
            await s2s.PostAsJsonAsync(Consume, new { ticket, tenantId = user.TenantId, userId = user.Id, intent = "sign" });
            await s2s.PostAsJsonAsync(Consume, new { ticket, tenantId = user.TenantId, userId = user.Id, intent = "sign" });
        });

        Assert.False(string.IsNullOrWhiteSpace(ticket));
        Assert.Contains("/api/tenant-auth/esign/reauth", captured, StringComparison.Ordinal); // the request log WAS captured
        // Assert.False (not DoesNotContain) so a failure never prints the secret into the runner output.
        Assert.False(captured.Contains(user.Password, StringComparison.Ordinal), "a log line contains the correct password");
        Assert.False(captured.Contains(wrongPassword, StringComparison.Ordinal), "a log line contains the wrong password");
        Assert.False(captured.Contains(ticket!, StringComparison.Ordinal), "a log line contains the plain ticket");
        Assert.False(captured.Contains(windowTicket!, StringComparison.Ordinal), "a log line contains the plain window ticket");

        // …and no audit row carries them either.
        var audits = await _host.Database.GetCollection<AuthAuditLog>("authAuditLogs").Find(a => a.UserId == user.Id).ToListAsync();
        Assert.NotEmpty(audits);
        Assert.All(audits, a =>
        {
            Assert.False(a.Metadata.Contains(user.Password, StringComparison.Ordinal));
            Assert.False(a.Metadata.Contains(ticket!, StringComparison.Ordinal));
        });
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────────
    private sealed record TestUser(Guid Id, Guid TenantId, string Email, string Password, string Token);

    private async Task<TestUser> NewUserAsync()
    {
        var tenantId = Guid.NewGuid();
        var password = "Pw" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(10)) + "a1";
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var user = new User($"esign.{Guid.NewGuid():N}@acceptance.invalid", sp.GetRequiredService<IPasswordHasher>().Hash(password), "E", "Signer", tenantId);
        user.ConfirmEmail();
        var created = await sp.GetRequiredService<IUserRepository>().CreateAsync(user, CancellationToken.None);
        var token = sp.GetRequiredService<ITokenService>().GenerateAccessToken(created, [], [], 30);
        return new TestUser(created.Id, tenantId, created.Email, password, token);
    }

    private async Task<User> LoadUserAsync(TestUser user)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(user.TenantId);
        return await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAndTenantAsync(user.Id, user.TenantId, CancellationToken.None)
            ?? throw new InvalidOperationException("test user vanished");
    }

    private async Task<string> ReauthAsync(TestUser user, string intent)
    {
        using var client = _host.Client(user.Token);
        var response = await client.PostAsJsonAsync(Reauth, new { password = user.Password, intent });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} {body}");
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("data").GetProperty("ticket").GetString()!;
    }

    private HttpClient Browser(TestUser user, string userAgent)
    {
        var client = _host.Client(user.Token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        return client;
    }

    private HttpClient InternalClient()
    {
        var client = _host.Client();
        var key = _host.Factory.Services.GetRequiredService<IOptions<InternalEventAuthSettings>>().Value.ApiKey;
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", key);
        return client;
    }

    private IMongoCollection<ESignReauthTicket> Tickets() => _host.Database.GetCollection<ESignReauthTicket>("esignReauthTickets");

    private IMongoCollection<ESignReauthWindow> Windows() => _host.Database.GetCollection<ESignReauthWindow>("esignReauthWindows");

    private Task<long> RefreshTokenCountAsync(TestUser user) =>
        _host.Database.GetCollection<RefreshToken>("refreshTokens").CountDocumentsAsync(r => r.UserId == user.Id);

    private Task<long> AuditCountAsync(TestUser user, string eventName) =>
        _host.Database.GetCollection<AuthAuditLog>("authAuditLogs").CountDocumentsAsync(a => a.UserId == user.Id && a.EventName == eventName);

    private static async Task<ResponseError> FirstErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var first = json.RootElement.GetProperty("errorCodes")[0];
        var parameters = new Dictionary<string, string>();
        if (first.TryGetProperty("params", out var p) && p.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in p.EnumerateObject())
            {
                parameters[property.Name] = property.Value.GetString() ?? string.Empty;
            }
        }

        return new ResponseError(first.GetProperty("code").GetString()!, parameters);
    }

    /// <summary>Tees Console.Out/Error (the Serilog console sink resolves Console.Out per event) while the action runs.</summary>
    private static class ConsoleTee
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);

        public static async Task<string> CaptureAsync(Func<Task> action)
        {
            await Gate.WaitAsync();
            var originalOut = Console.Out;
            var originalError = Console.Error;
            var buffer = new StringWriter(new StringBuilder());
            var synchronized = TextWriter.Synchronized(buffer);
            Console.SetOut(new TeeWriter(originalOut, synchronized));
            Console.SetError(new TeeWriter(originalError, synchronized));
            try
            {
                await action();
                await Task.Delay(300); // let the request-completion log line land
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
                Gate.Release();
            }

            return buffer.ToString();
        }

        private sealed class TeeWriter(TextWriter first, TextWriter second) : TextWriter
        {
            public override Encoding Encoding => first.Encoding;
            public override void Write(char value) { first.Write(value); second.Write(value); }
            public override void Write(string? value) { first.Write(value); second.Write(value); }
            public override void WriteLine(string? value) { first.WriteLine(value); second.WriteLine(value); }
            public override void Flush() { first.Flush(); second.Flush(); }
        }
    }

    // ── host: Platform's login-settings edge answered in-process with the tenant defaults ─────────────
    public sealed class ESignReauthTestHost : AccountKindAcceptance.AuthTestHost
    {
        public ConcurrentQueue<string> PlatformRequests { get; } = new();

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            services.AddHttpClient<ITenantLoginSettingsClient, PlatformTenantLoginSettingsClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new LoginSettingsEdge(PlatformRequests));
        }
    }

    private sealed class LoginSettingsEdge(ConcurrentQueue<string> requests) : HttpMessageHandler
    {
        private static readonly Regex LoginSettingsPath = new(
            @"^/api/internal/tenants/(?<tenant>[0-9a-fA-F-]{36})/login-settings$", RegexOptions.Compiled);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            requests.Enqueue(path);
            var match = LoginSettingsPath.Match(path);
            if (request.Method != HttpMethod.Get || !match.Success)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var snapshot = new TenantLoginSettingsSnapshot(
                Guid.Parse(match.Groups["tenant"].Value),
                TwoFactorEnabled: false,
                MfaRequired: false,
                EmailLoginEnabled: true,
                PhoneLoginEnabled: false,
                PasswordMinLength: 8,
                PasswordRequireUppercase: true,
                PasswordRequireLowercase: true,
                PasswordRequireDigit: true,
                PasswordRequireSpecialChar: false,
                PasswordExpirationDays: null,
                SessionTimeoutMinutes: 30,
                RefreshTokenLifetimeDays: 7,
                MaxFailedLoginAttempts: 5,
                LockoutDurationMinutes: 15);
            var body = JsonSerializer.Serialize(new { succeeded = true, message = (string?)null, data = snapshot });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
