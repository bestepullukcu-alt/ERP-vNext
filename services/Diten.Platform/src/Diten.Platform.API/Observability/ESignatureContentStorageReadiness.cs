using Diten.Platform.Application.Contracts.DocumentRepository;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Diten.Platform.API.Observability;

/// <summary>
/// MOD-0022 CT-D4 — signature images are evidence: a configured <c>DocumentManagement:ContentStorage:RootPath</c> is a go-live
/// precondition for a GxP tenant (the <c>%TEMP%</c> default is not acceptable). In this slice that is a WARNING, not a
/// stop: outside Development an unset root makes this check <see cref="HealthStatus.Degraded"/> (readiness stays 200, the
/// reason is visible on /health) and logs one warning at startup. Development may use the temp default freely.
/// </summary>
public sealed class ESignatureContentStorageHealthCheck : IHealthCheck
{
    private readonly IOptions<ContentStorageOptions> _options;
    private readonly IHostEnvironment _environment;

    public ESignatureContentStorageHealthCheck(IOptions<ContentStorageOptions> options, IHostEnvironment environment)
    {
        _options = options;
        _environment = environment;
    }

    public static bool RootMissing(ContentStorageOptions options, IHostEnvironment environment) =>
        !environment.IsDevelopment() && string.IsNullOrWhiteSpace(options.RootPath);

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(RootMissing(_options.Value, _environment)
            ? HealthCheckResult.Degraded(
                "e-signature evidence storage: DocumentManagement:ContentStorage:RootPath is not configured (CT-D4 — go-live precondition for a GxP tenant).")
            : HealthCheckResult.Healthy("e-signature evidence storage root configured."));
}

/// <summary>Logs the CT-D4 warning once when the service starts.</summary>
public sealed class ESignatureContentStorageStartupWarning : IHostedService
{
    private readonly IOptions<ContentStorageOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ESignatureContentStorageStartupWarning> _logger;

    public ESignatureContentStorageStartupWarning(IOptions<ContentStorageOptions> options, IHostEnvironment environment,
        ILogger<ESignatureContentStorageStartupWarning> logger)
    {
        _options = options;
        _environment = environment;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (ESignatureContentStorageHealthCheck.RootMissing(_options.Value, _environment))
        {
            _logger.LogWarning(
                "e-signature evidence storage uses the temporary-directory default: configure DocumentManagement:ContentStorage:RootPath "
                + "before a GxP tenant goes live (MOD-0022 CT-D4).");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
