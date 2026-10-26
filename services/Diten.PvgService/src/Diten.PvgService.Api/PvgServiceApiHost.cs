using Diten.PvgService.Application.CaseProcessing;
using Diten.PvgService.Application.MeddraCoding;
using Diten.PvgService.Application.RegPvBase;
using Diten.PvgService.Application.SignalManagement;
using Diten.PvgService.Infrastructure.RegPvBase;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace Diten.PvgService.Api;

public static class PvgServiceApiHost
{
    public const string NonOperationalMode = "local-dev-ci-build-test";

    private const string RequiredDependencyStartupRefusal =
        "PVG production-like startup refused; required dependencies are not registered: ";

    // Single source for the PvgIntakeDraftApplicationService dependencies checked at startup and by /health/ready.
    private static readonly (Type ServiceType, string SafeCode)[] RequiredDependencies =
    [
        (typeof(IPvgFieldSecurityPolicy), "PVG_DEPENDENCY_FIELD_SECURITY"),
        (typeof(IPvgWorkflowTransitionGate), "PVG_DEPENDENCY_WORKFLOW_GATE"),
        (typeof(IPvgEvidenceLinkPort), "PVG_DEPENDENCY_EVIDENCE_LINK"),
        (typeof(IPvgPermissionGate), "PVG_DEPENDENCY_PERMISSION_GATE"),
        (typeof(IPvgIntakeDraftStore), "PVG_DEPENDENCY_INTAKE_STORE")
    ];

    public static IWebHostBuilder CreatePvgServiceWebHostBuilder(
        string? environmentName,
        IConfiguration configuration,
        string urls,
        Action<IServiceCollection>? configureServices = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(urls);

        return new WebHostBuilder()
            .UseKestrel()
            .UseContentRoot(AppContext.BaseDirectory)
            .UseConfiguration(configuration)
            .UseEnvironment(environmentName ?? Environments.Production)
            .UseUrls(urls)
            .ConfigureServices((context, services) =>
            {
                services.AddRouting();
                services.AddPvgServiceApiHost(context.Configuration, context.HostingEnvironment);
                configureServices?.Invoke(services);

                ThrowIfRequiredDependenciesMissing(
                    services,
                    PvgServiceApiRuntimeOptions.From(context.Configuration, context.HostingEnvironment.EnvironmentName));
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapPvgServiceHealthEndpoints();
                    endpoints.MapPvgCaseIntakeTriageEndpoints();
                });
            });
    }

    public static IServiceCollection AddPvgServiceApiHost(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var options = PvgServiceApiRuntimeOptions.From(configuration, environment.EnvironmentName);
        options.ThrowIfUnsafe();

        services.AddSingleton(Options.Create(options));
        services.AddHealthChecks();

        services.AddSingleton<PvgIntakeDraftApplicationService>();
        services.AddSingleton<PvgCaseProcessingApplicationService>();
        services.AddSingleton<InMemoryMeddraCodingApplicationService>();
        services.AddSingleton<InMemorySignalManagementService>();

        if (options.UseNonProductionAdapters)
        {
            services.AddSingleton<IPvgFieldSecurityPolicy, DenyAllFieldSecurityPolicy>();
            services.AddSingleton<IPvgWorkflowTransitionGate, DenyAllWorkflowTransitionGate>();
            services.AddSingleton<IPvgEvidenceLinkPort, DenyAllEvidenceLinkPort>();
            services.AddSingleton<IPvgPermissionGate, DenyAllPermissionGate>();
            services.AddSingleton<IPvgIntakeDraftStore, InMemoryPvgIntakeDraftRepository>();
        }

        return services;
    }

    public static IEndpointRouteBuilder MapPvgServiceHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", Ok<PvgServiceHealthStatus> () =>
            TypedResults.Ok(PvgServiceHealthStatus.Live()));

        endpoints.MapGet("/health/ready", Results<Ok<PvgServiceHealthStatus>, JsonHttpResult<PvgServiceNotReadyStatus>> (
            HttpContext httpContext,
            IOptions<PvgServiceApiRuntimeOptions> options) =>
        {
            var serviceRegistry = httpContext.RequestServices.GetService<IServiceProviderIsService>();
            var missingCodes = MissingRequiredDependencyCodes(
                serviceType => serviceRegistry?.IsService(serviceType) == true);

            return missingCodes.Count == 0
                ? TypedResults.Ok(PvgServiceHealthStatus.Ready(options.Value))
                : TypedResults.Json(
                    PvgServiceNotReadyStatus.From(options.Value, missingCodes),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        return endpoints;
    }

    private static void ThrowIfRequiredDependenciesMissing(
        IServiceCollection services,
        PvgServiceApiRuntimeOptions options)
    {
        if (!options.IsProductionLike)
        {
            return;
        }

        var missingCodes = MissingRequiredDependencyCodes(serviceType =>
            services.Any(descriptor => descriptor.ServiceType == serviceType && !descriptor.IsKeyedService));

        if (missingCodes.Count > 0)
        {
            throw new InvalidOperationException(
                RequiredDependencyStartupRefusal + string.Join(", ", missingCodes));
        }
    }

    private static IReadOnlyList<string> MissingRequiredDependencyCodes(Func<Type, bool> isRegistered) =>
        RequiredDependencies
            .Where(dependency => !isRegistered(dependency.ServiceType))
            .Select(dependency => dependency.SafeCode)
            .ToArray();
}

public sealed record PvgServiceApiRuntimeOptions(
    bool IsProductionLike,
    bool OperationalRuntimeAuthorized,
    bool UseNonProductionAdapters)
{
    public static PvgServiceApiRuntimeOptions From(IConfiguration configuration, string environmentName)
    {
        var isProductionLike =
            string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase) ||
            configuration.GetValue("Pvg:Runtime:ProductionLike", false);

        var operationalRuntimeAuthorized = configuration.GetValue(
            "Pvg:Runtime:OperationalRuntimeAuthorized",
            false);

        var useNonProductionAdapters = configuration.GetValue(
            "Pvg:Runtime:UseNonProductionAdapters",
            !isProductionLike);

        return new PvgServiceApiRuntimeOptions(
            isProductionLike,
            operationalRuntimeAuthorized,
            useNonProductionAdapters);
    }

    public void ThrowIfUnsafe()
    {
        if (IsProductionLike && !OperationalRuntimeAuthorized)
        {
            throw new InvalidOperationException(
                "PVG operational runtime is not authorized for production-like startup.");
        }

        if (IsProductionLike && UseNonProductionAdapters)
        {
            throw new InvalidOperationException(
                "PVG production-like startup cannot use non-production adapters.");
        }
    }
}

public sealed record PvgServiceHealthStatus(
    string Status,
    string Mode,
    bool OperationalRuntimeAuthorized,
    bool NonProductionAdaptersEnabled)
{
    public static PvgServiceHealthStatus Live() =>
        new("live", PvgServiceApiHost.NonOperationalMode, false, false);

    public static PvgServiceHealthStatus Ready(PvgServiceApiRuntimeOptions options) =>
        new(
            "ready",
            PvgServiceApiHost.NonOperationalMode,
            options.OperationalRuntimeAuthorized,
            options.UseNonProductionAdapters);
}

public sealed record PvgServiceNotReadyStatus(
    string Status,
    string Mode,
    bool OperationalRuntimeAuthorized,
    bool NonProductionAdaptersEnabled,
    IReadOnlyList<string> MissingDependencyCodes)
{
    public static PvgServiceNotReadyStatus From(
        PvgServiceApiRuntimeOptions options,
        IReadOnlyList<string> missingDependencyCodes) =>
        new(
            "not-ready",
            PvgServiceApiHost.NonOperationalMode,
            options.OperationalRuntimeAuthorized,
            options.UseNonProductionAdapters,
            missingDependencyCodes);
}
