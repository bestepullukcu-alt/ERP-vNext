using System.Globalization;
using System.Net.Http.Headers;
using System.Numerics;
using System.Text.Json;
using Diten.Web.Models.CaseIntakeTriage;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0230 tenant-shell MVC proxy. Browser calls stay same-origin; the tenant comes from the verified principal's
/// tenant_id claim and the Bearer token travels as the Gateway's caller identity (the service verifies it) before
/// forwarding to the Gateway PV case intake/triage route family.
/// Every action checks its permission key first (house pattern: RequirePage / RequireJson).
/// </summary>
[Authorize]
[Route("Pharmacovigilance/CaseIntakeTriage")]
public sealed class PharmacovigilanceCaseIntakeTriageController : Controller
{
    private const string ApiBase = "/api/pv-case-intake-triage";

    private const string ReadPermission = "pvg.case-intake-triage.read";
    private const string CreatePermission = "pvg.case-intake-triage.create";
    private const string UpdatePermission = "pvg.case-intake-triage.update";
    private const string TriagePermission = "pvg.case-intake-triage.triage";
    private const string RoutePermission = "pvg.case-intake-triage.route";

    internal const int MaxListPageSize = 100;
    private const int DefaultListPageSize = 25;
    // The API does no paging arithmetic in a checked context: the proxy never asks beyond a page whose offset fits an int.
    private const int MaxListPageNumber = 1_000_000;

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
    public IActionResult Index() =>
        RequirePage(ReadPermission) ?? View("~/Views/Pharmacovigilance/CaseIntakeTriage/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() =>
        RequirePage(CreatePermission)
        ?? View("~/Views/Pharmacovigilance/CaseIntakeTriage/Create.cshtml", new PvgCaseIntakeFormViewModel());

    [HttpGet("Edit/{intakeDraftId}")]
    public IActionResult Edit(string intakeDraftId)
    {
        if (RequirePage(UpdatePermission) is { } denied)
        {
            return denied;
        }

        ViewData["IntakeDraftId"] = intakeDraftId;
        return View("~/Views/Pharmacovigilance/CaseIntakeTriage/Edit.cshtml", new PvgCaseIntakeFormViewModel { IntakeDraftId = intakeDraftId });
    }

    [HttpGet("Details/{intakeDraftId}")]
    public IActionResult Details(string intakeDraftId)
    {
        if (RequirePage(ReadPermission) is { } denied)
        {
            return denied;
        }

        ViewData["IntakeDraftId"] = intakeDraftId;
        return View("~/Views/Pharmacovigilance/CaseIntakeTriage/Details.cshtml");
    }

    // The list component's server request (start, length, status, draw) becomes the API's page query, and the API's
    // page (items, totalCount, filteredCount) becomes the component's envelope { data: { items, total, filteredTotal } }.
    [HttpGet("api/list")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (RequireJson(ReadPermission) is { } denied)
        {
            return denied;
        }

        return await ProxyListAsync($"{ApiBase}?{BuildListQuery(Request.Query)}", ct);
    }

    [HttpGet("api/detail/{intakeDraftId}")]
    public async Task<IActionResult> Detail(string intakeDraftId, CancellationToken ct)
    {
        if (RequireJson(ReadPermission) is { } denied)
        {
            return denied;
        }

        return await ProxyGetAsync($"{ApiBase}/{Uri.EscapeDataString(intakeDraftId)}", ct);
    }

    [HttpPost("api/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDraft([FromForm] CaseIntakeFormInput input, CancellationToken ct)
    {
        if (RequireJson(CreatePermission) is { } denied)
        {
            return denied;
        }

        return await ProxyJsonAsync(HttpMethod.Post, ApiBase, input.ToPayload(includeVersion: false), ct);
    }

    [HttpPost("api/update/{intakeDraftId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDraft(string intakeDraftId, [FromForm] CaseIntakeFormInput input, CancellationToken ct)
    {
        if (RequireJson(UpdatePermission) is { } denied)
        {
            return denied;
        }

        return await ProxyJsonAsync(HttpMethod.Put, $"{ApiBase}/{Uri.EscapeDataString(intakeDraftId)}", input.ToPayload(includeVersion: true), ct);
    }

    [HttpPost("api/triage/{intakeDraftId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TriageDraft(string intakeDraftId, [FromForm] TriageFormInput input, CancellationToken ct)
    {
        if (RequireJson(TriagePermission) is { } denied)
        {
            return denied;
        }

        return await ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{Uri.EscapeDataString(intakeDraftId)}/triage", input.ToPayload(), ct);
    }

    [HttpPost("api/route/{intakeDraftId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RouteDraft(string intakeDraftId, [FromForm] RouteFormInput input, CancellationToken ct)
    {
        if (RequireJson(RoutePermission) is { } denied)
        {
            return denied;
        }

        return await ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{Uri.EscapeDataString(intakeDraftId)}/route", input.ToPayload(), ct);
    }

    /// <summary>Page-action permission gate. Missing → bare 403, re-executed to the shared /Home/Status/403 page by
    /// UseStatusCodePagesWithReExecute (UAS-001: no redirect, no skeleton of the screen).</summary>
    private IActionResult? RequirePage(string permission) =>
        PermissionClaims.HasPermission(User, permission) ? null : StatusCode(StatusCodes.Status403Forbidden);

    /// <summary>JSON-action permission gate. Missing → 403 with a message body in the reader's language (never the
    /// technical "Permission denied." text, UAS-001 §5); the Gateway is not called.</summary>
    private IActionResult? RequireJson(string permission) =>
        PermissionClaims.HasPermission(User, permission)
            ? null
            : JsonFailure(StatusCodes.Status403Forbidden, "FORBIDDEN", _sharedLocalizer["AccessDenied"].Value);

    internal static string BuildListQuery(IQueryCollection query)
    {
        var length = int.TryParse(query["length"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLength)
            && parsedLength > 0
                ? Math.Min(parsedLength, MaxListPageSize)
                : DefaultListPageSize;
        // A start beyond long is still "far past the end": it is capped, not read as zero.
        var start = BigInteger.TryParse(query["start"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStart)
            && parsedStart > 0
                ? (long)BigInteger.Min(parsedStart, long.MaxValue)
                : 0L;
        // Capped BEFORE the +1, so the largest start never wraps to a negative page.
        var pageNumber = (int)(Math.Min(start / length, MaxListPageNumber - 1L) + 1);
        var parts = new List<string>
        {
            $"pageNumber={pageNumber.ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={length.ToString(CultureInfo.InvariantCulture)}"
        };

        var status = query["status"].ToString().Trim();
        if (status.Length > 0)
        {
            parts.Add($"status={Uri.EscapeDataString(status)}");
        }

        return string.Join('&', parts);
    }

    private async Task<IActionResult> ProxyListAsync(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_gatewayUrl}{path}");
        if (!AddAuthHeaders(request))
        {
            return UnauthorizedJson();
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return await PassthroughAsync(response, ct);
            }

            return ToListEnvelope(await response.Content.ReadAsStringAsync(ct));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "PV case intake list proxy: the API answered an unreadable page.");
            return GatewayErrorJson();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PV case intake proxy GET failed.");
            return GatewayErrorJson();
        }
    }

    private IActionResult ToListEnvelope(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !(root.TryGetProperty("items", out var items) || root.TryGetProperty("Items", out items))
            || items.ValueKind != JsonValueKind.Array
            || !TryReadCount(root, "totalCount", "TotalCount", out var total)
            || !TryReadCount(root, "filteredCount", "FilteredCount", out var filteredTotal))
        {
            _logger.LogWarning("PV case intake list proxy: the API answered a page without items or counts.");
            return GatewayErrorJson();
        }

        return new ContentResult
        {
            StatusCode = StatusCodes.Status200OK,
            ContentType = "application/json",
            Content = JsonSerializer.Serialize(new { data = new { items = items.Clone(), total, filteredTotal } }, _jsonOptions)
        };
    }

    private static bool TryReadCount(JsonElement root, string camelName, string pascalName, out long count)
    {
        count = 0;
        return (root.TryGetProperty(camelName, out var value) || root.TryGetProperty(pascalName, out value))
            && value.TryGetInt64(out count)
            && count >= 0;
    }

    private async Task<IActionResult> ProxyGetAsync(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_gatewayUrl}{path}");
        if (!AddAuthHeaders(request))
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

        if (!AddAuthHeaders(request))
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

    /// <summary>The Bearer token is the caller identity (the service verifies it); the tenant reference comes only from the
    /// verified principal's tenant_id claim — a cookie token is never decoded here without verification. No tenant claim
    /// means no Gateway call. No actor header is sent: the service reads the actor from the verified Bearer.</summary>
    private bool AddAuthHeaders(HttpRequestMessage request)
    {
        var tenantReference = User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrWhiteSpace(tenantReference))
        {
            return false;
        }

        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantReference);
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", HttpContext.TraceIdentifier);
        return true;
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
        string? EvidenceLinkReferencesRaw,
        long? ExpectedVersion = null)
    {
        // The record version is the API's optimistic-concurrency token (DL4A): an update sends it, a create has none.
        public IReadOnlyDictionary<string, object?> ToPayload(bool includeVersion)
        {
            var payload = new Dictionary<string, object?>
            {
                ["IntakeChannel"] = Clean(IntakeChannel),
                ["SourceType"] = Clean(SourceType),
                ["SourceReference"] = Clean(SourceReference),
                ["ReceivedAtUtc"] = Clean(ReceivedAtUtc),
                ["ReporterType"] = Clean(ReporterType),
                ["ReporterContactSummary"] = Clean(ReporterContactSummary),
                ["PatientSubjectCode"] = Clean(PatientSubjectCode),
                ["EventOnsetDate"] = Clean(EventOnsetDate),
                ["AdverseEventNarrative"] = Clean(AdverseEventNarrative),
                ["SuspectProductText"] = Clean(SuspectProductText),
                ["Seriousness"] = Clean(Seriousness),
                ["IntakePriority"] = Clean(IntakePriority),
                ["EvidenceLinkReferences"] = SplitReferences(EvidenceLinkReferencesRaw)
            };
            if (includeVersion)
            {
                payload["ExpectedVersion"] = ExpectedVersion;
            }

            return payload;
        }
    }

    public sealed record TriageFormInput(string? TriageOutcome, string? TriageReasonCode, string? TriageReason, long? ExpectedVersion = null)
    {
        public IReadOnlyDictionary<string, object?> ToPayload() => new Dictionary<string, object?>
        {
            ["TriageOutcome"] = Clean(TriageOutcome),
            ["TriageReasonCode"] = Clean(TriageReasonCode),
            ["TriageReason"] = Clean(TriageReason),
            ["ExpectedVersion"] = ExpectedVersion
        };
    }

    public sealed record RouteFormInput(string? RouteTargetQueue, long? ExpectedVersion = null)
    {
        public IReadOnlyDictionary<string, object?> ToPayload() => new Dictionary<string, object?>
        {
            ["RouteTargetQueue"] = Clean(RouteTargetQueue),
            ["ExpectedVersion"] = ExpectedVersion
        };
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
