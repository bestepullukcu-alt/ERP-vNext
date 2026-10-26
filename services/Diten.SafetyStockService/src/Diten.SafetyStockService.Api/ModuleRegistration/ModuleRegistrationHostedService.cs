using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Diten.SafetyStockService.Api.ModuleRegistration;

public sealed class PlatformRegistrationOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string InternalApiKey { get; set; } = string.Empty;
}

public sealed class ModuleRegistrationHostedService : BackgroundService
{
    private readonly IModuleManifestProvider _provider;
    private readonly PlatformRegistrationOptions _options;
    private readonly IHttpClientFactory _clients;
    private readonly ILogger<ModuleRegistrationHostedService> _logger;

    public ModuleRegistrationHostedService(IModuleManifestProvider provider,
        IOptions<PlatformRegistrationOptions> options, IHttpClientFactory clients,
        ILogger<ModuleRegistrationHostedService> logger)
    {
        _provider = provider;
        _options = options.Value;
        _clients = clients;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) || string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            _logger.LogWarning("Safety stock module registration skipped: Platform registration settings are unavailable.");
            return;
        }

        for (var attempt = 1; attempt <= 5 && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post,
                    $"{_options.BaseUrl.TrimEnd('/')}/api/internal/module-catalog/register-manifest")
                {
                    Content = JsonContent.Create(_provider.GetManifest())
                };
                request.Headers.Add("X-Internal-Api-Key", _options.InternalApiKey);
                using var response = await _clients.CreateClient().SendAsync(request, stoppingToken);
                if (response.IsSuccessStatusCode) return;
                if ((int)response.StatusCode < 500)
                {
                    _logger.LogWarning("Safety stock module registration rejected with {StatusCode}.", (int)response.StatusCode);
                    return;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Safety stock module registration could not reach Platform.");
            }

            if (attempt < 5) await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), stoppingToken);
        }
    }
}
