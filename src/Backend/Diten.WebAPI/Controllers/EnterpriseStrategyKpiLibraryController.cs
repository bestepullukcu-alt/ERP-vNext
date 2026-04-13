using Asp.Versioning;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Domain.Aggregates.EnterpriseStrategy;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/enterprise-strategy/kpi-library")]
public sealed class EnterpriseStrategyKpiLibraryController : EnterpriseStrategyApiControllerBase
{
    private readonly IKpiLibraryService _library;
    private readonly ICorrelationContextAccessor _correlation;

    public EnterpriseStrategyKpiLibraryController(IKpiLibraryService library, ICorrelationContextAccessor correlation)
    {
        _library = library;
        _correlation = correlation;
    }

    [HttpGet("templates")]
    public async Task<ActionResult<Response<PagedResponseDto<KpiTemplateDto>>>> Templates([FromQuery] PagedRequestDto request, CancellationToken ct)
        => HandleResult(await _library.CatalogAsync(request, ct), _correlation.CorrelationId);

    [HttpGet("templates/{id}")]
    public async Task<ActionResult<Response<KpiTemplateDto>>> Template(string id, CancellationToken ct)
        => HandleResult(await _library.TemplateAsync(id, ct), _correlation.CorrelationId);

    [HttpPost("templates/{id}/clone")]
    public async Task<ActionResult<Response<KpiTemplateDto>>> Clone(string id, CancellationToken ct)
    {
        if (!AllowRole("Admin", "Strategy Architect")) return ForbidEnvelope<KpiTemplateDto>("Clone requires admin or strategy architect role.");
        return HandleResult(await _library.CloneTemplateAsync(id, User?.Identity?.Name ?? "anonymous", _correlation.CorrelationId, ct), _correlation.CorrelationId);
    }

    [HttpPost("templates/{id}/lifecycle")]
    public async Task<ActionResult<Response<KpiTemplateDto>>> Lifecycle(string id, [FromBody] KpiLifecycleActionRequestDto body, CancellationToken ct)
    {
        if (!AllowRole("Admin", "Strategy Architect")) return ForbidEnvelope<KpiTemplateDto>("Lifecycle actions require admin or strategy architect role.");
        return HandleResult(await _library.LifecycleAsync(id, body.Action, User?.Identity?.Name ?? "anonymous", _correlation.CorrelationId, ct), _correlation.CorrelationId);
    }

    [HttpGet("threshold-models")]
    public async Task<ActionResult<Response<IReadOnlyList<KpiThresholdModelDto>>>> ThresholdModels(CancellationToken ct)
        => HandleResult(await _library.ThresholdModelsAsync(ct), _correlation.CorrelationId);

    [HttpGet("threshold-models/{idOrCode}")]
    public async Task<ActionResult<Response<KpiThresholdModelDto>>> ThresholdModel(string idOrCode, CancellationToken ct)
        => HandleResult(await _library.ThresholdModelAsync(idOrCode, ct), _correlation.CorrelationId);

    [HttpGet("packs")]
    public async Task<ActionResult<Response<PagedResponseDto<KpiScorecardPackDto>>>> Packs([FromQuery] PagedRequestDto request, CancellationToken ct)
        => HandleResult(await _library.PacksAsync(request, ct), _correlation.CorrelationId);

    [HttpGet("packs/{id}")]
    public async Task<ActionResult<Response<KpiScorecardPackDto>>> Pack(string id, CancellationToken ct)
        => HandleResult(await _library.PackAsync(id, ct), _correlation.CorrelationId);

    [HttpGet("packs/{id}/items")]
    public async Task<ActionResult<Response<IReadOnlyList<KpiScorecardPackItemDto>>>> PackItems(string id, CancellationToken ct)
        => HandleResult(await _library.PackItemsAsync(id, ct), _correlation.CorrelationId);

    [HttpGet("governance/summary")]
    public async Task<ActionResult<Response<KpiGovernanceSummaryDto>>> GovernanceSummary(CancellationToken ct)
        => HandleResult(await _library.GovernanceSummaryAsync(ct), _correlation.CorrelationId);

    [HttpGet("governance/exceptions")]
    public async Task<ActionResult<Response<IReadOnlyList<KpiGovernanceExceptionDto>>>> GovernanceExceptions(CancellationToken ct)
        => HandleResult(await _library.GovernanceExceptionsAsync(ct), _correlation.CorrelationId);

    [HttpGet("governance/actions")]
    public async Task<ActionResult<Response<IReadOnlyList<KpiGovernanceActionAggregate>>>> GovernanceActions(CancellationToken ct)
        => HandleResult(await _library.GovernanceActionsAsync(ct), _correlation.CorrelationId);

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
