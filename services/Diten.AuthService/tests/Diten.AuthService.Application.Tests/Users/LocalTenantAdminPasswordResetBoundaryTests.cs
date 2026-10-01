using System.Security.Claims;
using Diten.AuthService.Api.Controllers;
using Microsoft.Extensions.Hosting;

namespace Diten.AuthService.Application.Tests.Users;

public sealed class LocalTenantAdminPasswordResetBoundaryTests
{
    private static readonly Guid Tenant = Guid.Parse("74355e70-4c7d-410c-8cf6-db5fe3b9547f");
    private static readonly Guid UserId = Guid.Parse("a75d3a7e-84cf-465c-8585-e16cc0d74b3c");
    private static readonly Guid Operator = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Exact_development_actor_and_target_are_accepted()
    {
        Assert.True(Check("Development", Principal(), Settings(), Tenant, "sku-local-admin@example.test", out var userId, out var operatorId));
        Assert.Equal(UserId, userId);
        Assert.Equal(Operator, operatorId);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Non_development_environments_are_rejected(string environment)
    {
        Assert.False(Check(environment, Principal(), Settings(), Tenant, "sku-local-admin@example.test", out var userId, out var operatorId));
        Assert.Equal(Guid.Empty, userId);
        Assert.Equal(Guid.Empty, operatorId);
    }

    [Fact]
    public void Wrong_target_or_missing_process_flag_is_rejected()
    {
        Assert.False(Check("Development", Principal(), Settings(), Guid.NewGuid(), "sku-local-admin@example.test", out _, out _));
        Assert.False(Check("Development", Principal(), Settings(), Tenant, "other@example.test", out _, out _));
        var settings = Settings();
        settings["ENABLED"] = null;
        Assert.False(Check("Development", Principal(), settings, Tenant, "sku-local-admin@example.test", out _, out _));
        settings = Settings();
        settings["EMAIL"] = null;
        Assert.False(Check("Development", Principal(), settings, Tenant, "sku-local-admin@example.test", out _, out _));
    }

    [Fact]
    public void Wrong_or_ambiguous_platform_actor_is_rejected()
    {
        Assert.False(Check("Development", Principal(actorType: "tenant_user"), Settings(), Tenant, "sku-local-admin@example.test", out _, out _));
        Assert.False(Check("Development", Principal(operatorId: Guid.NewGuid()), Settings(), Tenant, "sku-local-admin@example.test", out _, out _));
        Assert.False(Check("Development", Principal(tenantId: Tenant), Settings(), Tenant, "sku-local-admin@example.test", out _, out _));
        Assert.False(Check("Development", Principal(passwordChange: "true"), Settings(), Tenant, "sku-local-admin@example.test", out _, out _));
        var duplicate = Principal();
        ((ClaimsIdentity)duplicate.Identity!).AddClaim(new Claim("actor_type", "platform_admin"));
        Assert.False(Check("Development", duplicate, Settings(), Tenant, "sku-local-admin@example.test", out _, out _));
        Assert.False(Check("Development", new ClaimsPrincipal(new ClaimsIdentity(Principal().Claims)), Settings(), Tenant, "sku-local-admin@example.test", out _, out _));
    }

    private static bool Check(string environment, ClaimsPrincipal principal, IReadOnlyDictionary<string, string?> settings,
        Guid tenant, string email, out Guid userId, out Guid operatorId) =>
        LocalTenantAdminPasswordResetBoundary.TryAuthorize(new TestEnvironment(environment), principal, settings,
            tenant, email, out userId, out operatorId);

    private static Dictionary<string, string?> Settings() => new(StringComparer.Ordinal)
    {
        ["ENABLED"] = "true", ["TENANT_ID"] = Tenant.ToString(), ["USER_ID"] = UserId.ToString(),
        ["OPERATOR_ID"] = Operator.ToString(), ["EMAIL"] = "sku-local-admin@example.test"
    };

    private static ClaimsPrincipal Principal(string actorType = "platform_admin", Guid? operatorId = null,
        Guid? tenantId = null, string passwordChange = "false") =>
        new(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, (operatorId ?? Operator).ToString()),
            new Claim("actor_type", actorType),
            new Claim("tenant_id", (tenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001")).ToString()),
            new Claim("pwd_change_required", passwordChange)
        ], "test"));

    private sealed class TestEnvironment(string environment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environment;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
