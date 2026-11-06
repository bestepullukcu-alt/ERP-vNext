using Diten.Application.DeliveryExecutionManagement.Shared;
using Diten.Application.EnterpriseStrategy.Shared;
using System.Globalization;
using System.Security.Claims;

namespace Diten.Application.Tests;

[Collection("DeliveryExecutionManagementAuthorization")]
// Environment variables are process-wide; these tests must not overlap other test collections.
[CollectionDefinition("DeliveryExecutionManagementAuthorization", DisableParallelization = true)]
public sealed class DeliveryExecutionManagementAuthorizationTests
{
    private readonly DefaultDeliveryExecutionManagementAuthorizationService _service = new();

    [Fact]
    public Task No_Environment_Anonymous_User_Is_Denied() => WithEnvironmentAsync(async () =>
    {
        Assert.False(await _service.HasPermissionAsync(
            DeliveryExecutionManagementPermissions.OverviewView, new ClaimsPrincipal()));
    });

    [Fact]
    public Task No_Environment_Authenticated_User_Without_Permission_Is_Denied() => WithEnvironmentAsync(async () =>
    {
        Assert.False(await _service.HasPermissionAsync(
            DeliveryExecutionManagementPermissions.OverviewView, Authenticated()));
    });

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("TRUE")]
    public Task Explicit_Bootstrap_Grants_Only_Authenticated_Known_Permissions(string bootstrap) =>
        WithEnvironmentAsync(async () =>
        {
            Assert.True(await _service.HasPermissionAsync(
                DeliveryExecutionManagementPermissions.OverviewView, Authenticated()));
            Assert.False(await _service.HasPermissionAsync(
                DeliveryExecutionManagementPermissions.OverviewView, new ClaimsPrincipal()));
            Assert.False(await _service.HasPermissionAsync(
                DeliveryExecutionManagementPermissions.OverviewView, null!));
            Assert.False(await _service.HasPermissionAsync("delivery-execution-management.unknown.view", Authenticated()));
        }, deliveryBootstrap: bootstrap);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("yes")]
    [InlineData("unknown")]
    [InlineData(" true ")]
    public Task Missing_Or_Unrecognized_Bootstrap_Does_Not_Grant_Permission(string? bootstrap) =>
        WithEnvironmentAsync(async () =>
        {
            Assert.False(await _service.HasPermissionAsync(
                DeliveryExecutionManagementPermissions.OverviewView, Authenticated()));
        }, deliveryBootstrap: bootstrap);

    [Theory]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("yes")]
    [InlineData("unknown")]
    public Task Enforcement_Flag_Alone_Never_Grants_Permission(string enforce) =>
        WithEnvironmentAsync(async () =>
        {
            Assert.False(await _service.HasPermissionAsync(
                DeliveryExecutionManagementPermissions.OverviewView, Authenticated()));
        }, deliveryEnforce: enforce);

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" ", true)]
    [InlineData("0", true)]
    [InlineData("false", true)]
    [InlineData("FALSE", true)]
    [InlineData("1", false)]
    [InlineData("true", false)]
    [InlineData("TRUE", false)]
    [InlineData("yes", false)]
    [InlineData("unknown", false)]
    [InlineData("no", false)]
    [InlineData(" false ", false)]
    public Task Enforcement_Flag_Can_Disable_But_Cannot_Enable_Bootstrap(string? enforce, bool expected) =>
        WithEnvironmentAsync(async () =>
        {
            Assert.Equal(expected, await _service.HasPermissionAsync(
                DeliveryExecutionManagementPermissions.OverviewView, Authenticated()));
            Assert.False(await _service.HasPermissionAsync(
                DeliveryExecutionManagementPermissions.OverviewView, new ClaimsPrincipal()));
        }, deliveryEnforce: enforce, deliveryBootstrap: "true");

    [Theory]
    [InlineData(null, null, null, "1", true)]
    [InlineData(null, null, null, "true", true)]
    [InlineData(" ", "", "0", "true", true)]
    [InlineData(null, null, "false", "true", true)]
    [InlineData(null, null, "1", "true", false)]
    [InlineData(null, null, "true", "true", false)]
    [InlineData(null, null, "yes", "true", false)]
    [InlineData(null, null, "unknown", "true", false)]
    [InlineData(null, null, null, "yes", false)]
    [InlineData(null, null, null, "false", false)]
    [InlineData(null, null, null, "unknown", false)]
    [InlineData("0", "true", "1", "false", true)]
    [InlineData("false", "1", "true", "false", true)]
    [InlineData("1", "true", "0", "true", false)]
    [InlineData("unknown", "true", "0", "true", false)]
    [InlineData(null, "false", null, "true", false)]
    [InlineData(null, "yes", null, "true", false)]
    [InlineData(null, "unknown", null, "true", false)]
    [InlineData(null, " true ", null, "true", false)]
    public Task Delivery_Flags_Take_Precedence_And_Blank_Flags_Fall_Back_To_ES(
        string? deliveryEnforce, string? deliveryBootstrap, string? esEnforce, string? esBootstrap, bool expected) =>
        WithEnvironmentAsync(async () =>
        {
            Assert.Equal(expected, await _service.HasPermissionAsync(
                DeliveryExecutionManagementPermissions.OverviewView, Authenticated()));
            Assert.False(await _service.HasPermissionAsync(
                DeliveryExecutionManagementPermissions.OverviewView, new ClaimsPrincipal()));
        }, deliveryEnforce, deliveryBootstrap, esEnforce, esBootstrap);

    [Theory]
    [InlineData(DeliveryExecutionManagementPermissions.InitiativeView, EnterpriseStrategyPermissions.InitiativeView)]
    [InlineData(DeliveryExecutionManagementPermissions.InitiativeLink, EnterpriseStrategyPermissions.InitiativeLink)]
    [InlineData(DeliveryExecutionManagementPermissions.InitiativeUnlink, EnterpriseStrategyPermissions.InitiativeUnlink)]
    [InlineData(DeliveryExecutionManagementPermissions.InitiativeSync, EnterpriseStrategyPermissions.InitiativeSync)]
    [InlineData(DeliveryExecutionManagementPermissions.ProjectView, EnterpriseStrategyPermissions.ProjectView)]
    [InlineData(DeliveryExecutionManagementPermissions.ProjectLink, EnterpriseStrategyPermissions.ProjectLink)]
    [InlineData(DeliveryExecutionManagementPermissions.ProjectUnlink, EnterpriseStrategyPermissions.ProjectUnlink)]
    [InlineData(DeliveryExecutionManagementPermissions.ProjectSync, EnterpriseStrategyPermissions.ProjectSync)]
    public Task Existing_Permission_And_ES_Alias_Are_Both_Accepted(string permission, string alias) =>
        WithEnvironmentAsync(async () =>
        {
            Assert.True(await _service.HasPermissionAsync(permission, Authenticated(new Claim("permission", permission))));
            Assert.True(await _service.HasPermissionAsync(permission, Authenticated(new Claim("permission", alias))));
        });

    [Theory]
    [InlineData(null, null)]
    [InlineData("1", "true")]
    [InlineData("unknown", "unknown")]
    public Task Matching_Claims_Are_Independent_Of_Bootstrap_Configuration(string? enforce, string? bootstrap) =>
        WithEnvironmentAsync(async () =>
        {
            var permission = DeliveryExecutionManagementPermissions.OverviewView;
            Assert.True(await _service.HasPermissionAsync(permission, Authenticated(new Claim("permission", permission))));
            Assert.False(await _service.HasPermissionAsync(permission,
                new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("permission", permission) }))));
        }, deliveryEnforce: enforce, deliveryBootstrap: bootstrap);

    [Theory]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public Task Existing_Ordinal_Case_Insensitive_Matching_Is_Preserved(string culture) =>
        WithEnvironmentAsync(async () =>
        {
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);
                CultureInfo.CurrentUICulture = new CultureInfo(culture);
                var user = Authenticated(new Claim("PERMISSION", "STRATEGY.INITIATIVE.VIEW"));
                Assert.True(await _service.HasPermissionAsync("DELIVERY-EXECUTION-MANAGEMENT.INITIATIVE.VIEW", user));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        });

    private static async Task WithEnvironmentAsync(
        Func<Task> assertion,
        string? deliveryEnforce = null,
        string? deliveryBootstrap = null,
        string? esEnforce = null,
        string? esBootstrap = null)
    {
        var variables = new Dictionary<string, string?>
        {
            ["DITEN_DELIVERY_EXECUTION_ENFORCE_PERMISSIONS"] = deliveryEnforce,
            ["DITEN_DELIVERY_EXECUTION_DEV_PERMISSION_BOOTSTRAP"] = deliveryBootstrap,
            ["DITEN_ESBP_ENFORCE_PERMISSIONS"] = esEnforce,
            ["DITEN_ESBP_DEV_PERMISSION_BOOTSTRAP"] = esBootstrap
        };
        var previous = variables.Keys.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var (name, value) in variables)
                Environment.SetEnvironmentVariable(name, value);
            await assertion();
        }
        finally
        {
            foreach (var (name, value) in previous)
                Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));
}
