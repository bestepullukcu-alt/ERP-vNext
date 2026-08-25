using Diten.Platform.Application.Services;
using Diten.Platform.Common.Catalog;
using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Diten.Platform.API.Services.BusinessReferenceData;
using Microsoft.Extensions.Hosting;
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
