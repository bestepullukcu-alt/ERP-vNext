using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.AuthService.Application.Tests.Password;

/// <summary>
/// WP-USERS-ERROR-CODES-01 (CT acceptance round, item 1) — the Api's GlobalExceptionHandler shapes EVERY
/// ValidationException, not only a pipeline validator's. <c>PasswordPolicyService</c> throws one from three places that
/// do not go through MediatR, so their wire changed too: <c>POST api/platform-auth/reset-password</c> (anonymous),
/// <c>POST api/platform-auth/change-password/forced</c> and <c>POST internal/events/tenant-admin-invited</c>.
/// Real HTTP on a test-owned mongod; the two Platform edges these doors ask (administrator status, login settings)
/// are fixed answers. A weak password keeps its four fields and gains <c>errorCodes</c> (with <c>minLength</c>); a
/// wrong token and an unknown e-mail answer exactly as before and exactly alike — nothing new leaks about accounts.
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class PasswordPolicyDoorEndpointTests : IClassFixture<PasswordPolicyDoorEndpointTests.Host>
{
    private static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string Weak = "weak";
    private const string CurrentPassword = "Current!Passw0rd";

    private readonly Host _host;

    public PasswordPolicyDoorEndpointTests(Host host) => _host = host;

    public sealed class Host : AccountKindAcceptance.AuthTestHost
    {
        protected override void ConfigureTestServices(IServiceCollection services)
        {
            base.ConfigureTestServices(services);
            services.AddScoped<IPlatformAdministratorStatusClient, AlwaysActive>();
            services.AddScoped<ITenantLoginSettingsClient, TenCharactersUpperAndSpecial>();
        }
    }

    [Fact]
    public async Task Platform_reset_with_a_weak_password_keeps_its_four_fields_and_gains_the_codes()
    {
        var (email, token, _) = await SeedPlatformUserAsync(withResetToken: true);
        using var client = _host.Client(null, PlatformTenantId);

        var response = await client.PostAsJsonAsync("api/platform-auth/reset-password", new { email, token, newPassword = Weak });

        AssertWeakPassword(await response.Content.ReadAsStringAsync(), response.StatusCode);
    }

    [Fact]
    public async Task Platform_reset_with_a_wrong_token_or_an_unknown_address_answers_as_before_and_alike()
    {
        var (email, _, _) = await SeedPlatformUserAsync(withResetToken: true);
        using var client = _host.Client(null, PlatformTenantId);

        var wrongToken = await client.PostAsJsonAsync("api/platform-auth/reset-password", new { email, token = "not-the-token", newPassword = Weak });
        var unknown = await client.PostAsJsonAsync("api/platform-auth/reset-password", new { email = $"nobody.{Guid.NewGuid():N}@policy.test", token = "not-the-token", newPassword = Weak });
        var wrongTokenBody = await wrongToken.Content.ReadAsStringAsync();
        var unknownBody = await unknown.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, wrongToken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(unknownBody, wrongTokenBody); // an existing account is not told apart from a missing one
        // The envelope it always was — the password is never judged before the token, so no policy code can leak.
        Assert.Equal("""{"data":{},"statusCode":400,"isSuccessful":false,"errors":["Password reset token is invalid or expired."],"errorCodes":[]}""", wrongTokenBody);
    }

    [Fact]
    public async Task Platform_forced_change_with_a_weak_password_keeps_its_four_fields_and_gains_the_codes()
    {
        var (_, _, accessToken) = await SeedPlatformUserAsync(withResetToken: false);
        using var client = _host.Client(accessToken, PlatformTenantId);

        var response = await client.PostAsJsonAsync("api/platform-auth/change-password/forced", new { currentPassword = CurrentPassword, newPassword = Weak, rememberMe = false });

        AssertWeakPassword(await response.Content.ReadAsStringAsync(), response.StatusCode);
    }

    [Fact]
    public async Task Platform_forced_change_with_the_wrong_current_password_answers_as_before()
    {
        var (_, _, accessToken) = await SeedPlatformUserAsync(withResetToken: false);
        using var client = _host.Client(accessToken, PlatformTenantId);

        var response = await client.PostAsJsonAsync("api/platform-auth/change-password/forced", new { currentPassword = "not-it", newPassword = Weak, rememberMe = false });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("""{"data":null,"statusCode":401,"isSuccessful":false,"errors":["Current password is incorrect."],"errorCodes":[]}""", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// The third door (<c>internal/events/tenant-admin-invited</c>) validates a password the service GENERATED from the
    /// same settings one line earlier. The generator satisfies every rule the validator has, for every settings shape —
    /// so that door cannot produce the refusal, and its wire did not change. Measured on the production service.
    /// </summary>
    [Theory]
    [InlineData(1, false, false)]
    [InlineData(8, true, false)]
    [InlineData(10, false, true)]
    [InlineData(64, true, true)]
    [InlineData(128, true, true)]
    [InlineData(500, true, true)]
    public async Task The_temporary_password_the_invitation_door_generates_always_passes_its_own_policy(int minLength, bool upper, bool special)
    {
        var settings = Settings(Guid.NewGuid(), minLength, upper, special);
        var policy = new PasswordPolicyService(new FixedSettings(settings), new ThrowingAudit());

        for (var i = 0; i < 200; i++)
        {
            // ThrowingAudit fails the test if the policy ever objects (the audit write precedes the ValidationException).
            await policy.ValidateTenantPasswordAsync(settings.TenantId, null, policy.GenerateTemporaryPassword(settings), "internal_temporary_password", CancellationToken.None);
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private static void AssertWeakPassword(string body, HttpStatusCode status)
    {
        Assert.True(status == HttpStatusCode.BadRequest, $"expected 400, got {(int)status}: {body}");
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.Equal(["title", "status", "detail", "traceId", "errorCodes"], root.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("Validation failed", root.GetProperty("title").GetString());
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal(
            "Validation failed: \n -- Password: Password must be at least 10 characters. Severity: Error\n -- Password: Password must contain at least one uppercase letter. Severity: Error\n -- Password: Password must contain at least one special character. Severity: Error",
            root.GetProperty("detail").GetString()!.ReplaceLineEndings("\n")); // Environment.NewLine on the wire

        var codes = root.GetProperty("errorCodes").EnumerateArray().ToList();
        Assert.Equal([PasswordErrorCodes.TooShort, PasswordErrorCodes.NeedsUppercase, PasswordErrorCodes.NeedsSpecial], codes.Select(c => c.GetProperty("code").GetString()));
        Assert.Equal("10", codes[0].GetProperty("params").GetProperty("minLength").GetString());
    }

    private async Task<(string Email, string ResetToken, string AccessToken)> SeedPlatformUserAsync(bool withResetToken)
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(PlatformTenantId);
        var resetToken = $"reset-{Guid.NewGuid():N}";

        var user = new User($"padmin.{Guid.NewGuid():N}@policy.test", sp.GetRequiredService<IPasswordHasher>().Hash(CurrentPassword), "Policy", "Door", PlatformTenantId);
        user.ConfirmEmail();
        if (withResetToken)
        {
            user.SetPasswordResetToken(sp.GetRequiredService<IRefreshTokenHasher>().Hash(resetToken), DateTime.UtcNow.AddHours(1));
        }

        user = await sp.GetRequiredService<IUserRepository>().CreateAsync(user, CancellationToken.None);
        var accessToken = sp.GetRequiredService<ITokenService>().GenerateAccessToken(user, [], [], expiresInMinutes: 60);
        return (user.Email, resetToken, accessToken);
    }

    private static TenantLoginSettingsSnapshot Settings(Guid tenantId, int minLength, bool upper, bool special) => new(
        tenantId, TwoFactorEnabled: false, MfaRequired: false, EmailLoginEnabled: true, PhoneLoginEnabled: false,
        PasswordMinLength: minLength, PasswordRequireUppercase: upper, PasswordRequireLowercase: true, PasswordRequireDigit: true,
        PasswordRequireSpecialChar: special, PasswordExpirationDays: null, SessionTimeoutMinutes: 60, RefreshTokenLifetimeDays: 7,
        MaxFailedLoginAttempts: 5, LockoutDurationMinutes: 15);

    private sealed class AlwaysActive : IPlatformAdministratorStatusClient
    {
        public Task<bool> IsActiveAsync(string email, CancellationToken ct) => Task.FromResult(true);
        public Task MarkLoginAcceptedAsync(string email, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class TenCharactersUpperAndSpecial : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(Settings(tenantId, 10, upper: true, special: true));
    }

    private sealed class FixedSettings(TenantLoginSettingsSnapshot settings) : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(settings);
    }

    private sealed class ThrowingAudit : IAuthAuditService
    {
        public Task WriteEmptyRoleLoginAsync(Guid userId, Guid tenantId, string email, CancellationToken ct = default) => throw new InvalidOperationException("not expected");
        public Task WriteAsync(string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default)
            => throw new InvalidOperationException($"the generated temporary password violated its own policy ({eventName})");
    }
}
