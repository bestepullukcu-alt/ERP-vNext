using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diten.BuildingBlocks.CentralAudit;

public static class CentralAuditServiceCollectionExtensions
{
    /// <summary>
    /// BL-501 — the one line a service adds to <c>Program.cs</c>, AFTER its own MediatR registration:
    /// <code>builder.Services.AddCentralAudit&lt;MyCentralAuditScope&gt;(builder.Configuration, "Diten.MyService");</code>
    /// Registers <see cref="CentralAuditBehavior{TRequest,TResponse}"/> (innermost), the request-scoped recorder (also the
    /// <see cref="ICentralAuditTransaction"/> repositories enlist through), the Mongo outbox store over the service's own
    /// scoped <c>IMongoDatabase</c>, the Platform client, the dispatcher and the health check.
    /// </summary>
    /// <typeparam name="TScope">The service's adapter over its own scoped tenant context.</typeparam>
    /// <param name="configure">Service-level choices that are code, not configuration (<see cref="CentralAuditOptions.RequireEnlistment"/>).</param>
    public static IServiceCollection AddCentralAudit<TScope>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sourceService,
        Action<CentralAuditOptions>? configure = null)
        where TScope : class, ICentralAuditScope
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceService);

        services.AddOptions<CentralAuditOptions>().Configure(options =>
        {
            configuration.GetSection(CentralAuditOptions.TuningSection).Bind(options);
            options.PlatformBaseUrl = configuration[$"{CentralAuditOptions.PlatformSection}:BaseUrl"];
            options.InternalApiKey = configuration[$"{CentralAuditOptions.PlatformSection}:InternalApiKey"];
            configure?.Invoke(options);
        });

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(new CentralAuditSource(sourceService));
        services.AddScoped<ICentralAuditScope, TScope>();
        services.AddScoped<ICentralAuditOutboxStore, MongoCentralAuditOutboxStore>();
        services.AddScoped<CentralAuditRecorder>();
        services.AddScoped<ICentralAuditTransaction>(sp => sp.GetRequiredService<CentralAuditRecorder>());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CentralAuditBehavior<,>));

        services.AddHttpClient(PlatformCentralAuditClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddSingleton<ICentralAuditPlatformClient, PlatformCentralAuditClient>();
        services.AddSingleton<CentralAuditDispatcher>();
        // Before the dispatcher and before the host serves requests: the outbox exists before any command's transaction.
        services.AddHostedService<CentralAuditOutboxInitializerHostedService>();
        if (configuration.GetValue($"{CentralAuditOptions.TuningSection}:{nameof(CentralAuditOptions.DispatcherEnabled)}", true))
        {
            services.AddHostedService<CentralAuditDispatcherHostedService>();
        }

        services.AddHealthChecks().AddCheck<CentralAuditHealthCheck>(CentralAuditHealthCheck.Name);
        return services;
    }
}
