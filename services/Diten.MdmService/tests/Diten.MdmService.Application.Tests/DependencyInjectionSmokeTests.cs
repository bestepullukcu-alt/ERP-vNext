using Diten.MdmService.Api.Configuration;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Authorization;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow.Handlers.CommandHandlers;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Infrastructure;
using Diten.MdmService.Persistence.Repositories;
using Diten.MdmService.Infrastructure.Security;
using Diten.MdmService.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
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

    [Fact]
    public void Persistence_api_serving_mode_skips_startup_migration_and_preserves_repository_registrations()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=25",
                ["Mongo:DatabaseName"] = "mdm_startup_mode_contract"
            })
            .Build();
        var services = new ServiceCollection();

        Diten.MdmService.Persistence.DependencyInjection.AddPersistence(
            services,
            configuration,
            runStartupMaintenance: false);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IMongoClient));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IMongoDatabase));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ILegalEntityRepository));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IGlobalProductRepository));
    }

    [Fact]
    public void Persistence_default_keeps_normal_startup_maintenance_enabled()
    {
        var method = typeof(Diten.MdmService.Persistence.DependencyInjection)
            .GetMethods()
            .Single(candidate => candidate.Name == nameof(Diten.MdmService.Persistence.DependencyInjection.AddPersistence)
                                 && candidate.GetParameters().Length == 3);
        var parameter = method.GetParameters()[2];

        Assert.True(parameter.HasDefaultValue);
        Assert.Equal(true, parameter.DefaultValue);
    }

    [Fact]
    public void Production_registration_seams_validate_and_supply_required_scoped_dependencies()
    {
        var configuration = CreateHostConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpClient();
        services.AddHttpContextAccessor();
        services.AddInfrastructure(configuration);
        services.AddPersistence(configuration, runStartupMaintenance: false);
        ConfigureGlobalProductWorkflowWorkerOptions(services, configuration);
        services.AddGlobalProductWorkflowExecutionConfigurations();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(Guid.NewGuid());

        Assert.IsType<ProductLegalEntityScopeOperationalReadinessRepository>(
            scope.ServiceProvider.GetRequiredService<IProductLegalEntityScopeOperationalReadinessRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProductIdentityWorkflowExecutionConfiguration>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<GlobalProductCorrectionExecutionConfiguration>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<GlobalProductRetirementRequestExecutionConfiguration>());
        AssertConstructorConsumes<StartGlobalProductIdentityWorkflowHandler,
            ProductIdentityWorkflowExecutionConfiguration>();
        AssertConstructorConsumes<WithdrawGlobalProductIdentityApprovalHandler,
            ProductIdentityWorkflowExecutionConfiguration>();
        AssertConstructorConsumes<StartGlobalProductCorrectionWorkflowHandler,
            GlobalProductCorrectionExecutionConfiguration>();
        AssertConstructorConsumes<StartGlobalProductRetirementRequestWorkflowHandler,
            GlobalProductRetirementRequestExecutionConfiguration>();
        AssertConstructorConsumes<ProductLegalEntityScopeWriteFenceCoordinator,
            IProductLegalEntityScopeOperationalReadinessRepository>();
    }

    [Fact]
    public void Production_execution_configuration_seam_is_scoped_and_uses_default_disabled_worker_options()
    {
        var configuration = CreateHostConfiguration();
        var services = new ServiceCollection();
        ConfigureGlobalProductWorkflowWorkerOptions(services, configuration);
        services.AddGlobalProductWorkflowExecutionConfigurations();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var identity = firstScope.ServiceProvider
            .GetRequiredService<ProductIdentityWorkflowExecutionConfiguration>();
        var correction = firstScope.ServiceProvider
            .GetRequiredService<GlobalProductCorrectionExecutionConfiguration>();
        var retirement = firstScope.ServiceProvider
            .GetRequiredService<GlobalProductRetirementRequestExecutionConfiguration>();

        Assert.False(firstScope.ServiceProvider
            .GetRequiredService<IOptions<ProductIdentityWorkflowWorkerOptions>>().Value.Enabled);
        Assert.False(firstScope.ServiceProvider
            .GetRequiredService<IOptions<GlobalProductCorrectionWorkflowWorkerOptions>>().Value.Enabled);
        Assert.False(firstScope.ServiceProvider
            .GetRequiredService<IOptions<GlobalProductRetirementRequestWorkflowWorkerOptions>>().Value.Enabled);
        Assert.Equal(TimeSpan.FromSeconds(60), identity.LeaseDuration);
        Assert.Equal(TimeSpan.FromSeconds(30), identity.RetryDelay);
        Assert.Equal(TimeSpan.FromSeconds(60), correction.LeaseDuration);
        Assert.Equal(TimeSpan.FromSeconds(10), correction.RetryDelay);
        Assert.Equal(TimeSpan.FromSeconds(60), retirement.LeaseDuration);
        Assert.Equal(TimeSpan.FromSeconds(10), retirement.RetryDelay);
        Assert.NotSame(identity, secondScope.ServiceProvider
            .GetRequiredService<ProductIdentityWorkflowExecutionConfiguration>());
        Assert.NotSame(correction, secondScope.ServiceProvider
            .GetRequiredService<GlobalProductCorrectionExecutionConfiguration>());
        Assert.NotSame(retirement, secondScope.ServiceProvider
            .GetRequiredService<GlobalProductRetirementRequestExecutionConfiguration>());
    }

    [Fact]
    public void Production_execution_configuration_seam_flows_valid_custom_worker_windows()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ProductIdentityWorkflowWorkerOptions.SectionName}:LeaseSeconds"] = "75",
                [$"{ProductIdentityWorkflowWorkerOptions.SectionName}:RetryDelaySeconds"] = "12",
                [$"{GlobalProductCorrectionWorkflowWorkerOptions.SectionName}:LeaseSeconds"] = "76",
                [$"{GlobalProductCorrectionWorkflowWorkerOptions.SectionName}:RetryDelaySeconds"] = "13",
                [$"{GlobalProductRetirementRequestWorkflowWorkerOptions.SectionName}:LeaseSeconds"] = "77",
                [$"{GlobalProductRetirementRequestWorkflowWorkerOptions.SectionName}:RetryDelaySeconds"] = "14"
            })
            .Build();
        var services = new ServiceCollection();
        ConfigureGlobalProductWorkflowWorkerOptions(services, configuration);
        services.AddGlobalProductWorkflowExecutionConfigurations();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        Assert.Equal(TimeSpan.FromSeconds(75), scope.ServiceProvider
            .GetRequiredService<ProductIdentityWorkflowExecutionConfiguration>().LeaseDuration);
        Assert.Equal(TimeSpan.FromSeconds(12), scope.ServiceProvider
            .GetRequiredService<ProductIdentityWorkflowExecutionConfiguration>().RetryDelay);
        Assert.Equal(TimeSpan.FromSeconds(76), scope.ServiceProvider
            .GetRequiredService<GlobalProductCorrectionExecutionConfiguration>().LeaseDuration);
        Assert.Equal(TimeSpan.FromSeconds(13), scope.ServiceProvider
            .GetRequiredService<GlobalProductCorrectionExecutionConfiguration>().RetryDelay);
        Assert.Equal(TimeSpan.FromSeconds(77), scope.ServiceProvider
            .GetRequiredService<GlobalProductRetirementRequestExecutionConfiguration>().LeaseDuration);
        Assert.Equal(TimeSpan.FromSeconds(14), scope.ServiceProvider
            .GetRequiredService<GlobalProductRetirementRequestExecutionConfiguration>().RetryDelay);
    }

    [Theory]
    [InlineData(ProductIdentityWorkflowWorkerOptions.SectionName,
        nameof(ProductIdentityWorkflowWorkerOptions.LeaseSeconds), "9",
        "PRODUCT_IDENTITY_WORKFLOW_WORKER_CONFIGURATION_INVALID",
        typeof(ProductIdentityWorkflowExecutionConfiguration))]
    [InlineData(GlobalProductCorrectionWorkflowWorkerOptions.SectionName,
        nameof(GlobalProductCorrectionWorkflowWorkerOptions.RetryDelaySeconds), "0",
        "GLOBAL_PRODUCT_CORRECTION_WORKER_CONFIGURATION_INVALID",
        typeof(GlobalProductCorrectionExecutionConfiguration))]
    [InlineData(GlobalProductRetirementRequestWorkflowWorkerOptions.SectionName,
        nameof(GlobalProductRetirementRequestWorkflowWorkerOptions.LeaseSeconds), "901",
        "GLOBAL_PRODUCT_RETIREMENT_WORKER_CONFIGURATION_INVALID",
        typeof(GlobalProductRetirementRequestExecutionConfiguration))]
    public void Production_execution_configuration_seam_rejects_invalid_windows_while_workers_are_disabled(
        string section,
        string key,
        string value,
        string expectedError,
        Type configurationType)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{section}:{key}"] = value,
                [$"{section}:Enabled"] = "false"
            })
            .Build();
        var services = new ServiceCollection();
        ConfigureGlobalProductWorkflowWorkerOptions(services, configuration);
        services.AddGlobalProductWorkflowExecutionConfigurations();
        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService(configurationType));

        Assert.Equal(expectedError, exception.Message);
    }

    [Fact]
    public void Program_uses_the_tested_production_execution_configuration_seam()
    {
        var program = ReadRepoFile(
            "services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs");

        Assert.Contains(
            "builder.Services.AddGlobalProductWorkflowExecutionConfigurations();",
            program,
            StringComparison.Ordinal);
    }

    private static IConfiguration CreateHostConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=25",
                ["Mongo:DatabaseName"] = "mdm_host_di_contract"
            })
            .Build();

    private static void ConfigureGlobalProductWorkflowWorkerOptions(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ProductIdentityWorkflowWorkerOptions>(
            configuration.GetSection(ProductIdentityWorkflowWorkerOptions.SectionName));
        services.Configure<GlobalProductCorrectionWorkflowWorkerOptions>(
            configuration.GetSection(GlobalProductCorrectionWorkflowWorkerOptions.SectionName));
        services.Configure<GlobalProductRetirementRequestWorkflowWorkerOptions>(
            configuration.GetSection(GlobalProductRetirementRequestWorkflowWorkerOptions.SectionName));
    }

    private static void AssertConstructorConsumes<TConsumer, TDependency>()
    {
        var constructor = Assert.Single(typeof(TConsumer).GetConstructors());
        Assert.Contains(constructor.GetParameters(), parameter => parameter.ParameterType == typeof(TDependency));
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
