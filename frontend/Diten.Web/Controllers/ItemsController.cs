using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.Web.Models.Items;
using Diten.Web.Security;
using Diten.Web.Views.MasterDataManagement.Items;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0290-FU04 S5 — the Items ("Kalemler") screen: four pages (list, create, edit, details) and the same-origin JSON
/// proxy to MDM's <c>/api/mdm/items</c> through the gateway. The browser never holds a token or a tenant: the proxy
/// forwards the access-token cookie as Bearer and the tenant from the signed-in principal (the GSKU proxy's pattern).
///
/// <para><b>S5b (BL-600) — the lists to choose from</b>: <c>api/options</c> passes on MDM's item options (material
/// types, units, storage conditions, from Platform's enumeration). The form offers only those as selects; a list that
/// cannot be read is a refusal by name (503 / 504 / 401 with its sentence) — never an empty list, and the form never
/// falls back to typed codes. The list is not the proof of a choice: MDM still resolves every chosen code on the write
/// (ITEM_REFERENCE_VALUE_INVALID → a sentence naming the value).</para>
/// </summary>
[Authorize]
[Route("MasterDataManagement/Items")]
public sealed class ItemsController : Controller
{
    public const string ReadPermission = "mdm.items.read";
    public const string CreatePermission = "mdm.items.create";
    public const string UpdatePermission = "mdm.items.update";
    public const string SubmitPermission = "mdm.items.submit";
    public const string DeactivatePermission = "mdm.items.deactivate";
    public const string RetirePermission = "mdm.items.retire";

    /// <summary>
    /// S5b: whoever may open a form that offers the item lists — the item form (create / update) and the GSKU / LSKU /
    /// Finished Good stock forms (MDM's own any-of on <c>GET /api/mdm/items/options</c>). A read-only key is not one.
    /// </summary>
    public static readonly IReadOnlyList<string> OptionsPermissions =
    [
        CreatePermission, UpdatePermission, "mdm.gskus.update", "mdm.lskus.create", "mdm.finished-goods.create"
    ];

    public const string IndexView = "~/Views/MasterDataManagement/Items/Index.cshtml";
    public const string CreateView = "~/Views/MasterDataManagement/Items/Create.cshtml";
    public const string EditView = "~/Views/MasterDataManagement/Items/Edit.cshtml";
    public const string DetailsView = "~/Views/MasterDataManagement/Items/Details.cshtml";

    private static readonly TimeSpan FormAttemptLifetime = TimeSpan.FromMinutes(30);

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ITimeLimitedDataProtector _formAttemptProtector;
    private readonly IStringLocalizer<ItemsIndex> _localizer;
    private readonly ILogger<ItemsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public ItemsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<ItemsIndex> localizer,
        ILogger<ItemsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _formAttemptProtector = dataProtectionProvider
            .CreateProtector("Diten.Web", "MOD-0290-FU04", "ItemFormAttempt", "v1")
            .ToTimeLimitedDataProtector();
        _localizer = localizer;
        _logger = logger;
    }

    // ── Pages ─────────────────────────────────────────────────────────────────────────────────────────────────────
    // UAS-001: a signed-in reader without the page's key gets 403 and the page's own view, which draws the
    // access-denied surface inside the shell and nothing else (each view checks the same key first). No redirect.

    [HttpGet("")]
    public IActionResult Index()
    {
        if (!HasPermission(ReadPermission))
            return Denied(IndexView, null);

        ViewData["CanCreateItem"] = HasPermission(CreatePermission);
        return View(IndexView);
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        if (!HasPermission(CreatePermission))
            return Denied(CreateView, new ItemFormViewModel());

        ViewData["ItemFormAttemptToken"] = CreateFormAttemptToken();
        return View(CreateView, new ItemFormViewModel());
    }

    // S5-FIX1: the edit form reads the item first (api/{id} asks the read key), so the page asks BOTH keys — an update
    // key without the read key would draw a form that can never be filled.
    [HttpGet("Edit/{id:guid}")]
    public IActionResult Edit(Guid id)
    {
        if (!HasPermission(UpdatePermission) || !HasPermission(ReadPermission))
            return Denied(EditView, new ItemFormViewModel { Id = id });

        return View(EditView, new ItemFormViewModel { Id = id });
    }

    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id)
    {
        if (!HasPermission(ReadPermission))
            return Denied(DetailsView, new ItemFormViewModel { Id = id });

        return View(DetailsView, new ItemFormViewModel { Id = id });
    }

    private IActionResult Denied(string view, ItemFormViewModel? model)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return model is null ? View(view) : View(view, model);
    }

    // ── List ──────────────────────────────────────────────────────────────────────────────────────────────────────
    // The list component's server request (start, length, search, materialType, status) becomes MDM's page query
    // (page, pageSize, q, materialType, status), and MDM's page { items, total } becomes the component's envelope
    // { items, total, filteredTotal }. MDM sorts by code only (no orderBy); its one count is the filtered count.

    [HttpGet("api")]
    public Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!HasPermission(ReadPermission))
            return Task.FromResult(ForbiddenResult());

        return ProxyListAsync($"{_gatewayUrl}/api/mdm/items?{BuildListQuery(Request.Query)}", cancellationToken);
    }

    public const int MaxListPageSize = 100;
    private const int DefaultListPageSize = 20;
    // MDM's own bound (GetItemListHandler.MaximumPage): never beyond it, never wrapped to a negative page.
    public const int MaxListPageNumber = 1_000_000;

    public static string BuildListQuery(IQueryCollection query)
    {
        var length = int.TryParse(query["length"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLength)
            && parsedLength > 0
                ? Math.Min(parsedLength, MaxListPageSize)
                : DefaultListPageSize;
        var start = long.TryParse(query["start"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedStart)
            && parsedStart > 0
                ? parsedStart
                : 0;
        var page = (int)(Math.Min(start / length, MaxListPageNumber - 1L) + 1);
        return BuildPageQuery(query, page, length);
    }

    private static string BuildPageQuery(IQueryCollection query, int page, int pageSize)
    {
        var parts = new List<string>
        {
            $"page={page.ToString(CultureInfo.InvariantCulture)}",
            $"pageSize={pageSize.ToString(CultureInfo.InvariantCulture)}"
        };
        // Passed as typed; MDM is the one judge (an unknown material type or status, or a search over 200, is 400).
        foreach (var (from, to) in new[] { ("search", "q"), ("materialType", "materialType"), ("status", "status") })
        {
            var value = query[from].ToString().Trim();
            if (value.Length > 0)
                parts.Add($"{to}={Uri.EscapeDataString(value)}");
        }
        return string.Join("&", parts);
    }

    private async Task<IActionResult> ProxyListAsync(string targetUrl, CancellationToken cancellationToken)
    {
        try
        {
            var page = await ReadPageAsync(targetUrl, cancellationToken);
            if (page.Failure is not null)
                return page.Failure;

            return new ContentResult
            {
                StatusCode = StatusCodes.Status200OK,
                ContentType = "application/json",
                Content = JsonSerializer.Serialize(new
                {
                    success = true,
                    data = new { items = page.Items, total = page.Total, filteredTotal = page.Total }
                }, _jsonOptions)
            };
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The list URL carries the search term — it is not logged.
            _logger.LogWarning(exception, "Item list proxy timed out.");
            return SafeFailure(HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Item list proxy failed.");
            return SafeFailure(HttpStatusCode.ServiceUnavailable);
        }
    }

    private sealed record ItemPage(IActionResult? Failure, List<JsonElement> Items, long Total);

    private async Task<ItemPage> ReadPageAsync(string targetUrl, CancellationToken cancellationToken)
    {
        if (!TryCreateGatewayRequest(HttpMethod.Get, targetUrl, content: null, out var request))
            return new ItemPage(UnauthorizedResult(), [], 0);

        using (request)
        using (var response = await _httpClient.SendAsync(request, cancellationToken))
        {
            if (!response.IsSuccessStatusCode)
                return new ItemPage(await KnownOrStatusFailureAsync(response, cancellationToken), [], 0);

            try
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object
                    && (root.TryGetProperty("data", out var data) || root.TryGetProperty("Data", out data))
                    && data.ValueKind == JsonValueKind.Object
                    && (data.TryGetProperty("items", out var items) || data.TryGetProperty("Items", out items))
                    && items.ValueKind == JsonValueKind.Array
                    && (data.TryGetProperty("total", out var count) || data.TryGetProperty("Total", out count))
                    && count.ValueKind == JsonValueKind.Number
                    && count.TryGetInt64(out var total)
                    && total >= 0)
                {
                    return new ItemPage(null, items.EnumerateArray().Select(item => item.Clone()).ToList(), total);
                }
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Item list proxy: MDM answered an unreadable page.");
                return new ItemPage(SafeFailure(HttpStatusCode.BadGateway), [], 0);
            }

            // A page without its count is a broken contract, never an empty list.
            _logger.LogWarning("Item list proxy: MDM answered a page without items or total.");
            return new ItemPage(SafeFailure(HttpStatusCode.BadGateway), [], 0);
        }
    }

    // ── Export (BL-452): every matching row, never the page DataTables holds ──────────────────────────────────────
    // MDM has no export of its own: the proxy reads MDM's pages (100 at a time, the same search and filters) and writes
    // one CSV with the visible columns in the reader's language. Excel is answered with the same CSV (Excel opens it);
    // no spreadsheet library is part of this application.

    public const int MaxExportRows = 50_000;

    public static readonly IReadOnlyList<string> ExportColumns =
        ["canonicalCode", "name", "materialType", "baseUomCode", "stockable", "lifecycleStatus"];

    [HttpGet("api/export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        if (!HasPermission(ReadPermission))
            return ForbiddenResult();

        var requested = Request.Query["columns"].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(ExportColumns.Contains).Distinct(StringComparer.Ordinal).ToList();
        var columns = requested.Count > 0 ? requested : ExportColumns.ToList();

        var rows = new List<JsonElement>();
        try
        {
            for (var page = 1; ; page++)
            {
                var read = await ReadPageAsync(
                    $"{_gatewayUrl}/api/mdm/items?{BuildPageQuery(Request.Query, page, MaxListPageSize)}", cancellationToken);
                if (read.Failure is not null)
                    return read.Failure;
                if (read.Total > MaxExportRows)
                {
                    return StatusCode(StatusCodes.Status413PayloadTooLarge,
                        new { success = false, errors = new[] { _localizer["ErrorExportTooLarge"].Value } });
                }
                rows.AddRange(read.Items);
                if (read.Items.Count == 0 || rows.Count >= read.Total)
                    break;
            }
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Item export proxy timed out.");
            return SafeFailure(HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Item export proxy failed.");
            return SafeFailure(HttpStatusCode.ServiceUnavailable);
        }

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", columns.Select(column => CsvCell(_localizer[ExportHeaderKey(column)].Value))));
        foreach (var row in rows)
            csv.AppendLine(string.Join(",", columns.Select(column => CsvCell(ExportValue(row, column)))));

        var fileName = $"items-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv; charset=utf-8");
    }

    private static string ExportHeaderKey(string column) => column switch
    {
        "canonicalCode" => "CanonicalCode",
        "name" => "Name",
        "materialType" => "MaterialType",
        "baseUomCode" => "BaseUomCode",
        "stockable" => "Stockable",
        _ => "LifecycleStatus"
    };

    private string ExportValue(JsonElement row, string column)
    {
        if (!(row.TryGetProperty(column, out var value)
              || row.TryGetProperty(char.ToUpperInvariant(column[0]) + column[1..], out value)))
            return string.Empty;
        return column switch
        {
            "materialType" when value.ValueKind == JsonValueKind.String
                && ItemRefusalSentences.MaterialTypes.Contains(value.GetString()!)
                => _localizer[ItemRefusalSentences.MaterialTypeKey(value.GetString()!)].Value,
            "stockable" when value.ValueKind is JsonValueKind.True or JsonValueKind.False
                => _localizer[value.GetBoolean() ? "Yes" : "No"].Value,
            "lifecycleStatus" when value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var status)
                && ItemRefusalSentences.LifecycleStatuses.TryGetValue(status, out var name)
                => _localizer[ItemRefusalSentences.LifecycleKey(name)].Value,
            _ => value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString()
        };
    }

    // RFC 4180 quoting, and no cell a spreadsheet would run as a formula (CSV injection).
    public static string CsvCell(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    // ── Options (S5b, BL-600): the lists the forms choose from ─────────────────────────────────────────────────────
    // MDM's { catalogVersion, materialTypes, uoms, storageConditions } is passed on as codes, order and attributes only
    // (the English displayName is not a screen label: the screen labels by stable code). A list that is missing, empty or
    // not code-shaped is not a list to offer (502); a refusal keeps its status with its sentence (OptionsKeys).

    private static readonly Regex OptionCode = new("^[A-Z][A-Z0-9_]{0,63}$", RegexOptions.CultureInvariant);
    private static readonly string[] OptionLists = ["materialTypes", "uoms", "storageConditions"];

    [HttpGet("api/options")]
    public async Task<IActionResult> Options(CancellationToken cancellationToken)
    {
        if (!OptionsPermissions.Any(HasPermission))
            return ForbiddenResult();

        if (!TryCreateGatewayRequest(HttpMethod.Get, $"{_gatewayUrl}/api/mdm/items/options", content: null, out var request))
            return UnauthorizedResult();

        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    return await KnownOrStatusFailureAsync(response, cancellationToken, options: true);

                var lists = ReadOptions(await response.Content.ReadAsStringAsync(cancellationToken), out var catalogVersion);
                if (lists is null)
                {
                    _logger.LogWarning("Item options proxy: MDM answered lists that cannot be offered.");
                    return SafeFailure(HttpStatusCode.BadGateway);
                }

                return new ContentResult
                {
                    StatusCode = StatusCodes.Status200OK,
                    ContentType = "application/json",
                    Content = JsonSerializer.Serialize(new
                    {
                        success = true,
                        data = new
                        {
                            catalogVersion,
                            materialTypes = lists["materialTypes"],
                            uoms = lists["uoms"],
                            storageConditions = lists["storageConditions"]
                        }
                    }, _jsonOptions)
                };
            }
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Item options proxy timed out.");
            return OptionsRefusal(StatusCodes.Status504GatewayTimeout, "RefusalOptionsTimeout");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Item options proxy failed.");
            return OptionsRefusal(StatusCodes.Status503ServiceUnavailable, "RefusalOptionsUnavailable");
        }
    }

    private IActionResult OptionsRefusal(int status, string key) =>
        StatusCode(status, new { success = false, errors = new[] { _localizer[key].Value } });

    private sealed record OptionView(string Code, int SortOrder, Dictionary<string, string?> Attributes);

    // MDM's envelope → the three lists (each non-empty, every code code-shaped and once), or null.
    private static Dictionary<string, List<OptionView>>? ReadOptions(string body, out string? catalogVersion)
    {
        catalogVersion = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !(root.TryGetProperty("isSuccessful", out var successful) || root.TryGetProperty("IsSuccessful", out successful))
                || successful.ValueKind != JsonValueKind.True
                || !(root.TryGetProperty("data", out var data) || root.TryGetProperty("Data", out data))
                || data.ValueKind != JsonValueKind.Object)
                return null;

            if (Property(data, "catalogVersion") is { ValueKind: JsonValueKind.String } version)
                catalogVersion = version.GetString();
            var lists = new Dictionary<string, List<OptionView>>(StringComparer.Ordinal);
            foreach (var name in OptionLists)
            {
                if (Property(data, name) is not { ValueKind: JsonValueKind.Array } array || array.GetArrayLength() == 0)
                    return null;
                var options = new List<OptionView>();
                foreach (var option in array.EnumerateArray())
                {
                    if (option.ValueKind != JsonValueKind.Object
                        || Property(option, "code") is not { ValueKind: JsonValueKind.String } code
                        || !OptionCode.IsMatch(code.GetString()!)
                        || options.Any(seen => seen.Code == code.GetString()))
                        return null;
                    var sortOrder = Property(option, "sortOrder") is { ValueKind: JsonValueKind.Number } order
                        && order.TryGetInt32(out var parsed) ? parsed : 0;
                    var attributes = new Dictionary<string, string?>(StringComparer.Ordinal);
                    if (Property(option, "attributes") is { ValueKind: JsonValueKind.Object } values)
                    {
                        foreach (var attribute in values.EnumerateObject())
                        {
                            attributes[attribute.Name] = attribute.Value.ValueKind switch
                            {
                                JsonValueKind.String => attribute.Value.GetString(),
                                JsonValueKind.Null => null,
                                _ => attribute.Value.GetRawText()
                            };
                        }
                    }
                    options.Add(new OptionView(code.GetString()!, sortOrder, attributes));
                }
                lists[name] = options.OrderBy(option => option.SortOrder).ThenBy(option => option.Code, StringComparer.Ordinal).ToList();
            }
            return lists;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement? Property(JsonElement element, string camel) =>
        element.TryGetProperty(camel, out var value)
        || element.TryGetProperty(char.ToUpperInvariant(camel[0]) + camel[1..], out value)
            ? value
            : null;

    // ── Read one ──────────────────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("api/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        if (!HasPermission(ReadPermission))
            return ForbiddenResult();

        if (!TryCreateGatewayRequest(HttpMethod.Get, $"{_gatewayUrl}/api/mdm/items/{id:D}", content: null, out var request))
            return UnauthorizedResult();

        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    return await KnownOrStatusFailureAsync(response, cancellationToken);

                return new ContentResult
                {
                    StatusCode = StatusCodes.Status200OK,
                    ContentType = "application/json",
                    Content = await response.Content.ReadAsStringAsync(cancellationToken)
                };
            }
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Item detail proxy timed out.");
            return SafeFailure(HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Item detail proxy failed.");
            return SafeFailure(HttpStatusCode.ServiceUnavailable);
        }
    }

    // ── Create / update ───────────────────────────────────────────────────────────────────────────────────────────
    // The create carries an idempotency key the SERVER made for this form attempt (a protected, time-limited token
    // handed to the Create page): the same attempt sent twice is one item. A success hands the next attempt's token.

    [HttpPost("api")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateItem([FromBody] ItemWriteInput? input, CancellationToken cancellationToken)
    {
        if (!HasPermission(CreatePermission))
            return ForbiddenResult();
        if (input is null)
            return SafeFailure(HttpStatusCode.BadRequest);
        if (!TryReadFormAttempt(input.FormAttemptToken, out var idempotencyKey))
            return BadRequest(new { success = false, errors = new[] { _localizer["ErrorInvalidFormAttempt"].Value } });

        var fields = ItemFields(input);
        fields["idempotencyKey"] = idempotencyKey;
        if (!TryCreateGatewayRequest(HttpMethod.Post, $"{_gatewayUrl}/api/mdm/items",
                JsonContent.Create(fields, options: _jsonOptions), out var request))
        {
            return UnauthorizedResult();
        }

        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    return await KnownOrStatusFailureAsync(response, cancellationToken);

                var envelope = await ReadEnvelopeAsync(response, cancellationToken);
                var status = (int)response.StatusCode;
                // 202: the row (or its code binding) is not settled yet. The SAME attempt token, sent again, finishes it.
                if (status == StatusCodes.Status202Accepted)
                {
                    return StatusCode(StatusCodes.Status202Accepted, new
                    {
                        success = false,
                        errors = new[] { _localizer["RefusalCreatePending"].Value },
                        formAttemptToken = input.FormAttemptToken
                    });
                }
                if (status is not (StatusCodes.Status200OK or StatusCodes.Status201Created)
                    || envelope is not { IsSuccessful: true } || envelope.StatusCode != status
                    || envelope.Data.ValueKind != JsonValueKind.String || !Guid.TryParse(envelope.Data.GetString(), out var itemId))
                {
                    return SafeFailure(HttpStatusCode.BadGateway);
                }

                return StatusCode(status, new
                {
                    success = true,
                    data = new { id = itemId },
                    formAttemptToken = CreateFormAttemptToken()
                });
            }
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Item create proxy timed out.");
            return SafeFailure(HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Item create proxy failed.");
            return SafeFailure(HttpStatusCode.ServiceUnavailable);
        }
    }

    [HttpPut("api/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] ItemWriteInput? input, CancellationToken cancellationToken)
    {
        if (!HasPermission(UpdatePermission))
            return ForbiddenResult();
        if (id == Guid.Empty || input?.ExpectedVersion is null or < 0)
            return SafeFailure(HttpStatusCode.BadRequest);

        var fields = ItemFields(input);
        fields["expectedVersion"] = input.ExpectedVersion.Value;
        return await ProxyItemWriteAsync(HttpMethod.Put, $"{_gatewayUrl}/api/mdm/items/{id:D}",
            JsonContent.Create(fields, options: _jsonOptions), "update", cancellationToken);
    }

    // Exactly MDM's eleven fields (ItemFieldsInput), codes trimmed and upper-cased as MDM stores them.
    private static Dictionary<string, object?> ItemFields(ItemWriteInput input) => new(StringComparer.Ordinal)
    {
        ["name"] = input.Name?.Trim(),
        ["description"] = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
        ["materialType"] = Code(input.MaterialType),
        ["baseUomCode"] = Code(input.BaseUomCode),
        ["uomConversions"] = (input.UomConversions ?? []).Select(conversion => new
        {
            fromUom = Code(conversion?.FromUom),
            toUom = Code(conversion?.ToUom),
            numerator = conversion?.Numerator?.Trim(),
            denominator = conversion?.Denominator?.Trim()
        }).ToList(),
        ["storageConditionCodes"] = (input.StorageConditionCodes ?? []).Select(Code).ToList(),
        ["lotControlled"] = input.LotControlled,
        ["stockable"] = input.Stockable,
        ["shelfLifeDays"] = input.ShelfLifeDays,
        ["retestDays"] = input.RetestDays,
        ["minRemainingShelfLifeDays"] = input.MinRemainingShelfLifeDays
    };

    private static string? Code(string? value) => value?.Trim().ToUpperInvariant();

    // ── Lifecycle ─────────────────────────────────────────────────────────────────────────────────────────────────
    // Each answers MDM's ItemDto (the item after the change); the screen reads the item back and reloads. Submit and
    // reactivation are answered 503 ITEM_ACTIVATION_APPROVAL_UNAVAILABLE until the approval (S2) exists — said as such.

    [HttpPost("api/{id:guid}/submit")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Submit(Guid id, [FromForm] int? expectedVersion, CancellationToken cancellationToken) =>
        LifecycleAsync(id, SubmitPermission, "submit", expectedVersion, reason: null, reasonRequired: false, cancellationToken);

    [HttpPost("api/{id:guid}/reactivation-requests")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RequestReactivation(Guid id, [FromForm] int? expectedVersion, [FromForm] string? reason,
        CancellationToken cancellationToken) =>
        LifecycleAsync(id, SubmitPermission, "reactivation-requests", expectedVersion, reason, reasonRequired: true, cancellationToken);

    [HttpPost("api/{id:guid}/deactivate")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Deactivate(Guid id, [FromForm] int? expectedVersion, [FromForm] string? reason,
        CancellationToken cancellationToken) =>
        LifecycleAsync(id, DeactivatePermission, "deactivate", expectedVersion, reason, reasonRequired: true, cancellationToken);

    [HttpPost("api/{id:guid}/retire")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Retire(Guid id, [FromForm] int? expectedVersion, [FromForm] string? reason,
        CancellationToken cancellationToken) =>
        LifecycleAsync(id, RetirePermission, "retire", expectedVersion, reason, reasonRequired: true, cancellationToken);

    public const int MaxReasonLength = 500;

    private async Task<IActionResult> LifecycleAsync(Guid id, string permission, string suffix, int? expectedVersion,
        string? reason, bool reasonRequired, CancellationToken cancellationToken)
    {
        if (!HasPermission(permission))
            return ForbiddenResult();

        reason = reason?.Trim();
        if (id == Guid.Empty || expectedVersion is null or < 0)
            return SafeFailure(HttpStatusCode.BadRequest);
        if (reasonRequired && string.IsNullOrEmpty(reason)
            || reason is { Length: > MaxReasonLength }
            || reason?.Any(char.IsControl) == true)
        {
            return BadRequest(new { success = false, errors = new[] { _localizer["RefusalReasonRequired"].Value } });
        }

        var body = new Dictionary<string, object?>(StringComparer.Ordinal) { ["expectedVersion"] = expectedVersion.Value };
        if (!string.IsNullOrEmpty(reason))
            body["reason"] = reason;
        return await ProxyItemWriteAsync(HttpMethod.Post, $"{_gatewayUrl}/api/mdm/items/{id:D}/{suffix}",
            JsonContent.Create(body, options: _jsonOptions), suffix, cancellationToken);
    }

    // A write answered with MDM's ItemDto: passed on only as MDM's own successful envelope with a matching status.
    private async Task<IActionResult> ProxyItemWriteAsync(HttpMethod method, string targetUrl, HttpContent payload,
        string action, CancellationToken cancellationToken)
    {
        if (!TryCreateGatewayRequest(method, targetUrl, payload, out var request))
        {
            payload.Dispose();
            return UnauthorizedResult();
        }

        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                    return await KnownOrStatusFailureAsync(response, cancellationToken);

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                ItemEnvelope? envelope;
                try
                {
                    envelope = JsonSerializer.Deserialize<ItemEnvelope>(body, _jsonOptions);
                }
                catch (JsonException exception)
                {
                    _logger.LogWarning(exception, "Item proxy received malformed JSON for {Action}.", action);
                    return SafeFailure(HttpStatusCode.BadGateway);
                }

                if (response.StatusCode != HttpStatusCode.OK
                    || envelope is not { IsSuccessful: true } || envelope.StatusCode != StatusCodes.Status200OK
                    || envelope.Data.ValueKind != JsonValueKind.Object)
                {
                    return SafeFailure(HttpStatusCode.BadGateway);
                }

                return new ContentResult { StatusCode = StatusCodes.Status200OK, ContentType = "application/json", Content = body };
            }
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Item proxy timed out for {Action}.", action);
            return SafeFailure(HttpStatusCode.GatewayTimeout);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Item proxy failed for {Action}.", action);
            return SafeFailure(HttpStatusCode.ServiceUnavailable);
        }
    }

    private sealed class ItemEnvelope
    {
        public bool IsSuccessful { get; init; }
        public int StatusCode { get; init; }
        public JsonElement Data { get; init; }
    }

    private async Task<ItemEnvelope?> ReadEnvelopeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ItemEnvelope>(_jsonOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Item create proxy: MDM answered an unreadable envelope.");
            return null;
        }
    }

    // ── Refusals: code → sentence ─────────────────────────────────────────────────────────────────────────────────
    // Every refusal MDM names is said as a sentence (ItemRefusalSentences, seven languages), whatever its status.
    // Several codes → each one's sentence, once. A code with no sentence is a missing mapping: the status's sentence
    // is shown, the code is logged here and the browser is told (unmapped) — the raw code never reaches the browser.

    private async Task<IActionResult> KnownOrStatusFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken,
        bool options = false)
    {
        if (!IsJsonMediaType(response.Content.Headers.ContentType?.MediaType))
            return SafeFailure(response.StatusCode);
        string[] items;
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !(document.RootElement.TryGetProperty("errors", out var errors)
                     || document.RootElement.TryGetProperty("Errors", out errors))
                || errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() is 0 or > 20
                || errors.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
                return SafeFailure(response.StatusCode);
            items = errors.EnumerateArray().Select(item => item.GetString()!).ToArray();
        }
        catch (JsonException)
        {
            return SafeFailure(response.StatusCode);
        }

        var status = NormalizedStatus(response.StatusCode);
        var sentences = new List<string>();
        var unmapped = false;
        for (var index = 0; index < items.Length; index++)
        {
            var code = items[index];
            string? detail = null;
            // A detail-bearing code owns the ONE item after it, whatever its shape (SAME_UOM, "item-uom:XYZ").
            if (ItemRefusalSentences.DetailBearing.ContainsKey(code) && index + 1 < items.Length)
                detail = items[++index];
            // Any other non-code item after a code is that code's detail — never an unmapped code.
            else if (!IsCode(code) && index > 0)
                continue;

            var sentence = (options ? OptionsSentence(code) : null) ?? Sentence(code, detail);
            if (sentence is null)
            {
                unmapped = true;
                _logger.LogWarning("Item proxy: MDM answered {Status} with a code that has no sentence ({ErrorCode}).",
                    status, IsCode(code) ? code : "UNREADABLE_CODE");
                sentence = MapStatusMessage(status);
            }
            if (!sentences.Contains(sentence, StringComparer.Ordinal))
                sentences.Add(sentence);
        }
        if (sentences.Count == 0)
            sentences.Add(MapStatusMessage(status));
        return unmapped
            ? StatusCode(status, new { success = false, errors = sentences.ToArray(), unmapped = true })
            : StatusCode(status, new { success = false, errors = sentences.ToArray() });
    }

    // The options read's refusals (S5b): its own map, then any REFERENCE_ code Platform passes on — all "cannot be loaded".
    private string? OptionsSentence(string code)
    {
        if (ItemRefusalSentences.OptionsKeys.TryGetValue(code, out var key))
            return _localizer[key].Value;
        if (code.StartsWith(ItemRefusalSentences.OptionsCodePrefix, StringComparison.Ordinal) && IsCode(code))
        {
            _logger.LogWarning("Item options proxy: a provider code without its own sentence ({ErrorCode}).", code);
            return _localizer["RefusalOptionsUnavailable"].Value;
        }
        return null;
    }

    private string? Sentence(string code, string? detail)
    {
        if (!ItemRefusalSentences.Keys.TryGetValue(code, out var key))
            return null;
        if (detail is null)
            return _localizer[key].Value;
        if (ItemRefusalSentences.DetailBearing[code].TryGetValue(detail, out var detailKey))
            return _localizer[detailKey].Value;
        return code switch
        {
            "ITEM_CRITICAL_FIELD_LOCKED" when ItemRefusalSentences.LockedFieldLabels.TryGetValue(detail, out var label)
                => string.Format(CultureInfo.CurrentCulture, _localizer["RefusalCriticalFieldLockedNamed"].Value,
                    _localizer[label].Value),
            "ITEM_REFERENCE_VALUE_INVALID" when ReferenceValues(detail) is { Count: > 0 } named
                => string.Format(CultureInfo.CurrentCulture, _localizer["RefusalReferenceValueInvalidNamed"].Value,
                    string.Join(_localizer["ListSeparator"].Value, named)),
            _ => _localizer[key].Value
        };
    }

    private static readonly Regex ReferenceValue = new(@"^(?<set>[a-z\-]{1,40}):(?<value>[A-Za-z0-9_\-]{1,40})(?::NOT_SELECTABLE)?$",
        RegexOptions.CultureInvariant);

    // "set:value[:NOT_SELECTABLE],…" (or a bare set code) → "Unit code: XYZ". Only the three item lists and a
    // code-shaped value are said; anything else leaves the general sentence.
    private List<string> ReferenceValues(string detail)
    {
        var named = new List<string>();
        foreach (var part in detail.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (ItemRefusalSentences.ReferenceSetLabels.TryGetValue(part, out var bareLabel))
            {
                named.Add(_localizer[bareLabel].Value);
                continue;
            }
            var match = ReferenceValue.Match(part);
            if (!match.Success || !ItemRefusalSentences.ReferenceSetLabels.TryGetValue(match.Groups["set"].Value, out var label))
                return [];
            named.Add($"{_localizer[label].Value}: {match.Groups["value"].Value}");
        }
        return named;
    }

    // A code starts with a capital letter, has an underscore and only [A-Z0-9_] (the GSKU bridge's rule).
    public static bool IsCode(string value) => value.Length is > 2 and <= 100
        && value[0] is >= 'A' and <= 'Z'
        && value.Contains('_')
        && value.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');

    private static bool IsJsonMediaType(string? mediaType) =>
        string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
        || mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) == true;

    private IActionResult SafeFailure(HttpStatusCode statusCode)
    {
        var normalizedStatus = NormalizedStatus(statusCode);
        return StatusCode(normalizedStatus, new { success = false, errors = new[] { MapStatusMessage(normalizedStatus) } });
    }

    private static int NormalizedStatus(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => StatusCodes.Status400BadRequest,
        HttpStatusCode.Unauthorized => StatusCodes.Status401Unauthorized,
        HttpStatusCode.Forbidden => StatusCodes.Status403Forbidden,
        HttpStatusCode.NotFound => StatusCodes.Status404NotFound,
        HttpStatusCode.Conflict => StatusCodes.Status409Conflict,
        // The item's rule refusals (unknown list value, two temperatures, conversions, stock completeness) are 422 —
        // said as themselves, never turned into a gateway failure.
        HttpStatusCode.UnprocessableEntity => StatusCodes.Status422UnprocessableEntity,
        HttpStatusCode.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
        HttpStatusCode.GatewayTimeout => StatusCodes.Status504GatewayTimeout,
        _ => StatusCodes.Status502BadGateway
    };

    private IActionResult ForbiddenResult() => StatusCode(StatusCodes.Status403Forbidden, new
    {
        success = false,
        errors = new[] { _localizer["ErrorForbidden"].Value }
    });

    private IActionResult UnauthorizedResult() => Unauthorized(new
    {
        success = false,
        errors = new[] { _localizer["ErrorUnauthorized"].Value }
    });

    private string MapStatusMessage(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest or StatusCodes.Status422UnprocessableEntity => _localizer["ErrorValidation"].Value,
        StatusCodes.Status401Unauthorized => _localizer["ErrorUnauthorized"].Value,
        StatusCodes.Status403Forbidden => _localizer["ErrorForbidden"].Value,
        StatusCodes.Status404NotFound => _localizer["ErrorNotFound"].Value,
        StatusCodes.Status409Conflict => _localizer["ErrorConflict"].Value,
        StatusCodes.Status503ServiceUnavailable => _localizer["ErrorProviderUnavailable"].Value,
        StatusCodes.Status504GatewayTimeout => _localizer["ErrorProviderTimeout"].Value,
        _ => _localizer["ErrorGateway"].Value
    };

    // ── Gateway request: the cookie's token as Bearer, the tenant from the principal (never from the browser) ───────

    private bool TryCreateGatewayRequest(HttpMethod method, string url, HttpContent? content, out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, url);
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // One tenant, however many claim spellings carry it; two different tenants (or an unreadable one) = no request.
        var tenantValues = User.Claims
            .Where(claim => claim.Type == "tenantId" || claim.Type == "tenant_id"
                            || claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value)
            .ToArray();
        if (tenantValues.Length == 0
            || !tenantValues.All(value => Guid.TryParse(value, out _))
            || tenantValues.Select(Guid.Parse).Distinct().Count() != 1
            || User.Claims.Count(claim => claim.Type == "tenant_id") > 1
            || Guid.Parse(tenantValues[0]) == Guid.Empty)
        {
            request.Dispose();
            request = null!;
            return false;
        }

        request.Headers.Add("X-Tenant-Id", Guid.Parse(tenantValues[0]).ToString("D"));
        request.Content = content;
        return true;
    }

    // ── Form attempt (the create's idempotency key, made and kept by the server) ───────────────────────────────────

    private string CreateFormAttemptToken()
    {
        var payload = new FormAttemptPayload(ResolveUserSubject(), Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
        return _formAttemptProtector.Protect(JsonSerializer.Serialize(payload, _jsonOptions), FormAttemptLifetime);
    }

    private bool TryReadFormAttempt(string? token, out string operationKey)
    {
        operationKey = string.Empty;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var payload = JsonSerializer.Deserialize<FormAttemptPayload>(_formAttemptProtector.Unprotect(token, out _), _jsonOptions);
            if (payload is null
                || string.IsNullOrWhiteSpace(payload.OperationKey)
                || !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(payload.UserSubject), Encoding.UTF8.GetBytes(ResolveUserSubject())))
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
