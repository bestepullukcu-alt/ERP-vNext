using Diten.Web.Models.ManagementGovernance.ProcessModeling;
using Diten.Web.Services.ManagementGovernance.ProcessModeling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace Diten.Web.Controllers;

[Authorize]
[Route("management-governance/process-modeling")]
public sealed class ProcessModelingFrontendController : Controller
{
    private readonly ProcessModelingFrontendGateway _gateway;

    public ProcessModelingFrontendController(
        HttpClient httpClient,
        IConfiguration configuration,
        ILoggerFactory loggerFactory)
    {
        _gateway = new ProcessModelingFrontendGateway(
            httpClient,
            configuration,
            loggerFactory.CreateLogger<ProcessModelingFrontendGateway>());
    }

    [HttpGet("models")]
    public IActionResult Index()
    {
        if (!HasPermission(ProcessModelingFrontendPermissions.Read))
            return StatusCode(StatusCodes.Status403Forbidden);

        return View("~/Views/ManagementGovernance/ProcessModeling/Index.cshtml",
            new ProcessModelingIndexViewModel
            {
                GatewayReady = _gateway.IsReady,
                Permissions = ResolvePermissions()
            });
    }

    [HttpGet("models/{id:guid}")]
    public IActionResult Editor(Guid id)
    {
        if (!HasPermission(ProcessModelingFrontendPermissions.Read))
            return StatusCode(StatusCodes.Status403Forbidden);

        return View("~/Views/ManagementGovernance/ProcessModeling/Editor.cshtml",
            new ProcessModelingEditorViewModel
            {
                ProcessModelId = id,
                GatewayReady = _gateway.IsReady,
                Permissions = ResolvePermissions()
            });
    }

    [HttpGet("api/models")]
    public Task<IActionResult> Models(CancellationToken cancellationToken) =>
        ProxyReadAsync("models", cancellationToken);

    [HttpGet("api/models/{id:guid}")]
    public Task<IActionResult> Model(Guid id, CancellationToken cancellationToken) =>
        ProxyReadAsync($"models/{id:D}", cancellationToken);

    [HttpPost("api/models"), ValidateAntiForgeryToken]
    public Task<IActionResult> CreateModel([FromBody] ProcessModelIdentityInput body, CancellationToken ct) => ProxyWriteAsync(HttpMethod.Post, "models", body, ProcessModelingFrontendPermissions.Create, ct);
    [HttpPut("api/models/{id:guid}"), ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateModel(Guid id, [FromBody] ProcessModelUpdateInput body, CancellationToken ct) => ProxyWriteAsync(HttpMethod.Put, $"models/{id:D}", body, ProcessModelingFrontendPermissions.Update, ct);
    [HttpPut("api/model-versions/{id:guid}/draft-content"), ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateDraft(Guid id, [FromBody] ProcessModelDraftInput body, CancellationToken ct) => ProxyWriteAsync(HttpMethod.Put, $"model-versions/{id:D}/draft-content", body, ProcessModelingFrontendPermissions.Update, ct);
    [HttpPost("api/model-versions/{id:guid}/request-review"), ValidateAntiForgeryToken]
    public Task<IActionResult> RequestReview(Guid id, [FromBody] ExpectedVersionInput body, CancellationToken ct) => ProxyWriteAsync(HttpMethod.Post, $"model-versions/{id:D}/request-review", body, ProcessModelingFrontendPermissions.RequestReview, ct);
    [HttpPost("api/model-versions/{id:guid}/return-to-draft"), ValidateAntiForgeryToken]
    public Task<IActionResult> ReturnToDraft(Guid id, [FromBody] ExpectedVersionInput body, CancellationToken ct) => ProxyWriteAsync(HttpMethod.Post, $"model-versions/{id:D}/return-to-draft", body, ProcessModelingFrontendPermissions.ReturnToDraft, ct);
    [HttpPost("api/model-versions/{id:guid}/publish"), ValidateAntiForgeryToken]
    public Task<IActionResult> Publish(Guid id, [FromBody] ExpectedVersionInput body, CancellationToken ct) => ProxyWriteAsync(HttpMethod.Post, $"model-versions/{id:D}/publish", body, ProcessModelingFrontendPermissions.Publish, ct);
    [HttpPost("api/model-versions/{id:guid}/retire"), ValidateAntiForgeryToken]
    public Task<IActionResult> Retire(Guid id, [FromBody] ExpectedVersionInput body, CancellationToken ct) => ProxyWriteAsync(HttpMethod.Post, $"model-versions/{id:D}/retire", body, ProcessModelingFrontendPermissions.Retire, ct);
    [HttpPost("api/models/{id:guid}/revisions"), ValidateAntiForgeryToken]
    public Task<IActionResult> CreateRevision(Guid id, [FromBody] ProcessModelRevisionInput body, CancellationToken ct) => ProxyWriteAsync(HttpMethod.Post, $"models/{id:D}/revisions", body, ProcessModelingFrontendPermissions.CreateRevision, ct);

    private async Task<IActionResult> ProxyReadAsync(string relativePath, CancellationToken cancellationToken)
    {
        if (!HasPermission(ProcessModelingFrontendPermissions.Read))
        {
            var failure = ProcessModelingFrontendProxyResult.Failure(
                403,
                "process_modeling_permission_denied",
                HttpContext.TraceIdentifier);
            return new ContentResult
            {
                Content = failure.Content,
                ContentType = failure.ContentType,
                StatusCode = failure.StatusCode
            };
        }

        var result = await _gateway.GetAsync(Request, relativePath, cancellationToken);
        return new ContentResult
        {
            Content = result.Content,
            ContentType = result.ContentType,
            StatusCode = result.StatusCode
        };
    }

    private bool HasPermission(string permission) =>
        User.FindAll("permission").Any(claim => string.Equals(claim.Value, permission, StringComparison.Ordinal));

    private IReadOnlySet<string> ResolvePermissions()
    {
        var granted = User.FindAll("permission")
            .Select(claim => claim.Value)
            .Where(value => ProcessModelingFrontendPermissions.ExactVisibleActions.Contains(value, StringComparer.Ordinal)
                || string.Equals(value, ProcessModelingFrontendPermissions.Read, StringComparison.Ordinal));
        return granted.ToHashSet(StringComparer.Ordinal);
    }

    private async Task<IActionResult> ProxyWriteAsync<T>(HttpMethod method, string path, T body, string permission, CancellationToken ct)
    {
        // The downstream tenant-scoped boundary is authoritative for write permission ordering:
        // foreign/missing/deleted must remain 404 before an own-tenant permission denial becomes 403.
        // Require the requested operation to be from the closed local inventory, but never invent a grant here.
        if (!ProcessModelingFrontendPermissions.ExactVisibleActions.Contains(permission, StringComparer.Ordinal))
            return ProxyResult(ProcessModelingFrontendProxyResult.Failure(400, "process_modeling_bad_request", HttpContext.TraceIdentifier));
        if (!ModelState.IsValid) return ProxyResult(ProcessModelingFrontendProxyResult.Failure(400, "process_modeling_bad_request", HttpContext.TraceIdentifier));
        using var content = JsonContent.Create(body);
        var result = method == HttpMethod.Put ? await _gateway.PutAsync(Request, path, content, ct) : await _gateway.PostAsync(Request, path, content, ct);
        return ProxyResult(result);
    }

    private static IActionResult ProxyResult(ProcessModelingFrontendProxyResult result) => new ContentResult { Content = result.Content, ContentType = result.ContentType, StatusCode = result.StatusCode };
}
