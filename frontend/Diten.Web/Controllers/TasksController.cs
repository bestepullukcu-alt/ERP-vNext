using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0024 — the Task Engine tenant surface plus its same-origin API proxy.
///
/// <para>The browser never addresses a service port: it calls <c>/Tasks/api/*</c> on this app, and the JWT is read
/// server-side from the HTTP-only auth cookie (never exposed to JS). The proxy path deliberately avoids
/// <c>api/tasks</c>, which the frozen legacy <c>TaskApiController</c> owns.</para>
/// </summary>
[Authorize]
[Route("Tasks")]
public sealed class TasksController : Controller
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string CorrelationHeaderName = "X-Correlation-Id";

    /// <summary>The Task Center — the single personal entry point for work.</summary>
    private const string WorkCenterUrl = "/WorkCenterNext";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _gatewayUrl;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TasksController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
        _logger = logger;
    }

    // ── Views (Golden Reference Compact: separate Create/Edit/Details pages) ──

    /// <summary>
    /// The Task Center (/WorkCenterNext) is the ONE personal work list, so /Tasks deliberately renders no list of
    /// its own — a second list is two places to disagree about the same work, and a competing "Tasks" surface
    /// fragments the entry point. The route is kept (permission assignment and the manifest's ParentPageCode chain
    /// hang off PageTasks) and simply forwards; only the surface behaviour changed.
    /// </summary>
    /// <remarks>
    /// A 302, not a 301: browsers cache a permanent redirect indefinitely, which would make reversing this
    /// product decision impossible for anyone who had already visited the page once.
    /// </remarks>
    [HttpGet("")]
    public IActionResult Index() => Redirect(WorkCenterUrl);

    /// <summary>
    /// The full create form, optionally opened as a SUBTASK of an existing task and told where to go back to.
    /// </summary>
    /// <remarks>
    /// WHY THE PARAMETERS EXIST (2026-08-24). Measured, there are three create gates and they offer different
    /// numbers of fields: the inline box (1 field, the rest inherited from the parent), the detail panel
    /// (5 fields), and this page (20). Only this one renders <c>#taskCustomFields</c>, which is populated at
    /// runtime from <c>TaskFieldDefinition</c> — so the day a tenant defines a REQUIRED custom field, the two
    /// shortcuts cannot collect it and this page can. The panel therefore needs a door to here.
    ///
    /// <para>
    /// ⚠ <paramref name="returnUrl"/> IS NOT TRUSTED. <see cref="Url.IsLocalUrl"/> is the gate: an absolute URL,
    /// a protocol-relative <c>//evil.example</c> or anything else off-site is dropped and the caller falls back
    /// to the task list. An unchecked return parameter is an open-redirect, and this one is reachable from a
    /// link a user can be handed.
    /// </para>
    /// </remarks>
    [HttpGet("Create")]
    public IActionResult Create(Guid? parent, string? returnUrl)
    {
        ViewBag.ActiveMenu = "tasks";
        ViewData["ParentTaskId"] = parent?.ToString();
        ViewData["ReturnUrl"] = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : null;
        return View("~/Views/Tasks/Create.cshtml");
    }

    [HttpGet("{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewBag.ActiveMenu = "tasks";
        ViewData["TaskId"] = id.ToString();
        return View("~/Views/Tasks/Details.cshtml");
    }

    [HttpGet("{id:guid}/Edit")]
    public IActionResult Edit(Guid id)
    {
        ViewBag.ActiveMenu = "tasks";
        ViewData["TaskId"] = id.ToString();
        return View("~/Views/Tasks/Edit.cshtml");
    }

    // ── Same-origin API proxy ────────────────────────────────────────────────

    [HttpGet("api/list")]
    public Task<IActionResult> ApiList()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks", readBody: false);

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> ApiGet(Guid id)
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/{id}", readBody: false);

    [HttpPost("api")]
    public Task<IActionResult> ApiCreate()
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks", readBody: true);

    [HttpPut("api/{id:guid}")]
    public Task<IActionResult> ApiUpdate(Guid id)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/tasks/{id}", readBody: true);

    [HttpDelete("api/{id:guid}")]
    public Task<IActionResult> ApiDelete(Guid id)
        => ProxyAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/v1/tasks/{id}", readBody: false);

    /// <summary>
    /// Lifecycle/ownership transitions. The accepted set is <see cref="TaskTransitionRoutes.All"/>; the literal
    /// below must stay identical to <see cref="TaskTransitionRoutes.Pattern"/>, which TaskTransitionRouteTests
    /// asserts — a route constraint has to be a compile-time constant, so the compiler cannot do it for us.
    ///
    /// <para>A code missing here is NOT a missing feature, it is a 404 on a button the user can see: the client
    /// turns a projected action code straight into this URL. That is how <c>inquire</c> shipped unreachable.</para>
    ///
    /// <para>The route parameter is named <c>transition</c>, not <c>action</c>: <c>action</c> is reserved by MVC
    /// routing and combining it with a constraint fails endpoint construction at startup.</para>
    /// </summary>
    [HttpPost("api/{id:guid}/{transition:regex(^(accept|claim|release|plan|start|inquire|submitReview|return|reassign|complete|cancel)$)}")]
    public Task<IActionResult> ApiTransition(Guid id, string transition)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/{id}/{transition}", readBody: true);

    // ── Configurable field definitions (Phase 5) ─────────────────────────────
    //
    // Their own resource, so NOT transition codes and not in TaskTransitionRoutes — each one has to be listed
    // here by hand. A route Platform exposes and this proxy does not answers 404 inside the web tier, which is
    // how `inquire` shipped unreachable.

    [HttpGet("api/field-definitions")]
    public Task<IActionResult> ApiFieldDefinitions()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/field-definitions", readBody: false);

    // One field's option list. Declared BEFORE the {id:guid} route so the two cannot be confused, and taking a
    // code rather than an id because the form knows definitions by code — the same key the stored values join on.
    [HttpGet("api/field-definitions/{code}/options")]
    public Task<IActionResult> ApiFieldDefinitionOptions(string code)
        => ProxyAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}/api/v1/tasks/field-definitions/{Uri.EscapeDataString(code)}/options",
            readBody: false);

    /// <summary>
    /// One field's records, searched in the module that owns them. The query string is forwarded WHOLE — the
    /// term, the ids and the cap are Platform's contract, not this tier's, and re-listing them here is how a
    /// parameter gets dropped silently.
    /// </summary>
    [HttpGet("api/field-definitions/{code}/records")]
    public Task<IActionResult> ApiFieldDefinitionRecords(string code)
        => ProxyAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}/api/v1/tasks/field-definitions/{Uri.EscapeDataString(code)}/records"
            + Request.QueryString.Value,
            readBody: false);

    /// <summary>
    /// The sources an administrator may choose from on the field-definition screen. Declared before the
    /// {id:guid} route for the same reason the two above are: "option-sources" is not a Guid, but a route that
    /// only fails at match time is a route nobody notices until the screen is empty.
    /// </summary>
    [HttpGet("api/field-definitions/option-sources")]
    public Task<IActionResult> ApiFieldOptionSources()
        => ProxyAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}/api/v1/tasks/field-definitions/option-sources{Request.QueryString.Value}",
            readBody: false);

    [HttpGet("api/field-definitions/{id:guid}")]
    public Task<IActionResult> ApiFieldDefinition(Guid id)
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/field-definitions/{id}", readBody: false);

    [HttpPost("api/field-definitions")]
    public Task<IActionResult> ApiCreateFieldDefinition()
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/field-definitions", readBody: true);

    [HttpPut("api/field-definitions/{id:guid}")]
    public Task<IActionResult> ApiUpdateFieldDefinition(Guid id)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/tasks/field-definitions/{id}", readBody: true);

    // The bulk retire. It exists here because it did NOT, and the button was live: the user could select rows,
    // press "Bulk delete", and the request 404'd inside the web tier. Second time in this module — `inquire` was
    // the first.
    [HttpPost("api/field-definitions/bulk-delete")]
    public Task<IActionResult> ApiBulkDeleteFieldDefinitions()
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/field-definitions/bulk-delete", readBody: true);

    [HttpDelete("api/field-definitions/{id:guid}")]
    public Task<IActionResult> ApiDeleteFieldDefinition(Guid id)
        => ProxyAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/v1/tasks/field-definitions/{id}", readBody: false);

    // ── Recurrence rules (Phase 4) ───────────────────────────────────────────
    //
    // Their own resource, so NOT transition codes and not in TaskTransitionRoutes — and therefore each one has
    // to be listed here by hand. A route Platform exposes and this proxy does not answers 404 inside the web
    // tier, which is how `inquire` shipped unreachable.

    [HttpGet("api/recurrence-rules")]
    public Task<IActionResult> ApiRecurrenceRules()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/recurrence-rules", readBody: false);

    [HttpGet("api/recurrence-rules/{id:guid}")]
    public Task<IActionResult> ApiRecurrenceRule(Guid id)
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/recurrence-rules/{id}", readBody: false);

    [HttpPost("api/recurrence-rules")]
    public Task<IActionResult> ApiCreateRecurrenceRule()
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/recurrence-rules", readBody: true);

    [HttpPut("api/recurrence-rules/{id:guid}")]
    public Task<IActionResult> ApiUpdateRecurrenceRule(Guid id)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/tasks/recurrence-rules/{id}", readBody: true);

    [HttpDelete("api/recurrence-rules/{id:guid}")]
    public Task<IActionResult> ApiDeleteRecurrenceRule(Guid id)
        => ProxyAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/v1/tasks/recurrence-rules/{id}", readBody: false);

    // ── The template chain (BL-054) ──────────────────────────────────────────
    //
    // The rule screen's template picker had a lookup and no source; these are the routes behind the two screens
    // that fill it. Listed BY HAND for the same reason as everything above: a path Platform serves and this
    // proxy does not answers 404 inside the web tier, before the request ever leaves it.

    [HttpGet("api/checklist-templates")]
    public Task<IActionResult> ApiChecklistTemplates()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/checklist-templates", readBody: false);

    [HttpGet("api/checklist-templates/{id:guid}")]
    public Task<IActionResult> ApiChecklistTemplate(Guid id)
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/checklist-templates/{id}", readBody: false);

    [HttpPost("api/checklist-templates")]
    public Task<IActionResult> ApiCreateChecklistTemplate()
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/checklist-templates", readBody: true);

    [HttpPut("api/checklist-templates/{id:guid}")]
    public Task<IActionResult> ApiUpdateChecklistTemplate(Guid id)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/tasks/checklist-templates/{id}", readBody: true);

    [HttpDelete("api/checklist-templates/{id:guid}")]
    public Task<IActionResult> ApiDeleteChecklistTemplate(Guid id)
        => ProxyAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/v1/tasks/checklist-templates/{id}", readBody: false);

    /// <summary>
    /// The checklist picker on the TASK-TEMPLATE form. A different route from the list above and deliberately so:
    /// a picker offers only what may still be bound, while the management list has to show a paused template or
    /// it could never be switched back on.
    /// </summary>
    [HttpGet("api/checklist-template-lookup")]
    public Task<IActionResult> ApiChecklistTemplateLookup()
        => ProxyAsync(
            HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/lookups/checklist-templates", readBody: false);

    [HttpGet("api/templates")]
    public Task<IActionResult> ApiTemplates()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/templates", readBody: false);

    [HttpGet("api/templates/{id:guid}")]
    public Task<IActionResult> ApiTemplate(Guid id)
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/templates/{id}", readBody: false);

    [HttpPost("api/templates")]
    public Task<IActionResult> ApiCreateTemplate()
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/templates", readBody: true);

    [HttpPut("api/templates/{id:guid}")]
    public Task<IActionResult> ApiUpdateTemplate(Guid id)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/tasks/templates/{id}", readBody: true);

    [HttpDelete("api/templates/{id:guid}")]
    public Task<IActionResult> ApiDeleteTemplate(Guid id)
        => ProxyAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/v1/tasks/templates/{id}", readBody: false);

    /// <summary>
    /// The COMPANY picker on the task-template form, fed by MDM's referenceable-only lookup — the same feed the
    /// organization-unit form uses, so a template cannot name a company that form could not.
    ///
    /// <para>⚠ Left EMPTY means every company, and that is a supported answer rather than a missing one. The
    /// field is a single id and never a list: a multi-select would have to be revisited for every template on the
    /// day a new company is opened, and nobody does that — so it would come to mean "the companies we had when
    /// somebody last looked".</para>
    /// </summary>
    [HttpGet("api/legal-entities")]
    public Task<IActionResult> ApiLegalEntities()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/legal-entities/lookup", readBody: false);

    /// <summary>
    /// Assignable positions. Carries the organization unit code+name so the picker renders
    /// "QA Specialist — Facility A"; without it a pooled task can silently reach the wrong facility.
    /// </summary>
    [HttpGet("api/assignable-positions")]
    public Task<IActionResult> ApiAssignablePositions()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/lookups/assignable-positions", readBody: false);

    // ── Phase 2: checklist + subtasks ────────────────────────────────────────

    /// <summary>Tick/untick a checklist item. Expected-version write against the checklist RUN.</summary>
    [HttpPost("api/{id:guid}/checklist/items/state")]
    public Task<IActionResult> ApiSetChecklistItemState(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/{id}/checklist/items/state", readBody: true);

    /// <summary>Add an ad-hoc checklist item (the user's own text — never a resource key).</summary>
    [HttpPost("api/{id:guid}/checklist/items")]
    public Task<IActionResult> ApiAddChecklistItem(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/{id}/checklist/items", readBody: true);

    /// <summary>Edit one checklist item — its text, its level, its evidence flag.</summary>
    [HttpPut("api/{id:guid}/checklist/items/{code}")]
    public Task<IActionResult> UpdateChecklistItem(Guid id, string code)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/tasks/{id}/checklist/items/{code}", readBody: true);

    /// <summary>Remove one checklist item.</summary>
    [HttpDelete("api/{id:guid}/checklist/items/{code}")]
    public Task<IActionResult> RemoveChecklistItem(Guid id, string code)
        => ProxyAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/v1/tasks/{id}/checklist/items/{code}", readBody: true);

    /// <summary>Write the whole checklist order in one call.</summary>
    [HttpPut("api/{id:guid}/checklist/order")]
    public Task<IActionResult> ReorderChecklist(Guid id)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/tasks/{id}/checklist/order", readBody: true);

    /// <summary>Create a task from a template; its checklist is instantiated server-side.</summary>
    [HttpPost("api/from-template")]
    public Task<IActionResult> ApiCreateFromTemplate()
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/from-template", readBody: true);

    /// <summary>
    /// Post a comment. Its own resource under a task, so it is NOT a transition code and not in
    /// TaskTransitionRoutes — but it still has to be listed here explicitly, or the composer posts into a 404 that
    /// never leaves the web tier. That is how `inquire` shipped unreachable.
    /// </summary>
    [HttpPost("api/{id:guid}/comments")]
    public Task<IActionResult> ApiAddComment(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/{id}/comments", readBody: true);

    /// <summary>Rewrite one's own comment. The AUTHOR check is the engine's; this only carries the call.</summary>
    [HttpPut("api/{id:guid}/comments/{commentId:guid}")]
    public Task<IActionResult> ApiUpdateComment(Guid id, Guid commentId)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/tasks/{id}/comments/{commentId}", readBody: true);

    /// <summary>Withdraw one's own comment — a tombstone on the engine's side, never a removal.</summary>
    [HttpDelete("api/{id:guid}/comments/{commentId:guid}")]
    public Task<IActionResult> ApiWithdrawComment(Guid id, Guid commentId)
        => ProxyAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/v1/tasks/{id}/comments/{commentId}", readBody: false);

    // ── The personal overlay (WC-1) ──────────────────────────────────────────
    //
    // Their own resource under a task, so they are NOT transition codes and not in TaskTransitionRoutes. Listed
    // here explicitly for the reason that list exists: a route that lives on Platform and not here answers 404
    // before the request ever leaves Diten.Web, which is how `inquire` shipped unreachable.

    /// <summary>Add one private note. Visible to its author and to nobody else — the server filters it.</summary>
    [HttpPost("api/{id:guid}/personal/notes")]
    public Task<IActionResult> ApiAddPersonalNote(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/{id}/personal/notes", readBody: true);

    /// <summary>Delete one of the caller's own notes.</summary>
    [HttpDelete("api/{id:guid}/personal/notes/{noteId:guid}")]
    public Task<IActionResult> ApiDeletePersonalNote(Guid id, Guid noteId)
        => ProxyAsync(
            HttpMethod.Delete,
            $"{_gatewayUrl}/api/v1/tasks/{id}/personal/notes/{noteId}",
            readBody: false);

    /// <summary>Set or clear the caller's own snooze. Never moves the task.</summary>
    [HttpPut("api/{id:guid}/personal/snooze")]
    public Task<IActionResult> ApiSetSnooze(Guid id)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/tasks/{id}/personal/snooze", readBody: true);

    /// <summary>
    /// Pin or unpin for the caller. Never moves the task — the same promise the snooze above makes.
    ///
    /// ⚠ THIS LINE IS WHY THE FIRST LIVE TEST RETURNED 404: this controller is a PROXY with one method per
    /// endpoint, so a route that exists on the service is still invisible to the browser until it is named
    /// here. Measured, not guessed — the request went out and came back 404 with the handler already in place.
    /// </summary>
    [HttpPut("api/{id:guid}/personal/pin")]
    public Task<IActionResult> ApiSetPinned(Guid id)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/tasks/{id}/personal/pin", readBody: true);

    /// <summary>The document-list management screen (DCP-005 slice 2).</summary>
    [HttpGet("DocumentList")]
    public IActionResult DocumentList()
    {
        ViewBag.ActiveMenu = "tasks";
        return View("~/Views/Tasks/DocumentList.cshtml");
    }

    /*
     * ── THE UPLOAD HOP IS MULTIPART; THE GATEWAY HOP IS BASE64 ────────────────────────────────────────────
     *
     * MEASURED before choosing (the brief asked): the taxonomy wizard's browser posts `multipart/form-data` to
     * ITS MVC controller, which base64s the bytes and forwards JSON to the gateway. So the two "different
     * transports" were never in conflict — they are two hops of the same path, and our gateway endpoint already
     * takes `ContentBase64` exactly as the precedent's does.
     *
     * The screen is aligned to the precedent rather than the endpoint changed, for two reasons: base64-ing a
     * file in the browser doubles it in memory for no gain, and the gateway contract is already committed.
     */
    [HttpPost("DocumentList/dry-run")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DocumentListDryRun(IFormFile? file, [FromForm] string? sourceKey, CancellationToken ct)
        => ForwardDocumentListAsync("dry-run", file, sourceKey, listVersion: null, ct);

    [HttpPost("DocumentList/import")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DocumentListImport(
        IFormFile? file, [FromForm] string? sourceKey, [FromForm] string? listVersion, CancellationToken ct)
        => ForwardDocumentListAsync("import", file, sourceKey, listVersion, ct);

    private async Task<IActionResult> ForwardDocumentListAsync(
        string action, IFormFile? file, string? sourceKey, string? listVersion, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return UnprocessableEntity(new { isSuccessful = false, errors = new[] { "invalid_document_list_upload" } });
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);

        var payload = new
        {
            fileName = file.FileName,
            contentBase64 = Convert.ToBase64String(buffer.ToArray()),
            sourceKey = sourceKey ?? string.Empty,
            listVersion = listVersion ?? string.Empty
        };

        return await ProxyAsync(
            HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/document-list/{action}", readBody: false, body: payload);
    }

    // ── DCP-005 slice 2: the controlled-document reference list ──────────
    //
    // ⚠ Named one by one because this controller is a PROXY: a route that exists on the service is invisible to
    // the browser until it is listed here. Measured the expensive way once already, on the pin endpoint.

    [HttpPost("api/document-list/dry-run")]
    public Task<IActionResult> ApiDocumentListDryRun()
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/document-list/dry-run", readBody: true);

    [HttpPost("api/document-list/import")]
    public Task<IActionResult> ApiDocumentListImport()
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/document-list/import", readBody: true);

    /// <summary>
    /// Take a list version out of service.
    ///
    /// ⚠ THIRD TIME THIS PROXY GAP HAS BITTEN (pin, task types, and now this). A route added to the SERVICE is
    /// invisible to the browser until it is named here, and the symptom is always the same: the button appears
    /// to do nothing. Measured again rather than assumed — the withdraw click 404'd with the handler in place.
    /// </summary>
    [HttpPut("api/document-list/versions/{id:guid}/withdraw")]
    public Task<IActionResult> ApiWithdrawDocumentListVersion(Guid id)
        => ProxyAsync(
            HttpMethod.Put,
            $"{_gatewayUrl}/api/v1/tasks/document-list/versions/{id}/withdraw",
            readBody: true);

    [HttpGet("api/document-list/versions")]
    public Task<IActionResult> ApiDocumentListVersions()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/document-list/versions", readBody: false);

    /// <summary>Search the current list. The query string travels; blocked rows come back and are shown.</summary>
    [HttpGet("api/document-list/search")]
    public Task<IActionResult> ApiDocumentListSearch([FromQuery] string? term, [FromQuery] int limit)
        => ProxyAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}/api/v1/tasks/document-list/search?term={Uri.EscapeDataString(term ?? string.Empty)}&limit={limit}",
            readBody: false);

    /// <summary>
    /// The task TYPES a new task may be given (DCP-005 slice 1).
    ///
    /// ⚠ Named here because this controller is a PROXY with one method per endpoint — a route that exists on the
    /// service is invisible to the browser until it is listed. That is not a guess: the pin endpoint returned 404
    /// on its first live click for exactly this reason.
    /// </summary>
    [HttpGet("api/task-types/active")]
    public Task<IActionResult> ApiActiveTaskTypes()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/task-types/active", readBody: false);

    /// <summary>
    /// DCP-005 slice 3 — what this type suggests citing.
    ///
    /// <para>⚠ THIS METHOD IS THE POINT. A route that exists on the service is INVISIBLE to the browser until
    /// it is named here; that gap has now produced a live 404 three times in this module (the pin, the task
    /// types, the withdrawal). Adding the service route without this one ships a feature nobody can reach.</para>
    /// </summary>
    [HttpGet("api/task-types/{id:guid}/governing-documents")]
    public Task<IActionResult> ApiTaskTypeGoverningDocuments(Guid id, [FromQuery] string? organizationCode)
        => ProxyAsync(
            HttpMethod.Get,
            $"{_gatewayUrl}/api/v1/tasks/task-types/{id}/governing-documents"
                + $"?organizationCode={Uri.EscapeDataString(organizationCode ?? string.Empty)}",
            readBody: false);

    /// <summary>Every type, retired ones included — the management grid reads this.</summary>
    [HttpGet("api/task-types")]
    public Task<IActionResult> ApiTaskTypes()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/task-types", readBody: false);

    /// <summary>
    /// Retire or restore a type.
    ///
    /// ⚠ THERE IS NO DELETE ROUTE HERE EITHER, and its absence is deliberate rather than pending: a used type is
    /// part of the identity of every task opened under it. The service exposes none, the grid offers none, and
    /// adding one here would be the only door left.
    /// </summary>
    [HttpPut("api/task-types/{id:guid}/active")]
    public Task<IActionResult> ApiSetTaskTypeActive(Guid id)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/tasks/task-types/{id}/active", readBody: true);

    // ── Dependencies (BL-028) ────────────────────────────────────────────────
    //
    // Not transitions, so they are NOT in TaskTransitionRoutes: these are their own resource under a task
    // (POST .../dependencies, DELETE .../dependencies/{id}) rather than a code appended to the task's URL. The
    // proxy still has to carry them explicitly — a route that exists on Platform and not here answers 404 before
    // the request ever leaves Diten.Web, which is exactly how `inquire` shipped unreachable.

    /// <summary>Add a typed dependency edge between two MOD-0024 tasks.</summary>
    [HttpPost("api/{id:guid}/dependencies")]
    public Task<IActionResult> ApiAddDependency(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/tasks/{id}/dependencies", readBody: true);

    /// <summary>Remove one dependency edge.</summary>
    [HttpDelete("api/{id:guid}/dependencies/{dependencyId:guid}")]
    public Task<IActionResult> ApiRemoveDependency(Guid id, Guid dependencyId)
        => ProxyAsync(
            HttpMethod.Delete,
            $"{_gatewayUrl}/api/v1/tasks/{id}/dependencies/{dependencyId}",
            readBody: false);

    /// <summary>
    /// People a task may be assigned to (whoever holds a position). Carries the display name, position and
    /// organization unit so the picker never has to show a user GUID.
    /// </summary>
    /// <summary>
    /// Templates a recurrence rule can be bound to (BL-052). Listed here BY HAND like every other non-transition
    /// route: a path Platform serves and this proxy does not answers 404 inside the web tier, which is how
    /// `inquire` once shipped unreachable.
    /// </summary>
    [HttpGet("api/task-templates")]
    public Task<IActionResult> ApiTaskTemplates()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/lookups/task-templates", readBody: false);

    [HttpGet("api/assignable-people")]
    public Task<IActionResult> ApiAssignablePeople()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/lookups/assignable-people", readBody: false);

    /// <summary>
    /// BL-057 — who may DECIDE about a task (approver, reviewer), as opposed to who may receive it. Listed by
    /// hand like every other non-transition route, for the reason above: a path Platform serves and this proxy
    /// does not answers 404 inside the web tier.
    /// </summary>
    /// <summary>BL-023 — would assigning to this person be an upward REQUEST rather than an order?</summary>
    [HttpGet("api/assignment-direction/{userId:guid}")]
    public Task<IActionResult> ApiAssignmentDirection(Guid userId)
        => ProxyAsync(HttpMethod.Get,
            $"{_gatewayUrl}/api/v1/tasks/lookups/assignment-direction/{userId}", readBody: false);

    [HttpGet("api/decision-makers")]
    public Task<IActionResult> ApiDecisionMakers()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/tasks/lookups/decision-makers", readBody: false);

    /// <summary>
    /// Forward to the gateway.
    ///
    /// <para><c>body</c> is for calls whose payload this controller BUILDS rather than relays — the document
    /// list's upload, where the browser sends a file and the gateway wants base64. `readBody` still relays the
    /// incoming stream for everything else; passing both would be two sources for one body.</para>
    /// </summary>
    private async Task<IActionResult> ProxyAsync(
        HttpMethod method, string targetUrl, bool readBody, object? body = null)
    {
        if (!TryCreateTenantRequest(method, targetUrl, out var request))
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        try
        {
            using (request)
            {
                if (body is not null)
                {
                    request.Content = new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                }
                else if (readBody)
                {
                    using var reader = new StreamReader(Request.Body, Encoding.UTF8);
                    var relayed = await reader.ReadToEndAsync(HttpContext.RequestAborted);
                    request.Content = new StringContent(
                        relayed, Encoding.UTF8, Request.ContentType ?? "application/json");
                }

                var client = _httpClientFactory.CreateClient();
                using var response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
                var content = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);

                // Pass the upstream status through verbatim: a 403 (permission not granted) or 409 (claim race)
                // must reach the browser as itself so the UI can react precisely.
                return new ContentResult
                {
                    Content = content,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    StatusCode = (int)response.StatusCode
                };
            }
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task engine proxy failed for {Method} {TargetUrl}.", method, targetUrl);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Task engine dependency unavailable." });
        }
    }

    private bool TryCreateTenantRequest(HttpMethod method, string targetUrl, out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, targetUrl);
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token) || !TryResolveTenantId(token, out var tenantId))
        {
            request.Dispose();
            request = null!;
            return false;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation(TenantHeaderName, tenantId.ToString("D"));
        request.Headers.TryAddWithoutValidation(CorrelationHeaderName, ResolveCorrelationId());
        if (Request.Headers.TryGetValue("Accept-Language", out var acceptLanguage))
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage.ToString());
        }

        return true;
    }

    private string ResolveCorrelationId()
    {
        if (Request.Headers.TryGetValue(CorrelationHeaderName, out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId.ToString()))
        {
            return correlationId.ToString();
        }

        return HttpContext.TraceIdentifier;
    }

    private static bool TryResolveTenantId(string token, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var claimValue = FindClaim(jwt.Claims, "tenant_id", "tenantId");
            return Guid.TryParse(claimValue, out tenantId) && tenantId != Guid.Empty && jwt.ValidTo > DateTime.UtcNow;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindClaim(IEnumerable<Claim> claims, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var match = claims.FirstOrDefault(c => string.Equals(c.Type, claimType, StringComparison.OrdinalIgnoreCase));
            if (match is not null && !string.IsNullOrWhiteSpace(match.Value))
            {
                return match.Value;
            }
        }

        return null;
    }
}
