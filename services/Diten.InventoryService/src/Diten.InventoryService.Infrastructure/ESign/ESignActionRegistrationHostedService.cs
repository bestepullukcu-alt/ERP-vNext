using System.Net.Http.Json;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Infrastructure.ESign;

/// <summary>
/// MOD-0175 Q3 — registers the seven signable actions of pack §8 with MOD-0022 at start-up (CT-D12,
/// <c>POST {Platform}/api/internal/esign/actions/register</c>, <c>X-Internal-Api-Key</c>), the
/// <c>ModuleRegistrationHostedService</c> pattern and the same <c>PlatformRegistration</c> settings (BaseUrl, InternalApiKey):
/// best effort, 5 attempts with 2^n s backoff, never stops the host; not configured → skipped with a warning. Platform upserts
/// by action code and keeps a tenant's policy rows, so every restart is safe.
/// </summary>
public sealed class ESignActionRegistrationHostedService : BackgroundService
{
    public const string RegisterPath = "/api/internal/esign/actions/register";
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const int MaxAttempts = 5;

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _clients;
    private readonly ILogger<ESignActionRegistrationHostedService> _logger;

    public ESignActionRegistrationHostedService(IConfiguration configuration, IHttpClientFactory clients, ILogger<ESignActionRegistrationHostedService> logger)
    {
        _configuration = configuration;
        _clients = clients;
        _logger = logger;
    }

    /// <summary>The request body (ESIGN-BUNDLE <c>RegisterActionsRequest</c>).</summary>
    public static object Body() => new
    {
        module = QuarantineSignableActions.Module,
        actions = QuarantineSignableActions.All.Select(a => new
        {
            actionCode = a.Code,
            names = a.Names,
            defaultRequired = a.DefaultRequired,
            defaultMeaningCode = a.DefaultMeaning,
            defaultVisualMode = a.DefaultVisualMode
        }).ToList()
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseUrl = _configuration["PlatformRegistration:BaseUrl"];
        var key = _configuration["PlatformRegistration:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(key) || !Uri.TryCreate(baseUrl.TrimEnd('/') + RegisterPath, UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("quarantine.esign.actions.registration_skipped PlatformRegistration BaseUrl/InternalApiKey not configured.");
            return;
        }

        for (var attempt = 1; attempt <= MaxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(Body()) };
                request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, key);
                using var response = await _clients.CreateClient(nameof(ESignActionRegistrationHostedService)).SendAsync(request, stoppingToken);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("quarantine.esign.actions.registered Actions={Count} Attempt={Attempt}", QuarantineSignableActions.All.Count, attempt);
                    return;
                }

                _logger.LogWarning("quarantine.esign.actions.registration_refused Status={Status} Attempt={Attempt}", (int)response.StatusCode, attempt);
                if ((int)response.StatusCode is >= 400 and < 500 and not 408 and not 429)
                {
                    return; // a configuration / contract error does not heal by retrying
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("quarantine.esign.actions.registration_failed {Error} Attempt={Attempt}", ex.GetType().Name, attempt);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }
}
