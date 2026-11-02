using Diten.Application.EnterpriseStrategy.Shared;
using System.Globalization;
using System.Security.Claims;

namespace Diten.Application.Tests;

[Collection("EnterpriseStrategyAuthorization")]
// Environment variables are process-wide; these tests must not overlap other test collections.
[CollectionDefinition("EnterpriseStrategyAuthorization", DisableParallelization = true)]
public sealed class EnterpriseStrategyAuthorizationTests : IDisposable
{
    private const string EnforceVariable = "DITEN_ESBP_ENFORCE_PERMISSIONS";
    private const string BootstrapVariable = "DITEN_ESBP_DEV_PERMISSION_BOOTSTRAP";
    private readonly string? _previousEnforce = Environment.GetEnvironmentVariable(EnforceVariable);
    private readonly string? _previousBootstrap = Environment.GetEnvironmentVariable(BootstrapVariable);
    private readonly DefaultEnterpriseStrategyAuthorizationService _service = new();

    public EnterpriseStrategyAuthorizationTests()
    {
        Environment.SetEnvironmentVariable(EnforceVariable, null);
        Environment.SetEnvironmentVariable(BootstrapVariable, null);
    }

    [Fact]
    public async Task No_Environment_Anonymous_User_Is_Denied()
    {
        Assert.False(await _service.HasPermissionAsync(
            EnterpriseStrategyPermissions.GoalView, new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public async Task No_Environment_Authenticated_User_Without_Permission_Is_Denied()
    {
        Assert.False(await _service.HasPermissionAsync(EnterpriseStrategyPermissions.GoalView, Authenticated()));
    }

    [Fact]
    public async Task Missing_Principal_Or_Identity_Is_Denied()
    {
        Assert.False(await _service.HasPermissionAsync(EnterpriseStrategyPermissions.GoalView, null!));
        Assert.False(await _service.HasPermissionAsync(EnterpriseStrategyPermissions.GoalView, new ClaimsPrincipal()));
    }

    [Fact]
    public async Task Unauthenticated_Permission_Claim_Is_Denied()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("permission", EnterpriseStrategyPermissions.GoalView)
        }));

        Assert.False(await _service.HasPermissionAsync(EnterpriseStrategyPermissions.GoalView, user));
    }

    [Theory]
    [InlineData("permission", "")]
    [InlineData("permission", " ")]
    [InlineData("permission", EnterpriseStrategyPermissions.GoalCreate)]
    [InlineData("permissions", EnterpriseStrategyPermissions.GoalView)]
    [InlineData(ClaimTypes.Role, "Admin")]
    [InlineData(ClaimTypes.Role, EnterpriseStrategyPermissions.GoalView)]
    public async Task Nonmatching_Claim_Does_Not_Grant_Permission(string type, string value)
    {
        Assert.False(await _service.HasPermissionAsync(
            EnterpriseStrategyPermissions.GoalView, Authenticated(new Claim(type, value))));
    }

    [Fact]
    public async Task Matching_Explicit_Permission_Is_Granted()
    {
        var user = Authenticated(
            new Claim("permission", EnterpriseStrategyPermissions.GoalCreate),
            new Claim("permission", EnterpriseStrategyPermissions.GoalView));

        Assert.True(await _service.HasPermissionAsync(EnterpriseStrategyPermissions.GoalView, user));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public async Task Existing_Claim_Matching_Remains_Culture_Independent_And_Case_Insensitive(string culture)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            var user = Authenticated(new Claim("PERMISSION", EnterpriseStrategyPermissions.InitiativeView));

            Assert.True(await _service.HasPermissionAsync("STRATEGY.INITIATIVE.VIEW", user));
            Assert.False(await _service.HasPermissionAsync(EnterpriseStrategyPermissions.InitiativeLink, user));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("", "", false)]
    [InlineData("1", null, true)]
    [InlineData("true", null, true)]
    [InlineData("TRUE", null, true)]
    [InlineData("yes", null, false)]
    [InlineData("false", null, false)]
    [InlineData("0", null, false)]
    [InlineData("garbage", null, false)]
    [InlineData(" ", null, false)]
    [InlineData(" true ", null, false)]
    [InlineData("1", "1", false)]
    [InlineData("1", "true", false)]
    [InlineData("1", "yes", false)]
    [InlineData("1", "garbage", false)]
    [InlineData("1", "false", true)]
    [InlineData("1", "0", true)]
    [InlineData(null, "false", false)]
    [InlineData("garbage", "false", false)]
    public async Task Bootstrap_Requires_Explicit_Opt_In_Authentication_And_Known_Permission(
        string? bootstrap, string? enforcePermissions, bool bootstrapExpected)
    {
        Environment.SetEnvironmentVariable(EnforceVariable, enforcePermissions);
        Environment.SetEnvironmentVariable(BootstrapVariable, bootstrap);

        Assert.False(await _service.HasPermissionAsync(EnterpriseStrategyPermissions.GoalView, null!));
        Assert.False(await _service.HasPermissionAsync(EnterpriseStrategyPermissions.GoalView, new ClaimsPrincipal()));
        Assert.False(await _service.HasPermissionAsync(EnterpriseStrategyPermissions.GoalView,
            new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("permission", EnterpriseStrategyPermissions.GoalView) }))));
        Assert.Equal(bootstrapExpected, await _service.HasPermissionAsync(EnterpriseStrategyPermissions.GoalView, Authenticated()));
        Assert.False(await _service.HasPermissionAsync("strategy.unknown.view", Authenticated()));
        Assert.True(await _service.HasPermissionAsync(EnterpriseStrategyPermissions.GoalView,
            Authenticated(new Claim("permission", EnterpriseStrategyPermissions.GoalView))));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public async Task Bootstrap_Opt_In_Is_Culture_Independent(string culture)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            Environment.SetEnvironmentVariable(BootstrapVariable, "TRUE");

            Assert.True(await _service.HasPermissionAsync(EnterpriseStrategyPermissions.InitiativeView, Authenticated()));

            Environment.SetEnvironmentVariable(EnforceVariable, "TRUE");
            Assert.False(await _service.HasPermissionAsync(EnterpriseStrategyPermissions.InitiativeView, Authenticated()));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EnforceVariable, _previousEnforce);
        Environment.SetEnvironmentVariable(BootstrapVariable, _previousBootstrap);
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));
}
