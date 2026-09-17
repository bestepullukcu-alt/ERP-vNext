using Diten.PpmService.Application.Features.Portfolios;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Infrastructure;
using Diten.PpmService.Infrastructure.Portfolios;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diten.PpmService.Tests.Portfolios;

public sealed class PortfolioTemporaryNonProductionAccessCompositionTests
{
    [Theory]
    [InlineData(false, "Development", "Development", PortfolioAuthorityOutcome.Unavailable)]
    [InlineData(true, "Development", "Development", PortfolioAuthorityOutcome.Allowed)]
    [InlineData(true, "Production", "Production", PortfolioAuthorityOutcome.Unavailable)]
    [InlineData(true, "Staging", "Development", PortfolioAuthorityOutcome.Unavailable)]
    public async Task Composition_is_default_off_and_allows_only_the_explicit_nonproduction_environment(
        bool enabled, string environmentName, string configuredEnvironment, PortfolioAuthorityOutcome expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{PortfolioTemporaryNonProductionAccessOptions.SectionName}:Enabled"] = enabled.ToString(),
            [$"{PortfolioTemporaryNonProductionAccessOptions.SectionName}:NonProductionEnvironmentName"] = configuredEnvironment
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environmentName));
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var authority = scope.ServiceProvider.GetRequiredService<IPortfolioRecordAccessAuthority>();

        var evidence = await authority.EvaluateAsync(BindingFreeScope("page"), default);

        Assert.Equal(expected, evidence.Outcome);
    }

    [Theory]
    [InlineData("manage-owner")]
    [InlineData("owner-candidates")]
    [InlineData("assign-owner")]
    [InlineData("transfer-owner")]
    public async Task Unrelated_actor_cannot_receive_a_local_allow_for_any_owner_operation(string operation)
    {
        var authority = new PortfolioTemporaryNonProductionRecordAccessAuthority(
            enabled: true, PortfolioTemporaryNonProductionAccessEnvironment.NonProduction);
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var binding = authority.CreateBinding(portfolioId);
        var related = RecordScope(tenantId, actorId, portfolioId, binding, "read", actorId, null);
        var unrelated = related with { CreatorId = Guid.NewGuid(), Operation = "read" };

        Assert.Equal(PortfolioAuthorityOutcome.Allowed, (await authority.EvaluateAsync(related, default)).Outcome);
        Assert.Equal(PortfolioAuthorityOutcome.Denied,
            (await authority.EvaluateAsync(unrelated with { Operation = operation }, default)).Outcome);
        Assert.Equal(PortfolioAuthorityOutcome.Denied,
            (await authority.EvaluateAsync(related with { Operation = "history-read" }, default)).Outcome);
    }

    [Fact]
    public async Task Legacy_or_malformed_binding_cannot_open_a_record_surface()
    {
        var authority = new PortfolioTemporaryNonProductionRecordAccessAuthority(
            enabled: true, PortfolioTemporaryNonProductionAccessEnvironment.NonProduction);
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var portfolioId = Guid.NewGuid();
        var valid = authority.CreateBinding(portfolioId);
        var scope = RecordScope(tenantId, actorId, portfolioId, valid, "edit", actorId, null);

        Assert.Equal(PortfolioAuthorityOutcome.NotFound,
            (await authority.EvaluateAsync(scope with { TemporaryNonProductionAccessBinding = null }, default)).Outcome);
        Assert.Equal(PortfolioAuthorityOutcome.Unavailable,
            (await authority.EvaluateAsync(scope with
            {
                TemporaryNonProductionAccessBinding = valid with { PortfolioId = Guid.NewGuid() }
            }, default)).Outcome);
        Assert.Equal(PortfolioAuthorityOutcome.Unavailable,
            (await authority.EvaluateAsync(scope with { Version = 0 }, default)).Outcome);
    }

    private static PortfolioAuthorityScope BindingFreeScope(string operation) =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, operation);

    private static PortfolioAuthorityScope RecordScope(Guid tenantId, Guid actorId, Guid portfolioId,
        PortfolioTemporaryNonProductionAccessBinding binding, string operation, Guid creatorId, Guid? ownerId) =>
        new(tenantId, actorId, portfolioId, operation, Version: 1, RecordTenantId: tenantId,
            CreatorId: creatorId, CurrentOwnerUserId: ownerId, LifecycleState: PortfolioLifecycleState.Draft,
            TemporaryNonProductionAccessBinding: binding);

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Diten.PpmService.Tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
