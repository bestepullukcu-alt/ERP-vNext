using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Diten.Platform.Common.Observability;

public static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddDitenObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        Action<IHealthChecksBuilder>? configureReadiness = null)
    {
        var section = configuration.GetSection(ObservabilityOptions.SectionName);
        services.Configure<ObservabilityOptions>(section);

        var options = section.Get<ObservabilityOptions>() ?? new ObservabilityOptions();
        if (string.IsNullOrWhiteSpace(options.Environment))
        {
            options.Environment = environment.EnvironmentName;
        }

        services.AddScoped<ICorrelationContext, CorrelationContext>();

        var healthChecks = services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live", "ready" });
        configureReadiness?.Invoke(healthChecks);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(options.ServiceName)
                .AddAttributes(new KeyValuePair<string, object>[]
                {
                    new("deployment.environment", options.Environment)
                }))
            .WithTracing(tracing =>
            {
                if (!options.Tracing.Enabled)
                {
                    return;
                }

                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (options.Tracing.OtlpExporterEnabled)
                {
                    if (string.IsNullOrWhiteSpace(options.Tracing.OtlpEndpoint))
                    {
                        throw new InvalidOperationException("Observability:Tracing:OtlpEndpoint is required when OTLP exporter is enabled.");
                    }

                    tracing.AddOtlpExporter(exporter =>
                    {
                        exporter.Endpoint = new Uri(options.Tracing.OtlpEndpoint);
                    });
                }
            });

        return services;
    }
}
