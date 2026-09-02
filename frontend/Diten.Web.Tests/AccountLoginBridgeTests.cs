using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Diten.Web.Controllers;
using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.Web.Tests;

public sealed class AccountLoginBridgeTests
{
    private static readonly Guid TenantId = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");

    [Fact]
    public async Task AccountLogin_without_tenant_id_accepts_platform_admin_when_platform_auth_succeeds()
    {
        var gateway = new StubAuthGateway
        {
            PlatformResult = SuccessResult(CreateToken("platform_admin"))
        };
        var cookies = new RecordingCookieService();
        var controller = CreateController(gateway, cookies);

        var result = await controller.Login(new AccountController.LoginRequest(
            "admin@diten.com",
            "valid-password",
            null,
            null,
            false), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AccountController.LoginBridgeResponse>(ok.Value);
        Assert.Equal("/Platform/Tenants", payload.RedirectUrl);
        Assert.True(cookies.Cleared);
        Assert.True(cookies.WroteTokens);
        Assert.Equal(1, gateway.PlatformLoginCalls);
        Assert.Equal(0, gateway.TenantLoginCalls);
    }

    [Fact]
    public async Task AccountLogin_without_tenant_id_rejects_wrong_platform_password()
    {
        var gateway = new StubAuthGateway
        {
            PlatformResult = FailureResult("Invalid credentials.")
        };
        var cookies = new RecordingCookieService();
        var controller = CreateController(gateway, cookies);

        var result = await controller.Login(new AccountController.LoginRequest(
            "admin@diten.com",
            "wrong-password",
            null,
            null,
            false), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.False(cookies.WroteTokens);
        Assert.Equal(1, gateway.PlatformLoginCalls);
        Assert.Equal(0, gateway.TenantLoginCalls);
    }

    [Fact]
    public async Task AccountLogin_with_wrong_tenant_stays_on_tenant_auth_path()
    {
        var wrongTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var gateway = new StubAuthGateway
        {
            TenantResult = FailureResult("Invalid credentials.")
        };
        var cookies = new RecordingCookieService();
        var controller = CreateController(gateway, cookies);

        var result = await controller.Login(new AccountController.LoginRequest(
            "admin@diten.com",
            "valid-password",
            wrongTenantId,
            null,
            false), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.False(cookies.WroteTokens);
        Assert.Equal(wrongTenantId, gateway.LastTenantId);
        Assert.Equal(0, gateway.PlatformLoginCalls);
        Assert.Equal(1, gateway.TenantLoginCalls);
    }

    [Fact]
    public async Task AccountLogin_without_tenant_id_rejects_non_platform_actor_token()
    {
        var gateway = new StubAuthGateway
        {
            PlatformResult = SuccessResult(CreateToken("tenant_user"))
        };
        var cookies = new RecordingCookieService();
        var controller = CreateController(gateway, cookies);

        var result = await controller.Login(new AccountController.LoginRequest(
            "admin@diten.com",
            "valid-password",
            null,
            null,
            false), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.False(cookies.WroteTokens);
    }

    private static AccountController CreateController(IAuthGateway gateway, IAuthCookieService cookieService)
    {
        var controller = new AccountController(gateway, cookieService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private static AuthBridgeResult SuccessResult(string accessToken) => new(
        true,
        accessToken,
        "refresh-token",
        DateTime.UtcNow.AddHours(1),
        new AuthBridgeUser(Guid.NewGuid(), "admin@diten.com", "Platform", "Admin", true, ["PlatformAdmin"], null),
        null);

    private static AuthBridgeResult FailureResult(string error) => new(
        false,
        null,
        null,
        null,
        null,
        error);

    private static string CreateToken(string actorType)
    {
        var token = new JwtSecurityToken(claims: [new Claim("actor_type", actorType)]);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class RecordingCookieService : IAuthCookieService
    {
        public bool Cleared { get; private set; }
        public bool WroteTokens { get; private set; }

        public void WriteTokens(HttpResponse response, string accessToken, string refreshToken, DateTime refreshExpiresAtUtc)
        {
            WroteTokens = true;
        }

        public void ClearTokens(HttpResponse response)
        {
            Cleared = true;
        }
    }

    private sealed class StubAuthGateway : IAuthGateway
    {
        public AuthBridgeResult PlatformResult { get; init; } = FailureResult("Platform login failed.");
        public AuthBridgeResult TenantResult { get; init; } = FailureResult("Tenant login failed.");
        public int PlatformLoginCalls { get; private set; }
        public int TenantLoginCalls { get; private set; }
        public Guid? LastTenantId { get; private set; }

        public Task<AuthBridgeResult> LoginTenantAsync(string email, string password, Guid tenantId, bool rememberMe = false, CancellationToken ct = default)
        {
            TenantLoginCalls++;
            LastTenantId = tenantId;
            return Task.FromResult(TenantResult);
        }

        public Task<AuthBridgeResult> LoginPlatformAsync(string email, string password, bool rememberMe = false, CancellationToken ct = default)
        {
            PlatformLoginCalls++;
            return Task.FromResult(PlatformResult);
        }

        public Task<AuthBridgeResult> VerifyTenantMfaAsync(string challengeId, string code, CancellationToken ct = default) =>
            Task.FromResult(FailureResult("Not implemented."));

        public Task<AuthBridgeResult> ResendTenantMfaAsync(string challengeId, CancellationToken ct = default) =>
            Task.FromResult(FailureResult("Not implemented."));

        public Task<AuthBridgeResult> ChangePlatformPasswordAsync(string currentPassword, string newPassword, bool rememberMe = false, CancellationToken ct = default) =>
            Task.FromResult(FailureResult("Not implemented."));

        public Task<bool> ForgotPlatformPasswordAsync(string email, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<AuthBridgeResult> ResetPlatformPasswordAsync(string email, string token, string newPassword, CancellationToken ct = default) =>
            Task.FromResult(FailureResult("Not implemented."));

        public Task<AuthBridgeResult> RefreshAsync(string accessToken, string refreshToken, Guid? tenantId, CancellationToken ct = default) =>
            Task.FromResult(FailureResult("Not implemented."));

        public Task LogoutAsync(string accessToken, string refreshToken, Guid? tenantId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
