using Diten.Platform.Application.Services;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Common.Catalog;
using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.API.Configuration;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Migrations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Services.Audit;
using Microsoft.Extensions.Hosting;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests;

public sealed class DependencyInjectionSmokeTests
{
    [Fact]
    public void Trusted_workflow_start_policy_resolves_without_options_cycle_and_defaults_to_deny()
    {
        var services = new ServiceCollection();
        services.AddOptions<TrustedWorkflowStartAuthorizationOptions>();
        services.AddSingleton<IValidateOptions<TrustedWorkflowStartAuthorizationOptions>,
            TrustedWorkflowStartAuthorizationOptionsValidator>();
        services.AddSingleton<ITrustedWorkflowStartAuthorizationPolicy,
            ConfiguredTrustedWorkflowStartAuthorizationPolicy>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        var policy = provider.GetRequiredService<ITrustedWorkflowStartAuthorizationPolicy>();

        Assert.False(policy.IsAuthorized(new(
            Guid.NewGuid(), "Diten.MDM", "TRUSTED_WORKFLOW_CONSUMER",
            "GlobalProduct", Guid.NewGuid(), null)));
    }

    [Fact]
    public void AddApplication_ResolvesTrustedWorkflowStartCoordinator()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddSingleton(Mock.Of<IWorkflowTemplateRepository>());
        services.AddSingleton(Mock.Of<IWorkflowTemplateVersionRepository>());
        services.AddSingleton(Mock.Of<IWorkflowInstanceRepository>());
        services.AddSingleton(Mock.Of<IApprovalTaskRepository>());
        services.AddSingleton(Mock.Of<IRuntimeAssignmentSnapshotRepository>());
        services.AddSingleton(Mock.Of<IWorkflowTransitionLogRepository>());
        services.AddSingleton(Mock.Of<ITenantContext>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<WorkflowInstanceStartCoordinator>(
            scope.ServiceProvider.GetRequiredService<IWorkflowInstanceStartCoordinator>());
    }

    [Fact]
    public void AddApplication_RegistersPlatformCatalogContract()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplication();

        // Assert
        var descriptor = services.FirstOrDefault(x => x.ServiceType == typeof(IPlatformCatalogContract));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(PlatformCatalogContract), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddApplication_RegistersVerifiedGskuResolverHandler()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IRequestHandler<ResolveVerifiedGskuReferenceDataQuery,
                Response<BusinessReferenceDataVerifiedResolveResult>>)
            && descriptor.ImplementationType == typeof(ResolveVerifiedGskuReferenceDataHandler));
    }

    [Fact]
    public void Program_RegistersMarketOperationalRunnerAsExplicitScopedServiceOnly()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(
            root,
            "services", "Diten.Platform", "src", "Diten.Platform.API", "Program.cs"));

        Assert.Contains("AddScoped<Diten.Platform.Application.Features.BusinessReferenceData.Services.IBusinessReferenceDataVerifiedMarketOperationalEligibility", program, StringComparison.Ordinal);
        Assert.Contains("DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility", program, StringComparison.Ordinal);
        // The runner is composed by the one-shot operational mode (its own container, built before the web host
        // exists) — Program.cs only asks that mode whether it was requested. The serving host must not register
        // the runner at all: "explicit scoped service ONLY" is exactly that it exists nowhere else.
        var operationalMode = File.ReadAllText(Path.Combine(
            root,
            "services", "Diten.Platform", "src", "Diten.Platform.API", "Services", "BusinessReferenceData",
            "VerifiedMarketOperationalMode.cs"));
        Assert.Contains("services.AddScoped<VerifiedMarketOperationalProvisioningRunner>()", operationalMode, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService", operationalMode, StringComparison.Ordinal);
        Assert.Contains("VerifiedMarketOperationalMode.TryRunAsync(args)", program, StringComparison.Ordinal);
        Assert.True(
            program.IndexOf("VerifiedMarketOperationalMode.TryRunAsync(args)", StringComparison.Ordinal)
            < program.IndexOf("WebApplication.CreateBuilder", StringComparison.Ordinal),
            "The operational mode must be decided before the web host is built.");
        Assert.DoesNotContain("VerifiedMarketOperationalProvisioningRunner", program, StringComparison.Ordinal);
        Assert.False(typeof(IHostedService).IsAssignableFrom(typeof(VerifiedMarketOperationalProvisioningRunner)));
    }

    [Fact]
    public void Program_registers_FU21_services_without_hosted_service_or_MDM_callback()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "services", "Diten.Platform", "src", "Diten.Platform.API", "Program.cs"));

        Assert.Contains("AddSingleton<ITrustedLegalEntityScopeCredentialAuthenticator", program, StringComparison.Ordinal);
        Assert.Contains("AddScoped<ITrustedLegalEntityScopeJwtContext", program, StringComparison.Ordinal);
        Assert.Contains("AddScoped<ITrustedLegalEntityScopeRequestExecutor", program, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IOrgDataScopeCandidateAvailabilityClassifier", program, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<TrustedLegalEntityScope", program, StringComparison.Ordinal);
        Assert.DoesNotContain("Diten.MdmService", program, StringComparison.Ordinal);

        var infrastructure = File.ReadAllText(Path.Combine(
            root, "services", "Diten.Platform", "src", "Diten.Platform.Infrastructure", "DependencyInjection.cs"));
        Assert.Contains("AddScoped<IOrgDataScopeCandidateFactReader, OrgDataScopeCandidateFactReader>()", infrastructure, StringComparison.Ordinal);

        var services = new ServiceCollection();
        services.AddApplication();
        var descriptor = Assert.Single(services.Where(x => x.ServiceType == typeof(IOrgDataScopeCandidateResolver)));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void Program_RegistersAuditTemporalMigrationAsExplicitCommandLineScopedServiceOnly()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(
            root,
            "services", "Diten.Platform", "src", "Diten.Platform.API", "Program.cs"));
        var dependencyInjection = File.ReadAllText(Path.Combine(
            root,
            "services", "Diten.Platform", "src", "Diten.Platform.Infrastructure", "DependencyInjection.cs"));

        Assert.Contains("--run-audit-outbox-temporal-storage-migration", program, StringComparison.Ordinal);
        Assert.Contains("AuditOutboxTemporalStorageMigrationOptions", program, StringComparison.Ordinal);
        Assert.Contains("AddScoped<AuditOutboxTemporalStorageMigrationRunner>()", dependencyInjection, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<AuditOutboxTemporalStorageMigrationRunner", program, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<AuditOutboxTemporalStorageMigrationRunner", dependencyInjection, StringComparison.Ordinal);
        Assert.False(typeof(IHostedService).IsAssignableFrom(typeof(AuditOutboxTemporalStorageMigrationRunner)));
    }

    [Fact]
    public void DefaultAuditOutboxActivation_WhenBothTwoArgumentConstructorsAreResolvable_IsAmbiguous()
    {
        // Arrange
        var services = new ServiceCollection();
        var mongoClient = new MongoClient("mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=10");
        var database = mongoClient.GetDatabase("platform_di_ambiguity_guard");
        services.AddSingleton<IPlatformDbContext>(new PlatformDbContext(mongoClient, database));
        services.AddScoped<IMongoDatabase>(_ => database);
        services.AddScoped<AuditOutboxTemporalMigrationRepository>();
        services.AddScoped<AuditOutboxRepository>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<AuditOutboxRepository>());

        // Assert
        Assert.Contains("constructors", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ambiguous", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddAuditOutboxRepositories_WithBothConstructorDependenciesRegistered_UsesTransactionAwareScopedIdentity()
    {
        // Arrange
        var services = new ServiceCollection();
        var mongoClient = new MongoClient("mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=10");
        var database = mongoClient.GetDatabase("platform_di_registration_guard");
        var platformDbContext = new PlatformDbContext(mongoClient, database);
        services.AddSingleton<IPlatformDbContext>(platformDbContext);
        services.AddScoped<IMongoDatabase>(_ => database);

        // Act: invoke the production registration path without running startup migrations or seeds.
        Diten.Platform.Infrastructure.DependencyInjection.AddAuditOutboxRepositories(services);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var firstScope = provider.CreateScope();
        var repository = firstScope.ServiceProvider.GetRequiredService<AuditOutboxRepository>();
        var writer = firstScope.ServiceProvider.GetRequiredService<IAuditOutboxWriter>();
        var transactionalStore = firstScope.ServiceProvider.GetRequiredService<ITransactionalAuditOutboxStore>();
        var trustedIntentOutbox = firstScope.ServiceProvider.GetRequiredService<ITrustedSourceAuditIntentOutbox>();
        var processingRepository = firstScope.ServiceProvider.GetRequiredService<IAuditOutboxProcessingRepository>();

        using var secondScope = provider.CreateScope();
        var repositoryFromSecondScope = secondScope.ServiceProvider.GetRequiredService<AuditOutboxRepository>();

        // Assert
        Assert.Same(repository, writer);
        Assert.Same(repository, transactionalStore);
        Assert.Same(repository, trustedIntentOutbox);
        // WP-PLATFORM-AUDIT-INTX-01 (merge WP-CT-LANE-TKO-MERGE-01) — the repository is only the STORE: the door handlers
        // get is never registered here (see the composition test below).
        Assert.DoesNotContain(services, candidate => candidate.ServiceType == typeof(ITransactionalAuditOutboxWriter));
        Assert.Same(repository, processingRepository);
        Assert.NotSame(repository, repositoryFromSecondScope);

        var contextField = typeof(AuditOutboxRepository).GetField(
            "_dbContext",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(contextField);
        Assert.Same(platformDbContext, contextField.GetValue(repository));

        var registration = Assert.Single(services.Where(candidate =>
            candidate.ServiceType == typeof(AuditOutboxRepository)));
        Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
        Assert.NotNull(registration.ImplementationFactory);

        Assert.Contains(typeof(AuditOutboxRepository).GetConstructors(), constructor =>
            constructor.GetParameters().Select(parameter => parameter.ParameterType)
                .SequenceEqual(new[]
                {
                    typeof(IMongoDatabase),
                    typeof(AuditOutboxTemporalMigrationRepository)
                }));
    }

    /// <summary>
    /// Merge WP-CT-LANE-TKO-MERGE-01 — THE ONE IN-TRANSACTION AUDIT DOOR, WHICHEVER REGISTRATION RUNS FIRST. The takeover's
    /// <c>AddAuditOutboxRepositories</c> once bound <see cref="ITransactionalAuditOutboxWriter"/> to the repository, which —
    /// registered after AddApplication — silently replaced the lane's canonical writer (the payload builder and actor
    /// naming of WP-PLATFORM-AUDIT-INTX-01). Composed in both orders, there is exactly one registration and it is the
    /// canonical writer; the trusted-source intent outbox is the repository.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_in_transaction_audit_door_is_the_canonical_writer_whatever_the_registration_order(bool applicationFirst)
    {
        var services = new ServiceCollection();
        if (applicationFirst)
        {
            services.AddApplication();
            Diten.Platform.Infrastructure.DependencyInjection.AddAuditOutboxRepositories(services);
        }
        else
        {
            Diten.Platform.Infrastructure.DependencyInjection.AddAuditOutboxRepositories(services);
            services.AddApplication();
        }

        var door = Assert.Single(services, candidate => candidate.ServiceType == typeof(ITransactionalAuditOutboxWriter));
        Assert.Equal(typeof(Diten.Platform.Application.Features.Audit.CanonicalTransactionalAuditOutboxWriter), door.ImplementationType);
        var store = Assert.Single(services, candidate => candidate.ServiceType == typeof(ITransactionalAuditOutboxStore));
        var intents = Assert.Single(services, candidate => candidate.ServiceType == typeof(ITrustedSourceAuditIntentOutbox));
        Assert.NotNull(store.ImplementationFactory);   // → AuditOutboxRepository
        Assert.NotNull(intents.ImplementationFactory); // → AuditOutboxRepository
    }

    [Fact]
    public async Task TrustedServiceTokenValidation_RegistersNamedSchemeWithoutChangingHumanDefault()
    {
        var baselineServices = new ServiceCollection();
        baselineServices.AddLogging();
        baselineServices.AddAuthentication(options =>
        {
            options.DefaultScheme = "HumanBearer";
            options.DefaultAuthenticateScheme = "HumanBearer";
            options.DefaultChallengeScheme = "HumanBearer";
        });
        await using var baselineProvider = baselineServices.BuildServiceProvider();
        var baseline = baselineProvider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = "HumanBearer";
            options.DefaultAuthenticateScheme = "HumanBearer";
            options.DefaultChallengeScheme = "HumanBearer";
        });

        services.AddTrustedServiceTokenValidation(new ConfigurationBuilder().Build());

        await using var provider = services.BuildServiceProvider();
        var authentication = provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        Assert.Equal("HumanBearer", authentication.DefaultScheme);
        Assert.Equal(baseline.DefaultScheme, authentication.DefaultScheme);
        Assert.Equal(baseline.DefaultAuthenticateScheme, authentication.DefaultAuthenticateScheme);
        Assert.Equal(baseline.DefaultChallengeScheme, authentication.DefaultChallengeScheme);
        Assert.Equal(baseline.DefaultForbidScheme, authentication.DefaultForbidScheme);
        Assert.Equal(baseline.DefaultSignInScheme, authentication.DefaultSignInScheme);
        Assert.Equal(baseline.DefaultSignOutScheme, authentication.DefaultSignOutScheme);
        Assert.NotNull(await schemes.GetSchemeAsync(TrustedServiceTokenValidationExtensions.AuthenticationScheme));
        Assert.NotNull(await schemes.GetSchemeAsync(
            TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme));
        Assert.NotNull(await schemes.GetSchemeAsync(
            TrustedServiceTokenValidationExtensions.WorkflowDelegatedUserAuthenticationScheme));
    }

    [Fact]
    public void Program_RegistersTrustedServiceTokenValidationWithoutChangingInfrastructureAuthentication()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(
            root,
            "services", "Diten.Platform", "src", "Diten.Platform.API", "Program.cs"));

        Assert.Contains("AddInfrastructure(builder.Configuration, builder.Environment)", program, StringComparison.Ordinal);
        Assert.Contains("AddTrustedServiceTokenValidation(builder.Configuration)", program, StringComparison.Ordinal);
        Assert.DoesNotContain("AddAuthentication(TrustedServiceTokenValidationExtensions.AuthenticationScheme", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersTrustedWorkflowTransportWithoutHostedOrDefaultSchemeMutation()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(
            root,
            "services", "Diten.Platform", "src", "Diten.Platform.API", "Program.cs"));

        Assert.Contains("TrustedWorkflowConsumerRequestParser", program, StringComparison.Ordinal);
        Assert.Contains("ITrustedWorkflowConsumerRequestExecutor", program, StringComparison.Ordinal);
        Assert.Contains("TrustedWorkflowConsumerRequestExecutor", program, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "AddAuthentication(TrustedServiceTokenValidationExtensions.WorkflowAuthenticationScheme",
            program,
            StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<TrustedWorkflow", program, StringComparison.Ordinal);
        Assert.False(typeof(IHostedService).IsAssignableFrom(typeof(TrustedWorkflowConsumerRequestExecutor)));
    }

    [Fact]
    public void Program_RegistersTrustedSourceAuditIntentAgainstRs256ServiceTokenSchemeOnly()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(
            root,
            "services", "Diten.Platform", "src", "Diten.Platform.API", "Program.cs"));
        var dependencyInjection = File.ReadAllText(Path.Combine(
            root,
            "services", "Diten.Platform", "src", "Diten.Platform.Infrastructure", "DependencyInjection.cs"));

        Assert.Contains("AddTrustedServiceTokenValidation(builder.Configuration)", program, StringComparison.Ordinal);
        Assert.Contains("AddScoped<ITrustedSourceAuditIntentServiceIdentity", program, StringComparison.Ordinal);
        Assert.Contains("AddScoped<ITrustedSourceAuditIntentRequestExecutor", program, StringComparison.Ordinal);
        Assert.Contains("ITrustedSourceAuditIntentAcceptanceService", dependencyInjection, StringComparison.Ordinal);
        Assert.Contains("ITrustedSourceAuditIntentOutbox", dependencyInjection, StringComparison.Ordinal);
        Assert.DoesNotContain("TrustedSourceAuditIntentCredentialOptions", program, StringComparison.Ordinal);
        Assert.DoesNotContain("ITrustedSourceAuditIntentCredentialAuthenticator", program, StringComparison.Ordinal);
        Assert.DoesNotContain("AddJwtBearer(TrustedSourceAuditIntentServiceIdentity.AuthenticationScheme", program, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<TrustedSourceAuditIntent", program, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<TrustedSourceAuditIntent", dependencyInjection, StringComparison.Ordinal);
        Assert.False(typeof(IHostedService).IsAssignableFrom(typeof(TrustedSourceAuditIntentRequestExecutor)));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("repository_root_not_found");
    }
}
