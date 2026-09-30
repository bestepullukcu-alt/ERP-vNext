using Diten.AuthService.Api.Configuration;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Operational;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Diten.AuthService.Persistence;
using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.Extensions.FileProviders;

namespace Diten.AuthService.Api.Services.ServiceIdentityTokens;

/// <summary>One-shot, process-environment-only composition. Does not build or start the normal Auth host.</summary>
public static class ServiceClientOperationalBootstrap
{
    public static async Task<int> RunAsync(IReadOnlyList<string> arguments, TextReader input, TextWriter output,
        CancellationToken cancellationToken = default)
    {
        if (!ServiceClientOperationalProvisioningRunner.IsExactInvocation(arguments))
            return await FailureAsync(output, "contract");
        if (!string.Equals(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"), "Development", StringComparison.Ordinal)
            || !string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.Ordinal))
            return await FailureAsync(output, "environment");
        try
        {
            // No JSON, user-secret or command-line provider: every option/secret must originate in this process envelope.
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<IHostEnvironment>(new ProcessEnvironment());
            services.AddSingleton(TimeProvider.System);
            services.AddOptions();
            services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
            services.Configure<ServiceClientOperationalProvisioningOptions>(
                configuration.GetSection(ServiceClientOperationalProvisioningOptions.SectionName));
            services.AddSingleton<ISecretRotationResolver>(new JwtSecretRotationResolver(configuration));
            services.AddScoped<IServiceClientCredentialVerifier, ServiceClientCredentialVerifier>();
            services.AddScoped<IServiceClientOperationalProvisioningEligibility, DevelopmentServiceClientOperationalProvisioningEligibility>();
            services.AddScoped<IServiceClientOperationalActorAuthorizer, ServiceClientOperationalActorAuthorizer>();
            services.AddScoped<IServiceClientSecretOutputSink, ServiceClientSecretOutputSink>();
            services.AddScoped<IServiceClientOperationalProvisioningService, ServiceClientOperationalProvisioningService>();
            services.AddScoped<ServiceClientOperationalProvisioningRunner>();
            ServiceClientOperationalPersistenceRegistration.AddServiceClientOperationalPersistence(services, configuration);
            await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true, ValidateOnBuild = true
            });
            await using var scope = provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ServiceClientOperationalProvisioningRunner>()
                .RunAsync(arguments, input, output, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await output.WriteLineAsync("{\"status\":\"failed\",\"code\":\"cancelled\"}");
            return 130;
        }
        catch
        {
            // Configuration, repository and parser exceptions may contain secrets; expose only a stable reason code.
            return await FailureAsync(output, "isolated-composition-unavailable");
        }
    }

    private static async Task<int> FailureAsync(TextWriter output, string code)
    {
        await output.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(new { status = "failed", code }));
        return 2;
    }

    private sealed class ProcessEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Diten.AuthService.Operational";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
