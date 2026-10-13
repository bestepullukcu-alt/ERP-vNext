using System.Net.Http.Json;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Infrastructure.ESign;

/// <summary>
/// Registers the signable actions of the InventoryService modules with MOD-0022 at start-up (CT-D12,
/// <c>POST {Platform}/api/internal/esign/actions/register</c>, <c>X-Internal-Api-Key</c>) — ONE call per module (Platform enforces the
/// <c>{module}.</c> prefix per call): MOD-0175 <c>quarantine</c> (seven actions, pack §8) and, since WP-0176-F3, MOD-0176 <c>expiry</c>
/// (four actions, pack §8.6). The <c>ModuleRegistrationHostedService</c> pattern and the same <c>PlatformRegistration</c> settings
/// (BaseUrl, InternalApiKey): best effort, 5 attempts per module with 2^n s backoff, never stops the host; not configured → skipped with a
/// warning. Platform upserts by action code and keeps a tenant's policy rows, so every restart is safe. The quarantine call is
/// byte-identical to the pre-F3 one (regression test with the golden SHA-256 of its body).
/// </summary>
public sealed class ESignActionRegistrationHostedService : BackgroundService
{
    public const string RegisterPath = "/api/internal/esign/actions/register";
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const int MaxAttempts = 5;

    /// <summary>One module's registration: its module code and its actions in the ESIGN-BUNDLE <c>RegisterActionsRequest</c> item shape.</summary>
    public sealed record ModuleActions(string Module, IReadOnlyList<RegisteredAction> Actions);

    public sealed record RegisteredAction(string Code, IReadOnlyDictionary<string, string> Names, bool DefaultRequired, string DefaultMeaning, string DefaultVisualMode);

    /// <summary>The modules registered, in call order.</summary>
    public static readonly IReadOnlyList<ModuleActions> Modules =
    [
        new(QuarantineSignableActions.Module,
            QuarantineSignableActions.All.Select(a => new RegisteredAction(a.Code, a.Names, a.DefaultRequired, a.DefaultMeaning, a.DefaultVisualMode)).ToList()),
        new(ExpirySignableActions.Module,
            ExpirySignableActions.All.Select(a => new RegisteredAction(a.Code, a.Names, a.DefaultRequired, a.DefaultMeaning, a.DefaultVisualMode)).ToList())
    ];

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _clients;
    private readonly ILogger<ESignActionRegistrationHostedService> _logger;

    public ESignActionRegistrationHostedService(IConfiguration configuration, IHttpClientFactory clients, ILogger<ESignActionRegistrationHostedService> logger)
    {
        _configuration = configuration;
        _clients = clients;
        _logger = logger;
    }

    /// <summary>The MOD-0175 request body (pre-F3 API, kept for its tests).</summary>
    public static object Body() => Body(QuarantineSignableActions.Module);

    /// <summary>The request body of one module (ESIGN-BUNDLE <c>RegisterActionsRequest</c>).</summary>
    public static object Body(string module)
    {
        var entry = Modules.Single(m => m.Module == module);
        return new
        {
            module = entry.Module,
            actions = entry.Actions.Select(a => new
            {
                actionCode = a.Code,
                names = a.Names,
                defaultRequired = a.DefaultRequired,
                defaultMeaningCode = a.DefaultMeaning,
                defaultVisualMode = a.DefaultVisualMode
            }).ToList()
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseUrl = _configuration["PlatformRegistration:BaseUrl"];
        var key = _configuration["PlatformRegistration:InternalApiKey"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(key) || !Uri.TryCreate(baseUrl.TrimEnd('/') + RegisterPath, UriKind.Absolute, out var uri))
        {
            foreach (var module in Modules)
            {
                _logger.LogWarning("{Module}.esign.actions.registration_skipped PlatformRegistration BaseUrl/InternalApiKey not configured.", module.Module);
            }

            return;
        }

        await Task.WhenAll(Modules.Select(m => RegisterAsync(m, uri, key, stoppingToken)));
    }

    private async Task RegisterAsync(ModuleActions module, Uri uri, string key, CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(Body(module.Module)) };
                request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, key);
                using var response = await _clients.CreateClient(nameof(ESignActionRegistrationHostedService)).SendAsync(request, stoppingToken);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("{Module}.esign.actions.registered Actions={Count} Attempt={Attempt}", module.Module, module.Actions.Count, attempt);
                    return;
                }

                _logger.LogWarning("{Module}.esign.actions.registration_refused Status={Status} Attempt={Attempt}", module.Module, (int)response.StatusCode, attempt);
                if ((int)response.StatusCode is >= 400 and < 500 and not 408 and not 429)
                {
                    return; // a configuration / contract error does not heal by retrying
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("{Module}.esign.actions.registration_failed {Error} Attempt={Attempt}", module.Module, ex.GetType().Name, attempt);
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
