using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.HumanCapital.HrDocumentation;
using Diten.Web.Views.HumanCapital.HrDocumentation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("HumanCapital/HrDocumentation")]
public sealed class HrDocumentationController : Controller
{
    private const string BaseView = "~/Views/HumanCapital/HrDocumentation/";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly IStringLocalizer<HrDocumentationIndex> _localizer;
    private readonly ILogger<HrDocumentationController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public HrDocumentationController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizer<HrDocumentationIndex> localizer,
        ILogger<HrDocumentationController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View($"{BaseView}Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() =>
        View($"{BaseView}Create.cshtml", new HrDocumentationEditViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] HrDocumentationEditViewModel model)
    {
        if (!ModelState.IsValid)
            return View($"{BaseView}Create.cshtml", model);

        if (!AddAuthHeaders())
        {
            ModelState.AddModelError(string.Empty, _localizer["Unauthorized"].Value);
            return View($"{BaseView}Create.cshtml", model);
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayUrl}/api/hr-documentation",
                ToPayload(model),
                _jsonOptions);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
                return RedirectToAction(nameof(Index));
            }

            AddGatewayErrorsToModelState(await ExtractGatewayErrorsAsync(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HrDocumentation create failed.");
            AddGatewayErrorsToModelState(BuildExceptionErrors(ex));
        }

        return View($"{BaseView}Create.cshtml", model);
    }

    [HttpGet("Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var detail = await LoadApiModelAsync(id);
        if (detail is null)
        {
            TempData["ErrorMessage"] = _sharedLocalizer["GatewayError"].Value;
            return RedirectToAction(nameof(Index));
        }

        return View($"{BaseView}Details.cshtml", detail);
    }

    private async Task<HrDocumentationDetailViewModel?> LoadApiModelAsync(Guid id)
    {
        if (!AddAuthHeaders())
            return null;

        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/hr-documentation/{id}");
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content
            .ReadFromJsonAsync<GatewayResponse<HrDocumentationDetailViewModel>>(_jsonOptions);
        return payload?.Data;
    }

    private static HrDocumentationSavePayload ToPayload(HrDocumentationEditViewModel model) => new()
    {
        Code = model.Code,
        DisplayName = model.DisplayName,
        HrDocumentationReadinessState = model.HrDocumentationReadinessState,
        DocumentWorkspaceBoundaryState = model.DocumentWorkspaceBoundaryState,
        EvidenceLinkBoundaryState = model.EvidenceLinkBoundaryState,
        DocumentClassificationBoundaryState = model.DocumentClassificationBoundaryState,
        LegalHoldBoundaryState = model.LegalHoldBoundaryState,
        DispositionScheduleBoundaryState = model.DispositionScheduleBoundaryState,
        AutomatedDecisionBoundaryState = model.AutomatedDecisionBoundaryState,
        DocumentRepositoryDependencyState = model.DocumentRepositoryDependencyState,
        EvidenceStoreDependencyState = model.EvidenceStoreDependencyState,
        DocumentDependencyState = model.DocumentDependencyState,
        NotificationDependencyState = model.NotificationDependencyState,
        ConsentPreconditionState = model.ConsentPreconditionState,
        DataMinimizationState = model.DataMinimizationState,
        RetentionPolicyState = model.RetentionPolicyState,
        EvidencePolicyState = model.EvidencePolicyState,
        SourceContractVersion = model.SourceContractVersion,
        HrDocumentationReadinessVersion = model.HrDocumentationReadinessVersion,
        DeferredReason = string.IsNullOrWhiteSpace(model.DeferredReason) ? null : model.DeferredReason.Trim()
    };

    private void AddGatewayErrorsToModelState(IEnumerable<string> errors)
    {
        foreach (var error in errors)
            ModelState.AddModelError(string.Empty, error);
    }

    private List<string> BuildExceptionErrors(Exception ex)
    {
        var message = ex.GetBaseException().Message;
        if (string.IsNullOrWhiteSpace(message))
            message = _sharedLocalizer["GatewayError"].Value;

        return [message];
    }

    private async Task<List<string>> ExtractGatewayErrorsAsync(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return [_localizer["Unauthorized"].Value];

        try
        {
            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<object>>(_jsonOptions);
            var errors = payload?.Errors?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
            if (errors?.Count > 0)
                return errors;
        }
        catch
        {
            // fall through to the generic gateway error below
        }

        return [_sharedLocalizer["GatewayError"].Value];
    }

    private bool AddAuthHeaders()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (_httpClient.DefaultRequestHeaders.Contains("X-Tenant-Id"))
            _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");

        var tenantId = GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
            return false;

        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        return true;
    }

    private string? GetTenantId() =>
        User.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
}
