using Diten.Web.Models.ManagementGovernance.ProcessModeling;
using Diten.Web.Services.ManagementGovernance.ProcessModeling;
using Diten.Web.Models.ManagementGovernance.ProcessModeling.PerformanceImprovement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    [HttpPost("api/models")]
    public Task<IActionResult> CreateModel(CancellationToken cancellationToken) =>
        ProxyWriteAsync(ProcessModelingFrontendPermissions.Create, HttpMethod.Post, "models", cancellationToken);

    [HttpPut("api/models/{id:guid}")]
    public Task<IActionResult> UpdateModel(Guid id, CancellationToken cancellationToken) =>
        ProxyWriteAsync(ProcessModelingFrontendPermissions.Update, HttpMethod.Put, $"models/{id:D}", cancellationToken);

    [HttpPost("api/models/{id:guid}/{action:regex(^(request-review|return-to-draft|publish|retire|create-revision)$)}")]
    public Task<IActionResult> ModelLifecycle(Guid id, string action, CancellationToken cancellationToken)
    {
        var permission = action switch
        {
            "request-review" => ProcessModelingFrontendPermissions.RequestReview,
            "return-to-draft" => ProcessModelingFrontendPermissions.ReturnToDraft,
            "publish" => ProcessModelingFrontendPermissions.Publish,
            "retire" => ProcessModelingFrontendPermissions.Retire,
            "create-revision" => ProcessModelingFrontendPermissions.CreateRevision,
            _ => string.Empty
        };
        return ProxyWriteAsync(permission, HttpMethod.Post, $"models/{id:D}/{action}", cancellationToken);
    }

    [HttpGet("improvements")]
    public IActionResult Improvements()
    {
        if (!HasPermission(PerformanceImprovementFrontendPermissions.Read))
            return StatusCode(StatusCodes.Status403Forbidden);
        return View("~/Views/ManagementGovernance/ProcessModeling/PerformanceImprovement/Index.cshtml",
            new PerformanceImprovementIndexViewModel(_gateway.IsReady, ResolveImprovementPermissions()));
    }

    [HttpGet("improvements/{id:guid}")]
    public IActionResult Improvement(Guid id)
    {
        if (!HasPermission(PerformanceImprovementFrontendPermissions.Read))
            return StatusCode(StatusCodes.Status403Forbidden);
        return View("~/Views/ManagementGovernance/ProcessModeling/PerformanceImprovement/Details.cshtml",
            new PerformanceImprovementDetailViewModel(id, _gateway.IsReady, ResolveImprovementPermissions()));
    }

    [HttpGet("api/improvements")]
    public Task<IActionResult> ImprovementsApi(CancellationToken cancellationToken) =>
        ProxyImprovementReadAsync("improvements", cancellationToken);

    [HttpGet("api/improvements/{id:guid}")]
    public Task<IActionResult> ImprovementApi(Guid id, CancellationToken cancellationToken) =>
        ProxyImprovementReadAsync($"improvements/{id:D}", cancellationToken);

    [HttpPost("api/improvements")]
    public Task<IActionResult> CreateImprovement(CancellationToken cancellationToken) =>
        ProxyWriteAsync(PerformanceImprovementFrontendPermissions.Create, HttpMethod.Post, "improvements", cancellationToken);

    [HttpPut("api/improvements/{id:guid}")]
    public Task<IActionResult> UpdateImprovement(Guid id, CancellationToken cancellationToken) =>
        ProxyWriteAsync(PerformanceImprovementFrontendPermissions.Update, HttpMethod.Put, $"improvements/{id:D}", cancellationToken);

    [HttpPost("api/improvements/{id:guid}/qualify")]
    public Task<IActionResult> QualifyImprovement(Guid id, CancellationToken cancellationToken) =>
        ProxyWriteAsync(PerformanceImprovementFrontendPermissions.Qualify, HttpMethod.Post, $"improvements/{id:D}/qualify", cancellationToken);

    [HttpPost("api/improvements/{id:guid}/outcomes")]
    public Task<IActionResult> RecordOutcome(Guid id, CancellationToken cancellationToken) =>
        ProxyWriteAsync(PerformanceImprovementFrontendPermissions.RecordOutcome, HttpMethod.Post, $"improvements/{id:D}/outcomes", cancellationToken);

    [HttpPost("api/improvements/{id:guid}/close")]
    public Task<IActionResult> CloseImprovement(Guid id, CancellationToken cancellationToken) =>
        ProxyWriteAsync(PerformanceImprovementFrontendPermissions.Close, HttpMethod.Post, $"improvements/{id:D}/close", cancellationToken);

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

    private async Task<IActionResult> ProxyWriteAsync(string permission, HttpMethod method, string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(permission) || !HasPermission(permission))
        {
            var denied = ProcessModelingFrontendProxyResult.Failure(403, "process_modeling_permission_denied", HttpContext.TraceIdentifier);
            return new ContentResult { Content = denied.Content, ContentType = denied.ContentType, StatusCode = denied.StatusCode };
        }
        var result = await _gateway.SendAsync(Request, method, path, cancellationToken);
        return new ContentResult { Content = result.Content, ContentType = result.ContentType, StatusCode = result.StatusCode };
    }

    private IReadOnlySet<string> ResolvePermissions()
    {
        var granted = User.FindAll("permission")
            .Select(claim => claim.Value)
            .Where(value => ProcessModelingFrontendPermissions.ExactVisibleActions.Contains(value, StringComparer.Ordinal)
                || string.Equals(value, ProcessModelingFrontendPermissions.Read, StringComparison.Ordinal));
        return granted.ToHashSet(StringComparer.Ordinal);
    }

    private async Task<IActionResult> ProxyImprovementReadAsync(string relativePath, CancellationToken cancellationToken)
    {
        if (!HasPermission(PerformanceImprovementFrontendPermissions.Read))
        {
            var denied = ProcessModelingFrontendProxyResult.Failure(403, "performance_improvement_permission_denied", HttpContext.TraceIdentifier);
            return new ContentResult { Content = denied.Content, ContentType = denied.ContentType, StatusCode = denied.StatusCode };
        }
        var result = await _gateway.GetAsync(Request, relativePath, cancellationToken);
        return new ContentResult { Content = result.Content, ContentType = result.ContentType, StatusCode = result.StatusCode };
    }

    private IReadOnlySet<string> ResolveImprovementPermissions() => User.FindAll("permission")
        .Select(x => x.Value)
        .Where(PerformanceImprovementFrontendPermissions.Exact.Contains)
        .ToHashSet(StringComparer.Ordinal);
}
