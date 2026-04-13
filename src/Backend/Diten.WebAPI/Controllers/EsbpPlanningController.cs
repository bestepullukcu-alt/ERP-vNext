using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using Diten.WebAPI.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

[ApiController]
[Route("api/esbp")]
public sealed class EsbpPlanningController : EnterpriseStrategyApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContextAccessor _correlation;

    public EsbpPlanningController(IMediator mediator, ICorrelationContextAccessor correlation)
    {
        _mediator = mediator;
        _correlation = correlation;
    }

    [HttpGet("planning-cycles")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.PlanningCycleView)]
    public async Task<ActionResult<Response<IReadOnlyList<PlanningCycleListItemDto>>>> ListPlanningCycles(
        [FromQuery] string? status,
        [FromQuery] string? planningCycleType,
        [FromQuery] bool? activeOnly,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var listResult = await _mediator.Send(new ListPlanningCyclesQuery
        {
            Search = search,
            Status = activeOnly == true ? "Active" : status
        }, ct);
        if (!listResult.Success)
            return HandleResult(Response<IReadOnlyList<PlanningCycleListItemDto>>.Fail(listResult.Error?.Code ?? EnterpriseStrategyErrorCodes.InternalError, listResult.Error?.Details), _correlation.CorrelationId);

        var rows = listResult.Data ?? Array.Empty<PlanningCycleDto>();
        if (!string.IsNullOrWhiteSpace(planningCycleType))
            rows = rows.Where(x => string.Equals(x.PlanningCycleType, planningCycleType.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        var response = rows.Select(ToListItem).ToList();
        return HandleResult(Response<IReadOnlyList<PlanningCycleListItemDto>>.Ok(response), _correlation.CorrelationId);
    }

    [HttpGet("planning-cycles/{id}")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.PlanningCycleView)]
    public async Task<ActionResult<Response<PlanningCycleDetailDto>>> GetPlanningCycle(string id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPlanningCycleByIdQuery { PlanningCycleId = id }, ct);
        if (!result.Success)
            return HandleResult(Response<PlanningCycleDetailDto>.Fail(result.Error?.Code ?? EnterpriseStrategyErrorCodes.InternalError, result.Error?.Details), _correlation.CorrelationId);

        return HandleResult(Response<PlanningCycleDetailDto>.Ok(ToDetail(result.Data!)), _correlation.CorrelationId);
    }

    [HttpPost("planning-cycles")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.PlanningCycleCreate)]
    public async Task<ActionResult<Response<PlanningCycleDetailDto>>> CreatePlanningCycle([FromBody] CreatePlanningCycleRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreatePlanningCycleCommand
        {
            PlanningCycle = Map(request),
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct);
        if (!result.Success)
            return HandleResult(Response<PlanningCycleDetailDto>.Fail(result.Error?.Code ?? EnterpriseStrategyErrorCodes.InternalError, result.Error?.Details), _correlation.CorrelationId);

        return HandleResult(Response<PlanningCycleDetailDto>.Ok(ToDetail(result.Data!)), _correlation.CorrelationId);
    }

    [HttpPut("planning-cycles/{id}")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.PlanningCycleCreate)]
    public async Task<ActionResult<Response<PlanningCycleDetailDto>>> UpdatePlanningCycle(string id, [FromBody] UpdatePlanningCycleRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdatePlanningCycleCommand
        {
            PlanningCycleId = id,
            PlanningCycle = Map(request),
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct);
        if (!result.Success)
            return HandleResult(Response<PlanningCycleDetailDto>.Fail(result.Error?.Code ?? EnterpriseStrategyErrorCodes.InternalError, result.Error?.Details), _correlation.CorrelationId);

        return HandleResult(Response<PlanningCycleDetailDto>.Ok(ToDetail(result.Data!)), _correlation.CorrelationId);
    }

    [HttpPost("planning-cycles/{id}/archive")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.PlanningCycleCreate)]
    public async Task<ActionResult<Response<PlanningCycleDetailDto>>> ArchivePlanningCycle(string id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ChangePlanningCycleStatusCommand
        {
            PlanningCycleId = id,
            Status = "Archived",
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct);
        if (!result.Success)
            return HandleResult(Response<PlanningCycleDetailDto>.Fail(result.Error?.Code ?? EnterpriseStrategyErrorCodes.InternalError, result.Error?.Details), _correlation.CorrelationId);

        return HandleResult(Response<PlanningCycleDetailDto>.Ok(ToDetail(result.Data!)), _correlation.CorrelationId);
    }

    [HttpGet("strategy-periods/active-by-scope")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.StrategyPeriodView)]
    public async Task<ActionResult<Response<IReadOnlyList<StrategyPeriodListItemDto>>>> ActiveByScope(
        [FromQuery] string? companyId,
        [FromQuery] string? businessUnitId,
        [FromQuery] string? regionId,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var periodsResult = await _mediator.Send(new ListStrategyPeriodsQuery { Search = search }, ct);
        if (!periodsResult.Success)
            return HandleResult(Response<IReadOnlyList<StrategyPeriodListItemDto>>.Fail(periodsResult.Error?.Code ?? EnterpriseStrategyErrorCodes.InternalError, periodsResult.Error?.Details), _correlation.CorrelationId);

        var cyclesResult = await _mediator.Send(new ListPlanningCyclesQuery(), ct);
        if (!cyclesResult.Success)
            return HandleResult(Response<IReadOnlyList<StrategyPeriodListItemDto>>.Fail(cyclesResult.Error?.Code ?? EnterpriseStrategyErrorCodes.InternalError, cyclesResult.Error?.Details), _correlation.CorrelationId);

        var cycleMap = (cyclesResult.Data ?? Array.Empty<PlanningCycleDto>())
            .ToDictionary(x => x.Id, x => x, StringComparer.OrdinalIgnoreCase);

        IEnumerable<StrategyPeriodDto> rows = periodsResult.Data ?? Array.Empty<StrategyPeriodDto>();
        if (!string.IsNullOrWhiteSpace(companyId))
            rows = rows.Where(x => string.Equals(x.CompanyId, companyId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(businessUnitId))
            rows = rows.Where(x => string.Equals(x.BusinessUnitId, businessUnitId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(regionId))
            rows = rows.Where(x => string.Equals(x.RegionId, regionId.Trim(), StringComparison.OrdinalIgnoreCase));

        // Filter out Archived ones
        rows = rows.Where(x => !string.Equals(x.Status, "Archived", StringComparison.OrdinalIgnoreCase));

        var response = rows.Select(x => ToListItem(x, cycleMap)).ToList();
        return HandleResult(Response<IReadOnlyList<StrategyPeriodListItemDto>>.Ok(response), _correlation.CorrelationId);
    }

    [HttpGet("strategy-periods")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.StrategyPeriodView)]
    public async Task<ActionResult<Response<IReadOnlyList<StrategyPeriodListItemDto>>>> ListStrategyPeriods(
        [FromQuery] string? status,
        [FromQuery] string? companyId,
        [FromQuery] string? planningCycleId,
        [FromQuery] bool? activeOnly,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var periodsResult = await _mediator.Send(new ListStrategyPeriodsQuery
        {
            PlanningCycleId = planningCycleId,
            Search = search,
            Status = activeOnly == true ? "Active" : status
        }, ct);
        if (!periodsResult.Success)
            return HandleResult(Response<IReadOnlyList<StrategyPeriodListItemDto>>.Fail(periodsResult.Error?.Code ?? EnterpriseStrategyErrorCodes.InternalError, periodsResult.Error?.Details), _correlation.CorrelationId);

        var cyclesResult = await _mediator.Send(new ListPlanningCyclesQuery(), ct);
        if (!cyclesResult.Success)
            return HandleResult(Response<IReadOnlyList<StrategyPeriodListItemDto>>.Fail(cyclesResult.Error?.Code ?? EnterpriseStrategyErrorCodes.InternalError, cyclesResult.Error?.Details), _correlation.CorrelationId);

        var cycleMap = (cyclesResult.Data ?? Array.Empty<PlanningCycleDto>())
            .ToDictionary(x => x.Id, x => x, StringComparer.OrdinalIgnoreCase);

        IEnumerable<StrategyPeriodDto> rows = periodsResult.Data ?? Array.Empty<StrategyPeriodDto>();
        if (!string.IsNullOrWhiteSpace(companyId))
            rows = rows.Where(x => string.Equals(x.CompanyId, companyId.Trim(), StringComparison.OrdinalIgnoreCase));

        var response = rows.Select(x => ToListItem(x, cycleMap)).ToList();
        return HandleResult(Response<IReadOnlyList<StrategyPeriodListItemDto>>.Ok(response), _correlation.CorrelationId);
    }

    [HttpGet("strategy-periods/{id}")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.StrategyPeriodView)]
    public async Task<ActionResult<Response<StrategyPeriodDetailDto>>> GetStrategyPeriod(string id, CancellationToken ct)
    {
        var periodResult = await _mediator.Send(new GetStrategyPeriodByIdQuery { StrategyPeriodId = id }, ct);
        if (!periodResult.Success)
            return HandleResult(Response<StrategyPeriodDetailDto>.Fail(periodResult.Error?.Code ?? EnterpriseStrategyErrorCodes.InternalError, periodResult.Error?.Details), _correlation.CorrelationId);

        var cycleResult = await _mediator.Send(new GetPlanningCycleByIdQuery { PlanningCycleId = periodResult.Data!.PlanningCycleId }, ct);
        var cycle = cycleResult.Success ? cycleResult.Data : null;
        return HandleResult(Response<StrategyPeriodDetailDto>.Ok(ToDetail(periodResult.Data!, cycle)), _correlation.CorrelationId);
    }

    [HttpPost("strategy-periods")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.StrategyPeriodCreate)]
    public async Task<ActionResult<Response<StrategyPeriodDetailDto>>> CreateStrategyPeriod([FromBody] CreateStrategyPeriodRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateStrategyPeriodCommand
        {
            StrategyPeriod = Map(request),
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct);
        if (!result.Success)
            return HandleResult(Response<StrategyPeriodDetailDto>.Fail(result.Error?.Code ?? EnterpriseStrategyErrorCodes.InternalError, result.Error?.Details), _correlation.CorrelationId);

        var cycleResult = await _mediator.Send(new GetPlanningCycleByIdQuery { PlanningCycleId = result.Data!.PlanningCycleId }, ct);
        var cycle = cycleResult.Success ? cycleResult.Data : null;
        return HandleResult(Response<StrategyPeriodDetailDto>.Ok(ToDetail(result.Data!, cycle)), _correlation.CorrelationId);
    }

    [HttpPut("strategy-periods/{id}")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.StrategyPeriodCreate)]
    public async Task<ActionResult<Response<StrategyPeriodDetailDto>>> UpdateStrategyPeriod(string id, [FromBody] UpdateStrategyPeriodRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new UpdateStrategyPeriodCommand
        {
            StrategyPeriodId = id,
            StrategyPeriod = Map(request),
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct);
        if (!result.Success)
            return HandleResult(Response<StrategyPeriodDetailDto>.Fail(result.Error?.Code ?? EnterpriseStrategyErrorCodes.InternalError, result.Error?.Details), _correlation.CorrelationId);

        var cycleResult = await _mediator.Send(new GetPlanningCycleByIdQuery { PlanningCycleId = result.Data!.PlanningCycleId }, ct);
        var cycle = cycleResult.Success ? cycleResult.Data : null;
        return HandleResult(Response<StrategyPeriodDetailDto>.Ok(ToDetail(result.Data!, cycle)), _correlation.CorrelationId);
    }

    [HttpPost("strategy-periods/{id}/activate")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.StrategyPeriodCreate)]
    public async Task<ActionResult<Response<StrategyPeriodDetailDto>>> ActivateStrategyPeriod(string id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ChangeStrategyPeriodStatusCommand
        {
            StrategyPeriodId = id,
            Status = "Active",
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct);
        if (!result.Success)
            return HandleResult(Response<StrategyPeriodDetailDto>.Fail(result.Error?.Code ?? EnterpriseStrategyErrorCodes.InternalError, result.Error?.Details), _correlation.CorrelationId);

        var cycleResult = await _mediator.Send(new GetPlanningCycleByIdQuery { PlanningCycleId = result.Data!.PlanningCycleId }, ct);
        var cycle = cycleResult.Success ? cycleResult.Data : null;
        return HandleResult(Response<StrategyPeriodDetailDto>.Ok(ToDetail(result.Data!, cycle)), _correlation.CorrelationId);
    }

    [HttpPost("strategy-periods/{id}/archive")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.StrategyPeriodCreate)]
    public async Task<ActionResult<Response<StrategyPeriodDetailDto>>> ArchiveStrategyPeriod(string id, CancellationToken ct)
    {
        var result = await _mediator.Send(new ChangeStrategyPeriodStatusCommand
        {
            StrategyPeriodId = id,
            Status = "Archived",
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct);
        if (!result.Success)
            return HandleResult(Response<StrategyPeriodDetailDto>.Fail(result.Error?.Code ?? EnterpriseStrategyErrorCodes.InternalError, result.Error?.Details), _correlation.CorrelationId);

        var cycleResult = await _mediator.Send(new GetPlanningCycleByIdQuery { PlanningCycleId = result.Data!.PlanningCycleId }, ct);
        var cycle = cycleResult.Success ? cycleResult.Data : null;
        return HandleResult(Response<StrategyPeriodDetailDto>.Ok(ToDetail(result.Data!, cycle)), _correlation.CorrelationId);
    }

    [HttpGet("planning-cycle-types")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.PlanningCycleView)]
    public ActionResult<Response<IReadOnlyList<string>>> PlanningCycleTypes()
        => Ok(Response<IReadOnlyList<string>>.Ok(EnterpriseStrategyPlanningLookupCatalog.PlanningCycleTypeValues.ToList(), _correlation.CorrelationId));

    [HttpGet("review-cadences")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.StrategyPeriodView)]
    public ActionResult<Response<IReadOnlyList<string>>> ReviewCadences()
        => Ok(Response<IReadOnlyList<string>>.Ok(EnterpriseStrategyPlanningLookupCatalog.ReviewCadenceValues.ToList(), _correlation.CorrelationId));

    [HttpGet("scenario-types")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.StrategyPeriodView)]
    public ActionResult<Response<IReadOnlyList<string>>> ScenarioTypes()
        => Ok(Response<IReadOnlyList<string>>.Ok(EnterpriseStrategyPlanningLookupCatalog.ScenarioTypeValues.ToList(), _correlation.CorrelationId));

    private static PlanningCycleDto Map(CreatePlanningCycleRequest request) => new()
    {
        Code = request.Code,
        Name = request.Name,
        PlanningCycleType = request.PlanningCycleType,
        Description = request.Description ?? string.Empty,
        Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status,
        OwnerCompanyId = request.OwnerCompanyId ?? string.Empty,
        OwnerPositionId = request.OwnerPositionId,
        CurrentOwnerPersonId = request.CurrentOwnerPersonId,
        OwnerId = request.OwnerId ?? string.Empty,
        EffectiveFrom = request.EffectiveFrom,
        EffectiveTo = request.EffectiveTo
    };

    private static PlanningCycleDto Map(UpdatePlanningCycleRequest request) => new()
    {
        Code = request.Code,
        Name = request.Name,
        PlanningCycleType = request.PlanningCycleType,
        Description = request.Description ?? string.Empty,
        Status = request.Status ?? string.Empty,
        OwnerCompanyId = request.OwnerCompanyId ?? string.Empty,
        OwnerPositionId = request.OwnerPositionId,
        CurrentOwnerPersonId = request.CurrentOwnerPersonId,
        OwnerId = request.OwnerId ?? string.Empty,
        EffectiveFrom = request.EffectiveFrom,
        EffectiveTo = request.EffectiveTo
    };

    private static StrategyPeriodDto Map(CreateStrategyPeriodRequest request) => new()
    {
        PlanningCycleId = request.PlanningCycleId,
        Code = request.Code,
        Name = request.Name,
        OwnerCompanyId = request.OwnerCompanyId,
        OwnerEmployeeId = string.IsNullOrWhiteSpace(request.CurrentOwnerPersonId) ? request.OwnerEmployeeId : request.CurrentOwnerPersonId,
        OwnerPositionId = request.OwnerPositionId,
        CurrentOwnerPersonId = string.IsNullOrWhiteSpace(request.CurrentOwnerPersonId) ? request.OwnerEmployeeId : request.CurrentOwnerPersonId,
        CompanyId = request.CompanyId,
        BusinessUnitId = request.BusinessUnitId,
        RegionId = request.RegionId,
        StartDate = request.StartDate,
        EndDate = request.EndDate,
        ReviewCadence = request.ReviewCadence,
        ScenarioType = request.ScenarioType,
        VersionLabel = request.VersionLabel,
        Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status,
        IsDefaultForScope = request.IsDefaultForScope,
        Notes = request.Notes ?? string.Empty
    };

    private static StrategyPeriodDto Map(UpdateStrategyPeriodRequest request) => new()
    {
        PlanningCycleId = request.PlanningCycleId,
        Code = request.Code,
        Name = request.Name,
        OwnerCompanyId = request.OwnerCompanyId,
        OwnerEmployeeId = string.IsNullOrWhiteSpace(request.CurrentOwnerPersonId) ? request.OwnerEmployeeId : request.CurrentOwnerPersonId,
        OwnerPositionId = request.OwnerPositionId,
        CurrentOwnerPersonId = string.IsNullOrWhiteSpace(request.CurrentOwnerPersonId) ? request.OwnerEmployeeId : request.CurrentOwnerPersonId,
        CompanyId = request.CompanyId,
        BusinessUnitId = request.BusinessUnitId,
        RegionId = request.RegionId,
        StartDate = request.StartDate,
        EndDate = request.EndDate,
        ReviewCadence = request.ReviewCadence,
        ScenarioType = request.ScenarioType,
        VersionLabel = request.VersionLabel,
        Status = request.Status ?? string.Empty,
        IsDefaultForScope = request.IsDefaultForScope,
        Notes = request.Notes ?? string.Empty
    };

    private static PlanningCycleListItemDto ToListItem(PlanningCycleDto dto) => new()
    {
        Id = dto.Id,
        Code = dto.Code,
        Name = dto.Name,
        PlanningCycleType = dto.PlanningCycleType,
        Status = dto.Status,
        OwnerCompanyId = dto.OwnerCompanyId,
        OwnerPositionId = dto.OwnerPositionId,
        CurrentOwnerPersonId = dto.CurrentOwnerPersonId,
        OwnerId = dto.OwnerId,
        EffectiveFrom = dto.EffectiveFrom,
        EffectiveTo = dto.EffectiveTo
    };

    private static PlanningCycleDetailDto ToDetail(PlanningCycleDto dto) => new()
    {
        Id = dto.Id,
        Code = dto.Code,
        Name = dto.Name,
        PlanningCycleType = dto.PlanningCycleType,
        Description = dto.Description,
        Status = dto.Status,
        OwnerCompanyId = dto.OwnerCompanyId,
        OwnerPositionId = dto.OwnerPositionId,
        CurrentOwnerPersonId = dto.CurrentOwnerPersonId,
        OwnerId = dto.OwnerId,
        EffectiveFrom = dto.EffectiveFrom,
        EffectiveTo = dto.EffectiveTo,
        CreatedOn = dto.CreatedOn,
        CreatedBy = dto.CreatedBy,
        UpdatedOn = dto.UpdatedOn,
        UpdatedBy = dto.UpdatedBy,
        ArchivedAt = dto.ArchivedAt
    };

    private static StrategyPeriodListItemDto ToListItem(StrategyPeriodDto dto, IReadOnlyDictionary<string, PlanningCycleDto> cycleMap)
    {
        cycleMap.TryGetValue(dto.PlanningCycleId, out var cycle);
        return new StrategyPeriodListItemDto
        {
            Id = dto.Id,
            PlanningCycleId = dto.PlanningCycleId,
            PlanningCycleCode = cycle?.Code ?? string.Empty,
            PlanningCycleName = cycle?.Name ?? string.Empty,
            Code = dto.Code,
            Name = dto.Name,
            OwnerCompanyId = dto.OwnerCompanyId,
            OwnerEmployeeId = dto.OwnerEmployeeId,
            OwnerPositionId = dto.OwnerPositionId,
            CurrentOwnerPersonId = dto.CurrentOwnerPersonId,
            CompanyId = dto.CompanyId,
            BusinessUnitId = dto.BusinessUnitId,
            RegionId = dto.RegionId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ReviewCadence = dto.ReviewCadence,
            Status = dto.Status,
            IsDefaultForScope = dto.IsDefaultForScope
        };
    }

    private static StrategyPeriodDetailDto ToDetail(StrategyPeriodDto dto, PlanningCycleDto? cycle) => new()
    {
        Id = dto.Id,
        PlanningCycleId = dto.PlanningCycleId,
        PlanningCycleCode = cycle?.Code ?? string.Empty,
        PlanningCycleName = cycle?.Name ?? string.Empty,
        Code = dto.Code,
        Name = dto.Name,
        OwnerCompanyId = dto.OwnerCompanyId,
        OwnerEmployeeId = dto.OwnerEmployeeId,
        OwnerPositionId = dto.OwnerPositionId,
        CurrentOwnerPersonId = dto.CurrentOwnerPersonId,
        CompanyId = dto.CompanyId,
        BusinessUnitId = dto.BusinessUnitId,
        RegionId = dto.RegionId,
        StartDate = dto.StartDate,
        EndDate = dto.EndDate,
        ReviewCadence = dto.ReviewCadence,
        ScenarioType = dto.ScenarioType,
        VersionLabel = dto.VersionLabel,
        Status = dto.Status,
        IsDefaultForScope = dto.IsDefaultForScope,
        Notes = dto.Notes,
        CreatedOn = dto.CreatedOn,
        CreatedBy = dto.CreatedBy,
        UpdatedOn = dto.UpdatedOn,
        UpdatedBy = dto.UpdatedBy,
        ArchivedAt = dto.ArchivedAt
    };
}
