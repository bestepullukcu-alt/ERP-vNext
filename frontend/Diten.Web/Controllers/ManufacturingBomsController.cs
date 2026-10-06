using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.Manufacturing.Boms;
using Diten.Web.Services;
using Diten.Web.Views.Manufacturing.Boms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0193 BOM & Routings — tenant shell, Golden Compact (form fields > 8), server-mode list.
// The list and its export are read by the browser straight through the Gateway (window.API.manufacturing +
// /api/bom/versions[/export]); every write goes through this same-origin adapter, which forwards the caller's token and
// tenant and turns the service's contract error ({ error: { code, message, correlationId } }) into the screen's words.
// The service stays authoritative for every permission; the views only hide what the user cannot do (UAS-001).
[Authorize]
[Route("Manufacturing/Boms")]
public sealed class ManufacturingBomsController : Controller
{
    internal const string ReadPermission = "manufacturing.bom.read";
    internal const string CreatePermission = "manufacturing.bom.create";
    internal const string UpdatePermission = "manufacturing.bom.update";
    internal const string ReleasePermission = "manufacturing.bom.release";
    internal const string DeletePermission = "manufacturing.bom.delete";

    private const string ViewRoot = "~/Views/Manufacturing/Boms";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<BomsIndex> _localizer;
    private readonly IPermissionSnapshot _permissions;
    private readonly ILogger<ManufacturingBomsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public ManufacturingBomsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<BomsIndex> localizer,
        IPermissionSnapshot permissions,
        ILogger<ManufacturingBomsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = (configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.")).TrimEnd('/');
        _localizer = localizer;
        _permissions = permissions;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() => View($"{ViewRoot}/Create.cshtml", NewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] BomEditViewModel model, CancellationToken ct)
    {
        Normalize(model);
        if (!_permissions.Has(CreatePermission))
        {
            ModelState.AddModelError(string.Empty, _localizer["ErrDenied"].Value);
        }

        if (!ModelState.IsValid)
        {
            return View($"{ViewRoot}/Create.cshtml", model);
        }

        var (status, body) = await SendAsync(HttpMethod.Post, "/api/bom/versions", ToDraftPayload(model, includeItem: true), ct);
        if (status == 201 && Deserialize<BomApiView>(body) is { } created)
        {
            TempData["SuccessMessage"] = _localizer["FormTitleCreate"].Value;
            return RedirectToAction(nameof(Details), new { id = created.BomVersionId });
        }

        ModelState.AddModelError(string.Empty, ErrorText(status, body));
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var bom = await LoadAsync(id, ct);
        if (bom is null)
        {
            TempData["ErrorMessage"] = _localizer["ErrUnknownBom"].Value;
            return RedirectToAction(nameof(Index));
        }

        if (bom.Status != "Draft")
        {
            TempData["ErrorMessage"] = _localizer["NotDraftNotice"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }

        return View($"{ViewRoot}/Edit.cshtml", ToEditModel(bom));
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [FromForm] BomEditViewModel model, CancellationToken ct)
    {
        model.BomVersionId = id.ToString();
        Normalize(model);
        ModelState.Remove(nameof(BomEditViewModel.ItemId));
        if (!_permissions.Has(UpdatePermission))
        {
            ModelState.AddModelError(string.Empty, _localizer["ErrDenied"].Value);
        }

        if (!ModelState.IsValid)
        {
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        var payload = ToDraftPayload(model, includeItem: false);
        payload["rowVersion"] = model.RowVersion;
        var (status, body) = await SendAsync(HttpMethod.Put, $"/api/bom/version/{id}", payload, ct);
        if (status == 200)
        {
            TempData["SuccessMessage"] = _localizer["FormTitleEdit"].Value;
            return RedirectToAction(nameof(Details), new { id });
        }

        ModelState.AddModelError(string.Empty, ErrorText(status, body));
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var bom = await LoadAsync(id, ct);
        if (bom is null)
        {
            TempData["ErrorMessage"] = _localizer["ErrUnknownBom"].Value;
            return RedirectToAction(nameof(Index));
        }

        var (status, body) = await SendAsync(HttpMethod.Get, $"/api/bom/version/{id}/history", null, ct);
        var history = status == 200 ? Deserialize<BomHistoryApiResponse>(body)?.Entries : null;
        return View($"{ViewRoot}/Details.cshtml", new BomDetailsViewModel
        {
            Bom = bom,
            History = history ?? [],
            HistoryLoaded = history is not null
        });
    }

    // ── JSON sub-actions for details.js / index.js (same origin, antiforgery header) ──

    [HttpPost("api/{id:guid}/release")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Release(Guid id, [FromBody] BomReleaseInput input, CancellationToken ct) =>
        CommandAsync(ReleasePermission, HttpMethod.Post, $"/api/bom/version/{id}/release",
            new { changeControlRef = input.ChangeControlRef?.Trim(), rowVersion = input.RowVersion }, ct);

    [HttpPost("api/{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Delete(Guid id, [FromQuery] int rowVersion, CancellationToken ct) =>
        CommandAsync(DeletePermission, HttpMethod.Delete, $"/api/bom/version/{id}?rowVersion={rowVersion}", null, ct);

    [HttpPost("api/{itemId:guid}/explode")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Explode(Guid itemId, [FromBody] BomExplodeInput input, CancellationToken ct) =>
        CommandAsync(ReadPermission, HttpMethod.Post, "/api/bom/explode", new { itemId, quantity = input.Quantity?.Trim() }, ct);

    private async Task<IActionResult> CommandAsync(string permission, HttpMethod method, string path, object? payload, CancellationToken ct)
    {
        if (!_permissions.Has(permission))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = _localizer["ErrDenied"].Value });
        }

        var (status, body) = await SendAsync(method, path, payload, ct);
        if (status is >= 200 and < 300)
        {
            return string.IsNullOrWhiteSpace(body) ? StatusCode(status) : Content(body, "application/json");
        }

        return StatusCode(status is 0 ? StatusCodes.Status503ServiceUnavailable : status, new { message = ErrorText(status, body) });
    }

    // ── Gateway call ──

    private async Task<(int Status, string Body)> SendAsync(HttpMethod method, string path, object? payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}");
        var token = Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var tenantId = User.Claims.FirstOrDefault(c => c.Type is "tenantId" or "tenant_id")?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);
        }

        var correlation = Guid.NewGuid().ToString();
        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlation);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload, options: _jsonOptions);
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogInformation("BOM adapter {Method} {Path} → {Status} CorrelationId={CorrelationId}", method, path, (int)response.StatusCode, correlation);
            return ((int)response.StatusCode, body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "BOM adapter {Method} {Path} failed CorrelationId={CorrelationId}", method, path, correlation);
            return (0, string.Empty);
        }
    }

    private async Task<BomApiView?> LoadAsync(Guid id, CancellationToken ct)
    {
        var (status, body) = await SendAsync(HttpMethod.Get, $"/api/bom/version/{id}", null, ct);
        return status == 200 ? Deserialize<BomApiView>(body) : null;
    }

    private T? Deserialize<T>(string body)
    {
        try
        {
            return string.IsNullOrWhiteSpace(body) ? default : JsonSerializer.Deserialize<T>(body, _jsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>
    /// The screen's words for a service answer. Every contract code is mapped explicitly; a 5xx or an empty body is the
    /// "nothing was written" text, never the validation text (MVP-6 recipe 2.1 / 3.7).
    /// </summary>
    private string ErrorText(int status, string body)
    {
        var code = Deserialize<BomApiError>(body)?.Error?.Code;
        var key = code switch
        {
            "UNKNOWN_BOM" => "ErrUnknownBom",
            "UNKNOWN_ITEM" => "ErrUnknownItem",
            "SELF_REFERENCE" => "ErrSelfReference",
            "BOM_CYCLE" => "ErrBomCycle",
            "BOM_NOT_DRAFT" => "ErrBomNotDraft",
            "CONCURRENCY_CONFLICT" => "ErrConcurrency",
            "CHANGE_CONTROL_REJECTED" => "ErrChangeControl",
            "INVALID_REQUEST" => "ErrInvalid",
            _ => status switch
            {
                401 or 403 => "ErrDenied",
                404 => "ErrUnknownBom",
                _ => "ErrPersistence"
            }
        };
        return _localizer[key].Value;
    }

    // ── Form ↔ payload ──

    private static BomEditViewModel NewModel() => new()
    {
        Components = [new BomComponentInput { Position = 10 }],
        Steps = [new RoutingStepInput()]
    };

    private static void Normalize(BomEditViewModel model)
    {
        model.Components = (model.Components ?? [])
            .Where(c => c is not null && !(string.IsNullOrWhiteSpace(c.ComponentItemId) && string.IsNullOrWhiteSpace(c.Quantity) && string.IsNullOrWhiteSpace(c.UomId)))
            .ToList();
        model.Steps = (model.Steps ?? [])
            .Where(s => s is not null && !(s.StepNo is null && string.IsNullOrWhiteSpace(s.Operation) && string.IsNullOrWhiteSpace(s.WorkCenter)))
            .ToList();
    }

    private static Dictionary<string, object?> ToDraftPayload(BomEditViewModel model, bool includeItem)
    {
        var payload = new Dictionary<string, object?>
        {
            ["description"] = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
            ["components"] = model.Components.Select(c => new Dictionary<string, object?>
            {
                ["componentItemId"] = c.ComponentItemId?.Trim(),
                ["quantity"] = c.Quantity?.Trim(),
                ["uomId"] = c.UomId?.Trim(),
                ["position"] = c.Position ?? 0,
                ["alternates"] = string.IsNullOrWhiteSpace(c.Alternates)
                    ? null
                    : c.Alternates.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            }).ToList(),
            ["routing"] = model.Steps.Count == 0
                ? null
                : new { steps = model.Steps.Select(s => new { stepNo = s.StepNo ?? 0, operation = s.Operation?.Trim(), workCenter = string.IsNullOrWhiteSpace(s.WorkCenter) ? null : s.WorkCenter.Trim() }) }
        };
        if (includeItem)
        {
            payload["itemId"] = model.ItemId?.Trim();
        }

        return payload;
    }

    private static BomEditViewModel ToEditModel(BomApiView bom) => new()
    {
        BomVersionId = bom.BomVersionId,
        ItemId = bom.ItemId.ToString(),
        Description = bom.Description,
        RowVersion = bom.RowVersion,
        Version = bom.Version,
        Status = bom.Status,
        Components = bom.Components.OrderBy(c => c.Position).Select(c => new BomComponentInput
        {
            ComponentItemId = c.ComponentItemId.ToString(),
            Quantity = c.Quantity,
            UomId = c.UomId,
            Position = c.Position,
            Alternates = c.Alternates is { Count: > 0 } ? string.Join(", ", c.Alternates) : null
        }).ToList(),
        Steps = bom.Routing?.Steps.OrderBy(s => s.StepNo).Select(s => new RoutingStepInput { StepNo = s.StepNo, Operation = s.Operation, WorkCenter = s.WorkCenter }).ToList()
            ?? [new RoutingStepInput()]
    };
}
