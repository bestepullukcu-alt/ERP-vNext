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
        var usable = await CheckLinkAsync(reset.Email, _host.TenantEmails.LastTokenFor(reset.Email));
        Assert.Equal(HttpStatusCode.NoContent, usable.StatusCode);
        Assert.Equal(before, await RawUserAsync(reset.Id));

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

    /// <summary>The pre-check is limited like the link door: links that do not match are counted per client (and address) and
    /// earn the 429; the owner's valid link is never counted and still answers 204 from the same client.</summary>
    [Fact]
    public async Task The_link_check_counts_only_links_that_do_not_match()
    {
        var tenantId = _host.Seeded.TenantId;
        var user = await SeedTenantUserAsync(tenantId);
        Assert.Equal(HttpStatusCode.OK, (await ResetOnUsersScreenAsync(tenantId, user.Id)).StatusCode);
        var link = _host.TenantEmails.LastTokenFor(user.Email);
        var peer = RandomPeer();

        for (var i = 0; i < Diten.AuthService.Infrastructure.Security.PasswordDoorRateLimiter.DefaultPerAddressLimit; i++)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await PeerPostAsync(peer, "api/users/set-password/check", new { email = user.Email, token = link })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PeerPostAsync(peer, "api/users/set-password/check", new { email = user.Email, token = "junk" + i })).StatusCode);
        }

        var refused = await PeerPostAsync(peer, "api/users/set-password/check", new { email = user.Email, token = "junk-over" });
        Assert.Equal((HttpStatusCode)429, refused.StatusCode);
        Assert.Contains(AuthRefusalCodes.TooManyRequests, await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NoContent, (await PeerPostAsync(peer, "api/users/set-password/check", new { email = user.Email, token = link })).StatusCode);
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
