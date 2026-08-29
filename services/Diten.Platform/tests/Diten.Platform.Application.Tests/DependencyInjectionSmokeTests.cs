using Diten.Platform.Application.Services;
using Diten.Platform.Common.Catalog;
using Diten.Platform.Application.Features.BusinessReferenceData.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.BusinessReferenceData.Queries;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.API.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
