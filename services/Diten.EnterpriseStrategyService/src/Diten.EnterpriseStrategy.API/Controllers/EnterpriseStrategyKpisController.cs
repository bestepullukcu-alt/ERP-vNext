using Asp.Versioning;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.EnterpriseStrategy.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/enterprise-strategy/kpis")]
public sealed class EnterpriseStrategyKpisController : EnterpriseStrategyApiControllerBase
{
    private readonly IKpiRuntimeService _service;
    private readonly ICorrelationContextAccessor _correlation;

    public EnterpriseStrategyKpisController(IKpiRuntimeService service, ICorrelationContextAccessor correlation)
    {
        _service = service;
        _correlation = correlation;
    }

    [HttpGet]
    public async Task<ActionResult<Response<PagedResponseDto<KpiDefinitionDto>>>> List([FromQuery] PagedRequestDto request, CancellationToken ct)
    {
        try
        {
            return HandleResult(await _service.ListAsync(request, ct), _correlation.CorrelationId);
        }
        catch
        {
            var page = Math.Max(1, request.Page);
            var pageSize = request.PageSize > 0 ? request.PageSize : 20;
            return Ok(Response<PagedResponseDto<KpiDefinitionDto>>.Ok(new PagedResponseDto<KpiDefinitionDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = 0,
                Items = Array.Empty<KpiDefinitionDto>()
            }, _correlation.CorrelationId));
        }
    }

    [HttpGet("{kpiId}")]
    public async Task<ActionResult<Response<KpiDefinitionDto>>> Get(string kpiId, CancellationToken ct)
        => HandleResult(await _service.GetAsync(kpiId, ct), _correlation.CorrelationId);

    [HttpPost]
    public async Task<ActionResult<Response<KpiDefinitionDto>>> Create([FromBody] KpiDefinitionDto body, CancellationToken ct)
        => HandleResult(await _service.CreateAsync(body, User?.Identity?.Name ?? "anonymous", _correlation.CorrelationId, ct), _correlation.CorrelationId);

    [HttpPut("{kpiId}")]
    public async Task<ActionResult<Response<KpiDefinitionDto>>> Update(string kpiId, [FromBody] KpiDefinitionDto body, [FromHeader(Name = "If-Match")] int expectedVersion, CancellationToken ct)
        => HandleResult(await _service.UpdateAsync(kpiId, body, expectedVersion, User?.Identity?.Name ?? "anonymous", _correlation.CorrelationId, ct), _correlation.CorrelationId);

    [HttpPost("{kpiId}/archive")]
    public async Task<ActionResult<Response<KpiDefinitionDto>>> Archive(string kpiId, [FromBody] MutationMetadataDto body, CancellationToken ct)
        => HandleResult(await _service.ArchiveAsync(kpiId, body.ExpectedVersion, User?.Identity?.Name ?? "anonymous", _correlation.CorrelationId, ct), _correlation.CorrelationId);

    [HttpPost("instantiate-from-library")]
    public async Task<ActionResult<Response<KpiDefinitionDto>>> InstantiateFromLibrary([FromBody] KpiInstantiateFromTemplateRequestDto body, CancellationToken ct)
    {
        if (!AllowRole("Admin", "Strategy Architect", "Planner")) return ForbidEnvelope<KpiDefinitionDto>("Instantiate requires planner/architect/admin role.");
        return HandleResult(await _service.CreateFromTemplateAsync(body, User?.Identity?.Name ?? "anonymous", _correlation.CorrelationId, ct), _correlation.CorrelationId);
    }

    [HttpGet("{kpiId}/usage")]
    public async Task<ActionResult<Response<KpiUsageDto>>> Usage(string kpiId, CancellationToken ct)
        => HandleResult(await _service.UsageAsync(kpiId, ct), _correlation.CorrelationId);

    [HttpGet("ownership")]
    public async Task<ActionResult<Response<IReadOnlyList<KpiOwnershipRowDto>>>> Ownership(CancellationToken ct)
        => HandleResult(await _service.OwnershipAsync(ct), _correlation.CorrelationId);

    [HttpGet("scorecard")]
    public async Task<ActionResult<Response<ScorecardSnapshotDto>>> Scorecard([FromQuery] string? goalId, [FromQuery] string? objectiveId, [FromQuery] string? company, [FromQuery] string? period, CancellationToken ct)
        => HandleResult(await _service.ScorecardAsync(goalId, objectiveId, company, period, ct), _correlation.CorrelationId);

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
