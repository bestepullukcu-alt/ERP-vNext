using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.TalentEcosystem.MentorshipRecommendationNetwork;
using Diten.Web.Views.TalentEcosystem.MentorshipRecommendationNetwork;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("TalentEcosystem/MentorshipRecommendationNetwork")]
public sealed class MentorshipRecommendationNetworkController : Controller
{
    private const string BaseView = "~/Views/TalentEcosystem/MentorshipRecommendationNetwork/";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly IStringLocalizer<MentorshipRecommendationNetworkIndex> _localizer;
    private readonly ILogger<MentorshipRecommendationNetworkController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public MentorshipRecommendationNetworkController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        IStringLocalizer<MentorshipRecommendationNetworkIndex> localizer,
        ILogger<MentorshipRecommendationNetworkController> logger)
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
        View($"{BaseView}Create.cshtml", new MentorshipRecommendationNetworkEditViewModel());

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] MentorshipRecommendationNetworkEditViewModel model)
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
                $"{_gatewayUrl}/api/mentorship-recommendation-network",
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
            _logger.LogError(ex, "MentorshipRecommendationNetwork create failed.");
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

    private async Task<MentorshipRecommendationNetworkDetailViewModel?> LoadApiModelAsync(Guid id)
    {
        if (!AddAuthHeaders())
            return null;

        var response = await _httpClient.GetAsync($"{_gatewayUrl}/api/mentorship-recommendation-network/{id}");
        if (!response.IsSuccessStatusCode)
            return null;

        var payload = await response.Content
            .ReadFromJsonAsync<GatewayResponse<MentorshipRecommendationNetworkDetailViewModel>>(_jsonOptions);
        return payload?.Data;
    }

    private static MentorshipRecommendationNetworkSavePayload ToPayload(MentorshipRecommendationNetworkEditViewModel model) => new()
    {
        Code = model.Code,
        DisplayName = model.DisplayName,
        MentorshipRecommendationNetworkReadinessState = model.MentorshipRecommendationNetworkReadinessState,
        NetworkCatalogBoundaryState = model.NetworkCatalogBoundaryState,
        PairingIntakeBoundaryState = model.PairingIntakeBoundaryState,
        RecommendationScopeBoundaryState = model.RecommendationScopeBoundaryState,
        VisibilityControlBoundaryState = model.VisibilityControlBoundaryState,
        NetworkReviewBoundaryState = model.NetworkReviewBoundaryState,
        AutomatedDecisionBoundaryState = model.AutomatedDecisionBoundaryState,
        TalentDataSourceDependencyState = model.TalentDataSourceDependencyState,
        ConsentPolicyDependencyState = model.ConsentPolicyDependencyState,
        ReputationSourceDependencyState = model.ReputationSourceDependencyState,
        NotificationDependencyState = model.NotificationDependencyState,
        ConsentPreconditionState = model.ConsentPreconditionState,
        DataMinimizationState = model.DataMinimizationState,
        RetentionPolicyState = model.RetentionPolicyState,
        PublicationPolicyState = model.PublicationPolicyState,
        SourceContractVersion = model.SourceContractVersion,
        MentorshipRecommendationNetworkReadinessVersion = model.MentorshipRecommendationNetworkReadinessVersion,
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
