using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-USERS-ROLES-CLOSE-FIX1 items 2–4 — the sign-in / password doors' refusals (BL-496) and the set-password page's
/// pre-check (KR4), on the same real HTTP host as BL-529's tests.
/// <list type="bullet">
/// <item>Every refusal carries its code; the screens map the code, never the English.</item>
/// <item>ENUMERATION: an address that does not exist, a wrong password and a platform account at the tenant door are ONE
/// answer — same status, same sentence, same code.</item>
/// <item>The pre-check (<c>POST api/users/set-password/check</c>) says exactly what the redemption would — and writes
/// nothing. A link a deactivation deleted is "no longer valid", never "deactivated".</item>
/// </list>
/// </summary>
public sealed partial class AdminResetInvalidatesEndpointTests
{
    // ── item 2 (BL-496): one answer for every sign-in that did not match ─────────────────────────────────────────

    [Fact]
    public async Task An_unknown_address_a_wrong_password_and_a_platform_account_get_the_same_coded_answer_at_the_tenant_door()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        var platformAccount = new User($"pa.{Guid.NewGuid():N}@reset.test", new PasswordHasher().Hash(OldPassword), "Platform", "Account", tenantId);
        platformAccount.SetPlatformActorType("platform_admin");
        await _host.Database.GetCollection<User>("users").InsertOneAsync(platformAccount);

        var unknown = await RefusalOfAsync(await TenantLoginAsync(tenantId, $"nobody.{Guid.NewGuid():N}@reset.test", OldPassword));
        var wrong = await RefusalOfAsync(await TenantLoginAsync(tenantId, user.Email, WrongPassword));
        var platform = await RefusalOfAsync(await TenantLoginAsync(tenantId, platformAccount.Email, OldPassword));

        Assert.Equal(unknown, wrong);
        Assert.Equal(unknown, platform);
        Assert.Equal("401|Invalid email or password.|" + AuthRefusalCodes.InvalidCredentials, unknown);
    }

    [Fact]
    public async Task An_unknown_address_and_a_wrong_password_get_the_same_coded_answer_at_the_platform_door()
    {
        var email = await ProvisionedPlatformAdminAsync(OldPassword);

        var unknown = await RefusalOfAsync(await PlatformLoginAsync($"nobody.{Guid.NewGuid():N}@reset.test", OldPassword));
        var wrong = await RefusalOfAsync(await PlatformLoginAsync(email, WrongPassword));

        Assert.Equal(unknown, wrong);
        Assert.Equal("401|Invalid credentials.|" + AuthRefusalCodes.InvalidCredentials, unknown);
    }

    /// <summary>FIX2 item 1 — WITH A WRONG PASSWORD EVERY ACCOUNT STATE IS THE SAME ANSWER: no such address, a platform account
    /// at this door, a deactivated account, an invitation not yet accepted, a LOCKED account and an ordinary one — same status,
    /// sentence and code. Before, a locked account said "locked" before its password was looked at (and the failures that
    /// locked it were counted on deactivated and invited accounts too: their addresses could be read off the lock). The
    /// failure is still counted (a locked account included) but does not extend the running lockout.</summary>
    [Fact]
    public async Task A_wrong_password_is_one_answer_whatever_the_accounts_state()
    {
        var tenantId = _host.Seeded.TenantId;
        using var admin = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.create"), tenantId);
        var ordinary = await SeedTenantUserAsync(tenantId);
        var deactivated = await SeedTenantUserAsync(tenantId);
        await WriteAccountAsync(deactivated, a => a.DeactivateByAdministrator());
        var invitedEmail = $"invited.{Guid.NewGuid():N}@reset.test";
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("api/users", new { email = invitedEmail, firstName = "In", lastName = "Vited" })).StatusCode);
        var locked = await SeedTenantUserAsync(tenantId);
        var lockedUntil = DateTime.UtcNow.AddMinutes(10);
        // Locked the way it happens: its failures reached the threshold (5 on this host). The next failure reaches the lockout
        // write again — which must not move the end of the lock already running.
        await _host.Database.GetCollection<User>("users").UpdateOneAsync(u => u.Id == locked.Id,
            Builders<User>.Update.Set(u => u.LockoutEnd, lockedUntil).Set(u => u.FailedLoginAttempts, 5));
        var lockEndBefore = (await ReadUserAsync(locked.Id)).LockoutEnd;
        var platformAccount = new User($"pa.{Guid.NewGuid():N}@reset.test", new PasswordHasher().Hash(OldPassword), "Platform", "Account", tenantId);
        platformAccount.SetPlatformActorType("platform_admin");
        await _host.Database.GetCollection<User>("users").InsertOneAsync(platformAccount);

        var expected = "401|Invalid email or password.|" + AuthRefusalCodes.InvalidCredentials;
        foreach (var email in new[] { $"nobody.{Guid.NewGuid():N}@reset.test", platformAccount.Email, deactivated.Email, invitedEmail, locked.Email, ordinary.Email })
        {
            Assert.Equal(expected, await RefusalOfAsync(await TenantLoginAsync(tenantId, email, WrongPassword)));
        }

        var lockedAfter = await ReadUserAsync(locked.Id);
        Assert.Equal(6, lockedAfter.FailedLoginAttempts);                 // counted …
        Assert.Equal(lockEndBefore, lockedAfter.LockoutEnd);              // … the running lockout not extended
        Assert.Equal(1, (await ReadUserAsync(deactivated.Id)).FailedLoginAttempts);

        // The RIGHT password, and only it, learns the state.
        Assert.Equal("401|Account is locked. Please try again later.|" + AuthRefusalCodes.AccountLocked,
            await RefusalOfAsync(await TenantLoginAsync(tenantId, locked.Email, OldPassword)));
        Assert.Equal("401|Account is disabled.|" + AuthRefusalCodes.AccountDeactivated,
            await RefusalOfAsync(await TenantLoginAsync(tenantId, deactivated.Email, OldPassword)));
    }

    /// <summary>FIX2 item 2 — the branches that find NO account to check (an unknown address, a platform account at the tenant
    /// door, an unknown address at the platform door) still verify the submitted password once (against a fixed hash, with
    /// the same hasher): the answer's time does not tell an address that exists from one that does not. Measured by the
    /// verifications of each branch's own password, not by the clock (a timing test would be flaky).</summary>
    [Fact]
    public async Task Every_branch_that_finds_no_account_to_check_still_verifies_the_password_once()
    {
        var tenantId = _host.Seeded.TenantId;
        var platformAccount = new User($"pa.{Guid.NewGuid():N}@reset.test", new PasswordHasher().Hash(OldPassword), "Platform", "Account", tenantId);
        platformAccount.SetPlatformActorType("platform_admin");
        await _host.Database.GetCollection<User>("users").InsertOneAsync(platformAccount);
        string Fresh() => "Timing!" + Guid.NewGuid().ToString("N");
        var (unknownTenant, platformAtTenant, unknownPlatform) = (Fresh(), Fresh(), Fresh());

        Assert.Equal(HttpStatusCode.Unauthorized, (await TenantLoginAsync(tenantId, $"nobody.{Guid.NewGuid():N}@reset.test", unknownTenant)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await TenantLoginAsync(tenantId, platformAccount.Email, platformAtTenant)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PlatformLoginAsync($"nobody.{Guid.NewGuid():N}@reset.test", unknownPlatform)).StatusCode);

        Assert.Equal(1, GatedHasher.Verifications.GetValueOrDefault(unknownTenant));
        Assert.Equal(1, GatedHasher.Verifications.GetValueOrDefault(platformAtTenant));
        Assert.Equal(1, GatedHasher.Verifications.GetValueOrDefault(unknownPlatform));
    }

    /// <summary>The refusals said only AFTER the password matched (so they tell nothing about who exists) carry their own
    /// codes; the forced change's wrong current password too.</summary>
    [Fact]
    public async Task The_other_sign_in_and_forced_change_refusals_carry_their_codes()
    {
        var tenantId = _host.Seeded.TenantId;

        var disabled = await SeedTenantUserAsync(tenantId);
        await WriteAccountAsync(disabled, a => a.DeactivateByAdministrator());
        Assert.Equal("401|Account is disabled.|" + AuthRefusalCodes.AccountDeactivated,
            await RefusalOfAsync(await TenantLoginAsync(tenantId, disabled.Email, OldPassword)));

        var locked = await SeedTenantUserAsync(tenantId);
        await _host.Database.GetCollection<User>("users").UpdateOneAsync(u => u.Id == locked.Id,
            Builders<User>.Update.Set(u => u.LockoutEnd, DateTime.UtcNow.AddMinutes(10)));
        Assert.Equal("401|Account is locked. Please try again later.|" + AuthRefusalCodes.AccountLocked,
            await RefusalOfAsync(await TenantLoginAsync(tenantId, locked.Email, OldPassword)));

        var changing = await SeedTenantUserAsync(tenantId, mustChangePassword: true);
        var session = await TenantSessionAsync(tenantId, changing.Email, OldPassword);
        using var client = _host.Client(session.AccessToken);
        var change = await client.PostAsJsonAsync("api/tenant-auth/change-password/forced",
            new { currentPassword = WrongPassword, newPassword = NewPassword, rememberMe = false });
        Assert.Equal("401|Current password is incorrect.|" + AuthRefusalCodes.CurrentPasswordIncorrect, await RefusalOfAsync(change));
    }

    // ── item 3 (KR4): the set-password page's pre-check ─────────────────────────────────────────────────────────

    /// <summary>The pre-check and the redemption, for the same link, give the same answer — and the pre-check writes
    /// nothing (the stored account is byte-for-byte the same after it).</summary>
    [Fact]
    public async Task The_link_check_answers_what_the_redemption_would_and_writes_nothing()
    {
        var tenantId = _host.Seeded.TenantId;
        using var admin = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.create", "auth.users.update"), tenantId);

        // a usable link (a reset)
        var reset = await SeedTenantUserAsync(tenantId);
        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, reset.Id)).StatusCode);
        var before = await RawUserAsync(reset.Id);
        var everyCollectionBefore = await CollectionCountsAsync();
        var usable = await CheckLinkAsync(reset.Email, _host.TenantEmails.LastTokenFor(reset.Email));
        Assert.Equal(HttpStatusCode.NoContent, usable.StatusCode);
        Assert.Equal(before, await RawUserAsync(reset.Id));
        // FIX2 — "the check writes nothing", measured on the whole database: no audit row, no other document either.
        Assert.Equal(everyCollectionBefore, await CollectionCountsAsync());

        // a pending invitation an administrator deactivated: the link is still its own → "deactivated" (409), as the POST
        var email = $"invited.{Guid.NewGuid():N}@reset.test";
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("api/users", new { email, firstName = "In", lastName = "Vited" })).StatusCode);
        var invited = await TenantUserByEmailAsync(email, tenantId);
        Assert.True((await admin.PostAsync($"api/users/{invited.Id}/disable", null)).IsSuccessStatusCode);
        var deactivatedBefore = await RawUserAsync(invited.Id);
        var deactivated = await CheckLinkAsync(email, _host.TenantEmails.LastTokenFor(email));
        Assert.Equal("409|This account has been deactivated by an administrator.|" + AuthRefusalCodes.AccountDeactivated, await RefusalOfAsync(deactivated));
        Assert.Equal(deactivatedBefore, await RawUserAsync(invited.Id));
        Assert.Equal(HttpStatusCode.Conflict, (await RedeemTenantLinkAsync(email, NewPassword)).StatusCode);

        // a link that is not this address's, a made-up link, no link → one answer (400), as the POST
        var invalid = "400|Password setup link is invalid or expired.|" + AuthRefusalCodes.LinkInvalid;
        Assert.Equal(invalid, await RefusalOfAsync(await CheckLinkAsync($"other.{Guid.NewGuid():N}@reset.test", _host.TenantEmails.LastTokenFor(reset.Email))));
        Assert.Equal(invalid, await RefusalOfAsync(await CheckLinkAsync(reset.Email, "made-up-" + Guid.NewGuid().ToString("N"))));
        Assert.Equal(invalid, await RefusalOfAsync(await CheckLinkAsync(reset.Email, "")));
        var postOtherAddress = await AnonymousPostAsync("api/users/set-password",
            new { email = $"other.{Guid.NewGuid():N}@reset.test", token = _host.TenantEmails.LastTokenFor(reset.Email), newPassword = NewPassword });
        Assert.Equal(invalid, await RefusalOfAsync(postOtherAddress));

        // and the usable link still redeems after being checked (the check did not use it up)
        Assert.Equal(HttpStatusCode.NoContent, (await RedeemTenantLinkAsync(reset.Email, NewPassword)).StatusCode);
        Assert.Equal(invalid, await RefusalOfAsync(await CheckLinkAsync(reset.Email, _host.TenantEmails.LastTokenFor(reset.Email)))); // used → dead
    }

    /// <summary>Item 4 — the owner's kr-7 / kr-8 measurement (2026-10-09), by name: a RESET account is deactivated — its link
    /// was deleted by the deactivation (BL-529 FIX3), so the page shows the dead-link face (b), never "deactivated"; after
    /// the administrator activates it again the same link is STILL face (b) (the design: the link is gone, a new one is
    /// sent). Behaviour unchanged: the redemption refuses it the same way.</summary>
    [Fact]
    public async Task Kr7_kr8_reset_then_deactivated_link_shows_the_dead_link_face_and_still_does_after_activation()
    {
        var tenantId = _host.Seeded.TenantId;
        using var admin = _host.Client(await TenantAdminTokenAsync(tenantId, "auth.users.update"), tenantId);
        var burak = await SeedTenantUserAsync(tenantId);
        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, burak.Id)).StatusCode);
        var link = _host.TenantEmails.LastTokenFor(burak.Email);
        var deadLink = "400|Password setup link is invalid or expired.|" + AuthRefusalCodes.LinkInvalid;

        Assert.True((await admin.PostAsync($"api/users/{burak.Id}/disable", null)).IsSuccessStatusCode);
        var afterDeactivation = await RefusalOfAsync(await CheckLinkAsync(burak.Email, link));
        Assert.Equal(deadLink, afterDeactivation);
        Assert.DoesNotContain(AuthRefusalCodes.AccountDeactivated, afterDeactivation, StringComparison.Ordinal);

        Assert.True((await admin.PostAsync($"api/users/{burak.Id}/enable", null)).IsSuccessStatusCode);
        Assert.Equal(deadLink, await RefusalOfAsync(await CheckLinkAsync(burak.Email, link)));
        Assert.Equal(HttpStatusCode.BadRequest, (await RedeemTenantLinkAsync(burak.Email, NewPassword)).StatusCode);
    }

    /// <summary>FIX2 item 4 — the pre-check counts EVERY request (the valid link's too), per client, before it reads anything:
    /// past the per-client limit even the owner's valid link gets the 429 from that client, and another client is not
    /// affected (its own count).</summary>
    [Fact]
    public async Task The_link_check_counts_every_request_the_valid_links_too()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode);
        var link = _host.TenantEmails.LastTokenFor(user.Email);
        var peer = RandomPeer();

        for (var i = 0; i < Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.DefaultPerClientLimit; i++)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await PeerPostAsync(peer, "api/users/set-password/check", new { email = user.Email, token = link })).StatusCode);
        }

        var refused = await PeerPostAsync(peer, "api/users/set-password/check", new { email = user.Email, token = link });
        Assert.Equal((HttpStatusCode)429, refused.StatusCode);
        Assert.Contains(AuthRefusalCodes.TooManyRequests, await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NoContent, (await PeerPostAsync(RandomPeer(), "api/users/set-password/check", new { email = user.Email, token = link })).StatusCode);
    }

    /// <summary>FIX2 item 4 — the redemption POST had no limit; it is limited now, with its OWN count (a client that used up
    /// its checks can still redeem, and the reverse).</summary>
    [Fact]
    public async Task The_redemption_is_limited_too_with_its_own_count()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode);
        var peer = RandomPeer();

        for (var i = 0; i < Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.DefaultPerClientLimit; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await PeerPostAsync(peer, "api/users/set-password",
                new { email = user.Email, token = "junk-" + i, newPassword = NewPassword })).StatusCode);
        }

        var refused = await PeerPostAsync(peer, "api/users/set-password", new { email = user.Email, token = "junk-over", newPassword = NewPassword });
        Assert.Equal((HttpStatusCode)429, refused.StatusCode);
        Assert.Contains(AuthRefusalCodes.TooManyRequests, await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        // its own count: the same client's check is not refused
        Assert.Equal(HttpStatusCode.NoContent, (await PeerPostAsync(peer, "api/users/set-password/check",
            new { email = user.Email, token = _host.TenantEmails.LastTokenFor(user.Email) })).StatusCode);
    }

    private async Task<string> CollectionCountsAsync()
    {
        var names = await (await _host.Database.ListCollectionNamesAsync()).ToListAsync();
        var counts = new List<string>();
        foreach (var name in names.OrderBy(n => n, StringComparer.Ordinal))
            counts.Add($"{name}={await _host.Database.GetCollection<BsonDocument>(name).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)}");
        return string.Join(";", counts);
    }

    /// <summary>FIX2 item 5 — the link's lookup (anonymous, across tenants) is served by an index, not a scan of every user:
    /// the sparse index exists and the planner's winning plan for the lookup's own filter uses it.</summary>
    [Fact]
    public async Task The_link_lookup_has_its_index_and_uses_it()
    {
        var users = _host.Database.GetCollection<BsonDocument>("users");
        var index = Assert.Single(await (await users.Indexes.ListAsync()).ToListAsync(),
            x => x["name"] == Diten.AuthService.Persistence.Configurations.MongoDbIndexConfigurations.PasswordResetTokenHashIndexName);
        Assert.True(index.GetValue("sparse", false).ToBoolean());

        var explain = await _host.Database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            ["explain"] = new BsonDocument
            {
                ["find"] = "users",
                ["filter"] = new BsonDocument { ["PasswordResetTokenHash"] = "no-such-hash", ["IsDeleted"] = false },
                ["limit"] = 1
            },
            ["verbosity"] = "queryPlanner"
        });
        Assert.Contains(Diten.AuthService.Persistence.Configurations.MongoDbIndexConfigurations.PasswordResetTokenHashIndexName,
            explain["queryPlanner"]["winningPlan"].ToJson(), StringComparison.Ordinal);
    }

    private async Task<HttpResponseMessage> CheckLinkAsync(string email, string token)
        => await AnonymousPostAsync("api/users/set-password/check", new { email, token });

    private async Task<string> RawUserAsync(Guid userId)
        => (await _host.Database.GetCollection<BsonDocument>("users").Find(new BsonDocument("_id", new BsonBinaryData(userId, GuidRepresentation.Standard))).SingleAsync()).ToJson();

    /// <summary>"status|first English sentence|codes" — the whole of what a refusal says, to compare two refusals.</summary>
    private static async Task<string> RefusalOfAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var errors = root.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Array
            ? string.Join(" ", e.EnumerateArray().Select(x => x.GetString()))
            : "";
        var codes = root.TryGetProperty("errorCodes", out var c) && c.ValueKind == JsonValueKind.Array
            ? string.Join(",", c.EnumerateArray().Select(x => x.GetProperty("code").GetString()))
            : "";
        return $"{(int)response.StatusCode}|{errors}|{codes}";
    }
}
