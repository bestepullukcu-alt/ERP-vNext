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

        endpoints.MapGet("/health/ready", Ok<PvgServiceHealthStatus> (IOptions<PvgServiceApiRuntimeOptions> options) =>
            TypedResults.Ok(PvgServiceHealthStatus.Ready(options.Value)));

        return endpoints;
    }
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
