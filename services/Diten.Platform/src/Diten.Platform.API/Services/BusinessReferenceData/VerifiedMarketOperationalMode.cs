using Diten.Platform.API.Configuration;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Microsoft.Extensions.FileProviders;

namespace Diten.Platform.API.Services.BusinessReferenceData;

public static class VerifiedMarketOperationalMode
{
    public static async Task<bool> TryRunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken ct = default)
    {
        var classification = VerifiedMarketOperationalCommandLine.Classify(arguments);
        if (classification == VerifiedMarketOperationalCommandClassification.NotRequested)
        {
            return false;
        }

        if (classification != VerifiedMarketOperationalCommandClassification.Exact)
        {
            throw new InvalidOperationException("VERIFIED_MARKET_OPERATIONAL_ARGUMENT_INVALID");
        }

        var environmentName = ResolveDevelopmentEnvironment();
        var contentRoot = Directory.GetCurrentDirectory();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(contentRoot)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .AddUserSecrets<Program>(optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var hostEnvironment = new OperationalHostEnvironment(
            environmentName,
            contentRoot,
            typeof(Program).Assembly.GetName().Name ?? "Diten.Platform.API");
        var services = CreateServices(configuration, hostEnvironment);

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<VerifiedMarketOperationalProvisioningRunner>()
            .RunAsync(ct);
        return true;
    }

    internal static IServiceCollection CreateServices(
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(hostEnvironment);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(hostEnvironment);
        services.Configure<VerifiedMarketOperationalProvisioningOptions>(
            configuration.GetSection(VerifiedMarketOperationalProvisioningOptions.SectionName));
        services.Configure<BusinessReferenceDataProviderOptions>(
            configuration.GetSection(BusinessReferenceDataProviderOptions.SectionName));
        services.AddScoped<IBusinessReferenceDataVerifiedMarketOperationalEligibility,
            DevelopmentBusinessReferenceDataVerifiedMarketOperationalEligibility>();
        services.AddScoped<VerifiedMarketOperationalProvisioningRunner>();
        services.AddVerifiedMarketOperationalPersistence(configuration);
        return services;
    }

    private static string ResolveDevelopmentEnvironment()
    {
        var aspNetCore = Environment.GetEnvironmentVariable(
            "ASPNETCORE_ENVIRONMENT",
            EnvironmentVariableTarget.Process);
        var dotNet = Environment.GetEnvironmentVariable(
            "DOTNET_ENVIRONMENT",
            EnvironmentVariableTarget.Process);

        if ((!string.IsNullOrWhiteSpace(aspNetCore)
             && !string.Equals(aspNetCore, Environments.Development, StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(dotNet)
                && !string.Equals(dotNet, Environments.Development, StringComparison.Ordinal))
            || (string.IsNullOrWhiteSpace(aspNetCore) && string.IsNullOrWhiteSpace(dotNet)))
        {
            throw new InvalidOperationException("VERIFIED_MARKET_OPERATIONAL_ENVIRONMENT_NOT_ALLOWED");
        }

        return Environments.Development;
    }

    private sealed class OperationalHostEnvironment : IHostEnvironment
    {
        public OperationalHostEnvironment(string environmentName, string contentRootPath, string applicationName)
        {
            EnvironmentName = environmentName;
            ContentRootPath = contentRootPath;
            ApplicationName = applicationName;
            ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
