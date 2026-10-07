using Diten.Platform.Application;
using Diten.Platform.Application.Features.Tasks.Recovery;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;

namespace Diten.Platform.API.Configuration;

/// <summary>
/// BL-540 (1) · WP-WF-ENGINE-RECOVERY-01 FIX1 (CT decision D) — the API host's one-shot `--run-workflow-orphan-recovery`
/// mode (the pattern of `--run-audit-outbox-temporal-storage-migration` and the verified-market mode): the host's own
/// configuration (appsettings, appsettings.{environment}, environment variables) and the APPLICATION's own services
/// (<c>AddApplication</c> + <c>AddInfrastructure</c> without startup maintenance), no web host, no hosted service started.
/// Everything — reading the configuration, building the services, the run — happens inside the command's failure net,
/// which names only an exception's type (<see cref="WorkflowOrphanRecoveryCommandLine"/>).
/// </summary>
public static class WorkflowOrphanRecoveryOperationalMode
{
    public static bool IsRequested(IReadOnlyList<string> args) => WorkflowOrphanRecoveryCommandLine.IsRequested(args);

    public static Task<int> RunAsync(IReadOnlyList<string> args) =>
        WorkflowOrphanRecoveryCommandLine.RunAsync(args, BuildServices, Console.Out, Console.Error, Environment.MachineName);

    private static IServiceProvider BuildServices()
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environments.Production;
        var contentRoot = AppContext.BaseDirectory;
        var configuration = new ConfigurationBuilder()
            .SetBasePath(contentRoot)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new OperationalHostEnvironment(environmentName, contentRoot));
        services.AddLogging();
        services.AddHttpContextAccessor(); // no request: the audit actor resolves to System
        services.AddApplication();
        services.AddInfrastructure(configuration, new OperationalHostEnvironment(environmentName, contentRoot), runStartupMaintenance: false);
        services.TryAddScoped<ITenantContext, TenantContext>();
        return services.BuildServiceProvider();
    }

    private sealed class OperationalHostEnvironment(string environmentName, string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = typeof(Program).Assembly.GetName().Name ?? "Diten.Platform.API";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRoot);
    }
}
