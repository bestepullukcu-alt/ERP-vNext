using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using Diten.MdmService.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class DependencyInjectionSmokeTests
{
    [Fact]
    public void Finished_good_human_admission_context_is_scoped_and_existing_actor_registrations_remain()
    {
        var services = new ServiceCollection();
        Diten.MdmService.Infrastructure.DependencyInjection.AddInfrastructure(
            services,
            new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider(validateScopes: true);

        using var firstScope = provider.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IFinishedGoodHumanAdmissionContext>();
        var firstAgain = firstScope.ServiceProvider.GetRequiredService<IFinishedGoodHumanAdmissionContext>();

        Assert.IsType<FinishedGoodHumanAdmissionContext>(first);
        Assert.Same(first, firstAgain);
        Assert.NotNull(firstScope.ServiceProvider.GetRequiredService<IProductIdentityActorContext>());
        Assert.NotNull(firstScope.ServiceProvider.GetRequiredService<IProductIdentityLifecycleActorContext>());
        Assert.NotNull(firstScope.ServiceProvider.GetRequiredService<IProductAbbreviationActorContext>());

        using var secondScope = provider.CreateScope();
        var second = secondScope.ServiceProvider.GetRequiredService<IFinishedGoodHumanAdmissionContext>();
        Assert.NotSame(first, second);
    }

    [Fact]
    public void Fu03_scope_dependencies_are_registered_without_audit_transport_or_operational_runner()
    {
        var application = ReadRepoFile(
            "services/Diten.MdmService/src/Diten.MdmService.Application/DependencyInjection.cs");
        var infrastructure = ReadRepoFile(
            "services/Diten.MdmService/src/Diten.MdmService.Infrastructure/DependencyInjection.cs");
        var persistence = ReadRepoFile(
            "services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs");

        Assert.Contains(nameof(IProductLegalEntityScopeEvaluator), application, StringComparison.Ordinal);
        Assert.Contains(nameof(ProductLegalEntityScopeCandidateFacade), infrastructure, StringComparison.Ordinal);
        Assert.Contains(nameof(IProductLegalEntityScopePolicyRepository), persistence, StringComparison.Ordinal);
        Assert.Contains(nameof(IProductLegalEntityScopeRolloutStateRepository), persistence, StringComparison.Ordinal);
        Assert.Contains(nameof(IProductLegalEntityScopeWriterAuthorityProvider), infrastructure, StringComparison.Ordinal);
        Assert.Contains(nameof(ProductLegalEntityScopeWriterAuthorityProvider), infrastructure, StringComparison.Ordinal);
        Assert.Contains(nameof(IProductLegalEntityScopeGuardedWriteSession), persistence, StringComparison.Ordinal);
        Assert.Contains(nameof(ProductLegalEntityScopeGuardedWriteSession), persistence, StringComparison.Ordinal);
        Assert.Contains(nameof(ProductLegalEntityScopeWriteFenceCoordinator), application, StringComparison.Ordinal);
        Assert.DoesNotContain("AuditIntentDeliveryWorker", application, StringComparison.Ordinal);
        Assert.DoesNotContain("ProductLegalEntityScopeOperationalRunner", application, StringComparison.Ordinal);
    }

    [Fact]
    public void Temporal_migration_is_scoped_required_and_never_hosted()
    {
        var source = ReadRepoFile(
            "services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs");

        Assert.Contains(
            "AddScoped<IAuditIntentTemporalMigrationRepository, AuditIntentTemporalMigrationRepository>()",
            source,
            StringComparison.Ordinal);
        Assert.Contains("AddScoped<AuditIntentTemporalMigrationRunner>()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<AuditIntentTemporalMigrationRunner>", source, StringComparison.Ordinal);
        Assert.False(typeof(IHostedService).IsAssignableFrom(typeof(AuditIntentTemporalMigrationRunner)));
        var productionConstructor = Assert.Single(
            typeof(AuditIntentDeliveryRepository).GetConstructors(),
            constructor => constructor.GetParameters().Length == 4);
        Assert.Contains(productionConstructor.GetParameters(), parameter =>
            parameter.ParameterType == typeof(IAuditIntentTemporalMigrationRepository));
    }

    [Fact]
    public void Temporal_migration_is_default_disabled_and_requires_exact_cli_argument()
    {
        Assert.False(new AuditIntentTemporalMigrationOptions().Enabled);
        Assert.True(AuditIntentTemporalMigrationCommandLine.IsRequested(
            [AuditIntentTemporalMigrationCommandLine.ExactArgument]));
        Assert.False(AuditIntentTemporalMigrationCommandLine.IsRequested([]));
        Assert.Equal(
            "AUDIT_INTENT_TEMPORAL_MIGRATION_COMMAND_DUPLICATE",
            Assert.Throws<InvalidOperationException>(() => AuditIntentTemporalMigrationCommandLine.IsRequested(
                [AuditIntentTemporalMigrationCommandLine.ExactArgument, AuditIntentTemporalMigrationCommandLine.ExactArgument])).Message);
        Assert.False(AuditIntentTemporalMigrationCommandLine.IsRequested(
            [AuditIntentTemporalMigrationCommandLine.ExactArgument.ToUpperInvariant()]));
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return File.ReadAllText(Path.Combine(
                    directory.FullName,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("REPO_ROOT_NOT_FOUND");
    }
}
