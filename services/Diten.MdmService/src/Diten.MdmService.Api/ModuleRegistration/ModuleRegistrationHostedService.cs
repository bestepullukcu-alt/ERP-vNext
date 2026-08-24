using System.Net.Http.Json;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.ModuleRegistration;

/// <summary>
/// Pushes every registered manifest independently to Platform at startup. Each manifest has its own retry loop, and
/// registration uses only the dedicated MDM per-service credential transport.
/// </summary>
public sealed class ModuleRegistrationHostedService : BackgroundService
{
    private const string CredentialIdentifierHeader = "X-Module-Registration-Credential-Id";
    private const string CredentialSecretHeader = "X-Module-Registration-Credential";
    private const int MaxAttempts = 5;

    private readonly IReadOnlyList<IModuleManifestProvider> _manifestProviders;
    private readonly PlatformRegistrationOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ModuleRegistrationHostedService> _logger;

    public ModuleRegistrationHostedService(
        IEnumerable<IModuleManifestProvider> manifestProviders,
        IOptions<PlatformRegistrationOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<ModuleRegistrationHostedService> logger)
    {
        _manifestProviders = manifestProviders.ToArray();
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunRegistrationsAsync(Task.Delay, stoppingToken);
    }

    public async Task RunRegistrationsAsync(
        Func<TimeSpan, CancellationToken, Task> delay,
        CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl)
            || string.IsNullOrWhiteSpace(_options.ModuleRegistrationCredentialIdentifier)
            || string.IsNullOrWhiteSpace(_options.ModuleRegistrationCredentialSecret))
        {
            _logger.LogWarning("Module self-registration skipped: dedicated Platform registration credential is not configured.");
            return;
        }

        foreach (var provider in _manifestProviders)
        {
            try
            {
                await RegisterProviderAsync(provider, delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Module manifest provider or delivery failed; continuing with the next provider.");
            }
        }
    }

    private async Task RegisterProviderAsync(
        IModuleManifestProvider provider,
        Func<TimeSpan, CancellationToken, Task> delay,
        CancellationToken stoppingToken)
    {
        var manifest = provider.GetManifest();
        try
        {
            var stopped = await ModuleRegistrationRetry.RunAsync(
                attempt: (attemptNumber, ct) => TryRegisterAsync(manifest, attemptNumber, ct),
                maxAttempts: MaxAttempts,
                backoff: attemptNumber => TimeSpan.FromSeconds(Math.Pow(2, attemptNumber)), // 2s, 4s, 8s, 16s
                delay: delay,
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
            request.Headers.Add(CredentialIdentifierHeader, _options.ModuleRegistrationCredentialIdentifier);
            request.Headers.Add(CredentialSecretHeader, _options.ModuleRegistrationCredentialSecret);

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
