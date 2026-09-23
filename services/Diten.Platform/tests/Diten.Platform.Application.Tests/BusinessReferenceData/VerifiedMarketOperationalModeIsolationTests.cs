using Diten.Platform.API.Services.BusinessReferenceData;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.BusinessReferenceData;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class VerifiedMarketOperationalEnvironmentCollection
{
    public const string Name = "VerifiedMarketOperationalEnvironment";
}

[Collection(VerifiedMarketOperationalEnvironmentCollection.Name)]
public sealed class VerifiedMarketOperationalModeIsolationTests
{
    [Fact]
    public async Task NoArgument_DoesNotEnterOrConstructOperationalMode()
    {
        var handled = await VerifiedMarketOperationalMode.TryRunAsync([]);

        Assert.False(handled);
    }

    [Theory]
    [InlineData("--RUN-VERIFIED-MARKET-PROVISIONING")]
    [InlineData("--run-verified-market-provisioning=true")]
    public async Task NearMatchArgument_FailsBeforeEnvironmentOrMongoComposition(string argument)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VerifiedMarketOperationalMode.TryRunAsync([argument]));

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_ARGUMENT_INVALID", exception.Message);
    }

    [Theory]
    [InlineData("Production", "Production")]
    [InlineData("Staging", "Staging")]
    [InlineData("Development", "Production")]
    [InlineData(null, null)]
    public async Task ExactArgument_RejectsNonDevelopmentEnvelopeBeforeMongoComposition(
        string? aspNetCoreEnvironment,
        string? dotNetEnvironment)
    {
        using var environment = TemporaryProcessEnvironment.Apply(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = aspNetCoreEnvironment,
            ["DOTNET_ENVIRONMENT"] = dotNetEnvironment,
            ["MongoDbSettings__ConnectionString"] = null,
            ["MongoDbSettings__DatabaseName"] = null
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            VerifiedMarketOperationalMode.TryRunAsync([VerifiedMarketOperationalCommandLine.RunArgument]));

        Assert.Equal("VERIFIED_MARKET_OPERATIONAL_ENVIRONMENT_NOT_ALLOWED", exception.Message);
    }

    [Fact]
    public void OperationalComposition_ContainsNoHostedServiceOrNormalStartupWorker()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDbSettings:ConnectionString"] = "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=10",
                ["MongoDbSettings:DatabaseName"] = "verified_market_isolation_test"
            })
            .Build();
        var host = new Mock<IHostEnvironment>();
        host.SetupGet(x => x.EnvironmentName).Returns(Environments.Development);

        var services = CreateOperationalServices(configuration, host.Object);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(VerifiedMarketOperationalProvisioningRunner));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IVerifiedMarketOperationalPreflight));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IBusinessReferenceDataVerifiedMarketOperationalEligibility));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(VerifiedMarketOperationalGovernanceAuditAdapter));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IBusinessReferenceDataEventPublisher>();
        Assert.IsNotType<DbBusinessReferenceDataEventPublisher>(eventPublisher);

        var prohibitedTypeNames = new[]
        {
            "BusinessReferenceDataCatalogLoadWorker",
            "VerifiedGskuOperationalProvisioningRunner",
            "PlatformModuleSelfRegistrationWorker",
            "PlatformPermissionAutoRegistrationWorker",
            "AuditOutboxWorker",
            "OutboxPublisherWorker",
            "SubscriptionPlanStartupInitializer",
            "RecurringJobRegistrationService"
        };
        var registeredTypeNames = services
            .SelectMany(descriptor => new[]
            {
                descriptor.ServiceType.Name,
                descriptor.ImplementationType?.Name
            })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(prohibitedTypeNames, name => Assert.DoesNotContain(name, registeredTypeNames));
    }

    [Fact]
    public void OperationalComposition_DoesNotRegisterGeneralHostSurfaces()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MongoDbSettings:ConnectionString"] = "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=10",
                ["MongoDbSettings:DatabaseName"] = "verified_market_isolation_test"
            })
            .Build();
        var host = new Mock<IHostEnvironment>();
        host.SetupGet(x => x.EnvironmentName).Returns(Environments.Development);

        var services = CreateOperationalServices(configuration, host.Object);
        var namespaces = services
            .Select(descriptor => descriptor.ImplementationType?.Namespace ?? descriptor.ServiceType.Namespace ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.Name == "IHealthCheck");
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType.Name.Contains("Controller", StringComparison.Ordinal));
        Assert.DoesNotContain(namespaces, value => value.Contains("Hangfire", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(namespaces, value => value.Contains("MassTransit", StringComparison.OrdinalIgnoreCase));
    }

    private static IServiceCollection CreateOperationalServices(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        var method = typeof(VerifiedMarketOperationalMode).GetMethod(
            "CreateServices",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsAssignableFrom<IServiceCollection>(method.Invoke(null, [configuration, hostEnvironment]));
    }
}
