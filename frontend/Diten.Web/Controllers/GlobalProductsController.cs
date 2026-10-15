using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Buffers.Binary;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.GlobalProducts;
using Diten.Web.Security;
using Diten.Web.Views.MasterDataManagement.GlobalProducts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("MasterDataManagement/GlobalProducts")]
public sealed class GlobalProductsController : Controller
{
    private const string ReadPermission = "mdm.global-products.read";
    private const string SubmitPermission = "mdm.global-products.submit";
    private const string UpdatePermission = "mdm.global-products.update";
    private const string WithdrawPermission = "mdm.global-products.withdraw";
    private const string RequestCorrectionPermission = "mdm.global-products.request-correction";
    private const string RequestRetirementPermission = "mdm.global-products.request-retirement";
    // MOD-0290-FU05: the approved product's brand change (MDM POST {id}/brand) and the brand picker's keys.
    private const string CreatePermission = "mdm.global-products.create";
    private const string UpdateBrandPermission = "mdm.global-products.update-brand";
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly IStringLocalizer<GlobalProductsIndex> _localizer;
    private readonly ILogger<GlobalProductsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public GlobalProductsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizer<GlobalProductsIndex> localizer,
        ILogger<GlobalProductsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        // UAS-001 (WP-MDM-GP-LIST-01, the GSKU register's B17 rule): no redirect for a signed-in reader without
        // mdm.global-products.read. The status says 403 and the view draws the access-denied surface inside the shell —
        // and nothing of the list (Index.cshtml checks first).
        if (!PermissionClaims.HasPermission(User, ReadPermission))
            Response.StatusCode = StatusCodes.Status403Forbidden;
        return View("~/Views/MasterDataManagement/GlobalProducts/Index.cshtml");
    }

    // WP-MDM-GP-LIST-01: the list component's server request (start, length, search, orderBy, orderDir, lifecycleStatus)
    // becomes MDM's page query, and MDM's page becomes the component's envelope { data: { items, total, filteredTotal } }.
    // MDM returns one count (the rows matching search + filter), so total and filteredTotal are both that count.
    [HttpGet("api")]
    public Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!PermissionClaims.HasPermission(User, ReadPermission))
            return Task.FromResult(LifecycleFailure(StatusCodes.Status403Forbidden));

        return ProxyListAsync($"{_gatewayUrl}/api/global-products?{BuildListQuery(Request.Query)}", cancellationToken);
    }

    internal const int MaxListPageSize = 100;
    private const int DefaultListPageSize = 20;
    // MDM's own bound (GetGlobalProductsValidator: 1–1 000 000): the proxy never asks beyond it (FIX5 item 11).
    private const int MaxListPageNumber = 1_000_000;

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
        // Capped BEFORE the +1, so the largest start never wraps to a negative page, and never past MDM's bound.
        var pageNumber = (int)(Math.Min(start / length, MaxListPageNumber - 1L) + 1);
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
        // MOD-0290-FU05: the brand filter — only a GUID is passed on (anything else is dropped here, MDM judges the rest).
        if (Guid.TryParse(query["brandId"].ToString().Trim(), out var brandId))
            parts.Add($"brandId={brandId:D}");
        return string.Join("&", parts);
    }

    private async Task<IActionResult> ProxyListAsync(string targetUrl, CancellationToken cancellationToken)
    {
        if (!TryCreateRequest(HttpMethod.Get, targetUrl, null, out var request))
            return LifecycleFailure(StatusCodes.Status401Unauthorized);

        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    return LifecycleFailure((int)response.StatusCode);

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
                    _logger.LogWarning("Global Product list proxy: MDM answered a page without items or totalCount.");
                    return LifecycleFailure(StatusCodes.Status502BadGateway);
                }

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
            _logger.LogWarning(exception, "Global Product list proxy: MDM answered an unreadable page.");
            return LifecycleFailure(StatusCodes.Status502BadGateway);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The list URL carries the search term — it is not logged.
            _logger.LogWarning(exception, "Global Product list proxy timed out.");
            return LifecycleFailure(StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Global Product list proxy failed.");
            return LifecycleFailure(StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken) =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/global-products/{id:D}", null, cancellationToken);

    [HttpGet("api/selector")]
    public Task<IActionResult> Selector(CancellationToken cancellationToken) =>
        ProxyGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/global-products/selector{Request.QueryString}", null, cancellationToken);

    // MOD-0290-FU05 (T-1): the brands a Global Product may be linked to now — MDM's brand-options, for whoever may write a
    // Global Product (create, update or update-brand; MDM checks the same). Only search and paging are passed on.
    [HttpGet("api/brand-options")]
    public Task<IActionResult> BrandOptions(CancellationToken cancellationToken)
    {
        if (!PermissionClaims.HasPermission(User, CreatePermission)
            && !PermissionClaims.HasPermission(User, UpdatePermission)
            && !PermissionClaims.HasPermission(User, UpdateBrandPermission))
            return Task.FromResult(LifecycleFailure(StatusCodes.Status403Forbidden));

        return ProxyGatewayAsync(HttpMethod.Get,
            $"{_gatewayUrl}/api/global-products/brand-options?{BuildBrandOptionsQuery(Request.Query)}", null, cancellationToken);
    }

    internal static string BuildBrandOptionsQuery(IQueryCollection query)
    {
        var parts = new List<string>();
        foreach (var key in new[] { "search", "pageNumber", "pageSize" })
        {
            var value = query[key].ToString().Trim();
            if (value.Length > 0)
                parts.Add($"{key}={Uri.EscapeDataString(value)}");
        }
        return string.Join("&", parts);
    }

    [HttpPost("api")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [FromForm] CreateGlobalProductViewModel model,
        CancellationToken cancellationToken)
    {
        model.GlobalProductName = model.GlobalProductName?.Trim() ?? string.Empty;
        // MOD-0290-FU05: the panel always sends BrandId (empty = no brand); a malformed id is refused, never dropped.
        Guid? brandId = null;
        var onlyKnownFields = await HasOnlyFormFieldsAsync("GlobalProductName", "BrandId");
        if (onlyKnownFields && !TryReadBrandId(out brandId))
        {
            return BadRequest(new
            {
                success = false,
                errors = new[] { _localizer["ErrorValidation"].Value }
            });
        }
        if (!onlyKnownFields
            || string.IsNullOrWhiteSpace(model.GlobalProductName)
            || model.GlobalProductName.EnumerateRunes().Count() > 200)
        {
            return BadRequest(new
            {
                success = false,
                errors = new[] { _localizer["GlobalProductNameRequired"].Value }
            });
        }

        var operationKey = Guid.NewGuid().ToString("N");

        try
        {
            var reservation = await ReserveCodeAsync(model.GlobalProductName, operationKey, cancellationToken);
            if (!reservation.Success || reservation.Data is null)
                return StatusCode(reservation.StatusCode, new { success = false, errors = reservation.Errors });

            model.BrandId = brandId;
            var draft = await CreateDraftAsync(
                model.GlobalProductName,
                reservation.Data.ReservationId,
                reservation.Data.Version,
                model.BrandId,
                operationKey,
                cancellationToken);

            if (!draft.Success || draft.Data is null)
            {
                // MOD-0290-FU05: MDM's brand refusals are said in the reader's language, never as MDM's code — the same
                // rule as the lifecycle bridge (exactly one error, its code before ':').
                var errors = draft.Errors.Count == 1 && BrandRefusalKey(draft.Errors[0]) is { } brandRefusal
                    ? new[] { _localizer[brandRefusal].Value }
                    : draft.Errors;
                return StatusCode(draft.StatusCode, new { success = false, errors });
            }

            return StatusCode(draft.StatusCode, new { success = true, data = draft.Data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Global Product create proxy flow failed.");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success = false,
                errors = new[] { _sharedLocalizer["GatewayError"].Value }
            });
        }
    }

    [HttpPost("api/{id:guid}/submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitLifecycle(
        Guid id,
        [FromForm] int? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!PermissionClaims.HasPermission(User, SubmitPermission))
            return LifecycleFailure(StatusCodes.Status403Forbidden);
        if (id == Guid.Empty || expectedVersion is null or < 0
            || !await HasOnlyFormFieldsAsync("ExpectedVersion"))
            return LifecycleFailure(StatusCodes.Status400BadRequest);
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return LifecycleFailure(StatusCodes.Status401Unauthorized);

        var operationId = CreateLifecycleOperationId(
            tenantId, actor, "GlobalProduct", "submit", id, expectedVersion.Value, string.Empty);
        return await ProxyLifecycleAsync(
            HttpMethod.Post,
            $"{_gatewayUrl}/api/global-products/{id:D}/submit",
            JsonContent.Create(new { expectedVersion = expectedVersion.Value }, options: _jsonOptions),
            operationId,
            new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted },
            LifecycleMutationKind.Submit,
            id,
            cancellationToken);
    }

    [HttpPut("api/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateDraft(
        Guid id,
        [FromForm] int? expectedVersion,
        [FromForm] string? globalProductName,
        CancellationToken cancellationToken)
    {
        globalProductName = globalProductName?.Trim();
        if (!PermissionClaims.HasPermission(User, UpdatePermission))
            return LifecycleFailure(StatusCodes.Status403Forbidden);
        // MOD-0290-FU05 (T-4, CT decision): the panel always sends BrandId and MDM always gets the field — empty removes
        // the brand. "No field = unchanged" stays an API-client rule only.
        if (id == Guid.Empty || expectedVersion is null or < 0
            || string.IsNullOrWhiteSpace(globalProductName)
            || globalProductName.EnumerateRunes().Count() > 200
            || !await HasOnlyFormFieldsAsync("ExpectedVersion", "GlobalProductName", "BrandId")
            || !TryReadBrandId(out var brandId))
            return LifecycleFailure(StatusCodes.Status400BadRequest);
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return LifecycleFailure(StatusCodes.Status401Unauthorized);

        // The brand is part of the operation: MDM treats the same key with another brand as a conflict.
        var operationId = CreateLifecycleOperationId(
            tenantId, actor, "GlobalProduct", "update", id, expectedVersion.Value,
            globalProductName + '\u001f' + (brandId?.ToString("D") ?? "none"));
        return await ProxyLifecycleAsync(
            HttpMethod.Put,
            $"{_gatewayUrl}/api/global-products/{id:D}",
            JsonContent.Create(new { expectedVersion = expectedVersion.Value, globalProductName, brandId }, options: _jsonOptions),
            operationId,
            new HashSet<int> { StatusCodes.Status200OK },
            LifecycleMutationKind.Update,
            id,
            cancellationToken);
    }

    // MOD-0290-FU05 slice 2 (owner Karar a): an approved product's brand — MDM POST {id}/brand under update-brand. The
    // panel always sends BrandId (empty = remove the brand).
    [HttpPost("api/{id:guid}/brand")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeBrand(
        Guid id,
        [FromForm] int? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!PermissionClaims.HasPermission(User, UpdateBrandPermission))
            return LifecycleFailure(StatusCodes.Status403Forbidden);
        if (id == Guid.Empty || expectedVersion is null or < 0
            || !await HasOnlyFormFieldsAsync("ExpectedVersion", "BrandId")
            || !TryReadBrandId(out var brandId))
            return LifecycleFailure(StatusCodes.Status400BadRequest);
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return LifecycleFailure(StatusCodes.Status401Unauthorized);

        var operationId = CreateLifecycleOperationId(
            tenantId, actor, "GlobalProduct", "change-brand", id, expectedVersion.Value, brandId?.ToString("D") ?? "none");
        return await ProxyLifecycleAsync(
            HttpMethod.Post,
            $"{_gatewayUrl}/api/global-products/{id:D}/brand",
            JsonContent.Create(new { expectedVersion = expectedVersion.Value, brandId }, options: _jsonOptions),
            operationId,
            new HashSet<int> { StatusCodes.Status200OK },
            LifecycleMutationKind.ChangeBrand,
            id,
            cancellationToken);
    }

    [HttpPost("api/{id:guid}/withdraw")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WithdrawApproval(
        Guid id,
        [FromForm] int? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!PermissionClaims.HasPermission(User, WithdrawPermission))
            return LifecycleFailure(StatusCodes.Status403Forbidden);
        if (id == Guid.Empty || expectedVersion is null or < 0
            || !await HasOnlyFormFieldsAsync("ExpectedVersion"))
            return LifecycleFailure(StatusCodes.Status400BadRequest);
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return LifecycleFailure(StatusCodes.Status401Unauthorized);

        var operationId = CreateLifecycleOperationId(
            tenantId, actor, "GlobalProduct", "withdraw-approval", id, expectedVersion.Value, string.Empty);
        return await ProxyLifecycleAsync(
            HttpMethod.Post,
            $"{_gatewayUrl}/api/global-products/{id:D}/withdraw",
            JsonContent.Create(new { expectedVersion = expectedVersion.Value }, options: _jsonOptions),
            operationId,
            new HashSet<int> { StatusCodes.Status200OK },
            LifecycleMutationKind.Withdraw,
            id,
            cancellationToken);
    }

    [HttpPost("api/{id:guid}/correction-requests")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestCorrection(
        Guid id,
        [FromForm] int? expectedVersion,
        [FromForm] string? globalProductName,
        CancellationToken cancellationToken)
    {
        globalProductName = globalProductName?.Trim();
        if (!PermissionClaims.HasPermission(User, RequestCorrectionPermission))
            return LifecycleFailure(StatusCodes.Status403Forbidden);
        if (id == Guid.Empty || expectedVersion is null or < 0
            || string.IsNullOrWhiteSpace(globalProductName)
            || globalProductName.EnumerateRunes().Count() > 200
            || !await HasOnlyFormFieldsAsync("ExpectedVersion", "GlobalProductName"))
            return LifecycleFailure(StatusCodes.Status400BadRequest);
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return LifecycleFailure(StatusCodes.Status401Unauthorized);

        var operationId = CreateLifecycleOperationId(
            tenantId, actor, "GlobalProduct", "request-correction", id, expectedVersion.Value, globalProductName);
        return await ProxyLifecycleAsync(
            HttpMethod.Post,
            $"{_gatewayUrl}/api/global-products/{id:D}/correction-requests",
            JsonContent.Create(new { expectedVersion = expectedVersion.Value, globalProductName }, options: _jsonOptions),
            operationId,
            new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted },
            LifecycleMutationKind.Correction,
            id,
            cancellationToken);
    }

    [HttpPost("api/{id:guid}/retirement-requests")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestRetirement(
        Guid id,
        [FromForm] int? expectedVersion,
        [FromForm] string? reason,
        CancellationToken cancellationToken)
    {
        reason = reason?.Trim();
        if (!PermissionClaims.HasPermission(User, RequestRetirementPermission))
            return LifecycleFailure(StatusCodes.Status403Forbidden);
        if (id == Guid.Empty || expectedVersion is null or < 0
            || string.IsNullOrWhiteSpace(reason)
            || reason.EnumerateRunes().Count() > 2000
            || reason.Any(char.IsControl)
            || !await HasOnlyFormFieldsAsync("ExpectedVersion", "Reason"))
            return LifecycleFailure(StatusCodes.Status400BadRequest);
        if (!TryResolveLifecycleIdentity(out var tenantId, out var actor))
            return LifecycleFailure(StatusCodes.Status401Unauthorized);

        var operationId = CreateLifecycleOperationId(
            tenantId, actor, "GlobalProduct", "request-retirement", id, expectedVersion.Value, reason);
        return await ProxyLifecycleAsync(
            HttpMethod.Post,
            $"{_gatewayUrl}/api/global-products/{id:D}/retirement-requests",
            JsonContent.Create(new { expectedVersion = expectedVersion.Value, reason }, options: _jsonOptions),
            operationId,
            new HashSet<int> { StatusCodes.Status200OK, StatusCodes.Status202Accepted },
            LifecycleMutationKind.Retirement,
            id,
            cancellationToken);
    }

    private async Task<IActionResult> ProxyLifecycleAsync(
        HttpMethod method,
        string targetUrl,
        HttpContent content,
        Guid operationId,
        IReadOnlySet<int> allowedSuccessStatusCodes,
        LifecycleMutationKind mutationKind,
        Guid aggregateId,
        CancellationToken cancellationToken)
    {
        var serializedContent = await content.ReadAsStringAsync(cancellationToken);
        content.Dispose();
        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var replayableContent = new StringContent(
                    serializedContent, Encoding.UTF8, "application/json");
                if (!TryCreateRequest(method, targetUrl, replayableContent, out var request))
                    return LifecycleFailure(StatusCodes.Status401Unauthorized);
                request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"));
                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, cancellationToken);
                }
                catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
                {
                    request.Dispose();
                    _logger.LogWarning(exception, "Global Product lifecycle proxy timed out.");
                    if (attempt == 0)
                        continue;
                    return LifecycleFailure(StatusCodes.Status504GatewayTimeout);
                }
                catch (HttpRequestException exception)
                {
                    request.Dispose();
                    _logger.LogError(exception, "Global Product lifecycle proxy failed.");
                    if (attempt == 0)
                        continue;
                    return LifecycleFailure(StatusCodes.Status503ServiceUnavailable);
                }
                using (request)
                using (response)
                {
                var responseStatus = (int)response.StatusCode;
                if (!response.IsSuccessStatusCode)
                {
                    // A 502 can be emitted after the downstream mutation committed but its response
                    // was lost. One exact idempotent replay is the only authoritative reconciliation.
                    if (responseStatus == StatusCodes.Status502BadGateway && attempt == 0)
                        continue;
                    // FIX2 item 8: an earlier withdrawal of this approval, with another reason or comment, is still
                    // under way. This request's text cannot finish it (MDM keeps what the first request said, and the
                    // FIX1 repeat with the other command was refused for exactly that) — the requester is told so.
                    if (responseStatus == StatusCodes.Status409Conflict
                        && mutationKind == LifecycleMutationKind.Withdraw
                        && await PendingWithdrawalCommandAsync(response, cancellationToken) is not null)
                    {
                        return StatusCode(StatusCodes.Status409Conflict, new
                        {
                            success = false,
                            errors = new[] { _localizer["ErrorWithdrawalPending"].Value }
                        });
                    }
                    // MOD-0290-FU05: MDM's brand refusals ("brand_not_found: …" 404, "brand_archived: …" /
                    // "brand_not_linkable: …" 409) are said in the reader's language, never as MDM's code or message.
                    if (responseStatus is StatusCodes.Status404NotFound or StatusCodes.Status409Conflict
                        && BrandRefusalKey(await SingleErrorCodeAsync(response, cancellationToken)) is { } brandRefusal)
                    {
                        return StatusCode(responseStatus, new
                        {
                            success = false,
                            errors = new[] { _localizer[brandRefusal].Value }
                        });
                    }
                    // FIX3 item 6: the in-flight rule's two refusals are said in words (the draft is being submitted;
                    // the draft changed before the submit could be reserved).
                    if (responseStatus == StatusCodes.Status409Conflict
                        && await SingleErrorCodeAsync(response, cancellationToken) is { } code
                        && InFlightRefusalKeys.TryGetValue(code, out var refusalKey))
                    {
                        return StatusCode(StatusCodes.Status409Conflict, new
                        {
                            success = false,
                            errors = new[] { _localizer[refusalKey].Value }
                        });
                    }
                    return LifecycleFailure((int)response.StatusCode);
                }

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (!allowedSuccessStatusCodes.Contains(responseStatus)
                    || !IsJsonMediaType(mediaType))
                {
                    if (attempt == 0)
                        continue;
                    return LifecycleFailure(StatusCodes.Status502BadGateway);
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                LifecycleGatewayEnvelope? envelope;
                try
                {
                    envelope = JsonSerializer.Deserialize<LifecycleGatewayEnvelope>(body, _jsonOptions);
                }
                catch (JsonException exception)
                {
                    _logger.LogWarning(exception, "Global Product lifecycle proxy received malformed JSON.");
                    if (attempt == 0)
                        continue;
                    return LifecycleFailure(StatusCodes.Status502BadGateway);
                }

                if (!IsValidLifecycleEnvelope(envelope, responseStatus, mutationKind, aggregateId))
                {
                    if (attempt == 0)
                        continue;
                    return LifecycleFailure(StatusCodes.Status502BadGateway);
                }

                return new ContentResult
                {
                    StatusCode = responseStatus,
                    ContentType = "application/json",
                    Content = body
                };
                }
            }
            return LifecycleFailure(StatusCodes.Status502BadGateway);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Global Product lifecycle proxy timed out.");
            return LifecycleFailure(StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Global Product lifecycle proxy failed.");
            return LifecycleFailure(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static readonly IReadOnlyDictionary<string, string> InFlightRefusalKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GLOBAL_PRODUCT_IDENTITY_SUBMIT_IN_FLIGHT"] = "ErrorSubmitInFlight",
            ["PRODUCT_IDENTITY_WORKFLOW_DRAFT_CHANGED"] = "ErrorDraftChanged",
            // BL-552 flow 3 FIX1 item 13 (CT): a submit STUCK in manual reconciliation holds the draft and is not "on
            // its way" — its own sentence names the way out (the same person submits again; else an administrator).
            ["GLOBAL_PRODUCT_IDENTITY_RECONCILIATION_REQUIRED"] = "ErrorSubmitStuck",
            // A submit whose start the engine could not yet confirm IS on its way.
            ["PRODUCT_IDENTITY_WORKFLOW_START_LOOKUP_REFUSED"] = "ErrorSubmitInFlight"
        };

    // MOD-0290-FU05: MDM's brand refusal codes (BrandProductReasonCodes) → this screen's sentences. MDM writes the first
    // error as "<code>: <English message>"; only the code before ':' is read.
    private static readonly IReadOnlyDictionary<string, string> BrandRefusalKeys =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["brand_not_found"] = "ErrorBrandNotFound",
            ["brand_archived"] = "ErrorBrandArchived",
            ["brand_not_linkable"] = "ErrorBrandNotLinkable"
        };

    private static string? BrandRefusalKey(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return null;
        var separator = error.IndexOf(':');
        var code = (separator < 0 ? error : error[..separator]).Trim();
        return BrandRefusalKeys.TryGetValue(code, out var key) ? key : null;
    }

    // MOD-0290-FU05: the form's BrandId — empty = no brand; anything else must be a non-empty GUID ("D").
    private bool TryReadBrandId(out Guid? brandId)
    {
        brandId = null;
        var raw = Request.Form["BrandId"].ToString().Trim();
        if (raw.Length == 0)
            return true;
        if (!Guid.TryParseExact(raw, "D", out var parsed) || parsed == Guid.Empty)
            return false;
        brandId = parsed;
        return true;
    }

    private static async Task<string?> SingleErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!IsJsonMediaType(response.Content.Headers.ContentType?.MediaType))
            return null;
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("errors", out var errors)
                   && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() == 1
                   && errors[0].ValueKind == JsonValueKind.String
                ? errors[0].GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<Guid?> PendingWithdrawalCommandAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!IsJsonMediaType(response.Content.Headers.ContentType?.MediaType))
            return null;
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("errors", out var errors)
                   && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() == 1
                   && errors[0].ValueKind == JsonValueKind.String
                   && errors[0].GetString() == "PRODUCT_IDENTITY_WITHDRAWAL_OPERATION_CONFLICT"
                   && document.RootElement.TryGetProperty("data", out var data)
                   && data.ValueKind == JsonValueKind.Object
                   && data.TryGetProperty("pendingWithdrawalCommandId", out var pending)
                   && pending.ValueKind == JsonValueKind.String
                   && Guid.TryParse(pending.GetString(), out var command) && command != Guid.Empty
                ? command
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private IActionResult LifecycleFailure(int statusCode)
    {
        var normalizedStatus = statusCode is 400 or 401 or 403 or 404 or 409 or 503 or 504
            ? statusCode
            : StatusCodes.Status502BadGateway;
        return StatusCode(normalizedStatus, new
        {
            success = false,
            errors = new[] { MapLifecycleStatusMessage(normalizedStatus) }
        });
    }

    private string MapLifecycleStatusMessage(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => _localizer["ErrorValidation"].Value,
        StatusCodes.Status401Unauthorized => _localizer["ErrorUnauthorized"].Value,
        StatusCodes.Status403Forbidden => _localizer["ErrorForbidden"].Value,
        StatusCodes.Status404NotFound => _localizer["ErrorNotFound"].Value,
        StatusCodes.Status409Conflict => _localizer["ErrorConflict"].Value,
        StatusCodes.Status503ServiceUnavailable => _localizer["ErrorServiceUnavailable"].Value,
        StatusCodes.Status504GatewayTimeout => _localizer["ErrorTimeout"].Value,
        _ => _localizer["ErrorGateway"].Value
    };

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

        actor = subjectId.ToString("D");
        return TryResolveCanonicalTenant(User, out tenantId);
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

    private static bool TryResolveCanonicalTenant(ClaimsPrincipal principal, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var values = principal.Claims
            .Where(claim => claim.Type == "tenantId" || claim.Type == "tenant_id"
                || claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value)
            .ToArray();
        return values.Length == 1
            && Guid.TryParseExact(values[0], "D", out tenantId)
            && tenantId != Guid.Empty;
    }

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
        AppendLengthPrefixed(hash, expectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
        public JsonElement Data { get; init; }
    }

    private enum LifecycleMutationKind
    {
        Submit,
        Update,
        Withdraw,
        Correction,
        Retirement,
        ChangeBrand
    }

    private static bool IsValidLifecycleEnvelope(
        LifecycleGatewayEnvelope? envelope,
        int responseStatus,
        LifecycleMutationKind mutationKind,
        Guid aggregateId)
    {
        if (envelope?.IsSuccessful != true || envelope.StatusCode != responseStatus
            || envelope.Data.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var data = envelope.Data;
        return mutationKind switch
        {
            LifecycleMutationKind.Submit =>
                HasGuid(data, "operationId") && HasMatchingGuid(data, "globalProductId", aggregateId)
                && HasNonEmptyString(data, "checkpoint"),
            LifecycleMutationKind.Update =>
                HasMatchingGuid(data, "id", aggregateId) && HasLifecycleState(data, "lifecycleStatus")
                && HasNonNegativeInt(data, "version"),
            LifecycleMutationKind.Withdraw =>
                HasMatchingGuid(data, "globalProductId", aggregateId) && HasLifecycleState(data, "lifecycleStatus")
                && HasNonNegativeInt(data, "version"),
            // MOD-0290-FU05: GlobalProductBrandChangeDto (id, lifecycleStatus, version; changed / isReplay / brand fields).
            LifecycleMutationKind.ChangeBrand =>
                HasMatchingGuid(data, "id", aggregateId) && HasLifecycleState(data, "lifecycleStatus")
                && HasNonNegativeInt(data, "version"),
            LifecycleMutationKind.Correction or LifecycleMutationKind.Retirement =>
                HasGuid(data, "operationId") && HasMatchingGuid(data, "globalProductId", aggregateId)
                && HasCheckpointForStatus(data, responseStatus) && HasNonNegativeInt(data, "productVersion"),
            _ => false
        };
    }

    private static bool TryGetProperty(JsonElement element, string camelName, out JsonElement value)
    {
        if (element.TryGetProperty(camelName, out value))
            return true;

        var pascalName = char.ToUpperInvariant(camelName[0]) + camelName[1..];
        return element.TryGetProperty(pascalName, out value);
    }

    private static bool HasGuid(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value)
        && value.ValueKind == JsonValueKind.String
        && Guid.TryParseExact(value.GetString(), "D", out var parsed)
        && parsed != Guid.Empty;

    private static bool HasMatchingGuid(JsonElement element, string name, Guid expected) =>
        TryGetProperty(element, name, out var value)
        && value.ValueKind == JsonValueKind.String
        && Guid.TryParseExact(value.GetString(), "D", out var parsed)
        && parsed == expected;

    private static bool HasNonEmptyString(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString());

    private static bool HasCheckpointForStatus(JsonElement element, int responseStatus)
    {
        if (!TryGetProperty(element, "checkpoint", out var value)
            || value.ValueKind != JsonValueKind.String)
            return false;

        var checkpoint = value.GetString();
        return responseStatus switch
        {
            StatusCodes.Status200OK => string.Equals(
                checkpoint, "Completed", StringComparison.Ordinal),
            StatusCodes.Status202Accepted => string.Equals(
                checkpoint, "AwaitingDecision", StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool HasNonNegativeInt(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var number)
        && number >= 0;

    private static bool HasLifecycleState(JsonElement element, string name) =>
        TryGetProperty(element, name, out var value)
        && (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            || value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number > 0);

    private async Task<GatewayCallResult<CodeReservationViewModel>> ReserveCodeAsync(
        string globalProductName,
        string operationKey,
        CancellationToken cancellationToken)
    {
        var payload = new { globalProductName, idempotencyKey = operationKey + ":reserve" };
        return await SendGatewayAsync<CodeReservationViewModel>(
            HttpMethod.Post,
            $"{_gatewayUrl}/api/global-products/code-reservations",
            JsonContent.Create(payload, options: _jsonOptions),
            cancellationToken);
    }

    private async Task<GatewayCallResult<GlobalProductDraftViewModel>> CreateDraftAsync(
        string globalProductName,
        Guid reservationId,
        int expectedReservationVersion,
        Guid? brandId,
        string operationKey,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            globalProductName,
            reservationId,
            expectedReservationVersion,
            idempotencyKey = operationKey + ":draft",
            brandId
        };

        return await SendGatewayAsync<GlobalProductDraftViewModel>(
            HttpMethod.Post,
            $"{_gatewayUrl}/api/global-products/drafts",
            JsonContent.Create(payload, options: _jsonOptions),
            cancellationToken);
    }

    private async Task<IActionResult> ProxyGatewayAsync(
        HttpMethod method,
        string targetUrl,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        if (!TryCreateRequest(method, targetUrl, content, out var request))
            return Unauthorized(new { errors = new[] { _sharedLocalizer["Unauthorized"].Value } });

        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    Content = body
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Global Product gateway proxy failed for {TargetUrl}.", targetUrl);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                errors = new[] { _sharedLocalizer["GatewayError"].Value }
            });
        }
    }

    private async Task<GatewayCallResult<T>> SendGatewayAsync<T>(
        HttpMethod method,
        string targetUrl,
        HttpContent content,
        CancellationToken cancellationToken)
        where T : class
    {
        if (!TryCreateRequest(method, targetUrl, content, out var request))
        {
            return GatewayCallResult<T>.Failure(
                StatusCodes.Status401Unauthorized,
                [_sharedLocalizer["Unauthorized"].Value]);
        }

        using (request)
        using (var response = await _httpClient.SendAsync(request, cancellationToken))
        {
            GatewayResponse<T>? payload = null;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<GatewayResponse<T>>(_jsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                // The raw response is converted into a bounded gateway error below.
            }

            if (response.IsSuccessStatusCode && payload?.Data is not null)
                return GatewayCallResult<T>.Succeeded((int)response.StatusCode, payload.Data);

            var errors = payload?.Errors.Where(error => !string.IsNullOrWhiteSpace(error)).ToList() ?? [];
            if (errors.Count == 0)
                errors.Add(MapStatusMessage(response.StatusCode));

            return GatewayCallResult<T>.Failure((int)response.StatusCode, errors);
        }
    }

    private string MapStatusMessage(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => _sharedLocalizer["Unauthorized"].Value,
        HttpStatusCode.Forbidden => _localizer["ErrorForbidden"].Value,
        HttpStatusCode.NotFound => _localizer["ErrorNotFound"].Value,
        HttpStatusCode.Conflict => _localizer["ErrorConflict"].Value,
        _ => _sharedLocalizer["GatewayError"].Value
    };

    private bool TryCreateRequest(HttpMethod method, string url, HttpContent? content, out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, url);

        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (!TryResolveCanonicalTenant(User, out var tenantId))
        {
            request.Dispose();
            request = null!;
            return false;
        }

        request.Headers.Add("X-Tenant-Id", tenantId.ToString("D"));
        request.Content = content;
        return true;
    }

    private sealed record GatewayCallResult<T>(bool Success, int StatusCode, T? Data, IReadOnlyList<string> Errors)
        where T : class
    {
        public static GatewayCallResult<T> Succeeded(int statusCode, T data) => new(true, statusCode, data, []);
        public static GatewayCallResult<T> Failure(int statusCode, IReadOnlyList<string> errors) => new(false, statusCode, default, errors);
    }
}
