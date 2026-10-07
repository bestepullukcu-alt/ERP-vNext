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
        return BuildServices(ReadConfiguration(environmentName, contentRoot), environmentName, contentRoot);
    }

    /// <summary>The host's own configuration, as the operator's run reads it: appsettings, appsettings.{environment},
    /// environment variables.</summary>
    public static IConfigurationRoot ReadConfiguration(string environmentName, string contentRoot) =>
        new ConfigurationBuilder()
            .SetBasePath(contentRoot)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

    /// <summary>
    /// The command's services from a given configuration: the production registrations, validated when built (FIX2 item 3 —
    /// a missing or mis-scoped registration fails HERE, inside the command's failure net, not half-way through a run).
    /// <para>Validated for what the command USES, not the whole container: <c>ValidateOnBuild</c> over every registration of
    /// AddApplication + AddInfrastructure fails without a web host (measured: services whose dependencies only Program.cs
    /// registers — the org data-scope classifier, the actor permission context, the trusted-start policy — and ASP.NET's
    /// EndpointDataSource). So scopes are validated, and the command's own graph — the mediator, the orphan sweep and the
    /// engine's cancel with their pipeline behaviours, the audit service — is resolved here, eagerly.</para>
    /// Public so the operator wiring is tested as it runs, over a real database.
    /// </summary>
    public static ServiceProvider BuildServices(IConfiguration configuration, string environmentName, string contentRoot)
    {
        var environment = new OperationalHostEnvironment(environmentName, contentRoot);
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddLogging();
        services.AddHttpContextAccessor(); // no request: the audit actor resolves to System
        services.AddApplication();
        services.AddInfrastructure(configuration, environment, runStartupMaintenance: false);
        services.TryAddScoped<ITenantContext, TenantContext>();
        // FIX2 item 3 — what Program.cs registers for the web host and the command's own graph needs too (measured: the
        // audit behaviour's authorization context resolves the org data scope, which needs this classifier). Registered here
        // exactly as Program.cs does; without it the command failed on the server before it read anything.
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<Diten.Platform.Application.Authorization.IOrgDataScopeCandidateAvailabilityClassifier,
            Diten.Platform.API.Security.MongoOrgDataScopeCandidateAvailabilityClassifier>();
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        ValidateCommandGraph(provider);
        return provider;
    }

    private static void ValidateCommandGraph(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var resolve = scope.ServiceProvider;
        resolve.GetRequiredService<ITenantContext>();
        resolve.GetRequiredService<MediatR.IMediator>();
        resolve.GetRequiredService<Diten.Platform.Application.Contracts.Audit.IAuditService>();
        resolve.GetRequiredService<MediatR.IRequestHandler<SweepWorkflowOrphansCommand,
            Diten.Platform.Application.Common.Response<WorkflowOrphanSweepResult>>>();
        resolve.GetServices<MediatR.IPipelineBehavior<SweepWorkflowOrphansCommand,
            Diten.Platform.Application.Common.Response<WorkflowOrphanSweepResult>>>().ToList();
        resolve.GetRequiredService<MediatR.IRequestHandler<Diten.Platform.Application.Features.Workflow.Commands.CancelWorkflowTaskCommand,
            Diten.Platform.Application.Common.Response<Diten.Platform.Application.Features.Workflow.WorkflowTaskTransitionResponse>>>();
        resolve.GetServices<MediatR.IPipelineBehavior<Diten.Platform.Application.Features.Workflow.Commands.CancelWorkflowTaskCommand,
            Diten.Platform.Application.Common.Response<Diten.Platform.Application.Features.Workflow.WorkflowTaskTransitionResponse>>>().ToList();
    }

    private sealed class OperationalHostEnvironment(string environmentName, string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = typeof(Program).Assembly.GetName().Name ?? "Diten.Platform.API";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRoot);
    }
}
