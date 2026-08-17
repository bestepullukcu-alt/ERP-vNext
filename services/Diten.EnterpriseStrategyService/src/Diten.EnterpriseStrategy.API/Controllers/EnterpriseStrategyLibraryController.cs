using Asp.Versioning;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.EnterpriseStrategy.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/enterprise-strategy/strategy-library")]
public sealed class EnterpriseStrategyLibraryController : EnterpriseStrategyApiControllerBase
{
    private readonly IStrategyLibraryImportService _import;
    private readonly IStrategyLibraryQueryService _query;
    private readonly IStrategyLibraryGovernanceService _governance;
    private readonly IStrategyInstantiationService _instantiation;
    private readonly IStrategyLibraryUsageService _usage;
    private readonly ICorrelationContextAccessor _correlation;

    public EnterpriseStrategyLibraryController(
        IStrategyLibraryImportService import,
        IStrategyLibraryQueryService query,
        IStrategyLibraryGovernanceService governance,
        IStrategyInstantiationService instantiation,
        IStrategyLibraryUsageService usage,
        ICorrelationContextAccessor correlation)
    {
        _import = import;
        _query = query;
        _governance = governance;
        _instantiation = instantiation;
        _usage = usage;
        _correlation = correlation;
    }

    [HttpPost("import")]
    public async Task<ActionResult<Response<StrategyLibraryImportBatchDto>>> Import([FromBody] StrategyLibraryImportPayloadDto body, CancellationToken ct)
    {
        if (!AllowRole("Admin", "Strategy Architect")) return ForbidEnvelope<StrategyLibraryImportBatchDto>("Import requires admin or strategy architect role.");
        return HandleResult(await _import.ImportAsync(body, Actor(), _correlation.CorrelationId, ct), _correlation.CorrelationId);
    }

    [HttpGet("import/{batchId}")]
    public async Task<ActionResult<Response<StrategyLibraryImportBatchDto>>> GetImport(string batchId, CancellationToken ct)
        => HandleResult(await _import.GetImportBatchAsync(batchId, ct), _correlation.CorrelationId);

    [HttpPost("import/{batchId}/approve")]
    public async Task<ActionResult<Response<StrategyLibraryImportBatchDto>>> ApproveImport(string batchId, CancellationToken ct)
    {
        if (!AllowRole("Admin", "Strategy Architect")) return ForbidEnvelope<StrategyLibraryImportBatchDto>("Import approval requires admin or strategy architect role.");
        return HandleResult(await _import.ApproveImportAsync(batchId, Actor(), _correlation.CorrelationId, ct), _correlation.CorrelationId);
    }

    [HttpGet("catalog")]
    public async Task<ActionResult<Response<PagedResponseDto<StrategyLibraryCatalogItemDto>>>> Catalog([FromQuery] StrategyLibraryCatalogRequestDto request, CancellationToken ct)
        => HandleResult(await _query.CatalogAsync(request, ct), _correlation.CorrelationId);

    [HttpGet("projects")]
    public async Task<ActionResult<Response<PagedResponseDto<ProjectLibraryRowDto>>>> ProjectsLibrary([FromQuery] ProjectLibraryCatalogRequestDto request, CancellationToken ct)
        => HandleResult(await _query.ProjectsLibraryAsync(request, ct), _correlation.CorrelationId);

    [HttpGet("projects/{id}/metrics")]
    public async Task<ActionResult<Response<IReadOnlyList<ProjectTemplateMetricDto>>>> ProjectLibraryMetrics(string id, CancellationToken ct)
        => HandleResult(await _query.ProjectLibraryMetricsAsync(id, ct), _correlation.CorrelationId);

    [HttpGet("templates/{id}")]
    public async Task<ActionResult<Response<StrategyTemplateDetailDto>>> Template(string id, CancellationToken ct)
        => HandleResult(await _query.GetTemplateAsync(id, ct), _correlation.CorrelationId);

    [HttpGet("blueprints/{id}")]
    public async Task<ActionResult<Response<StrategyBlueprintDetailDto>>> Blueprint(string id, CancellationToken ct)
        => HandleResult(await _query.GetBlueprintAsync(id, ct), _correlation.CorrelationId);

    [HttpGet("templates/{id}/versions")]
    public async Task<ActionResult<Response<IReadOnlyList<StrategyTemplateVersionDto>>>> Versions(string id, CancellationToken ct)
        => HandleResult(await _query.GetTemplateVersionsAsync(id, ct), _correlation.CorrelationId);

    [HttpPost("templates/{id}/submit-review")]
    public async Task<ActionResult<Response<bool>>> SubmitReviewTemplate(string id, CancellationToken ct)
    {
        if (!AllowRole("Admin", "Strategy Architect")) return ForbidEnvelope<bool>("Submit review requires admin or strategy architect role.");
        return HandleResult(await _governance.SubmitReviewTemplateAsync(id, Actor(), _correlation.CorrelationId, ct), _correlation.CorrelationId);
    }

    [HttpPost("templates/{id}/approve")]
    public async Task<ActionResult<Response<bool>>> ApproveTemplate(string id, CancellationToken ct)
    {
        if (!AllowRole("Admin", "Strategy Architect")) return ForbidEnvelope<bool>("Approve requires admin or strategy architect role.");
        return HandleResult(await _governance.ApproveTemplateAsync(id, Actor(), _correlation.CorrelationId, ct), _correlation.CorrelationId);
    }

    [HttpPost("templates/{id}/publish")]
    public async Task<ActionResult<Response<bool>>> PublishTemplate(string id, CancellationToken ct)
    {
        if (!AllowRole("Admin", "Strategy Architect")) return ForbidEnvelope<bool>("Publish requires admin or strategy architect role.");
        return HandleResult(await _governance.PublishTemplateAsync(id, Actor(), _correlation.CorrelationId, ct), _correlation.CorrelationId);
    }

    [HttpPost("templates/{id}/retire")]
    public async Task<ActionResult<Response<bool>>> RetireTemplate(string id, CancellationToken ct)
    {
        if (!AllowRole("Admin", "Strategy Architect")) return ForbidEnvelope<bool>("Retire requires admin or strategy architect role.");
        return HandleResult(await _governance.RetireTemplateAsync(id, Actor(), _correlation.CorrelationId, ct), _correlation.CorrelationId);
    }

    [HttpPost("blueprints/{id}/publish")]
    public async Task<ActionResult<Response<bool>>> PublishBlueprint(string id, CancellationToken ct)
    {
        if (!AllowRole("Admin", "Strategy Architect")) return ForbidEnvelope<bool>("Publish requires admin or strategy architect role.");
        return HandleResult(await _governance.PublishBlueprintAsync(id, Actor(), _correlation.CorrelationId, ct), _correlation.CorrelationId);
    }

    [HttpPost("blueprints/{id}/retire")]
    public async Task<ActionResult<Response<bool>>> RetireBlueprint(string id, CancellationToken ct)
    {
        if (!AllowRole("Admin", "Strategy Architect")) return ForbidEnvelope<bool>("Retire requires admin or strategy architect role.");
        return HandleResult(await _governance.RetireBlueprintAsync(id, Actor(), _correlation.CorrelationId, ct), _correlation.CorrelationId);
    }

    [HttpPost("templates/{id}/instantiate")]
    public async Task<ActionResult<Response<StrategyInstantiationResultDto>>> InstantiateTemplate(string id, [FromBody] StrategyTemplateInstantiateRequestDto body, CancellationToken ct)
    {
        if (!AllowRole("Admin", "Strategy Architect", "Planner")) return ForbidEnvelope<StrategyInstantiationResultDto>("Instantiate requires planner/architect/admin role.");
        body.TemplateId = id;
        return HandleResult(await _instantiation.InstantiateTemplateAsync(id, body, Actor(), _correlation.CorrelationId, ct), _correlation.CorrelationId);
    }

    [HttpPost("blueprints/{id}/instantiate")]
    public async Task<ActionResult<Response<StrategyInstantiationResultDto>>> InstantiateBlueprint(string id, [FromBody] StrategyBlueprintInstantiateRequestDto body, CancellationToken ct)
    {
        if (!AllowRole("Admin", "Strategy Architect", "Planner")) return ForbidEnvelope<StrategyInstantiationResultDto>("Instantiate requires planner/architect/admin role.");
        body.BlueprintPackId = id;
        return HandleResult(await _instantiation.InstantiateBlueprintAsync(id, body, Actor(), _correlation.CorrelationId, ct), _correlation.CorrelationId);
    }

    [HttpGet("usage/summary")]
    public async Task<ActionResult<Response<StrategyLibraryUsageSummaryDto>>> UsageSummary(CancellationToken ct)
        => HandleResult(await _usage.SummaryAsync(ct), _correlation.CorrelationId);

    [HttpGet("usage/templates")]
    public async Task<ActionResult<Response<IReadOnlyList<StrategyLibraryUsageItemDto>>>> UsageTemplates(CancellationToken ct)
        => HandleResult(await _usage.TemplateUsageAsync(ct), _correlation.CorrelationId);

    [HttpGet("usage/blueprints")]
    public async Task<ActionResult<Response<IReadOnlyList<StrategyLibraryUsageItemDto>>>> UsageBlueprints(CancellationToken ct)
        => HandleResult(await _usage.BlueprintUsageAsync(ct), _correlation.CorrelationId);

    private string Actor() => User?.Identity?.Name ?? "anonymous";

    private bool AllowRole(params string[] roles)
    {
        if (User?.Identity?.IsAuthenticated != true) return true;
        if (roles.Length == 0) return true;
        var currentRoles = User.Claims.Where(c => c.Type == "role" || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase)).Select(c => c.Value).ToList();
        return currentRoles.Any(r => roles.Contains(r, StringComparer.OrdinalIgnoreCase));
    }

    private ActionResult<Response<T>> ForbidEnvelope<T>(string message)
    {
        var payload = Response<T>.Fail(EnterpriseStrategyErrorCodes.Forbidden, StatusCodes.Status403Forbidden, _correlation.CorrelationId, new Dictionary<string, List<string>>
        {
            ["general"] = new() { message }
        });
        return StatusCode(StatusCodes.Status403Forbidden, payload);
    }
}
