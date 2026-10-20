using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.BuildingBlocks.Auditing;

public static class AuditForwardingServiceCollectionExtensions
{
    /// <summary>
    /// The forwarder and what it needs: options (bind them with <c>services.Configure&lt;PlatformAuditForwarderOptions&gt;</c>),
    /// the counters, the default caller (<see cref="HttpAuditCallContext"/>; a service may replace
    /// <see cref="IAuditCallContext"/>), the HTTP client and the start-up check. Idempotent (TryAdd).
    /// <para>
    /// It does NOT add <see cref="AuditForwardingBehavior{TRequest,TResponse}"/> to the pipeline: the service registers
    /// the behavior itself, where its other behaviors are, so the registration is visible in the service (AUD-001 K4 —
    /// the architecture test looks for it there).
    /// </para>
    /// </summary>
    public static IServiceCollection AddPlatformAuditForwarding(this IServiceCollection services)
    {
        services.AddOptions<PlatformAuditForwarderOptions>();
        services.AddHttpContextAccessor();
        services.AddHttpClient(PlatformAuditForwarder.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(5));
        services.TryAddSingleton<AuditForwardingCounters>();
        services.TryAddScoped<HttpAuditCallContext>();
        services.TryAddScoped<IAuditCallContext>(sp => sp.GetRequiredService<HttpAuditCallContext>());
        services.TryAddScoped<IPlatformAuditForwarder, PlatformAuditForwarder>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, AuditForwardingStartupCheck>());
        return services;
    }
}

/// <summary>
/// K8 — an unconfigured forwarder is said at start-up, once, as a WARNING (not Debug), naming what is missing; each
/// record skipped afterwards is a warning of its own (<see cref="PlatformAuditForwarder"/>).
/// </summary>
public sealed class AuditForwardingStartupCheck : IHostedService
{
    private readonly IOptions<PlatformAuditForwarderOptions> _options;
    private readonly ILogger<AuditForwardingStartupCheck> _logger;

    public AuditForwardingStartupCheck(IOptions<PlatformAuditForwarderOptions> options, ILogger<AuditForwardingStartupCheck> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var missing = options.MissingSettings();
        if (missing.Count > 0)
        {
            _logger.LogWarning(
                "AUD-001: central audit forwarding is NOT configured (missing {MissingSettings}); every audited command will log a skipped record.",
                string.Join(", ", missing));
        }
        else
        {
            _logger.LogInformation("AUD-001: central audit forwarding on ({SourceService} → {BaseUrl}).", options.SourceService, options.BaseUrl);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
