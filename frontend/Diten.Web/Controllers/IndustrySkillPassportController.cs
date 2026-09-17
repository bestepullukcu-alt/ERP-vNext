using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.TalentEcosystem.IndustrySkillPassport;
using Diten.Web.Views.TalentEcosystem.IndustrySkillPassport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("TalentEcosystem/IndustrySkillPassport")]
public sealed class IndustrySkillPassportController : Controller
{
    private const string BaseView = "~/Views/TalentEcosystem/IndustrySkillPassport/";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly IStringLocalizer<IndustrySkillPassportIndex> _localizer;
    private readonly ILogger<IndustrySkillPassportController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public IndustrySkillPassportController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizer<IndustrySkillPassportIndex> localizer,
        ILogger<IndustrySkillPassportController> logger)
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
        View($"{BaseView}Create.cshtml", new IndustrySkillPassportEditViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] IndustrySkillPassportEditViewModel model)
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
                $"{_gatewayUrl}/api/industry-skill-passport",
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
            _logger.LogError(ex, "IndustrySkillPassport create failed.");
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

    private async Task<IndustrySkillPassportDetailViewModel?> LoadApiModelAsync(Guid id)
    {
        if (!AddAuthHeaders())
            return null;

        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/industry-skill-passport/{id}");
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content
            .ReadFromJsonAsync<GatewayResponse<IndustrySkillPassportDetailViewModel>>(_jsonOptions);
        return payload?.Data;
    }

    private static IndustrySkillPassportSavePayload ToPayload(IndustrySkillPassportEditViewModel model) => new()
    {
        Code = model.Code,
        DisplayName = model.DisplayName,
        IndustrySkillPassportReadinessState = model.IndustrySkillPassportReadinessState,
        SkillClaimCatalogBoundaryState = model.SkillClaimCatalogBoundaryState,
        AttestationIntakeBoundaryState = model.AttestationIntakeBoundaryState,
        VerificationScopeBoundaryState = model.VerificationScopeBoundaryState,
        VisibilityControlBoundaryState = model.VisibilityControlBoundaryState,
        PassportReviewBoundaryState = model.PassportReviewBoundaryState,
        AutomatedDecisionBoundaryState = model.AutomatedDecisionBoundaryState,
        TalentDataSourceDependencyState = model.TalentDataSourceDependencyState,
        ConsentPolicyDependencyState = model.ConsentPolicyDependencyState,
        CertificationSourceDependencyState = model.CertificationSourceDependencyState,
        NotificationDependencyState = model.NotificationDependencyState,
        ConsentPreconditionState = model.ConsentPreconditionState,
        DataMinimizationState = model.DataMinimizationState,
        RetentionPolicyState = model.RetentionPolicyState,
        VerificationPolicyState = model.VerificationPolicyState,
        SourceContractVersion = model.SourceContractVersion,
        IndustrySkillPassportReadinessVersion = model.IndustrySkillPassportReadinessVersion,
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
