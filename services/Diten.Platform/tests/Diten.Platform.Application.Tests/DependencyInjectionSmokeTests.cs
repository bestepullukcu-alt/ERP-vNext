using Diten.Platform.Application.Services;
using Diten.Platform.Common.Catalog;
using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Infrastructure.Persistence.Migrations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests;

public sealed class DependencyInjectionSmokeTests
{
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

        var coordinator = scope.ServiceProvider.GetRequiredService<IWorkflowInstanceStartCoordinator>();

        Assert.IsType<WorkflowInstanceStartCoordinator>(coordinator);
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
        Assert.Contains("AddScoped<VerifiedMarketOperationalProvisioningRunner>()", program, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<VerifiedMarketOperationalProvisioningRunner", program, StringComparison.Ordinal);
        Assert.False(typeof(IHostedService).IsAssignableFrom(typeof(VerifiedMarketOperationalProvisioningRunner)));
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
    public void AuditOutboxRepository_RequiresTemporalStateRepositoryAndDiRegistersBoth()
    {
        var constructor = Assert.Single(typeof(AuditOutboxRepository).GetConstructors());
        var temporalParameter = Assert.Single(constructor.GetParameters(), parameter =>
            parameter.ParameterType == typeof(AuditOutboxTemporalMigrationRepository));
        var root = FindRepositoryRoot();
        var dependencyInjection = File.ReadAllText(Path.Combine(
            root,
            "services", "Diten.Platform", "src", "Diten.Platform.Infrastructure", "DependencyInjection.cs"));

        Assert.False(temporalParameter.HasDefaultValue);
        Assert.Contains("AddScoped<AuditOutboxTemporalMigrationRepository>()", dependencyInjection, StringComparison.Ordinal);
        Assert.Contains("AddScoped<AuditOutboxRepository>()", dependencyInjection, StringComparison.Ordinal);
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
    public void Program_RegistersTrustedSourceAuditIntentAgainstRs256ServiceTokenSchemeOnly()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(
            root,
            "services", "Diten.Platform", "src", "Diten.Platform.API", "Program.cs"));
        var dependencyInjection = File.ReadAllText(Path.Combine(
            root,
            "services", "Diten.Platform", "src", "Diten.Platform.Infrastructure", "DependencyInjection.cs"));

        Assert.Contains("AddInfrastructure(builder.Configuration, builder.Environment)", program, StringComparison.Ordinal);
        Assert.Contains("AddTrustedServiceTokenValidation(builder.Configuration)", program, StringComparison.Ordinal);
        Assert.Contains("ITrustedSourceAuditIntentRequestExecutor", program, StringComparison.Ordinal);
        Assert.Contains("ITrustedSourceAuditIntentAcceptanceService", dependencyInjection, StringComparison.Ordinal);
        Assert.Contains("ITrustedSourceAuditIntentOutbox", dependencyInjection, StringComparison.Ordinal);
        Assert.DoesNotContain("AddAuthentication(TrustedServiceTokenValidationExtensions.AuthenticationScheme", program, StringComparison.Ordinal);
        Assert.DoesNotContain("TrustedSourceAuditIntentCredentialOptions", program, StringComparison.Ordinal);
        Assert.DoesNotContain("ITrustedSourceAuditIntentCredentialAuthenticator", program, StringComparison.Ordinal);
        Assert.DoesNotContain("AddJwtBearer(TrustedSourceAuditIntentServiceIdentity.AuthenticationScheme", program, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<TrustedSourceAuditIntent", program, StringComparison.Ordinal);
        Assert.DoesNotContain("AddHostedService<TrustedSourceAuditIntent", dependencyInjection, StringComparison.Ordinal);
        Assert.False(typeof(IHostedService).IsAssignableFrom(typeof(TrustedSourceAuditIntentRequestExecutor)));
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
