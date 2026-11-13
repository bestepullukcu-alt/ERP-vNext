using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.CrmService.Application.Features.Commercial.PreOrders;
using Diten.CrmService.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.CrmService.Infrastructure.Commercial;

/// <summary>
/// WP-VW-W6 W6-BE-c2 — the Platform's internal document repository door (PLT-W6-DOC), straight to the Platform
/// (<c>PlatformService:BaseUrl</c>, never the Gateway: an internal endpoint is not on its surface) with the shared internal
/// API key and NO user token — the scheduler and the Work Center consumer have none. <c>sourceModule = MOD-0168</c>: the
/// Platform lets it write <c>CommercialPreOrderDocument</c> only and read that scope plus <c>CrmVisitEvidence</c> (the
/// pharmacist's signature W6-BE-a stored).
/// <para>Transport (the W5 client's profile, a longer budget for a file): 10 seconds in total, one transient retry (502 / 503
/// / 504). It never throws: anything but the answer is null, and the transmission attempt fails with a reason.</para>
/// </summary>
public sealed class PlatformPreOrderDocumentStore : IPreOrderDocumentStore
{
    public const string ObjectsPath = "api/internal/document-repository/objects";
    public const string SourceModule = "MOD-0168";
    public const string Scope = "CommercialPreOrderDocument";
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(75);

    private readonly HttpClient _http;
    private readonly string _key;
    private readonly ILogger<PlatformPreOrderDocumentStore> _logger;

    public PlatformPreOrderDocumentStore(HttpClient http, IConfiguration configuration, IOptions<AuthServiceOptions> auth,
        ILogger<PlatformPreOrderDocumentStore> logger)
    {
        _http = http;
        _key = auth.Value.InternalApiKey ?? string.Empty;
        _logger = logger;
        _http.BaseAddress = new Uri((configuration["PlatformService:BaseUrl"] ?? "http://localhost:5057").TrimEnd('/') + "/");
    }

    public async Task<Guid?> StoreAsync(Guid tenantId, Guid preOrderId, string fileName, byte[] pdf, CancellationToken ct)
    {
        var body = new
        {
            tenantId,
            sourceModule = SourceModule,
            scope = Scope,
            ownerItemId = preOrderId,
            fileName,
            contentType = "application/pdf",
            contentBase64 = Convert.ToBase64String(pdf)
        };
        var answer = await SendAsync(() => new HttpRequestMessage(HttpMethod.Post, ObjectsPath) { Content = JsonContent.Create(body) }, ct);
        if (answer is null)
        {
            return null;
        }

        using (answer)
        {
            try
            {
                using var json = JsonDocument.Parse(await answer.Content.ReadAsStringAsync(ct));
                var root = json.RootElement;
                var data = root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d : root;
                return data.TryGetProperty("documentRef", out var r) && r.TryGetGuid(out var id) && id != Guid.Empty ? id : null;
            }
            catch (JsonException)
            {
                _logger.LogWarning("pre_order.document.store_unreadable PreOrderId={Id}", preOrderId);
                return null;
            }
        }
    }

    public async Task<byte[]?> ReadAsync(Guid tenantId, Guid documentRef, CancellationToken ct)
    {
        var path = $"{ObjectsPath}/{documentRef:D}?tenantId={tenantId:D}&sourceModule={SourceModule}";
        var answer = await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, path), ct);
        if (answer is null)
        {
            return null;
        }

        using (answer)
        {
            return await answer.Content.ReadAsByteArrayAsync(ct);
        }
    }

    /// <summary>The successful answer, or null (no key, time-out, network error, error status) — logged by status only.</summary>
    private async Task<HttpResponseMessage?> SendAsync(Func<HttpRequestMessage> build, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_key))
        {
            _logger.LogWarning("pre_order.document.no_internal_key");
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TotalTimeout);
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var message = build();
                message.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _key);
                var response = await _http.SendAsync(message, timeout.Token);
                if (IsTransient(response.StatusCode) && attempt == 0)
                {
                    response.Dispose();
                    await Task.Delay(RetryDelay, timeout.Token);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("pre_order.document.platform_refused Status={Status}", (int)response.StatusCode);
                    response.Dispose();
                    return null;
                }

                return response;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning("pre_order.document.platform_unreachable {Type}", ex.GetType().Name);
        }

        return null;
    }

    private static bool IsTransient(HttpStatusCode status)
        => status is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
}

/// <summary><c>Commercial:PreOrders:Document:FallbackLanguage</c> (default <c>en</c>).</summary>
public sealed class ConfigurationPreOrderDocumentSettings : IPreOrderDocumentSettings
{
    public ConfigurationPreOrderDocumentSettings(IConfiguration configuration)
    {
        var language = configuration["Commercial:PreOrders:Document:FallbackLanguage"]?.Trim().ToLowerInvariant();
        FallbackLanguage = string.IsNullOrEmpty(language) ? "en" : language;
    }

    public string FallbackLanguage { get; }
}
