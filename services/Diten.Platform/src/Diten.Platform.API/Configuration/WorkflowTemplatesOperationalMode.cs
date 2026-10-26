using Diten.Platform.Application;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Operations;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;

namespace Diten.Platform.API.Configuration;

/// <summary>
/// WP-PLT-WORKFLOW-TEMPLATES-01 (owner decision 2026-10-15) — the API host's one-shot <c>--ensure-workflow-templates</c> mode,
/// the pattern of <see cref="WorkflowOrphanRecoveryOperationalMode"/>: the host's own configuration (appsettings,
/// appsettings.{environment}, environment variables) and the APPLICATION's own services (<c>AddApplication</c> +
/// <c>AddInfrastructure</c> without startup maintenance); no web host, no hosted service started; no request, so the actor
/// that writes is System. The catalog is the copy next to the host's binaries. See
/// <see cref="EnsureWorkflowTemplatesCommandLine"/> for the arguments and the exit codes.
/// </summary>
public static class WorkflowTemplatesOperationalMode
{
    public static bool IsRequested(IReadOnlyList<string> args) => EnsureWorkflowTemplatesCommandLine.IsRequested(args);

    public static Task<int> RunAsync(IReadOnlyList<string> args) =>
        EnsureWorkflowTemplatesCommandLine.RunAsync(
            args,
            BuildServices,
            Path.Combine(AppContext.BaseDirectory, WorkflowTemplateCatalog.RelativePath),
            Console.Out,
            Console.Error);

    private static IServiceProvider BuildServices()
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environments.Production;
        var contentRoot = AppContext.BaseDirectory;
        return BuildServices(
            WorkflowOrphanRecoveryOperationalMode.ReadConfiguration(environmentName, contentRoot), environmentName, contentRoot);
    }

    /// <summary>
    /// The command's services from a given configuration: the production registrations, scopes validated and the command's
    /// own graph (the mediator, the two definition commands with their pipeline, the repositories it reads) resolved here,
    /// eagerly — a missing registration fails inside the command's failure net, before anything is read.
    /// </summary>
    /// <param name="afterRegistrations">TEST SEAM — runs after the production registrations (a test wraps a repository to
    /// stage a concurrent publish). Null in production.</param>
    public static ServiceProvider BuildServices(
        IConfiguration configuration,
        string environmentName,
        string contentRoot,
        Action<IServiceCollection>? afterRegistrations = null)
    {
        var environment = new OperationalHostEnvironment(environmentName, contentRoot);
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddLogging();
        services.AddHttpContextAccessor(); // no request: the actor resolves to System ("system")
        services.AddApplication();
        services.AddInfrastructure(configuration, environment, runStartupMaintenance: false);
        services.TryAddScoped<ITenantContext, TenantContext>();
        // What Program.cs registers for the web host and the command pipeline's behaviours need too (as the orphan mode).
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<Diten.Platform.Application.Authorization.IOrgDataScopeCandidateAvailabilityClassifier,
            Diten.Platform.API.Security.MongoOrgDataScopeCandidateAvailabilityClassifier>();
        afterRegistrations?.Invoke(services);
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
        resolve.GetRequiredService<Diten.Platform.Domain.Repositories.ITenantRegistryRepository>();
        resolve.GetRequiredService<Diten.Platform.Domain.Repositories.IWorkflowTemplateRepository>();
        resolve.GetRequiredService<Diten.Platform.Domain.Repositories.IWorkflowTemplateVersionRepository>();
        resolve.GetRequiredService<MediatR.IRequestHandler<CreateWorkflowDefinitionCommand, Response<WorkflowDefinitionDetailDto>>>();
        resolve.GetServices<MediatR.IPipelineBehavior<CreateWorkflowDefinitionCommand, Response<WorkflowDefinitionDetailDto>>>().ToList();
        resolve.GetRequiredService<MediatR.IRequestHandler<PublishWorkflowDefinitionCommand, Response<PublishWorkflowDefinitionResponse>>>();
        resolve.GetServices<MediatR.IPipelineBehavior<PublishWorkflowDefinitionCommand, Response<PublishWorkflowDefinitionResponse>>>().ToList();
    }

    private sealed class OperationalHostEnvironment(string environmentName, string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = typeof(Program).Assembly.GetName().Name ?? "Diten.Platform.API";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRoot);
    }
}
