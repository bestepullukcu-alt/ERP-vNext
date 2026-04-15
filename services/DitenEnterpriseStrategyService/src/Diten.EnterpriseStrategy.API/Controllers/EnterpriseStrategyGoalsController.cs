using Asp.Versioning;
using Diten.Application.Commands.EnterpriseStrategyCommands;
using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Application.Queries.EnterpriseStrategyQueries;
using Diten.WebAPI.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Diten.WebAPI.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/enterprise-strategy/goals")]
public sealed class EnterpriseStrategyGoalsController : EnterpriseStrategyApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICorrelationContextAccessor _correlation;

    public EnterpriseStrategyGoalsController(IMediator mediator, ICorrelationContextAccessor correlation)
    {
        _mediator = mediator;
        _correlation = correlation;
    }

    [HttpGet]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.GoalView)]
    public async Task<ActionResult<Response<PagedResponseDto<GoalDto>>>> List([FromQuery] PagedRequestDto request, CancellationToken ct)
        => HandleResult(await _mediator.Send(new ListGoalsQuery { Request = request }, ct), _correlation.CorrelationId);

    [HttpPost]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.GoalCreate)]
    public async Task<ActionResult<Response<CreateGoalResponseDto>>> Create([FromBody] CreateGoalRequestDto body, CancellationToken ct)
    {
        var mapped = ToGoalDto(body);
        var result = await _mediator.Send(new CreateGoalCommand
        {
            Goal = mapped,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct);
        if (!result.Success)
            return HandleResult(Response<CreateGoalResponseDto>.Fail(result.Error?.Code ?? EnterpriseStrategyErrorCodes.ValidationError, result.Error?.Details), _correlation.CorrelationId);

        var response = ToCreateResponse(result.Data!);
        return HandleResult(Response<CreateGoalResponseDto>.Ok(response), _correlation.CorrelationId);
    }

    [HttpGet("{goalId}")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.GoalView)]
    public async Task<ActionResult<Response<GoalDetailDto>>> Get(string goalId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetGoalByIdQuery { GoalId = goalId }, ct), _correlation.CorrelationId);

    [HttpPut("{goalId}")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.GoalEdit)]
    public async Task<ActionResult<Response<GoalDto>>> Update(string goalId, [FromBody] GoalDto body, [FromHeader(Name = "If-Match")] int expectedVersion, CancellationToken ct)
        => HandleResult(await _mediator.Send(new UpdateGoalCommand
        {
            GoalId = goalId,
            Goal = body,
            ExpectedVersion = expectedVersion,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPatch("{goalId}/status")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.GoalActivate)]
    public async Task<ActionResult<Response<GoalDto>>> ChangeStatus(string goalId, [FromBody] StatusChangeRequestDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new ChangeGoalStatusCommand
        {
            GoalId = goalId,
            Status = body.Status,
            ExpectedVersion = body.ExpectedVersion,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPost("{goalId}/archive")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.GoalArchive)]
    public async Task<ActionResult<Response<GoalDto>>> Archive(string goalId, [FromBody] MutationMetadataDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new ArchiveGoalCommand
        {
            GoalId = goalId,
            ExpectedVersion = body.ExpectedVersion,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpPost("{goalId}/restore")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.GoalEdit)]
    public async Task<ActionResult<Response<GoalDto>>> Restore(string goalId, [FromBody] MutationMetadataDto body, CancellationToken ct)
        => HandleResult(await _mediator.Send(new RestoreGoalCommand
        {
            GoalId = goalId,
            ExpectedVersion = body.ExpectedVersion,
            Actor = User?.Identity?.Name ?? "anonymous",
            CorrelationId = _correlation.CorrelationId
        }, ct), _correlation.CorrelationId);

    [HttpGet("{goalId}/objectives")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.ObjectiveView)]
    public async Task<ActionResult<Response<IReadOnlyList<ObjectiveDto>>>> Objectives(string goalId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetGoalObjectivesQuery { GoalId = goalId }, ct), _correlation.CorrelationId);

    [HttpGet("{goalId}/summary")]
    [EnterpriseStrategyPermission(EnterpriseStrategyPermissions.GoalView)]
    public async Task<ActionResult<Response<GoalSummaryDto>>> Summary(string goalId, CancellationToken ct)
        => HandleResult(await _mediator.Send(new GetGoalSummaryQuery { GoalId = goalId }, ct), _correlation.CorrelationId);

    private static GoalDto ToGoalDto(CreateGoalRequestDto request)
    {
        var scope = request.CompanyScope ?? new CreateGoalCompanyScopeDto();
        var planning = request.Planning ?? new CreateGoalPlanningDto();
        var governance = request.Governance ?? new CreateGoalGovernanceDto();
        var planningStart = planning.StartDate.HasValue
            ? DateTime.SpecifyKind(planning.StartDate.Value.Date, DateTimeKind.Utc)
            : (planning.StartYear.HasValue
                ? DateTime.SpecifyKind(new DateTime(planning.StartYear.Value, 1, 1), DateTimeKind.Utc)
                : (DateTime?)null);
        var planningEnd = planning.EndDate.HasValue
            ? DateTime.SpecifyKind(planning.EndDate.Value.Date, DateTimeKind.Utc)
            : (planning.EndYear.HasValue
                ? DateTime.SpecifyKind(new DateTime(planning.EndYear.Value, 12, 31), DateTimeKind.Utc)
                : (DateTime?)null);
        var scopeMode = NormalizeScopeMode(FirstNonEmpty(scope.ApplicabilityMode, scope.ScopeModeCode));
        var goalTitle = FirstNonEmpty(request.GoalTitle, request.Goal);
        var ownerRole = FirstNonEmpty(request.OwnerRole, request.OwnerId);
        var applicableCompanyIds = (scope.ApplicableCompanyIds ?? new())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var ownerCompanyId = FirstNonEmpty(
            request.OwnerCompanyId,
            scope.PrimaryCompanyId,
            applicableCompanyIds.FirstOrDefault());
        var budgetRows = request.BudgetEnvelopes is { Count: > 0 }
            ? request.BudgetEnvelopes
            : (request.YearlyBudgets ?? new());
        var appliesToAllCompanies =
            scope.AppliesToAllCompanies ||
            scope.AppliesToAllCompaniesFlag ||
            string.Equals(scopeMode, "Enterprise", StringComparison.OrdinalIgnoreCase);

        return new GoalDto
        {
            GoalId = string.Empty,
            GoalTitle = goalTitle?.Trim() ?? string.Empty,
            Category = FirstNonEmpty(request.Category, request.CategoryCode)?.Trim() ?? string.Empty,
            StrategicThemeId = request.StrategicThemeId?.Trim() ?? string.Empty,
            OwnerRole = ownerRole?.Trim() ?? string.Empty,
            OwnerCompanyId = ownerCompanyId?.Trim() ?? string.Empty,
            OwnerPersonId = string.IsNullOrWhiteSpace(request.CurrentOwnerPersonId) ? (string.IsNullOrWhiteSpace(request.OwnerPersonId) ? null : request.OwnerPersonId.Trim()) : request.CurrentOwnerPersonId.Trim(),
            Status = FirstNonEmpty(request.Status, request.StatusCode)?.Trim() ?? string.Empty,
            Priority = FirstNonEmpty(request.Priority, request.PriorityCode)?.Trim() ?? string.Empty,
            GoalStatement = request.GoalStatement?.Trim() ?? string.Empty,
            StartDate = planningStart,
            EndDate = planningEnd,
            StrategyPeriodId = string.IsNullOrWhiteSpace(planning.StrategyPeriodId) ? null : planning.StrategyPeriodId.Trim(),
            RelatedEntityScope = planning.RelatedEntityScope?.Trim() ?? string.Empty,
            ApplicabilityMode = scopeMode,
            AppliesToAllCompanies = appliesToAllCompanies,
            ApplicableCompanyIds = applicableCompanyIds,
            ChangeLogRef = planning.ChangeLogRef?.Trim(),
            DecisionReference = governance.DecisionReference?.Trim(),
            EvidenceLink = governance.EvidenceLink?.Trim(),
            Version = 1,
            BudgetEnvelopes = budgetRows.Select(x => new GoalYearlyBudgetEnvelopeDto
            {
                Year = x.Year,
                RevenueTarget = x.RevenueTarget,
                EbitdaTarget = x.EbitdaTarget,
                CapexEnvelope = x.CapexEnvelope,
                OpexEnvelope = x.OpexEnvelope,
                SavingsTarget = x.SavingsTarget,
                FundingPool = x.FundingPool ?? x.FundingPoolEnvelope,
                Commentary = x.Commentary
            }).ToList(),
            SourceTemplateType = string.IsNullOrWhiteSpace(request.CreationModeCode) ? null : request.CreationModeCode.Trim(),
            SourceTemplateId = string.IsNullOrWhiteSpace(request.SourceTemplateId) ? null : request.SourceTemplateId.Trim(),
            SourceTemplateVersion = request.SourceTemplateVersion,
            SourceBlueprintPackId = string.IsNullOrWhiteSpace(request.SourceBlueprintPackId) ? null : request.SourceBlueprintPackId.Trim(),
            CreatedFromLibrary = !string.IsNullOrWhiteSpace(request.SourceTemplateId) || !string.IsNullOrWhiteSpace(request.SourceBlueprintPackId),
            SaveAsTemplate = request.SaveAsTemplate,
            TemplateSave = request.TemplateSave,
            Metrics = (request.Metrics ?? new()).Select((m, i) => new GoalMetricDto
            {
                // Yearly targets are the authoritative metric-value model.
                // Baseline/target are derived for backward compatibility.
                // They are still persisted for legacy reads/exports.
                // This keeps create payloads deterministic across old/new clients.
                Id = string.Empty,
                MetricAssignmentId = string.IsNullOrWhiteSpace(m.MetricAssignmentId) ? string.Empty : m.MetricAssignmentId.Trim(),
                MetricDefinitionId = FirstNonEmpty(m.MetricDefinitionId, m.MetricDefId)?.Trim() ?? string.Empty,
                MetricName = m.MetricName?.Trim() ?? string.Empty,
                MetricType = FirstNonEmpty(m.MetricType, m.MetricTypeCode)?.Trim() ?? string.Empty,
                BaselineValue = ResolveBaselineValue(m),
                TargetValue = ResolveTargetValue(m),
                UnitOfMeasure = FirstNonEmpty(m.UnitOfMeasure, m.UnitOfMeasureCode)?.Trim() ?? string.Empty,
                AggregationMethod = FirstNonEmpty(m.AggregationMethod, m.AggregationMethodCode)?.Trim() ?? string.Empty,
                DirectionPolarity = FirstNonEmpty(m.DirectionPolarity, m.PolarityCode)?.Trim() ?? string.Empty,
                ThresholdModel = FirstNonEmpty(m.ThresholdModel, m.ThresholdModelCode)?.Trim() ?? string.Empty,
                ReportingFrequency = FirstNonEmpty(m.ReportingFrequency, m.ReportingFrequencyCode)?.Trim() ?? string.Empty,
                CascadeMetric = m.CascadeMetric,
                MetricOrigin = string.IsNullOrWhiteSpace(m.MetricOrigin) ? "Local" : m.MetricOrigin.Trim(),
                MetricRole = string.IsNullOrWhiteSpace(m.MetricRole) ? "Strategic" : m.MetricRole.Trim(),
                RestrictionMode = string.IsNullOrWhiteSpace(m.RestrictionMode) ? "GoalGovernedStructure" : m.RestrictionMode.Trim(),
                RollupEligible = m.RollupEligible,
                YearlyValues = GoalContractNormalizer.CoalesceCreateMetricYears(m).Select(y => new GoalMetricYearValueDto
                {
                    Year = y.Year,
                    BaselineValue = y.BaselineValue,
                    TargetValue = y.TargetValue,
                    ActualValue = y.ActualValue,
                    ForecastValue = y.ForecastValue,
                    ThresholdMin = y.ThresholdMin,
                    ThresholdMax = y.ThresholdMax,
                    Commentary = string.IsNullOrWhiteSpace(y.Commentary) ? y.ThresholdCommentary : y.Commentary
                }).OrderBy(y => y.Year).ToList(),
                SortOrder = m.SortOrder <= 0 ? i + 1 : m.SortOrder,
                MetricBindingStatus = "Bound"
            }).ToList()
        };
    }

    private static CreateGoalResponseDto ToCreateResponse(GoalDto goal)
    {
        var startDate = goal.StartDate?.Date ?? goal.PlanningHorizonStart?.Date;
        var endDate = goal.EndDate?.Date ?? goal.PlanningHorizonEnd?.Date;
        return new CreateGoalResponseDto
        {
            GoalId = goal.GoalId,
            GoalTitle = goal.GoalTitle,
            CreationModeCode = goal.SourceTemplateType,
            SourceTemplateId = goal.SourceTemplateId,
            SourceTemplateVersion = goal.SourceTemplateVersion,
            SourceBlueprintPackId = goal.SourceBlueprintPackId,
            Category = goal.Category,
            StrategicThemeId = goal.StrategicThemeId,
            OwnerRole = goal.OwnerRole,
            OwnerCompanyId = goal.OwnerCompanyId,
            OwnerPersonId = goal.OwnerPersonId,
            OwnerDisplayName = goal.OwnerPersonId ?? string.Empty,
            Status = goal.Status,
            Priority = goal.Priority,
            GoalStatement = goal.GoalStatement,
            Planning = new CreateGoalPlanningDto
            {
                StartDate = startDate,
                EndDate = endDate,
                StrategyPeriodId = goal.StrategyPeriodId,
                RelatedEntityScope = goal.RelatedEntityScope,
                ChangeLogRef = goal.ChangeLogRef
            },
            CompanyScope = new CreateGoalCompanyScopeDto
            {
                ApplicabilityMode = goal.ApplicabilityMode,
                AppliesToAllCompanies = goal.AppliesToAllCompanies,
                RelatedEntityScopeSummary = BuildRelatedEntityScopeSummary(goal),
                PrimaryCompanyId = goal.OwnerCompanyId,
                ApplicableCompanyIds = goal.ApplicableCompanyIds
            },
            BudgetEnvelopes = (goal.BudgetEnvelopes ?? new()).Select(x => new CreateGoalYearlyBudgetDto
            {
                Year = x.Year,
                RevenueTarget = x.RevenueTarget,
                EbitdaTarget = x.EbitdaTarget,
                CapexEnvelope = x.CapexEnvelope,
                OpexEnvelope = x.OpexEnvelope,
                SavingsTarget = x.SavingsTarget,
                FundingPool = x.FundingPool,
                Commentary = x.Commentary
            }).ToList(),
            Metrics = (goal.Metrics ?? new()).Select((m, i) => new CreateGoalResponseMetricDto
            {
                Id = m.Id,
                MetricAssignmentId = string.IsNullOrWhiteSpace(m.MetricAssignmentId) ? m.Id : m.MetricAssignmentId,
                GoalId = goal.GoalId,
                MetricDefinitionId = m.MetricDefinitionId,
                MetricName = m.MetricName,
                MetricType = m.MetricType,
                BaselineValue = m.BaselineValue,
                TargetValue = m.TargetValue,
                UnitOfMeasure = m.UnitOfMeasure,
                AggregationMethod = m.AggregationMethod,
                DirectionPolarity = m.DirectionPolarity,
                ThresholdModel = m.ThresholdModel,
                ReportingFrequency = m.ReportingFrequency,
                CascadeMetric = m.CascadeMetric,
                MetricOrigin = m.MetricOrigin,
                MetricRole = m.MetricRole,
                RestrictionMode = m.RestrictionMode,
                RollupEligible = m.RollupEligible,
                YearlyValues = (m.YearlyValues ?? new()).OrderBy(y => y.Year).Select(y => new CreateGoalMetricYearDto
                {
                    Year = y.Year,
                    BaselineValue = y.BaselineValue,
                    TargetValue = y.TargetValue,
                    ActualValue = y.ActualValue,
                    ForecastValue = y.ForecastValue,
                    ThresholdMin = y.ThresholdMin,
                    ThresholdMax = y.ThresholdMax,
                    Commentary = y.Commentary,
                    ThresholdCommentary = y.ThresholdCommentary
                }).ToList(),
                SortOrder = m.SortOrder <= 0 ? i + 1 : m.SortOrder
            }).ToList(),
            Governance = new CreateGoalGovernanceDto
            {
                DecisionReference = goal.DecisionReference,
                EvidenceLink = goal.EvidenceLink
            },
            Version = goal.Version,
            CreatedAtUtc = goal.CreatedAt,
            CreatedBy = goal.CreatedBy ?? string.Empty,
            SavedTemplateId = goal.SavedTemplateId
        };
    }

    private static string NormalizeScopeMode(string? code)
    {
        var value = (code ?? string.Empty).Trim();
        if (value.Equals("SINGLE_COMPANY", StringComparison.OrdinalIgnoreCase)) return "SingleCompany";
        if (value.Equals("MULTI_COMPANY", StringComparison.OrdinalIgnoreCase)) return "MultiCompany";
        if (value.Equals("ENTERPRISE", StringComparison.OrdinalIgnoreCase)) return "Enterprise";
        if (value.Equals("AppliesToSelectedCompanies", StringComparison.OrdinalIgnoreCase)) return "MultiCompany";
        return value;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string BuildRelatedEntityScopeSummary(GoalDto goal)
    {
        if (string.IsNullOrWhiteSpace(goal.RelatedEntityScope))
            return string.Empty;
        if (goal.AppliesToAllCompanies || string.Equals(goal.ApplicabilityMode, "Enterprise", StringComparison.OrdinalIgnoreCase))
            return $"{goal.RelatedEntityScope} | All Companies";
        var companyCount = goal.ApplicableCompanyIds?.Count ?? 0;
        if (companyCount > 0)
            return $"{goal.RelatedEntityScope} | {companyCount} companies";
        return goal.RelatedEntityScope;
    }

    private static decimal ResolveBaselineValue(CreateGoalMetricDto metric)
    {
        var yearly = GoalContractNormalizer.CoalesceCreateMetricYears(metric).OrderBy(x => x.Year).ToList();
        return yearly.Count > 0 ? (yearly[0].TargetValue ?? 0m) : (metric.BaselineValue ?? 0m);
    }

    private static decimal ResolveTargetValue(CreateGoalMetricDto metric)
    {
        var yearly = GoalContractNormalizer.CoalesceCreateMetricYears(metric).OrderBy(x => x.Year).ToList();
        return yearly.Count > 0 ? (yearly[^1].TargetValue ?? 0m) : (metric.TargetValue ?? 0m);
    }
}
