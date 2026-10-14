using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Diten.PlanningService.Api.ModuleRegistration;

public sealed class ModuleRegistrationHostedService : BackgroundService
{
    private readonly IEnumerable<IModuleManifestProvider> _providers;
    private readonly PlatformRegistrationOptions _options;
    private readonly IHttpClientFactory _clients;
    private readonly ILogger<ModuleRegistrationHostedService> _logger;

    public ModuleRegistrationHostedService(IEnumerable<IModuleManifestProvider> providers,
        IOptions<PlatformRegistrationOptions> options, IHttpClientFactory clients,
        ILogger<ModuleRegistrationHostedService> logger)
    {
        _providers = providers;
        _options = options.Value;
        _clients = clients;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) ||
            string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            _logger.LogWarning("Demand module registration skipped: Platform registration key is unavailable.");
            return;
        }

        foreach (var provider in _providers)
        {
            var manifest = provider.GetManifest();
            for (var attempt = 1; attempt <= 5 && !stoppingToken.IsCancellationRequested; attempt++)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post,
                        $"{_options.BaseUrl.TrimEnd('/')}/api/internal/module-catalog/register-manifest")
                    {
                        Content = JsonContent.Create(manifest)
                    };
                    request.Headers.Add("X-Internal-Api-Key", _options.InternalApiKey);
                    using var response = await _clients.CreateClient().SendAsync(request, stoppingToken);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Demand module registered with Platform.");
                        break;
                    }
                    if ((int)response.StatusCode < 500)
                    {
                        _logger.LogWarning("Demand module registration rejected with {StatusCode}.",
                            (int)response.StatusCode);
                        break;
                    }
                    _logger.LogWarning("Demand module registration retry after {StatusCode}.",
                        (int)response.StatusCode);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Demand module registration could not reach Platform.");
                }

                if (attempt < 5)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), stoppingToken);
            }
        }
    }
}
