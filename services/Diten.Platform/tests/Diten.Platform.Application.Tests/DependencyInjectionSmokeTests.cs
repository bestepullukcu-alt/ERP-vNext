using Diten.Platform.Application.Services;
using Diten.Platform.Common.Catalog;
using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Migrations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Hosting;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Authorization;
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
    public void AuditOutboxRepository_RequiresTemporalStateRepositoryAndDiRegistersBoth()
    {
        var constructor = Assert.Single(typeof(AuditOutboxRepository).GetConstructors(), candidate =>
            candidate.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(AuditOutboxTemporalMigrationRepository))
            && candidate.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(IPlatformDbContext)));
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
