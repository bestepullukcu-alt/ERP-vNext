using System.Globalization;
using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.Gskus;
using Diten.Web.Security;
using Diten.Web.Views.MasterDataManagement.Gskus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("MasterDataManagement/Gskus")]
public sealed class GskusController : Controller
{
    private const string ReadPermission = "mdm.gskus.read";
    private const string CreatePermission = "mdm.gskus.create";
    private const string UpdatePermission = "mdm.gskus.update";
    private const string SubmitPermission = "mdm.gskus.submit";
    private const string WithdrawPermission = "mdm.gskus.withdraw";
    private const string RequestCorrectionPermission = "mdm.gskus.request-correction";
    private const string RequestRetirementPermission = "mdm.gskus.request-retirement";
    private static readonly TimeSpan FormAttemptLifetime = TimeSpan.FromMinutes(30);

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ITimeLimitedDataProtector _formAttemptProtector;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly IStringLocalizer<GskusIndex> _localizer;
    private readonly IStringLocalizer<GskuRefusals> _refusals;
    private readonly ILogger<GskusController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public GskusController(
        HttpClient httpClient,
        IConfiguration configuration,
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizer<GskusIndex> localizer,
        ILogger<GskusController> logger,
        IStringLocalizer<GskuRefusals>? refusals = null)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _formAttemptProtector = dataProtectionProvider
            .CreateProtector("Diten.Web", "MOD-0290", "GskuFormAttempt", "v1")
            .ToTimeLimitedDataProtector();
        _sharedLocalizer = sharedLocalizer;
        _localizer = localizer;
        _logger = logger;
        // Resolved by DI in the application; a test that builds the controller by hand passes it or gets codes only.
        _refusals = refusals ?? new NoRefusals();
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        // UAS-001 (FIX3 B17): no redirect for a signed-in reader without mdm.gskus.read. The status says 403 and the
        // view draws the access-denied surface inside the shell — and nothing of the list (Index.cshtml checks first).
        if (!HasPermission(ReadPermission))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View("~/Views/MasterDataManagement/Gskus/Index.cshtml");
        }

        var canCreate = HasPermission(CreatePermission);
        ViewData["CanCreateGsku"] = canCreate;
        ViewData["CanUseGskuEditor"] = canCreate
            || HasPermission(UpdatePermission)
            || HasPermission(RequestCorrectionPermission);
        if (canCreate)
            ViewData["GskuFormAttemptToken"] = CreateFormAttemptToken();

        return View("~/Views/MasterDataManagement/Gskus/Index.cshtml");
    }

    [HttpPut("api/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDraft(
        Guid id,
        [FromForm] int? expectedVersion,
        [FromForm] string? packQuantity,
        [FromForm] string? packUomCode,
        CancellationToken cancellationToken)
    {
        packUomCode = packUomCode?.Trim().ToUpperInvariant();
        if (!HasPermission(UpdatePermission))
            return ForbiddenResult();
        if (id == Guid.Empty
            || expectedVersion is null or < 0
            || string.IsNullOrWhiteSpace(packUomCode)
            || packUomCode.Length > 16
            || packUomCode.Any(char.IsControl)
            || !decimal.TryParse(
                packQuantity,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var parsedPackQuantity)
            || parsedPackQuantity <= 0
            || !await HasOnlyFormFieldsAsync("ExpectedVersion", "PackQuantity", "PackUomCode"))
        {
            return SafeFailure(HttpStatusCode.BadRequest);
        }
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return UnauthorizedResult();

        var operationId = CreateLifecycleOperationId(
            tenantId,
            actor,
            "gsku",
            "update-draft",
            id,
            expectedVersion.Value,
            $"{parsedPackQuantity.ToString(CultureInfo.InvariantCulture)}\t{packUomCode}");
        return await ProxyLifecycleRequestAsync(
            HttpMethod.Put,
            $"{_gatewayUrl}/api/gskus/{id:D}",
            JsonContent.Create(new
            {
                expectedVersion = expectedVersion.Value,
                packQuantity = parsedPackQuantity,
                packUomCode
            }, options: _jsonOptions),
            operationId,
            new HashSet<int> { StatusCodes.Status200OK },
            "update-draft",
            cancellationToken);
    }

    [HttpPost("api/{id:guid}/identity-approval/withdraw")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WithdrawIdentityApproval(
        Guid id,
        [FromForm] int? expectedGskuVersion,
        [FromForm] string? reasonCode,
        [FromForm] string? comment,
        CancellationToken cancellationToken)
    {
        reasonCode = reasonCode?.Trim();
        comment = comment?.Trim() ?? string.Empty;
        if (!HasPermission(WithdrawPermission))
            return ForbiddenResult();
        if (id == Guid.Empty
            || expectedGskuVersion is null or < 0
            || string.IsNullOrWhiteSpace(reasonCode)
            || reasonCode.Length > 128
            || reasonCode.Any(char.IsControl)
            || comment.Length > 2000
            || comment.Any(char.IsControl)
            || !await HasOnlyFormFieldsAsync("ExpectedGskuVersion", "ReasonCode", "Comment"))
        {
            return SafeFailure(HttpStatusCode.BadRequest);
        }
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return UnauthorizedResult();

        var operationId = CreateLifecycleOperationId(
            tenantId,
            actor,
            "gsku",
            "withdraw-identity-approval",
            id,
            expectedGskuVersion.Value,
            $"{reasonCode}\t{comment}");
        return await ProxyLifecycleRequestAsync(
            HttpMethod.Post,
            $"{_gatewayUrl}/api/gskus/{id:D}/identity-approval/withdraw",
            JsonContent.Create(new
            {
                expectedGskuVersion = expectedGskuVersion.Value,
                reasonCode,
                comment
            }, options: _jsonOptions),
            operationId,
            new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted },
            "withdraw-identity-approval",
            cancellationToken);
    }

    [HttpPost("api/{id:guid}/correction-requests")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestCorrection(
        Guid id,
        [FromForm] int? expectedGskuVersion,
        [FromForm] string? packQuantity,
        [FromForm] string? packUomCode,
        CancellationToken cancellationToken)
    {
        packUomCode = packUomCode?.Trim().ToUpperInvariant();
        if (!HasPermission(RequestCorrectionPermission))
            return ForbiddenResult();
        if (id == Guid.Empty
            || expectedGskuVersion is null or < 0
            || string.IsNullOrWhiteSpace(packUomCode)
            || packUomCode.Length > 16
            || packUomCode.Any(char.IsControl)
            || !decimal.TryParse(packQuantity, NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var parsedPackQuantity)
            || parsedPackQuantity <= 0
            || !await HasOnlyFormFieldsAsync("ExpectedGskuVersion", "PackQuantity", "PackUomCode"))
        {
            return SafeFailure(HttpStatusCode.BadRequest);
        }
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return UnauthorizedResult();

        var operationId = CreateLifecycleOperationId(
            tenantId,
            actor,
            "gsku",
            "request-correction",
            id,
            expectedGskuVersion.Value,
            $"{parsedPackQuantity.ToString(CultureInfo.InvariantCulture)}\t{packUomCode}");
        return await ProxyLifecycleRequestAsync(
            HttpMethod.Post,
            $"{_gatewayUrl}/api/gskus/{id:D}/correction-requests",
            JsonContent.Create(new
            {
                expectedGskuVersion = expectedGskuVersion.Value,
                packQuantity = parsedPackQuantity,
                packUomCode
            }, options: _jsonOptions),
            operationId,
            new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted },
            "request-correction",
            cancellationToken);
    }

    [HttpPost("api/{id:guid}/retirement-requests")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestRetirement(
        Guid id,
        [FromForm] int? expectedGskuVersion,
        [FromForm] string? requestReason,
        CancellationToken cancellationToken)
    {
        requestReason = requestReason?.Trim();
        if (!HasPermission(RequestRetirementPermission))
            return ForbiddenResult();
        if (id == Guid.Empty
            || expectedGskuVersion is null or < 0
            || string.IsNullOrWhiteSpace(requestReason)
            || requestReason.Length > 128
            || requestReason.Any(char.IsControl)
            || !await HasOnlyFormFieldsAsync("ExpectedGskuVersion", "RequestReason"))
        {
            return SafeFailure(HttpStatusCode.BadRequest);
        }
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return UnauthorizedResult();

        var operationId = CreateLifecycleOperationId(
            tenantId, actor, "gsku", "request-retirement", id,
            expectedGskuVersion.Value, requestReason);
        return await ProxyLifecycleRequestAsync(
            HttpMethod.Post,
            $"{_gatewayUrl}/api/gskus/{id:D}/retirement-requests",
            JsonContent.Create(new
            {
                expectedGskuVersion = expectedGskuVersion.Value,
                requestReason
            }, options: _jsonOptions),
            operationId,
            new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted },
            "request-retirement",
            cancellationToken);
    }

    // FIX3 B17: the list component's server request (start, length, search, orderBy, orderDir, lifecycleStatus) becomes
    // MDM's page query, and MDM's page becomes the component's envelope { data: { items, total, filteredTotal } }.
    // MDM returns one count (the rows matching search + filter), so total and filteredTotal are both that count.
    [HttpGet("api")]
    public Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!HasPermission(ReadPermission))
            return Task.FromResult<IActionResult>(ForbiddenResult());

        return ProxyListAsync($"{_gatewayUrl}/api/gskus?{BuildListQuery(Request.Query)}", cancellationToken);
    }

    internal const int MaxListPageSize = 100;
    private const int DefaultListPageSize = 20;

    internal static string BuildListQuery(IQueryCollection query)
    {
        var length = int.TryParse(query["length"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLength)
            && parsedLength > 0
                ? Math.Min(parsedLength, MaxListPageSize)
                : DefaultListPageSize;
        var start = long.TryParse(query["start"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStart)
            && parsedStart > 0
                ? parsedStart
                : 0;
        var pageNumber = (int)Math.Min(start / length + 1, int.MaxValue);
        var parts = new List<string>
        {
            $"pageNumber={pageNumber.ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={length.ToString(CultureInfo.InvariantCulture)}"
        };
        // Passed as typed; MDM's validator is the one judge (unknown sort key or direction = 400, never a silent default).
        foreach (var key in new[] { "search", "orderBy", "orderDir", "lifecycleStatus" })
        {
            var value = query[key].ToString().Trim();
            if (value.Length > 0)
                parts.Add($"{key}={Uri.EscapeDataString(value)}");
        }
        return string.Join("&", parts);
    }

    private async Task<IActionResult> ProxyListAsync(string targetUrl, CancellationToken cancellationToken)
    {
        if (!TryCreateGatewayRequest(HttpMethod.Get, targetUrl, content: null, out var request))
            return UnauthorizedResult();

        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    return await KnownOrStatusFailureAsync(response, cancellationToken);

                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !(root.TryGetProperty("data", out var page) || root.TryGetProperty("Data", out page))
                    || page.ValueKind != JsonValueKind.Object
                    || !(page.TryGetProperty("items", out var items) || page.TryGetProperty("Items", out items))
                    || items.ValueKind != JsonValueKind.Array
                    || !(page.TryGetProperty("totalCount", out var count) || page.TryGetProperty("TotalCount", out count))
                    || !count.TryGetInt64(out var total)
                    || total < 0)
                {
                    _logger.LogWarning("GSKU list proxy: MDM answered a page without items or totalCount.");
                    return SafeFailure(HttpStatusCode.BadGateway);
                }

                // A ContentResult like every other GSKU proxy answer (the page re-shaped for the list component).
                return new ContentResult
                {
                    StatusCode = StatusCodes.Status200OK,
                    ContentType = "application/json",
                    Content = JsonSerializer.Serialize(new
                    {
                        success = true,
                        data = new { items = items.Clone(), total, filteredTotal = total }
                    }, _jsonOptions)
                };
            }
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "GSKU list proxy: MDM answered an unreadable page.");
            return SafeFailure(HttpStatusCode.BadGateway);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "GSKU gateway proxy timed out for {TargetUrl}.", targetUrl);
            return SafeFailure(HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "GSKU gateway proxy failed for {TargetUrl}.", targetUrl);
            return SafeFailure(HttpStatusCode.ServiceUnavailable);
        }
    }

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        if (!HasPermission(ReadPermission))
            return Task.FromResult<IActionResult>(ForbiddenResult());

        return ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/gskus/{id:D}", cancellationToken);
    }

    [HttpGet("api/create-options")]
    public Task<IActionResult> CreateOptions(CancellationToken cancellationToken)
    {
        if (!HasPermission(CreatePermission))
            return Task.FromResult<IActionResult>(ForbiddenResult());

        // FIX3 B17: the product picker pages and searches — its pageNumber, pageSize and search reach MDM (they were
        // dropped, so only MDM's first 20 products could ever be chosen). MDM validates them (pageSize 1–50).
        return ProxyGatewayAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}/api/gskus/create-options{BuildCreateOptionsQuery(Request.Query)}",
            cancellationToken);
    }

    internal static string BuildCreateOptionsQuery(IQueryCollection query)
    {
        var parts = new List<string>();
        foreach (var key in new[] { "pageNumber", "pageSize", "search" })
        {
            var value = query[key].ToString().Trim();
            if (value.Length > 0)
                parts.Add($"{key}={Uri.EscapeDataString(value)}");
        }
        return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
    }

    [HttpGet("api/{id:guid}/edit-options")]
    public Task<IActionResult> EditOptions(Guid id, CancellationToken cancellationToken) =>
        HasPermission(UpdatePermission)
            ? ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/gskus/{id:D}/edit-options", cancellationToken)
            : Task.FromResult<IActionResult>(ForbiddenResult());

    [HttpGet("api/{id:guid}/correction-options")]
    public Task<IActionResult> CorrectionOptions(Guid id, CancellationToken cancellationToken) =>
        HasPermission(RequestCorrectionPermission)
            ? ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/gskus/{id:D}/correction-options", cancellationToken)
            : Task.FromResult<IActionResult>(ForbiddenResult());

    [HttpPost("api")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [FromForm] CreateGskuViewModel model,
        [FromForm] string? formAttemptToken,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(CreatePermission))
            return ForbiddenResult();

        if (!TryReadFormAttempt(formAttemptToken, out var operationKey))
            return BadRequest(new { success = false, errors = new[] { _localizer["ErrorInvalidFormAttempt"].Value } });

        model.PackUomCode = model.PackUomCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!ModelState.IsValid
            || model.GlobalProductId == Guid.Empty
            || string.IsNullOrWhiteSpace(model.PackUomCode)
            || !decimal.TryParse(
                model.PackQuantity,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var packQuantity)
            || packQuantity <= 0)
        {
            return BadRequest(new { success = false, errors = new[] { _localizer["ErrorValidation"].Value } });
        }

        var payload = new
        {
            globalProductId = model.GlobalProductId,
            packQuantity,
            packUomCode = model.PackUomCode
        };

        if (!TryCreateGatewayRequest(
                HttpMethod.Post,
                $"{_gatewayUrl}/api/gskus/drafts",
                JsonContent.Create(payload, options: _jsonOptions),
                out var request))
        {
            return UnauthorizedResult();
        }

        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationKey);

        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                var envelope = await ReadEnvelopeAsync<GskuDraftViewModel>(response, cancellationToken);

                if (response.StatusCode == HttpStatusCode.Created
                    && envelope?.IsSuccessful == true
                    && envelope.Data is not null)
                {
                    return StatusCode(StatusCodes.Status201Created, new
                    {
                        success = true,
                        data = envelope.Data,
                        formAttemptToken = CreateFormAttemptToken()
                    });
                }

                if (response.StatusCode == HttpStatusCode.Accepted)
                {
                    return StatusCode(StatusCodes.Status202Accepted, new
                    {
                        success = false,
                        data = envelope?.Data,
                        errors = new[] { _localizer["CreateReconciliationPending"].Value },
                        formAttemptToken
                    });
                }

                return await KnownOrStatusFailureAsync(response, cancellationToken);
            }
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "GSKU create proxy timed out.");
            return SafeFailure(HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "GSKU create proxy failed.");
            return SafeFailure(HttpStatusCode.ServiceUnavailable);
        }
    }

    [HttpPost("api/{id:guid}/submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitLifecycle(
        Guid id,
        [FromForm] int? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(SubmitPermission))
            return ForbiddenResult();

        if (id == Guid.Empty || expectedVersion is null or < 0
            || !await HasOnlyFormFieldsAsync("ExpectedVersion"))
        {
            return SafeFailure(HttpStatusCode.BadRequest);
        }
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return UnauthorizedResult();

        var operationId = CreateLifecycleOperationId(
            tenantId, actor, "gsku", "submit", id, expectedVersion.Value, string.Empty);
        var payload = JsonContent.Create(new { expectedVersion = expectedVersion.Value }, options: _jsonOptions);
        IReadOnlySet<int> allowedSuccessStatusCodes =
            new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted };
        return await ProxyLifecycleRequestAsync(
            HttpMethod.Post,
            $"{_gatewayUrl}/api/gskus/{id:D}/submit",
            payload,
            operationId,
            allowedSuccessStatusCodes,
            "submit",
            cancellationToken);
    }

    private async Task<IActionResult> ProxyLifecycleRequestAsync(
        HttpMethod method,
        string targetUrl,
        HttpContent payload,
        Guid operationId,
        IReadOnlySet<int> allowedSuccessStatusCodes,
        string action,
        CancellationToken cancellationToken)
    {
        if (!TryCreateGatewayRequest(method, targetUrl, payload, out var request))
        {
            payload.Dispose();
            return UnauthorizedResult();
        }
        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"));

        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                var responseStatus = (int)response.StatusCode;
                if (!response.IsSuccessStatusCode)
                    return await KnownOrStatusFailureAsync(response, cancellationToken);

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (!allowedSuccessStatusCodes.Contains(responseStatus)
                    || !IsJsonMediaType(mediaType))
                {
                    return SafeFailure(HttpStatusCode.BadGateway);
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                LifecycleGatewayEnvelope? envelope;
                try
                {
                    envelope = JsonSerializer.Deserialize<LifecycleGatewayEnvelope>(body, _jsonOptions);
                }
                catch (JsonException exception)
                {
                    _logger.LogWarning(exception, "GSKU lifecycle proxy received malformed JSON for {Action}.", action);
                    return SafeFailure(HttpStatusCode.BadGateway);
                }

                if (envelope?.IsSuccessful != true || envelope.StatusCode != responseStatus)
                    return SafeFailure(HttpStatusCode.BadGateway);

                return new ContentResult
                {
                    StatusCode = responseStatus,
                    ContentType = "application/json",
                    Content = body
                };
            }
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "GSKU lifecycle proxy timed out for {Action}.", action);
            return SafeFailure(HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "GSKU lifecycle proxy failed for {Action}.", action);
            return SafeFailure(HttpStatusCode.ServiceUnavailable);
        }
    }

    private async Task<bool> HasOnlyFormFieldsAsync(params string[] allowedFields)
    {
        if (!Request.HasFormContentType)
            return false;
        var form = await Request.ReadFormAsync(HttpContext.RequestAborted);
        var required = new HashSet<string>(allowedFields, StringComparer.Ordinal);
        var allowed = new HashSet<string>(required, StringComparer.Ordinal) { "__RequestVerificationToken" };
        return form.Count == allowed.Count
            && form.Keys.All(allowed.Contains)
            && form.TryGetValue("__RequestVerificationToken", out var antiforgery)
            && antiforgery.Count == 1
            && required.All(field => form.TryGetValue(field, out var values) && values.Count == 1);
    }

    private static bool IsJsonMediaType(string? mediaType) =>
        string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
        || mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) == true;

    private bool TryResolveLifecycleIdentity(out Guid tenantId, out string actor)
    {
        tenantId = Guid.Empty;
        actor = string.Empty;
        if (User.Identity?.IsAuthenticated != true
            || !IsHumanActorType(SingleClaim(User, "actor_type")))
        {
            return false;
        }

        var subjects = ClaimValues(User, "sub");
        var nameIdentifiers = ClaimValues(User, ClaimTypes.NameIdentifier);
        if (subjects.Count > 1 || nameIdentifiers.Count > 1
            || subjects.Count == 0 && nameIdentifiers.Count == 0
            || !TryCanonicalGuid(subjects, out var subject)
            || !TryCanonicalGuid(nameIdentifiers, out var nameIdentifier)
            || subject.HasValue && nameIdentifier.HasValue && subject != nameIdentifier)
        {
            return false;
        }

        var subjectId = subject ?? nameIdentifier ?? Guid.Empty;
        if (subjectId == Guid.Empty)
            return false;

        var tenantValues = User.Claims.Where(claim =>
            claim.Type == "tenantId" || claim.Type == "tenant_id"
            || claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase)).Select(claim => claim.Value).ToArray();
        actor = subjectId.ToString("D");
        return tenantValues.Length == 1 && Guid.TryParseExact(tenantValues[0], "D", out tenantId) && tenantId != Guid.Empty;
    }

    private static string? SingleClaim(ClaimsPrincipal principal, string type)
    {
        var values = ClaimValues(principal, type);
        return values.Count == 1 ? values[0] : null;
    }

    private static bool IsHumanActorType(string? actorType) => actorType is
        "tenant_user" or "platform_admin" or "partner_admin";

    private static IReadOnlyList<string> ClaimValues(ClaimsPrincipal principal, string type) =>
        principal.Claims
            .Where(claim => string.Equals(claim.Type, type, StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .ToArray();

    private static bool TryCanonicalGuid(IReadOnlyList<string> values, out Guid? result)
    {
        result = null;
        if (values.Count == 0)
            return true;

        if (!Guid.TryParseExact(values[0], "D", out var parsed) || parsed == Guid.Empty)
            return false;

        result = parsed;
        return true;
    }

    private static Guid CreateLifecycleOperationId(
        Guid tenantId,
        string actor,
        string aggregateType,
        string action,
        Guid aggregateId,
        int expectedVersion,
        string reasonCode)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendLengthPrefixed(hash, tenantId.ToString("D"));
        AppendLengthPrefixed(hash, actor);
        AppendLengthPrefixed(hash, aggregateType);
        AppendLengthPrefixed(hash, action);
        AppendLengthPrefixed(hash, aggregateId.ToString("D"));
        AppendLengthPrefixed(hash, expectedVersion.ToString(CultureInfo.InvariantCulture));
        AppendLengthPrefixed(hash, reasonCode);
        var digest = hash.GetHashAndReset();
        var hex = Convert.ToHexString(digest.AsSpan(0, 16));
        return Guid.ParseExact(
            $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..32]}",
            "D");
    }

    private static void AppendLengthPrefixed(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private sealed class LifecycleGatewayEnvelope
    {
        public bool IsSuccessful { get; init; }
        public int StatusCode { get; init; }
    }

    private async Task<IActionResult> ProxyGatewayAsync(
        HttpMethod method,
        string targetUrl,
        CancellationToken cancellationToken)
    {
        if (!TryCreateGatewayRequest(method, targetUrl, content: null, out var request))
            return UnauthorizedResult();

        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    return await KnownOrStatusFailureAsync(response, cancellationToken);

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    Content = body
                };
            }
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "GSKU gateway proxy timed out for {TargetUrl}.", targetUrl);
            return SafeFailure(HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "GSKU gateway proxy failed for {TargetUrl}.", targetUrl);
            return SafeFailure(HttpStatusCode.ServiceUnavailable);
        }
    }

    private async Task<GskuGatewayResponse<T>?> ReadEnvelopeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<GskuGatewayResponse<T>>(_jsonOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "GSKU gateway returned an invalid response envelope.");
            return null;
        }
    }

    // GSKU FIX1: the reasons a maker can act on are said in words (7 languages); anything else stays a status message.
    // Only the codes listed here are ever turned into text — no other content of MDM's answer reaches the screen.
    private static readonly IReadOnlyDictionary<string, string> KnownFailureKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["GSKU_PARENT_NOT_APPROVED"] = "ErrorParentNotApproved",
        ["GSKU_IDENTITY_SUBMIT_IN_FLIGHT"] = "ErrorSubmitInFlight",
        ["GSKU_DEPENDENT_IDENTITIES_EXIST"] = "ErrorDependentIdentitiesExist",
        ["PRODUCT_IDENTITY_WORKFLOW_APPROVAL_CHANGING"] = "ErrorApprovalChanging",
        ["PRODUCT_IDENTITY_WORKFLOW_DEFINITION_NOT_READY"] = "ErrorApprovalTemplateMissing",
        // FIX2 item 8: an earlier withdrawal of this approval, with another reason or comment, is still under way. The
        // proxy cannot finish it with this request's text (MDM keeps what the first request said) — it says so.
        ["FIRST_GSKU_IDENTITY_WITHDRAWAL_OPERATION_CONFLICT"] = "ErrorWithdrawalPending",
        // FIX2 item 1: every draft state's refusal is said, with what to do next.
        ["GSKU_IDENTITY_RECONCILIATION_REQUIRED"] = "ErrorReconciliationRequired",
        ["FIRST_GSKU_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED"] = "ErrorReconciliationRequired",
        ["FIRST_GSKU_IDENTITY_SUBMISSION_ENDED"] = "ErrorSubmissionEnded",
        ["FIRST_GSKU_IDENTITY_WORKFLOW_DRAFT_CHANGED"] = "ErrorDraftChanged",
        ["FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED"] = "IdentityNotAppliedParent",
        ["REFERENCE_DATA_CONTRACT_CONFLICT"] = "IdentityNotAppliedReference",
        ["FIRST_GSKU_IDENTITY_DECISION_NOT_APPLICABLE"] = "IdentityNotAppliedGeneric"
    };

    // FIX3 item 4 (B18): every refusal MDM names is said as a sentence, whatever its status (FIX1/FIX2 translated only a
    // single known code on a 409). Several codes → each one's sentence, once. A code with no sentence is a missing
    // mapping: the status's general sentence is shown, the code is logged here and the browser is told (unmapped) so the
    // console warns — the raw code itself never reaches the browser.
    private async Task<IActionResult> KnownOrStatusFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!IsJsonMediaType(response.Content.Headers.ContentType?.MediaType))
            return SafeFailure(response.StatusCode);
        string[] codes;
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !(document.RootElement.TryGetProperty("errors", out var errors)
                     || document.RootElement.TryGetProperty("Errors", out errors))
                || errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() is 0 or > 20
                || errors.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
                return SafeFailure(response.StatusCode);
            codes = errors.EnumerateArray().Select(item => item.GetString()!).ToArray();
        }
        catch (JsonException)
        {
            // Not a readable answer: the status message below.
            return SafeFailure(response.StatusCode);
        }

        var status = NormalizedStatus(response.StatusCode);
        var sentences = new List<string>();
        var unmapped = false;
        foreach (var code in codes)
        {
            string? sentence = KnownFailureKeys.TryGetValue(code, out var known)
                ? _localizer[known].Value
                : GskuRefusalSentences.Keys.TryGetValue(code, out var refusal)
                    ? _refusals[refusal].Value
                    : null;
            if (sentence is null)
            {
                unmapped = true;
                _logger.LogWarning("GSKU proxy: MDM answered {Status} with a code that has no sentence ({ErrorCode}).",
                    status, IsCode(code) ? code : "UNREADABLE_CODE");
                sentence = MapStatusMessage(status);
            }
            if (!sentences.Contains(sentence, StringComparer.Ordinal))
                sentences.Add(sentence);
        }
        return unmapped
            ? StatusCode(status, new { success = false, errors = sentences.ToArray(), unmapped = true })
            : StatusCode(status, new { success = false, errors = sentences.ToArray() });
    }

    private static bool IsCode(string value) => value.Length is > 0 and <= 100
        && value.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');

    private sealed class NoRefusals : IStringLocalizer<GskuRefusals>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: true);
        public LocalizedString this[string name, params object[] arguments] => this[name];
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private IActionResult SafeFailure(HttpStatusCode statusCode)
    {
        var normalizedStatus = NormalizedStatus(statusCode);
        return StatusCode(normalizedStatus, new
        {
            success = false,
            errors = new[] { MapStatusMessage(normalizedStatus) }
        });
    }

    private static int NormalizedStatus(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.BadRequest => StatusCodes.Status400BadRequest,
            HttpStatusCode.Unauthorized => StatusCodes.Status401Unauthorized,
            HttpStatusCode.Forbidden => StatusCodes.Status403Forbidden,
            HttpStatusCode.NotFound => StatusCodes.Status404NotFound,
            HttpStatusCode.Conflict => StatusCodes.Status409Conflict,
            HttpStatusCode.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
            HttpStatusCode.GatewayTimeout => StatusCodes.Status504GatewayTimeout,
            _ => StatusCodes.Status502BadGateway
        };
    }

    private IActionResult ForbiddenResult() => StatusCode(StatusCodes.Status403Forbidden, new
    {
        success = false,
        errors = new[] { _localizer["ErrorForbidden"].Value }
    });

    private IActionResult UnauthorizedResult() => Unauthorized(new
    {
        success = false,
        errors = new[] { _sharedLocalizer["Unauthorized"].Value }
    });

    private string MapStatusMessage(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => _localizer["ErrorValidation"].Value,
        StatusCodes.Status401Unauthorized => _sharedLocalizer["Unauthorized"].Value,
        StatusCodes.Status403Forbidden => _localizer["ErrorForbidden"].Value,
        StatusCodes.Status404NotFound => _localizer["ErrorNotFound"].Value,
        StatusCodes.Status409Conflict => _localizer["ErrorConflict"].Value,
        StatusCodes.Status503ServiceUnavailable => _localizer["ErrorProviderUnavailable"].Value,
        StatusCodes.Status504GatewayTimeout => _localizer["ErrorProviderTimeout"].Value,
        _ => _sharedLocalizer["GatewayError"].Value
    };

    private bool TryCreateGatewayRequest(
        HttpMethod method,
        string url,
        HttpContent? content,
        out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, url);
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var canonicalTenantValues = User.Claims
            .Where(claim => claim.Type == "tenant_id")
            .Select(claim => claim.Value)
            .ToArray();
        var legacyTenantValues = User.Claims
            .Where(claim => claim.Type == "tenantId"
                || claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value)
            .ToArray();
        var supportedTenantValues = User.Claims
            .Where(claim => claim.Type == "tenantId"
                || claim.Type == "tenant_id"
                || claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value)
            .ToArray();
        var tenantValue = supportedTenantValues.FirstOrDefault();
        if (!Guid.TryParse(tenantValue, out var tenantId)
            || canonicalTenantValues.Length > 1
            || legacyTenantValues.Length > 1
            || supportedTenantValues.Any(value => Guid.TryParse(value, out var supportedTenantId)
                && supportedTenantId != tenantId)
            || canonicalTenantValues.Length == 1
                && (!Guid.TryParse(canonicalTenantValues[0], out var canonicalTenantId)
                    || canonicalTenantId != tenantId
                    || User.Claims
                        .Where(claim => claim.Type == "tenantId"
                            || claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))
                        .Select(claim => claim.Value)
                        .Any(value => !Guid.TryParse(value, out var legacyTenantId)
                            || legacyTenantId != canonicalTenantId)))
        {
            request.Dispose();
            request = null!;
            return false;
        }

        request.Headers.Add("X-Tenant-Id", tenantId.ToString("D"));
        request.Content = content;
        return true;
    }

    private string CreateFormAttemptToken()
    {
        var payload = new FormAttemptPayload(
            ResolveUserSubject(),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        return _formAttemptProtector.Protect(JsonSerializer.Serialize(payload, _jsonOptions), FormAttemptLifetime);
    }

    private bool TryReadFormAttempt(string? token, out string operationKey)
    {
        operationKey = string.Empty;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var json = _formAttemptProtector.Unprotect(token, out _);
            var payload = JsonSerializer.Deserialize<FormAttemptPayload>(json, _jsonOptions);
            if (payload is null
                || string.IsNullOrWhiteSpace(payload.OperationKey)
                || !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(payload.UserSubject),
                    System.Text.Encoding.UTF8.GetBytes(ResolveUserSubject())))
            {
                return false;
            }

            operationKey = payload.OperationKey;
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private string ResolveUserSubject() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? User.Identity?.Name
        ?? string.Empty;

    private bool HasPermission(string permission) => PermissionClaims.HasPermission(User, permission);

    private sealed record FormAttemptPayload(string UserSubject, string OperationKey);
}
