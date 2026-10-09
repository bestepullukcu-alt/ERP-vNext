using System.Text.Json;
using Diten.Web.Models.CRM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-KP-5a-UI — Country Legal Profiles (country × language; WP-KP-5a). Same structure as Safety Texts without a
/// product: list, Create / Edit (draft only), Details at <c>/CRM/LegalProfiles/{id}</c> (the Regulatory task's deep
/// link) with the decision panel. CRM under <c>/api/crm/knowledge/country-legal-profiles</c> decides every rule.
/// </summary>
[Authorize]
[Route("CRM/LegalProfiles")]
public sealed class LegalProfilesController : RegulatoryTextsControllerBase
{
    public const string Read = "crm.country-legal-profile.read";
    public const string Manage = "crm.country-legal-profile.manage";
    public const string Submit = "crm.country-legal-profile.submit";
    private const string ViewRoot = "~/Views/CRM/LegalProfiles";

    public LegalProfilesController(HttpClient httpClient, IConfiguration configuration, ILogger<LegalProfilesController> logger)
        : base(httpClient, configuration, logger)
    {
    }

    protected override string CrmBase => "/api/crm/knowledge/country-legal-profiles";
    protected override string ReadPermission => Read;
    protected override string ManagePermission => Manage;
    protected override string SubmitPermission => Submit;

    // ---------------- pages (UAS-001: no key → plain 403, no skeleton, no redirect) ----------------

    [HttpGet("")]
    public IActionResult Index()
    {
        if (RequirePage(Read) is { } denied) return denied;
        ViewData["CanManage"] = CanManage;
        return View($"{ViewRoot}/Index.cshtml");
    }

    [HttpGet("Create")]
    public IActionResult Create() =>
        RequirePage(Manage) ?? View($"{ViewRoot}/Create.cshtml", new LegalProfileEditViewModel());

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        if (RequirePage(Manage) is { } denied) return denied;
        var detail = await ReadDetailAsync(id, ct);
        if (detail is not { ValueKind: JsonValueKind.Object } d) return NotFound();
        if (!SafetyTextsController.Flag(d, "canEdit")) return RedirectToAction(nameof(Details), new { id });
        return View($"{ViewRoot}/Edit.cshtml", new LegalProfileEditViewModel
        {
            Id = id,
            ProfileCode = SafetyTextsController.Str(d, "countryLegalProfileCode") ?? SafetyTextsController.Str(d, "profileCode"),
            Version = SafetyTextsController.Num(d, "version"),
            CountryCode = SafetyTextsController.Str(d, "countryCode"),
            LanguageCode = SafetyTextsController.Str(d, "languageCode"),
            LegalFooterText = SafetyTextsController.Str(d, "legalFooterText"),
            MarketingAuthorizationHolder = SafetyTextsController.Str(d, "marketingAuthorizationHolder"),
            AdverseEventReportingText = SafetyTextsController.Str(d, "adverseEventReportingText"),
            PromotionalNotice = SafetyTextsController.Str(d, "promotionalNotice"),
            PageApprovalCodeFormat = SafetyTextsController.Str(d, "pageApprovalCodeFormat")
        });
    }

    /// <summary>The Details page and the WCN deep link (<c>/CRM/LegalProfiles/{id}</c>).</summary>
    [HttpGet("{id:guid}")]
    public IActionResult Details(Guid id)
    {
        if (RequirePage(Read) is { } denied) return denied;
        ViewData["Id"] = id.ToString();
        ViewData["CanManage"] = CanManage;
        ViewData["CanSubmit"] = CanSubmit;
        return View($"{ViewRoot}/Details.cshtml");
    }

    // ---------------- same-origin JSON proxy ----------------

    [HttpGet("api/legal-profiles")]
    public Task<IActionResult> List(CancellationToken ct) => ListAsync(ct);

    [HttpGet("api/legal-profiles/resolve")]
    public Task<IActionResult> Resolve([FromQuery] string? countryCode, [FromQuery] string? languageCode, CancellationToken ct) =>
        ResolveAsync(null, countryCode, languageCode, ct);

    [HttpGet("api/legal-profiles/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => GetAsync(id, ct);

    [HttpPost("api/legal-profiles")]
    public Task<IActionResult> CreateItem([FromBody] JsonElement body, CancellationToken ct) => CreateAsync(body, ct);

    [HttpPut("api/legal-profiles/{id:guid}")]
    public Task<IActionResult> UpdateItem(Guid id, [FromBody] JsonElement body, CancellationToken ct) => UpdateAsync(id, body, ct);

    [HttpPost("api/legal-profiles/{id:guid}/submit")]
    public Task<IActionResult> SubmitItem(Guid id, CancellationToken ct) => SubmitAsync(id, ct);

    [HttpPost("api/legal-profiles/{id:guid}/withdraw")]
    public Task<IActionResult> WithdrawItem(Guid id, CancellationToken ct) => WithdrawAsync(id, ct);

    [HttpPost("api/legal-profiles/{id:guid}/decision")]
    public Task<IActionResult> Decide(Guid id, [FromBody] JsonElement body, CancellationToken ct) => DecideAsync(id, body, ct);

    [HttpPost("api/legal-profiles/{id:guid}/new-version")]
    public Task<IActionResult> NewVersion(Guid id, CancellationToken ct) => NewVersionAsync(id, ct);

    [HttpPost("api/legal-profiles/{id:guid}/archive")]
    public Task<IActionResult> Archive(Guid id, CancellationToken ct) => ArchiveAsync(id, ct);

    [HttpGet("api/lookups/countries")]
    public Task<IActionResult> Countries(CancellationToken ct) => CountriesAsync(ct);
}
