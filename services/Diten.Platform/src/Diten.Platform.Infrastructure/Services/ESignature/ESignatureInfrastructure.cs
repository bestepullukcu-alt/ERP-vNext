using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Platform.Application.Features.ESignature.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.ESignature;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services.ESignature;

/// <summary>MOD-0022 — operator configuration of the e-signature service (section <c>ESign</c>).</summary>
public sealed class ESignOptions
{
    public const string SectionName = "ESign";

    /// <summary>
    /// Remote subject providers by module code: <c>ESign:SubjectProviders:{module}:BaseUrl</c> (ESIGN-BUNDLE §6). Operator
    /// config only — never from a manifest (WC-D1 Y2). Empty = no remote module can be signed.
    /// </summary>
    public Dictionary<string, ESignSubjectProviderOptions> SubjectProviders { get; set; } = new(StringComparer.Ordinal);

    public int SubjectProviderTimeoutSeconds { get; set; } = 10;
}

public sealed class ESignSubjectProviderOptions
{
    public string BaseUrl { get; set; } = string.Empty;
}

/// <summary>
/// Platform → AuthService <c>POST internal/esign-tickets/consume</c> (E1, ESIGN-BUNDLE §4.3) with the internal key. Fail
/// closed: no key, no answer, or any status other than 200 / 409 ⇒ <see cref="TicketConsumeOutcome.Unavailable"/> (503).
/// The ticket is sent once in the body and never logged.
/// </summary>
public sealed class AuthServiceESignTicketConsumer : IESignTicketConsumer
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly HttpClient _http;
    private readonly AuthServiceOptions _options;
    private readonly ILogger<AuthServiceESignTicketConsumer> _logger;

    public AuthServiceESignTicketConsumer(HttpClient http, IOptions<AuthServiceOptions> options, ILogger<AuthServiceESignTicketConsumer> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        }
    }

    public async Task<TicketConsumeResult> ConsumeAsync(string ticket, Guid tenantId, Guid userId, string intent, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.InternalApiKey) || _http.BaseAddress is null)
        {
            _logger.LogError("E-signature ticket consume is not configured (AuthService:BaseUrl / AuthService:InternalApiKey).");
            return TicketConsumeResult.Unavailable;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "internal/esign-tickets/consume")
        {
            Content = JsonContent.Create(new { ticket, tenantId, userId, intent, consumedBy = "platform" })
        };
        request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _options.InternalApiKey);

        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return TicketConsumeResult.Invalid;
            }

            if (response.StatusCode != HttpStatusCode.OK)
            {
                _logger.LogWarning("E-signature ticket consume answered {Status}.", (int)response.StatusCode);
                return TicketConsumeResult.Unavailable;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var data = document.RootElement.TryGetProperty("data", out var wrapped) ? wrapped : document.RootElement;
            var kind = ReadKind(data);
            if (kind is null
                || !data.TryGetProperty("passwordVerifiedAtUtc", out var verified)
                || !data.TryGetProperty("issuedAtUtc", out var issued))
            {
                _logger.LogWarning("E-signature ticket consume answered 200 with an unreadable body.");
                return TicketConsumeResult.Unavailable;
            }

            return new TicketConsumeResult(TicketConsumeOutcome.Consumed, kind.Value,
                verified.GetDateTime().ToUniversalTime(), issued.GetDateTime().ToUniversalTime());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException or InvalidOperationException)
        {
            _logger.LogWarning("E-signature ticket consume failed: {ErrorType}.", ex.GetType().Name);
            return TicketConsumeResult.Unavailable;
        }
    }

    /// <summary>AuthService writes the kind as its enum name ("Password" / "Window"); a numeric value is accepted too.</summary>
    private static ESignatureReauthKind? ReadKind(JsonElement data)
    {
        if (!data.TryGetProperty("kind", out var element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number) && Enum.IsDefined(typeof(ESignatureReauthKind), number))
        {
            return (ESignatureReauthKind)number;
        }

        return element.ValueKind == JsonValueKind.String && ESignatureWire.TryParse<ESignatureReauthKind>(element.GetString(), out var parsed)
            ? parsed
            : null;
    }
}

/// <summary>
/// The ONE generic remote subject provider (WC-D1 Y1): <c>GET {base}/esign-subjects/{subjectType}/{subjectId}</c> with the
/// caller's own bearer token and <c>X-Tenant-Id</c> (ESIGN-BUNDLE §6). 404 = not visible; anything unreadable = unavailable.
/// </summary>
public sealed class RemoteSignableSubjectClient : IRemoteSignableSubjectClient
{
    public const string HttpClientName = "esign-subject-providers";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _clients;
    private readonly IHttpContextAccessor _http;
    private readonly ITenantContext _tenant;
    private readonly ESignOptions _options;
    private readonly ILogger<RemoteSignableSubjectClient> _logger;

    public RemoteSignableSubjectClient(IHttpClientFactory clients, IHttpContextAccessor http, ITenantContext tenant, IOptions<ESignOptions> options,
        ILogger<RemoteSignableSubjectClient> logger)
    {
        _clients = clients;
        _http = http;
        _tenant = tenant;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured(string module) =>
        _options.SubjectProviders.TryGetValue(module, out var provider) && Uri.TryCreate(provider.BaseUrl, UriKind.Absolute, out _);

    public async Task<SignableSubjectResolution> ResolveAsync(string module, string subjectType, string subjectId, CancellationToken ct)
    {
        if (!_options.SubjectProviders.TryGetValue(module, out var provider) || !Uri.TryCreate(provider.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
        {
            return SignableSubjectResolution.NotFound;
        }

        var authorization = _http.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization) || !_tenant.IsResolved)
        {
            // No caller token to forward: the owner cannot judge visibility, so nothing is visible.
            return SignableSubjectResolution.NotFound;
        }

        var client = _clients.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.SubjectProviderTimeoutSeconds, 1, 60));
        using var request = new HttpRequestMessage(HttpMethod.Get,
            new Uri(baseUri, $"esign-subjects/{Uri.EscapeDataString(subjectType)}/{Uri.EscapeDataString(subjectId)}"));
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorization);
        request.Headers.TryAddWithoutValidation("X-Tenant-Id", _tenant.TenantId.ToString("D"));

        try
        {
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
            {
                return SignableSubjectResolution.NotFound;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("E-signature subject provider {Module} answered {Status}.", module, (int)response.StatusCode);
                return SignableSubjectResolution.Unavailable;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = document.RootElement.TryGetProperty("data", out var wrapped) && wrapped.ValueKind == JsonValueKind.Object
                ? wrapped
                : document.RootElement;
            var wire = root.Deserialize<WireSubject>(Json);
            if (wire?.Display is null || wire.Fields is null)
            {
                return SignableSubjectResolution.Unavailable;
            }

            return SignableSubjectResolution.Found(new SignableSubject(
                wire.Display,
                (wire.Summary ?? []).Select(l => new SignableSubjectLine(l.Label ?? "", l.Value ?? "")).ToList(),
                wire.LegalEntityId,
                wire.Fields));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException)
        {
            _logger.LogWarning("E-signature subject provider {Module} failed: {ErrorType}.", module, ex.GetType().Name);
            return SignableSubjectResolution.Unavailable;
        }
    }

    private sealed record WireSubject(string? Display, List<WireLine>? Summary, Guid? LegalEntityId, Dictionary<string, string?>? Fields);

    private sealed record WireLine(string? Label, string? Value);
}

/// <summary>The signer's UI language from <c>Accept-Language</c> (falls back to tr in the picker).</summary>
public sealed class HttpESignRequestLanguage : IESignRequestLanguage
{
    private readonly IHttpContextAccessor _http;

    public HttpESignRequestLanguage(IHttpContextAccessor http) => _http = http;

    public string Current() => _http.HttpContext?.Request.Headers.AcceptLanguage.ToString() ?? string.Empty;
}
