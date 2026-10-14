using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// ASSUMPTION-0173-07: the Location-LE check may be switched off ONLY in the Mock environment. Anywhere else the host
/// refuses to start with it off, so a misconfigured production cannot silently accept cross-LE locations.
/// </summary>
public sealed class StartupPolicyTests
{
    [Theory]
    [InlineData("Testing")]
    [InlineData("Development")]
    [InlineData("Production")]
    public void Disabling_the_location_LE_check_outside_Mock_stops_the_host(string environment)
    {
        using var factory = new PolicyFactory(environment, enforce: false);

        var error = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(error);
        Assert.Contains("EnforceLocationLegalEntityMatch", error!.ToString());
    }

    [Fact]
    public void Control_the_Mock_environment_may_disable_it()
    {
        using var factory = new PolicyFactory("Mock", enforce: false);

        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }

    private sealed class PolicyFactory : InventoryApiFactory
    {
        private readonly bool _enforce;

        public PolicyFactory(string environment, bool enforce)
        {
            _enforce = enforce;
            HostEnvironmentOverride = environment;
        }

        private string HostEnvironmentOverride { get; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseEnvironment(HostEnvironmentOverride);
            builder.UseSetting("Inventory:Policy:EnforceLocationLegalEntityMatch", _enforce ? "true" : "false");
        }
    }
}
