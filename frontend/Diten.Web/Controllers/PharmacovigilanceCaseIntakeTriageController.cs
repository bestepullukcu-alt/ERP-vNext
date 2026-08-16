using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0230 tenant-shell MVC proxy. Browser calls stay same-origin; tenant, actor, and correlation headers are
/// resolved server-side before forwarding to the Gateway PV case intake/triage route family.
/// </summary>
[Authorize]
[Route("Pharmacovigilance/CaseIntakeTriage")]
public sealed class PharmacovigilanceCaseIntakeTriageController : Controller
{
    private const string ApiBase = "/api/pv-case-intake-triage";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<PharmacovigilanceCaseIntakeTriageController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public PharmacovigilanceCaseIntakeTriageController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<PharmacovigilanceCaseIntakeTriageController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Pharmacovigilance/CaseIntakeTriage/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() => View("~/Views/Pharmacovigilance/CaseIntakeTriage/Create.cshtml");

    [HttpGet("Edit/{intakeDraftId}")]
    public IActionResult Edit(string intakeDraftId)
    {
        ViewData["IntakeDraftId"] = intakeDraftId;
        return View("~/Views/Pharmacovigilance/CaseIntakeTriage/Edit.cshtml");
    }

    [HttpGet("Details/{intakeDraftId}")]
    public IActionResult Details(string intakeDraftId)
    {
        ViewData["IntakeDraftId"] = intakeDraftId;
        return View("~/Views/Pharmacovigilance/CaseIntakeTriage/Details.cshtml");
    }

    [HttpGet("api/list")]
    public Task<IActionResult> List([FromQuery] string? status, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var query = new List<string>
        {
            $"pageNumber={Math.Max(1, pageNumber)}",
            $"pageSize={Math.Clamp(pageSize, 1, 100)}"
        };

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status.Trim())}");
        }

        return ProxyGetAsync($"{ApiBase}?{string.Join('&', query)}", ct);
    }

    [HttpGet("api/detail/{intakeDraftId}")]
    public Task<IActionResult> Detail(string intakeDraftId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{Uri.EscapeDataString(intakeDraftId)}", ct);

    [HttpPost("api/create")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CreateDraft([FromForm] CaseIntakeFormInput input, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, ApiBase, input.ToPayload(), ct);

    [HttpPost("api/update/{intakeDraftId}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateDraft(string intakeDraftId, [FromForm] CaseIntakeFormInput input, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"{ApiBase}/{Uri.EscapeDataString(intakeDraftId)}", input.ToPayload(), ct);

    [HttpPost("api/triage/{intakeDraftId}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> TriageDraft(string intakeDraftId, [FromForm] TriageFormInput input, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{Uri.EscapeDataString(intakeDraftId)}/triage", input.ToPayload(), ct);

    [HttpPost("api/route/{intakeDraftId}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RouteDraft(string intakeDraftId, [FromForm] RouteFormInput input, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{Uri.EscapeDataString(intakeDraftId)}/route", input.ToPayload(), ct);

    private async Task<IActionResult> ProxyGetAsync(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_gatewayUrl}{path}");
        if (!AddAuthHeaders(request, requireActor: false))
        {
            return UnauthorizedJson();
        }

        try
        {
            return await PassthroughAsync(await _httpClient.SendAsync(request, ct), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PV case intake proxy GET failed.");
            return GatewayErrorJson();
        }
    }

    private async Task<IActionResult> ProxyJsonAsync(HttpMethod method, string path, object payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}")
        {
            Content = System.Net.Http.Json.JsonContent.Create(payload, options: _jsonOptions)
        };

        if (!AddAuthHeaders(request, requireActor: true))
        {
            return UnauthorizedJson();
        }

        try
        {
            return await PassthroughAsync(await _httpClient.SendAsync(request, ct), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PV case intake proxy command failed.");
            return GatewayErrorJson();
        }
    }

    private Task<IActionResult> PassthroughAsync(HttpResponseMessage response, CancellationToken ct) =>
        Diten.Web.Infrastructure.TenantShellProxyResponse.PassthroughAsync(response, Request, ct);

    private IActionResult UnauthorizedJson() => JsonFailure(401, "UNAUTHORIZED", _sharedLocalizer["Unauthorized"].Value);
    private IActionResult GatewayErrorJson() => JsonFailure(502, "GATEWAY_ERROR", _sharedLocalizer["GatewayError"].Value);

    private ContentResult JsonFailure(int status, string reasonCode, string message)
    {
        var json = JsonSerializer.Serialize(new
        {
            data = (object?)null,
            isSuccessful = false,
            statusCode = status,
            errors = new[] { message },
            reason_code = reasonCode,
            correlation_id = HttpContext.TraceIdentifier
        });
        return new ContentResult { Content = json, ContentType = "application/json", StatusCode = status };
    }

    private bool AddAuthHeaders(HttpRequestMessage request, bool requireActor)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var tenantReference = GetClaimOrTokenValue(token, "tenantId", "tenant_id");
        if (string.IsNullOrWhiteSpace(tenantReference))
        {
            return false;
        }

        request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantReference);
        request.Headers.TryAddWithoutValidation("X-Diten-Tenant-Context", tenantReference);
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", HttpContext.TraceIdentifier);

        if (!requireActor)
        {
            return true;
        }

        var actorId = GetClaimOrTokenValue(token, "sub", "nameidentifier", "nameid") ?? User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return false;
        }

        request.Headers.TryAddWithoutValidation("X-Diten-Actor-Id", actorId);
        request.Headers.TryAddWithoutValidation("X-Diten-Actor-Kind", "User");
        return true;
    }

    private string? GetClaimOrTokenValue(string? accessToken, params string[] claimNames)
    {
        var claimValue = User.Claims.FirstOrDefault(claim =>
            claimNames.Any(name =>
                string.Equals(claim.Type, name, StringComparison.OrdinalIgnoreCase) ||
                claim.Type.EndsWith($"/{name}", StringComparison.OrdinalIgnoreCase)))?.Value;
        if (!string.IsNullOrWhiteSpace(claimValue))
        {
            return claimValue;
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(accessToken))
            {
                return null;
            }

            var token = handler.ReadJwtToken(accessToken);
            return token.Claims.FirstOrDefault(claim =>
                claimNames.Any(name =>
                    string.Equals(claim.Type, name, StringComparison.OrdinalIgnoreCase) ||
                    claim.Type.EndsWith($"/{name}", StringComparison.OrdinalIgnoreCase)))?.Value;
        }
        catch
        {
            return null;
        }
    }

    public sealed record CaseIntakeFormInput(
        string? IntakeChannel,
        string? SourceType,
        string? SourceReference,
        string? ReceivedAtUtc,
        string? ReporterType,
        string? ReporterContactSummary,
        string? PatientSubjectCode,
        string? EventOnsetDate,
        string? AdverseEventNarrative,
        string? SuspectProductText,
        string? Seriousness,
        string? IntakePriority,
        string? EvidenceLinkReferencesRaw)
    {
        public object ToPayload() => new
        {
            IntakeChannel = Clean(IntakeChannel),
            SourceType = Clean(SourceType),
            SourceReference = Clean(SourceReference),
            ReceivedAtUtc = Clean(ReceivedAtUtc),
            ReporterType = Clean(ReporterType),
            ReporterContactSummary = Clean(ReporterContactSummary),
            PatientSubjectCode = Clean(PatientSubjectCode),
            EventOnsetDate = Clean(EventOnsetDate),
            AdverseEventNarrative = Clean(AdverseEventNarrative),
            SuspectProductText = Clean(SuspectProductText),
            Seriousness = Clean(Seriousness),
            IntakePriority = Clean(IntakePriority),
            EvidenceLinkReferences = SplitReferences(EvidenceLinkReferencesRaw)
        };
    }

    public sealed record TriageFormInput(string? TriageOutcome, string? TriageReasonCode, string? TriageReason)
    {
        public object ToPayload() => new
        {
            TriageOutcome = Clean(TriageOutcome),
            TriageReasonCode = Clean(TriageReasonCode),
            TriageReason = Clean(TriageReason)
        };
    }

    public sealed record RouteFormInput(string? RouteTargetQueue)
    {
        public object ToPayload() => new { RouteTargetQueue = Clean(RouteTargetQueue) };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> SplitReferences(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
}
