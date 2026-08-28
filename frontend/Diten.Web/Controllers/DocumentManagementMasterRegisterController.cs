using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0029-FU24 — Document Master Register TenantShell screens (GMG-QMS-SOP-0001 §18 LOG-0001, §20).
/// Same-origin MVC proxy profile: the browser never talks to the Platform API (5057) directly and never sends a
/// tenant id — the bearer token and X-Tenant-Id are attached server-side from the HttpOnly auth cookie, exactly like
/// <see cref="DocumentManagementTemplateMastersController"/>. Read/write surface is limited to the FU06 master
/// register list/summary/detail/create/update endpoints; no delete/lifecycle/gate mutation is exposed here.
/// </summary>
[Authorize]
[Route("DocumentManagementMasterRegister")]
public sealed class DocumentManagementMasterRegisterController : Controller
{
    private const string ApiRoot = "/api/v1/document-management";
    private const string ApiBase = ApiRoot + "/document-master-register";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<DocumentManagementMasterRegisterController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public DocumentManagementMasterRegisterController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<DocumentManagementMasterRegisterController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    // ── Pages ────────────────────────────────────────────────────────────────

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/DocumentManagement/MasterRegister/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() => View("~/Views/DocumentManagement/MasterRegister/Create.cshtml");

    [HttpGet("CreateControlledDocument")]
    public IActionResult CreateControlledDocument() =>
        View("~/Views/DocumentManagement/MasterRegister/CreateControlledDocument.cshtml");

    [HttpGet("Edit/{id:guid}")]
    public IActionResult Edit(Guid id)
    {
        ViewData["MasterRegisterEntryId"] = id;
        return View("~/Views/DocumentManagement/MasterRegister/Edit.cshtml");
    }

    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewData["MasterRegisterEntryId"] = id;
        return View("~/Views/DocumentManagement/MasterRegister/Details.cshtml");
    }

    // ── Same-origin proxy API ────────────────────────────────────────────────

    /// <summary>
    /// FU06 list endpoint. Only the query parameters the backend actually accepts are forwarded; everything else the
    /// UI filter panel offers is applied client-side over the returned page (documented in the FU24 audit).
    /// </summary>
    [HttpGet("/DocumentManagement/MasterRegister/api/list")]
    public Task<IActionResult> List(
        [FromQuery] string? registerStatus,
        [FromQuery] string? lifecycleStatus,
        [FromQuery] string? criticality,
        [FromQuery] string? documentClass,
        [FromQuery] Guid? ownerCompanyId,
        CancellationToken ct)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(registerStatus)) qs.Add($"registerStatus={Uri.EscapeDataString(registerStatus)}");
        if (!string.IsNullOrWhiteSpace(lifecycleStatus)) qs.Add($"lifecycleStatus={Uri.EscapeDataString(lifecycleStatus)}");
        if (!string.IsNullOrWhiteSpace(criticality)) qs.Add($"criticality={Uri.EscapeDataString(criticality)}");
        if (!string.IsNullOrWhiteSpace(documentClass)) qs.Add($"documentClass={Uri.EscapeDataString(documentClass)}");
        if (ownerCompanyId is { } oc && oc != Guid.Empty) qs.Add($"ownerCompanyId={oc:D}");
        var suffix = qs.Count == 0 ? string.Empty : $"?{string.Join('&', qs)}";
        return ProxyGetAsync($"{ApiBase}{suffix}", ct);
    }

    [HttpGet("/DocumentManagement/MasterRegister/api/summary")]
    public Task<IActionResult> Summary(CancellationToken ct) => ProxyGetAsync($"{ApiBase}/summary", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/detail/{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken ct) => ProxyGetAsync($"{ApiBase}/{id}", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/legal-entities")]
    public Task<IActionResult> LegalEntities(CancellationToken ct) => ProxyGetAsync("/api/legal-entities", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/users")]
    public Task<IActionResult> Users(CancellationToken ct) => ProxyGetAsync("/api/users?page=1&pageSize=1000", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/collection-instances")]
    public Task<IActionResult> CollectionInstances([FromQuery] Guid companyId, CancellationToken ct)
    {
        if (companyId == Guid.Empty)
        {
            return Task.FromResult<IActionResult>(UnprocessableJson("company_required"));
        }

        return ProxyGetAsync(
            $"{ApiRoot}/collection-instances?companyId={companyId:D}&requiredAction=View",
            ct);
    }

    [HttpGet("/DocumentManagement/MasterRegister/api/corporate-collection-instances")]
    public Task<IActionResult> CorporateCollectionInstances(
        [FromQuery] Guid? corporateOwnerId,
        CancellationToken ct)
    {
        var suffix = corporateOwnerId is { } ownerId && ownerId != Guid.Empty
            ? $"?corporateOwnerId={ownerId:D}"
            : string.Empty;
        return ProxyGetAsync($"{ApiRoot}/corporate-collection-instances{suffix}", ct);
    }

    [HttpGet("/DocumentManagement/MasterRegister/api/governed-languages")]
    public Task<IActionResult> GovernedLanguages(CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/controlled-document-registrations/governed-languages", ct);

    /// <summary>
    /// Business Reference Data (PSS-012) published values, feeding the governed metadata dropdowns. Same-origin proxy
    /// → Gateway → Platform, mirroring the MOD-0028-FU04 QMS baseline designer. Option lists are never hardcoded in
    /// the view or the script; the set code is resolved server-side from configuration.
    /// </summary>
    [HttpGet("/DocumentManagement/MasterRegister/api/reference-data/{setCode}")]
    public Task<IActionResult> ReferenceData(string setCode, CancellationToken ct)
    {
        // A tenant/company/region-scoped set REJECTS a request without scope_key ("scope_key_required"), while a
        // global set REJECTS one with it ("scope_key_not_allowed_for_global"). We cannot know the set's scope here,
        // so the first attempt is the global shape and a scope-key retry follows — with the tenant id resolved
        // SERVER-SIDE from the auth context, never accepted from the browser.
        return ReferenceDataWithScopeFallbackAsync(setCode, ct);
    }

    private async Task<IActionResult> ReferenceDataWithScopeFallbackAsync(string setCode, CancellationToken ct)
    {
        var basePath = $"/api/v1/reference-data/sets/{Uri.EscapeDataString(setCode)}/published-values";
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        var tenantId = GetTenantId(token);

        var first = await ProxyGetAsync(basePath, ct);
        if (string.IsNullOrWhiteSpace(tenantId) || !IsScopeKeyFailure(first))
        {
            return first;
        }

        return await ProxyGetAsync($"{basePath}?scope_key={Uri.EscapeDataString(tenantId)}", ct);
    }

    private static bool IsScopeKeyFailure(IActionResult result) =>
        result is ContentResult { StatusCode: >= 400 } content
        && content.Content?.Contains("scope_key_required", StringComparison.OrdinalIgnoreCase) == true;

    [HttpPost("/DocumentManagement/MasterRegister/api/create")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CreateEntry([FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, ApiBase, ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/update/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateEntry(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"{ApiBase}/{id}", ParsePayload(payloadJson), ct);

    // ── MOD-0029-FU36B — unified controlled-document registration ───────────
    //
    // The browser sends multipart FormData so file bytes never become browser-side base64 state. This MVC boundary
    // adapts IFormFile to the FU36A JSON contract in memory, then forwards only through Gateway with server-resolved
    // auth and tenant headers. Orchestration, idempotency and compensation remain backend-owned.

    [HttpPost("/DocumentManagement/MasterRegister/api/controlled-document-registrations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateControlledDocumentRegistration(
        IFormFile? initialFile,
        [FromForm] string payloadJson,
        CancellationToken ct)
    {
        var payload = await BuildRegistrationPayloadAsync(initialFile, payloadJson, ct);
        if (payload is null)
        {
            return UnprocessableJson("invalid_registration_upload");
        }

        return await ProxyJsonAsync(
            HttpMethod.Post,
            $"{ApiRoot}/controlled-document-registrations",
            payload,
            ct);
    }

    [HttpGet("/DocumentManagement/MasterRegister/api/controlled-document-registrations/{operationId:guid}")]
    public Task<IActionResult> ControlledDocumentRegistrationOperation(Guid operationId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/controlled-document-registrations/{operationId:D}", ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/controlled-document-registrations/{operationId:guid}/retry")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RetryControlledDocumentRegistration(Guid operationId, CancellationToken ct) =>
        ProxyJsonAsync(
            HttpMethod.Post,
            $"{ApiRoot}/controlled-document-registrations/{operationId:D}/retry",
            new { },
            ct);

    // The register owns this relationship. Candidate reads remain permission-filtered by the authoritative
    // Controlled Documents API; TenantId is resolved server-side and is never accepted from the browser.
    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/controlled-documents")]
    public Task<IActionResult> ControlledDocuments(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/controlled-documents", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/controlled-documents/{controlledDocumentId:guid}")]
    public Task<IActionResult> ControlledDocumentDetail(Guid id, Guid controlledDocumentId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/controlled-documents/{controlledDocumentId:D}", ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/controlled-document/link")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> LinkControlledDocument(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id:D}/link-controlled-document", ParsePayload(payloadJson), ct);

    // ── MOD-0029-FU25 — Identifiers tab (FU07 backend) ───────────────────────
    //
    // Backend route reality (audited, NOT what a naive mapping would guess):
    //   • allocate-uid / allocate-code / allocate-identifiers hang off document-master-register/{id}
    //   • reserve + cancel + list live on the SEPARATE document-identifiers collection resource
    //   • there is NO dedicated ledger endpoint — the allocation list for the entry IS the append-only ledger
    // The stable UI-facing proxy paths below absorb that asymmetry so the browser sees one coherent resource.

    /// <summary>FU07 allocation ledger for one register entry (append-and-status-change only; nothing is ever deleted).</summary>
    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/identifiers")]
    public Task<IActionResult> Identifiers(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/document-identifiers?registerEntryId={id:D}", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/identifiers/ledger")]
    public Task<IActionResult> IdentifierLedger(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/document-identifiers?registerEntryId={id:D}", ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/identifiers/allocate-uid")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AllocateUid(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/allocate-uid", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/identifiers/allocate-code")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AllocateCode(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/allocate-code", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/identifiers/allocate-both")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AllocateBoth(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/allocate-identifiers", ParsePayload(payloadJson), ct);

    /// <summary>
    /// Manual / migration reservation. <c>registerEntryId</c> is forced SERVER-SIDE from the route so the browser
    /// cannot reserve an identifier against a different register entry than the one it is looking at.
    /// </summary>
    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/identifiers/reserve")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ReserveIdentifier(Guid id, [FromForm] string payloadJson, CancellationToken ct)
    {
        var payload = ParsePayload(payloadJson) as IDictionary<string, object?> ?? new Dictionary<string, object?>();
        payload["registerEntryId"] = id.ToString("D");
        return ProxyJsonAsync(HttpMethod.Post, $"{ApiRoot}/document-identifiers/reserve", payload, ct);
    }

    /// <summary>
    /// Cancels an identifier ALLOCATION (the value is retained forever and never reused — SOP §6.3). This is not a
    /// delete: no document, register entry or ledger row is removed.
    /// </summary>
    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/identifiers/cancel/{allocationId:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CancelIdentifierAllocation(Guid id, Guid allocationId, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiRoot}/document-identifiers/{allocationId}/cancel", ParsePayload(payloadJson), ct);

    // ── MOD-0029-FU25 — Lifecycle tab (FU08 / FU08A backend) ─────────────────
    //
    // The backend exposes ONE generic transition endpoint; Mark Effective / Supersede / Retire are target STATUSES,
    // not separate endpoints. The three convenience proxies below pin TargetStatus server-side and forward to that
    // same endpoint — the state machine, approval gate, release gate, identifier and effective-date guards all still
    // run in the backend. Nothing here bypasses or pre-satisfies a guard.

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/lifecycle/state")]
    public Task<IActionResult> LifecycleState(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}/lifecycle", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/lifecycle/history")]
    public Task<IActionResult> LifecycleHistory(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}/lifecycle/transitions", ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/lifecycle/transition")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> LifecycleTransition(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/lifecycle/transition", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/lifecycle/mark-effective")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> LifecycleMarkEffective(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/lifecycle/transition", WithTargetStatus(payloadJson, "Effective"), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/lifecycle/supersede")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> LifecycleSupersede(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/lifecycle/transition", WithTargetStatus(payloadJson, "Superseded"), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/lifecycle/retire")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> LifecycleRetire(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/lifecycle/transition", WithTargetStatus(payloadJson, "Retired"), ct);

    private object WithTargetStatus(string payloadJson, string targetStatus)
    {
        var payload = ParsePayload(payloadJson) as IDictionary<string, object?> ?? new Dictionary<string, object?>();
        // Drop any client-supplied target status (in any casing) before pinning ours — the route decides, not the caller.
        foreach (var key in payload.Keys.Where(k => string.Equals(k, "targetStatus", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            payload.Remove(key);
        }

        payload["targetStatus"] = targetStatus;
        return payload;
    }

    // ── MOD-0029-FU26 — Approval tab (FU09 backend) ──────────────────────────
    //
    // Backend route reality: the resources are `approval-route/resolve`, `approval-requirements`,
    // `approval-readiness` and `approval-evidence[/reject]` — NOT a single /approval/* tree. There is no evidence
    // LIST endpoint; per-requirement evidence state travels on the requirement rows themselves.
    // Resolving a route computes REQUIREMENTS only — it approves nothing.

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/approval/requirements")]
    public Task<IActionResult> ApprovalRequirements(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}/approval-requirements", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/approval/readiness")]
    public Task<IActionResult> ApprovalReadiness(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}/approval-readiness", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/approval/roles")]
    public Task<IActionResult> ApprovalRoles(Guid id, CancellationToken ct) =>
        ProxyGetAsync("/api/roles", ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/approval/resolve")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ApprovalResolve(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/approval-route/resolve", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/approval/evidence/record")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ApprovalRecordEvidence(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/approval-evidence", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/approval/evidence/reject")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ApprovalRejectEvidence(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/approval-evidence/reject", ParsePayload(payloadJson), ct);

    // ── MOD-0029-FU26 — Release Gates tab (FU10 backend) ─────────────────────
    //
    // Two different reads on purpose:
    //   • release-readiness  → recomputes the six gates WITHOUT persisting an evaluation (safe for tab open),
    //   • release-gates      → the last PERSISTED evaluation.
    // The tab opens on readiness so simply looking at a document never writes an audit record; the explicit
    // Evaluate button is what persists. Neither marks the document effective — that stays a lifecycle transition.

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/release-gates/readiness")]
    public Task<IActionResult> ReleaseGateReadiness(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}/release-readiness", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/release-gates")]
    public Task<IActionResult> ReleaseGatesLatest(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}/release-gates", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/release-gates/history")]
    public Task<IActionResult> ReleaseGatesHistory(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}/release-gates/history", ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/release-gates/evaluate")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ReleaseGatesEvaluate(Guid id, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/release-gates/evaluate", new { }, ct);

    /// <summary>
    /// Records EVIDENCE for one gate. The gate RESULT is always recomputed by the engine — the client cannot set a
    /// result, and the exception/waiver field is permanently false server-side and is not exposed here at all.
    /// </summary>
    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/release-gates/{gateKey}/evidence")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ReleaseGateRecordEvidence(Guid id, string gateKey, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/release-gates/{Uri.EscapeDataString(gateKey)}/evidence", ParsePayload(payloadJson), ct);

    // ── MOD-0029-FU27 — Training tab (FU11 backend) ──────────────────────────
    //
    // Backend route reality: `training-matrix/resolve`, `training-matrix/requirements`, `training-assignments[/{id}/…]`
    // and `training-readiness`. Three things the FU11 surface does NOT have, so nothing here invents them:
    //   • no GET for assignments (only requirements and the aggregate readiness counters are listable),
    //   • no bulk "generate assignments" (assignment is one requirement at a time),
    //   • no unrestrict (restriction is recorded, not toggled).
    // Training readiness feeds release gate 5; recording training evidence here never passes that gate.

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/training/readiness")]
    public Task<IActionResult> TrainingReadiness(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}/training-readiness", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/training/requirements")]
    public Task<IActionResult> TrainingRequirements(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}/training-matrix/requirements", ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/training/resolve")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> TrainingResolve(Guid id, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/training-matrix/resolve", new { }, ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/training/assignments")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> TrainingAssign(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/training-assignments", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/training/assignments/{assignmentId:guid}/complete")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> TrainingComplete(Guid id, Guid assignmentId, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/training-assignments/{assignmentId}/complete", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/training/assignments/{assignmentId:guid}/effectiveness")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> TrainingEffectiveness(Guid id, Guid assignmentId, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/training-assignments/{assignmentId}/effectiveness", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/training/assignments/{assignmentId:guid}/restrict")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> TrainingRestrict(Guid id, Guid assignmentId, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/training-assignments/{assignmentId}/restrict", ParsePayload(payloadJson), ct);

    // ── MOD-0029-FU28 — Repository assessment (FU16 backend) ─────────────────
    //
    // Repository assessments are TENANT-GLOBAL master data (`repository-assessments`), not children of a register
    // entry; a register entry points at one of them through `repository-assessment/link`. The evaluate call returns
    // the boundary readiness (CanSupportReleaseGate / CanSupportRegulatedESignature / BoundaryStatement) that FU10
    // gate 2 consumes — evaluating classifies, it never approves, and nothing here asserts DMS validation.

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/repository/linked")]
    public Task<IActionResult> RepositoryLinked(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}/repository-assessment", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/repository/assessments")]
    public Task<IActionResult> RepositoryAssessments(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/repository-assessments", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/repository/assessments/{assessmentId:guid}")]
    public Task<IActionResult> RepositoryAssessmentDetail(Guid id, Guid assessmentId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/repository-assessments/{assessmentId}", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/repository/assessments/{assessmentId:guid}/findings")]
    public Task<IActionResult> RepositoryAssessmentFindings(Guid id, Guid assessmentId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/repository-assessments/{assessmentId}/findings", ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/repository/assessments/{assessmentId:guid}/evaluate")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RepositoryEvaluate(Guid id, Guid assessmentId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiRoot}/repository-assessments/{assessmentId}/evaluate", new { }, ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/repository/assessments/{assessmentId:guid}/approve")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RepositoryApprove(Guid id, Guid assessmentId, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiRoot}/repository-assessments/{assessmentId}/approve", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/repository/assessments/{assessmentId:guid}/reject")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RepositoryReject(Guid id, Guid assessmentId, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiRoot}/repository-assessments/{assessmentId}/reject", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/repository/link")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RepositoryLink(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/repository-assessment/link", ParsePayload(payloadJson), ct);

    // ── MOD-0029-FU28 — Controlled copy / obsolete reconciliation (FU17) ──────
    //
    // Withdraw / reconcile / mark-missing / mark-obsolete are STATUS transitions on a retained log row — the
    // Controlled Copy Log is never deleted from. Readiness and findings feed FU10 gate 6; recording copy evidence
    // here never passes that gate.

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/controlled-copies")]
    public Task<IActionResult> ControlledCopies(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}/controlled-copies", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/controlled-copies/readiness")]
    public Task<IActionResult> CopyWithdrawalReadiness(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}/copy-withdrawal-readiness", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/controlled-copies/plans")]
    public Task<IActionResult> CopyWithdrawalPlans(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}/copy-withdrawal-plans", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/controlled-copies/findings")]
    public Task<IActionResult> ObsoleteCopyFindings(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}/obsolete-copy-findings", ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/controlled-copies/register")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RegisterControlledCopy(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/controlled-copies", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/controlled-copies/{copyId:guid}/withdraw")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> WithdrawControlledCopy(Guid id, Guid copyId, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/controlled-copies/{copyId}/withdraw", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/controlled-copies/{copyId:guid}/reconcile")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ReconcileControlledCopy(Guid id, Guid copyId, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/controlled-copies/{copyId}/reconcile", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/controlled-copies/{copyId:guid}/mark-missing")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MarkControlledCopyMissing(Guid id, Guid copyId, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/controlled-copies/{copyId}/mark-missing", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/controlled-copies/{copyId:guid}/mark-obsolete")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MarkControlledCopyObsolete(Guid id, Guid copyId, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/controlled-copies/{copyId}/mark-obsolete", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/controlled-copies/plans/generate")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> GenerateWithdrawalPlan(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/copy-withdrawal-plans/generate", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/controlled-copies/plans/{planId:guid}/complete")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CompleteWithdrawalPlan(Guid id, Guid planId, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/copy-withdrawal-plans/{planId}/complete", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/controlled-copies/reconciliation/evaluate")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EvaluateObsoleteReconciliation(Guid id, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/obsolete-copy-reconciliation/evaluate", new { }, ct);

    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/controlled-copies/findings/{findingId:guid}/resolve")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ResolveObsoleteFinding(Guid id, Guid findingId, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/obsolete-copy-findings/{findingId}/resolve", ParsePayload(payloadJson), ct);

    // ── MOD-0029-FU29 — Retention tab (FU15 backend) ─────────────────────────
    //
    // Backend route reality (audited): retention is TENANT-GLOBAL master data, NOT a child of a register entry.
    //   • the per-subject schedule lives on retention/subjects/{subjectType}/{subjectId} — for a register entry the
    //     subject type is the DomainEnum name "DocumentMasterRegisterEntry" and the subject id IS the entry id,
    //   • legal-holds and disposition-requests are global lists; each row carries a RegisterEntryId(s), so the UI
    //     narrows them to THIS entry client-side (same pattern the FU24 List proxy already documents).
    // Evaluate is opt-in and idempotent; it recomputes eligibility + legal-hold block, it disposes of nothing. The
    // subjectType/subjectId/registerEntryId are pinned SERVER-SIDE from the route so the browser cannot retarget it.
    // No apply/release/dispose is exposed here — those dual-evidence flows stay on the dedicated retention screen.

    private const string RetentionSubjectType = "DocumentMasterRegisterEntry";

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/retention/subject")]
    public Task<IActionResult> RetentionSubject(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/retention/subjects/{RetentionSubjectType}/{id:D}", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/retention/legal-holds")]
    public Task<IActionResult> RetentionLegalHolds(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/legal-holds", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/retention/dispositions")]
    public Task<IActionResult> RetentionDispositions(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/disposition-requests", ct);

    /// <summary>Opt-in retention evaluation. Subject identity is forced from the route — the browser sends nothing.</summary>
    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/retention/evaluate")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RetentionEvaluate(Guid id, [FromForm] string payloadJson, CancellationToken ct)
    {
        var payload = ParsePayload(payloadJson) as IDictionary<string, object?> ?? new Dictionary<string, object?>();
        payload["subjectType"] = RetentionSubjectType;
        payload["subjectId"] = id.ToString("D");
        payload["registerEntryId"] = id.ToString("D");
        return ProxyJsonAsync(HttpMethod.Post, $"{ApiRoot}/retention/evaluate", payload, ct);
    }

    // ── MOD-0029-FU29 — Signatures tab (FU23 backend) ────────────────────────
    //
    // Backend route reality: signatures key off EVIDENCE subjects (approval evidence, release-gate evidence, …),
    // not off the register entry itself — there is no "sign the register entry". Each policy/request/record is a
    // global list row carrying a RegisterEntryId, so the tab reads the global lists and narrows to THIS entry
    // client-side. Verify recomputes the stored canonical fingerprint (integrity only — no provider, no cert, no
    // compliance claim). No sign / create-request is exposed here: that needs an evidence-subject the register
    // sub-tab does not own, and the sign authority is a materially heavier one kept off this screen.

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/signatures/policies")]
    public Task<IActionResult> SignaturePolicies(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/signature-policies", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/signatures/requests")]
    public Task<IActionResult> SignatureRequests(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/signature-requests", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/signatures/records")]
    public Task<IActionResult> SignatureRecords(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/signatures", ct);

    /// <summary>Recomputes and compares the stored canonical metadata fingerprint. Integrity check only.</summary>
    [HttpPost("/DocumentManagement/MasterRegister/api/{id:guid}/signatures/{signatureId:guid}/verify")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SignatureVerify(Guid id, Guid signatureId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiRoot}/signatures/{signatureId}/verify", new { }, ct);

    // ── MOD-0029-FU29 — Quality Events tab (FU22 backend) ────────────────────
    //
    // Backend route reality: quality events / deviations / CAPA actions are global lists. Events carry a
    // RegisterEntryId; deviations hang off a QualityEventId; CAPA actions carry RelatedRegisterEntryIds and/or a
    // QualityEventId/DeviationId. The tab reads all three global lists and stitches them to THIS entry client-side.
    // Read-only on purpose: linking/creating/closing a quality record is a heavier QMS-bridge authoring flow kept
    // off the register sub-tab, and there is no safe "link an existing event to this entry" or "refresh link" verb.

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/quality-events")]
    public Task<IActionResult> QualityEvents(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/quality-events", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/quality-events/deviations")]
    public Task<IActionResult> QualityDeviations(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/deviations", ct);

    [HttpGet("/DocumentManagement/MasterRegister/api/{id:guid}/quality-events/capa")]
    public Task<IActionResult> QualityCapaActions(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiRoot}/capa-actions", ct);

    // ── Proxy plumbing (mirrors the Template Masters proxy profile) ──────────

    private object ParsePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new { };
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            return JsonElementToObject(doc.RootElement) ?? new { };
        }
        catch (JsonException)
        {
            return new { };
        }
    }

    private static object? JsonElementToObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };

    private static async Task<object?> BuildRegistrationPayloadAsync(
        IFormFile? initialFile,
        string payloadJson,
        CancellationToken ct)
    {
        if (initialFile is null || initialFile.Length == 0 || string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var payload = document.RootElement
                .EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => JsonElementToObject(property.Value),
                    StringComparer.OrdinalIgnoreCase);

            await using var source = initialFile.OpenReadStream();
            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, ct);
            payload["initialFile"] = new
            {
                fileName = Path.GetFileName(initialFile.FileName),
                mediaType = string.IsNullOrWhiteSpace(initialFile.ContentType)
                    ? "application/octet-stream"
                    : initialFile.ContentType,
                contentBase64 = Convert.ToBase64String(buffer.ToArray())
            };
            return payload;
        }
        catch (JsonException)
        {
            return null;
        }
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
            _logger.LogError(ex, "Master register proxy GET {Path} failed.", path);
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
            _logger.LogError(ex, "Master register proxy {Method} {Path} failed.", method, path);
            return GatewayErrorJson();
        }
    }

    // MOD-0029-FU04C — navigation 401/403 → friendly Not Authorized page; AJAX keeps the JSON envelope for a toast.
    private Task<IActionResult> PassthroughAsync(HttpResponseMessage response, CancellationToken ct) =>
        Diten.Web.Infrastructure.TenantShellProxyResponse.PassthroughAsync(response, Request, ct);

    private IActionResult UnauthorizedJson() => JsonFailure(401, "UNAUTHORIZED", _sharedLocalizer["Unauthorized"].Value);
    private IActionResult GatewayErrorJson() => JsonFailure(502, "GATEWAY_ERROR", _sharedLocalizer["GatewayError"].Value);
    private IActionResult UnprocessableJson(string reasonCode) =>
        JsonFailure(422, reasonCode, _sharedLocalizer["ValidationFailed"].Value);

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

    private bool AddAuthHeaders(HttpRequestMessage request)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Tenant id is resolved SERVER-SIDE from the signed-in principal / access token — never accepted from the client.
        var tenantId = GetTenantId(token);
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);
        return true;
    }

    private string? GetTenantId(string? accessToken)
    {
        var claimValue = User.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase) ||
            x.Type.EndsWith("/tenant_id", StringComparison.OrdinalIgnoreCase))?.Value;
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
            return token.Claims.FirstOrDefault(x =>
                string.Equals(x.Type, "tenantId", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Type, "tenant_id", StringComparison.OrdinalIgnoreCase) ||
                x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase) ||
                x.Type.EndsWith("/tenant_id", StringComparison.OrdinalIgnoreCase))?.Value;
        }
        catch
        {
            return null;
        }
    }
}
