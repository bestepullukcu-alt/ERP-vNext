using Diten.SupplyChainService.Api.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diten.SupplyChainService.Tests;

/// <summary>
/// G4 (Wave 6.1, owner decisions 2026-10-18): S&amp;OP/Capacity are composed only where a demand source exists. Measured
/// against the production gate Program.cs calls (DemandConsumerComposition.SeedFor), not a copy.
/// </summary>
public sealed class DemandConsumerCompositionTests
{
    private static readonly Dictionary<string, string?> Complete = new()
    {
        ["DemandFixtures:Enabled"] = "true",
        ["DemandFixtures:TenantId"] = "4b000000-0000-4000-8000-000000000001",
        ["DemandFixtures:LegalEntityId"] = "4b000000-0000-4000-8000-000000000002",
        ["DemandFixtures:PlanId"] = "dp-2027",
        ["DemandFixtures:Version"] = "3",
        ["DemandFixtures:Checksum"] = "sha256:seed"
    };

    private static DemandFixtureSeed? Seed(string environment, IDictionary<string, string?> values) =>
        DemandConsumerComposition.SeedFor(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), new Env(environment));

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public void Outside_production_a_complete_enabled_seed_composes(string environment)
    {
        var seed = Seed(environment, Complete);

        Assert.NotNull(seed);
        Assert.Equal(Guid.Parse("4b000000-0000-4000-8000-000000000002"), seed!.LegalEntityId);
        Assert.Equal("sha256:seed", seed.Checksum);
    }

    [Fact]
    public void Production_never_composes_on_a_fixture_even_when_the_section_is_present()
    {
        Assert.Null(Seed(Environments.Production, Complete));
    }

    [Fact]
    public void Without_the_section_nothing_is_composed()
    {
        Assert.Null(Seed("Development", new Dictionary<string, string?>()));
    }

    [Fact]
    public void Disabled_is_not_composed()
    {
        var values = new Dictionary<string, string?>(Complete) { ["DemandFixtures:Enabled"] = "false" };
        Assert.Null(Seed("Development", values));
    }

    [Theory]
    [InlineData("DemandFixtures:TenantId", "not-a-uuid")]
    [InlineData("DemandFixtures:LegalEntityId", "00000000-0000-0000-0000-000000000000")]
    [InlineData("DemandFixtures:Checksum", "")]
    public void Enabled_but_incomplete_fails_loudly_instead_of_composing_a_refusing_module(string key, string value)
    {
        var values = new Dictionary<string, string?>(Complete) { [key] = value };
        Assert.Throws<InvalidOperationException>(() => Seed("Development", values));
    }

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Diten.SupplyChainService.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
