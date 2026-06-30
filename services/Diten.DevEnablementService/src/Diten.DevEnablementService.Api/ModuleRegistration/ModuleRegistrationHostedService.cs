using System.Net.Http.Json;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Microsoft.Extensions.Options;

namespace Diten.DevEnablementService.Api.ModuleRegistration;

/// <summary>
/// Pushes this service's module manifest to the Platform module-catalog at startup (self-registration). BEST-EFFORT
/// with retry: runs after the app is up (BackgroundService), retries a few times with increasing backoff to swallow the
/// "Platform not ready yet" startup race, and NEVER crashes startup. Safe to repeat every restart (Platform reconcile is
/// idempotent). Sends X-Internal-Api-Key (S2S, direct to Platform — /api/internal is not gateway-exposed).
/// </summary>
public sealed class ModuleRegistrationHostedService : BackgroundService
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const int MaxAttempts = 5;

    private readonly IEnumerable<IModuleManifestProvider> _manifestProviders;
    private readonly PlatformRegistrationOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ModuleRegistrationHostedService> _logger;

    public ModuleRegistrationHostedService(
        IEnumerable<IModuleManifestProvider> manifestProviders,
        IOptions<PlatformRegistrationOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<ModuleRegistrationHostedService> logger)
    {
        _manifestProviders = manifestProviders;
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) || string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            _logger.LogWarning("Module self-registration skipped: PlatformRegistration BaseUrl/InternalApiKey not configured.");
            return;
        }

        // Push EVERY registered provider as its own manifest (GoldenSlim + GoldenCompact + …). Each is independent
        // and best-effort: one module's failure (or the shutdown signal) never blocks the others.
        foreach (var provider in _manifestProviders)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var manifest = provider.GetManifest();
            try
            {
                var stopped = await ModuleRegistrationRetry.RunAsync(
                    attempt: (attemptNumber, ct) => TryRegisterAsync(manifest, attemptNumber, ct),
                    maxAttempts: MaxAttempts,
                    backoff: attemptNumber => TimeSpan.FromSeconds(Math.Pow(2, attemptNumber)), // 2s, 4s, 8s, 16s
                    delay: Task.Delay,
                    stoppingToken);

                if (!stopped)
                {
                    _logger.LogWarning(
                        "Module self-registration gave up after {MaxAttempts} attempts (Platform unreachable?). ModuleCode={ModuleCode}",
                        MaxAttempts,
                        manifest.ModuleCode);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Service shutting down — fine.
                break;
            }
            catch (Exception ex)
            {
                // Best-effort: one module's failure must not block the others or startup.
                _logger.LogError(ex, "Self-registration failed for module {ModuleCode}.", manifest.ModuleCode);
            }
        }
    }

    /// <returns><c>true</c> to STOP (success or non-retryable), <c>false</c> to RETRY (connection refused / 5xx).</returns>
    private async Task<bool> TryRegisterAsync(ModuleManifestDocument manifest, int attemptNumber, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_options.BaseUrl.TrimEnd('/')}/api/internal/module-catalog/register-manifest")
            {
                Content = JsonContent.Create(manifest)
            };
            request.Headers.Add(InternalApiKeyHeader, _options.InternalApiKey);

            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Module manifest self-registered with Platform. ModuleCode={ModuleCode} Attempt={Attempt}",
                    manifest.ModuleCode,
                    attemptNumber);
                return true;
            }

            if ((int)response.StatusCode >= 500)
            {
                _logger.LogWarning(
                    "Module self-registration attempt {Attempt} got {StatusCode}; will retry. ModuleCode={ModuleCode}",
                    attemptNumber,
                    (int)response.StatusCode,
                    manifest.ModuleCode);
                return false;
            }

            // 4xx (e.g. 401 bad key) is not transient — stop without retrying.
            _logger.LogWarning(
                "Module self-registration got non-retryable {StatusCode}. ModuleCode={ModuleCode}",
                (int)response.StatusCode,
                manifest.ModuleCode);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return true; // shutting down
        }
        catch (HttpRequestException ex)
        {
            // Connection refused / Platform not up yet — retry.
            _logger.LogWarning(
                ex,
                "Module self-registration attempt {Attempt} could not reach Platform; will retry. ModuleCode={ModuleCode}",
                attemptNumber,
                manifest.ModuleCode);
            return false;
        }
    }
}
