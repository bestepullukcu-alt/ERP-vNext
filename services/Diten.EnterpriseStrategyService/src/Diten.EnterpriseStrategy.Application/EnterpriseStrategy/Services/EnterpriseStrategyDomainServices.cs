using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Events;
using Diten.Application.EnterpriseStrategy.Mappers;
using Diten.Application.EnterpriseStrategy.Repositories;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Application.EnterpriseStrategy.Validators;
using Diten.Domain.Aggregates.EnterpriseStrategy;

namespace Diten.Application.EnterpriseStrategy.Services;

public interface IGoalService
{
    Task<Response<PagedResponseDto<GoalDto>>> ListAsync(PagedRequestDto request, CancellationToken cancellationToken = default);
    Task<Response<GoalDetailDto>> GetAsync(string goalId, CancellationToken cancellationToken = default);
    Task<Response<GoalDto>> CreateAsync(GoalDto goal, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<GoalDto>> UpdateAsync(string goalId, GoalDto goal, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<GoalDto>> ChangeStatusAsync(string goalId, string status, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<GoalDto>> ArchiveAsync(string goalId, int expectedVersion, bool archiveGuardEnabled, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<GoalDto>> RestoreAsync(string goalId, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<ObjectiveDto>>> GetObjectivesAsync(string goalId, CancellationToken cancellationToken = default);
    Task<Response<GoalSummaryDto>> GetSummaryAsync(string goalId, CancellationToken cancellationToken = default);
    Task<Response<GoalPlanningContextDto>> ResolvePlanningContextAsync(string goalId, CancellationToken cancellationToken = default);
}

public interface IObjectiveService
{
    Task<Response<PagedResponseDto<ObjectiveDto>>> ListAsync(PagedRequestDto request, CancellationToken cancellationToken = default);
    Task<Response<ObjectiveDetailDto>> GetAsync(string objectiveId, CancellationToken cancellationToken = default);
    Task<Response<ObjectiveDto>> CreateAsync(ObjectiveDto objective, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<ObjectiveDto>> UpdateAsync(string objectiveId, ObjectiveDto objective, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<ObjectiveDto>> ChangeStatusAsync(string objectiveId, string status, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<ObjectiveDto>> ArchiveAsync(string objectiveId, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<ObjectiveDto>> RestoreAsync(string objectiveId, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<InitiativeStrategyLinkViewDto>>> GetInitiativesAsync(string objectiveId, CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<ProjectStrategyLinkViewDto>>> GetProjectsAsync(string objectiveId, CancellationToken cancellationToken = default);
    Task<Response<ObjectiveAlignmentSummaryDto>> GetAlignmentSummaryAsync(string objectiveId, CancellationToken cancellationToken = default);
}

public interface IConnectionService
{
    Task<Response<PagedResponseDto<StrategyConnectionDto>>> ListAsync(PagedRequestDto request, CancellationToken cancellationToken = default);
    Task<Response<StrategyConnectionDto>> GetAsync(string connectionId, CancellationToken cancellationToken = default);
    Task<Response<StrategyConnectionDto>> CreateAsync(StrategyConnectionDto connection, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<StrategyConnectionDto>> UpdateAsync(string connectionId, StrategyConnectionDto connection, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<StrategyConnectionDto>> ChangeStatusAsync(string connectionId, string status, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<bool>> DeleteAsync(string connectionId, CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<ConnectionTreeNodeDto>>> TreeAsync(CancellationToken cancellationToken = default);
    Task<Response<ConnectionGraphViewDto>> GraphAsync(CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<ConnectionMatrixCellDto>>> MatrixAsync(string mode, CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<CoverageGapDto>>> CoverageGapsAsync(CancellationToken cancellationToken = default);
    Task<Response<ConnectionGraphViewDto>> ValidateGraphAsync(CancellationToken cancellationToken = default);
}

public sealed class GoalService : IGoalService
{
    private readonly IGoalRepository _goals;
    private readonly IObjectiveRepository _objectives;
    private readonly IInitiativeStrategyLinkRepository _initiativeLinks;
    private readonly IProjectStrategyLinkRepository _projectLinks;
    private readonly IStrategyPeriodRepository _strategyPeriods;
    private readonly IEnterpriseStrategyAuditSink _audit;
    private readonly IGoalTemplateSnapshotWriter? _goalTemplateSnapshot;

    public GoalService(
        IGoalRepository goals,
        IObjectiveRepository objectives,
        IInitiativeStrategyLinkRepository initiativeLinks,
        IProjectStrategyLinkRepository projectLinks,
        IStrategyPeriodRepository strategyPeriods,
        IEnterpriseStrategyAuditSink audit,
        IGoalTemplateSnapshotWriter? goalTemplateSnapshot = null)
    {
        _goals = goals;
        _objectives = objectives;
        _initiativeLinks = initiativeLinks;
        _projectLinks = projectLinks;
        _strategyPeriods = strategyPeriods;
        _audit = audit;
        _goalTemplateSnapshot = goalTemplateSnapshot;
    }

    public async Task<Response<PagedResponseDto<GoalDto>>> ListAsync(PagedRequestDto request, CancellationToken cancellationToken = default)
    {
        var rows = await _goals.ListAsync(cancellationToken);
        var objectives = await _objectives.ListAsync(cancellationToken);
        var projectLinks = await _projectLinks.ListAsync(cancellationToken);
        var initiativeLinks = await _initiativeLinks.ListAsync(cancellationToken);
        var objectiveCountsByGoal = objectives
            .GroupBy(o => o.ParentGoalId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        var projectCountsByGoal = projectLinks
            .GroupBy(p => p.ParentGoalId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        IEnumerable<GoalAggregate> query = rows;
        var f = request.Filters;
        if (f.TryGetValue("category", out var category)) query = query.Where(x => string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("owner", out var owner)) query = query.Where(x => string.Equals(x.Owner, owner, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("status", out var status)) query = query.Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("priority", out var priority)) query = query.Where(x => string.Equals(x.Priority, priority, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("scopeMode", out var scopeMode)) query = query.Where(x => string.Equals(x.ScopeMode, scopeMode, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("company", out var company))
            query = query.Where(x =>
                string.Equals(x.PrimaryCompanyId, company, StringComparison.OrdinalIgnoreCase) ||
                x.ApplicableCompanyIds.Any(c => string.Equals(c, company, StringComparison.OrdinalIgnoreCase)));
        if (f.TryGetValue("entityScope", out var scope)) query = query.Where(x => string.Equals(x.EntityScope, scope, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(x => x.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase) || x.Statement.Contains(request.Search, StringComparison.OrdinalIgnoreCase));

        Func<GoalAggregate, object> sort = (request.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "name" => x => x.Name,
            "owner" => x => x.Owner,
            "priority" => x => x.Priority,
            "objectivecount" => x => objectiveCountsByGoal.TryGetValue(x.Id, out var count) ? count : 0,
            "projectcoveragecount" => x => projectCountsByGoal.TryGetValue(x.Id, out var count) ? count : 0,
            _ => x => x.UpdatedAt
        };
        query = string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase) ? query.OrderBy(sort) : query.OrderByDescending(sort);

        var total = query.Count();
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 10_000);
        var items = query.Skip((page - 1) * size).Take(size).Select(x => x.ToDto()).ToList();

        return Response<PagedResponseDto<GoalDto>>.Ok(new PagedResponseDto<GoalDto> { Page = page, PageSize = size, TotalCount = total, Items = items });
    }

    public async Task<Response<GoalDetailDto>> GetAsync(string goalId, CancellationToken cancellationToken = default)
    {
        var goal = await _goals.GetByIdAsync(goalId, cancellationToken);
        if (goal is null) return Response<GoalDetailDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        var summary = await GetSummaryInternal(goalId, cancellationToken);
        return Response<GoalDetailDto>.Ok(new GoalDetailDto
        {
            Goal = goal.ToDto(),
            ChildObjectivesSummary = summary.ChildObjectivesSummary,
            LinkedInitiativesCount = summary.LinkedInitiativesCount,
            LinkedProjectsCount = summary.LinkedProjectsCount,
            AuditSummary = summary.AuditSummary
        });
    }

    public async Task<Response<GoalDto>> CreateAsync(GoalDto goal, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        goal.Version = 1;
        NormalizeGoalPlanningDates(goal);
        goal.OwnerRole = string.IsNullOrWhiteSpace(goal.OwnerRole)
            ? (string.IsNullOrWhiteSpace(goal.OwnerId) ? goal.Owner : goal.OwnerId)
            : goal.OwnerRole;
        if (string.IsNullOrWhiteSpace(goal.OwnerCompanyId))
            goal.OwnerCompanyId = !string.IsNullOrWhiteSpace(goal.PrimaryCompanyId)
                ? goal.PrimaryCompanyId!
                : ((goal.ApplicableCompanyIds ?? new()).FirstOrDefault() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(goal.ApplicabilityMode))
            goal.ApplicabilityMode = goal.ScopeMode;
        goal.AppliesToAllCompanies = goal.AppliesToAllCompanies || goal.AppliesToAllCompaniesFlag;
        goal.ApplicableCompanyIds = (goal.ApplicableCompanyIds ?? new())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (string.Equals(goal.ApplicabilityMode, "SingleCompany", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(goal.PrimaryCompanyId))
        {
            if (goal.ApplicableCompanyIds.Count == 0)
                goal.ApplicableCompanyIds.Add(goal.PrimaryCompanyId);
            else
                goal.ApplicableCompanyIds = goal.ApplicableCompanyIds
                    .Where(x => string.Equals(x, goal.PrimaryCompanyId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
        }

        GoalContractNormalizer.Normalize(goal);
        if (string.IsNullOrWhiteSpace(goal.OwnerCompanyId) && !string.IsNullOrWhiteSpace(goal.StrategyPeriodId))
        {
            var strategyPeriod = await _strategyPeriods.GetByIdAsync(goal.StrategyPeriodId.Trim(), cancellationToken);
            if (strategyPeriod is not null && !string.IsNullOrWhiteSpace(strategyPeriod.CompanyId))
                goal.OwnerCompanyId = strategyPeriod.CompanyId.Trim();
        }

        if (string.IsNullOrWhiteSpace(goal.Id))
        {
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var existing = await _goals.ListAsync(cancellationToken);
                var candidate = EnterpriseStrategyRuntimeIds.NextGoalId(existing.Select(x => x.Id));
                if (!await _goals.ExistsAsync(candidate, cancellationToken))
                {
                    goal.Id = candidate;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(goal.Id))
                return Response<GoalDto>.Fail(EnterpriseStrategyErrorCodes.InternalError, new() { ["id"] = new() { "Could not allocate a unique goal id." } });
        }
        else if (await _goals.ExistsAsync(goal.Id, cancellationToken))
            return Response<GoalDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, new() { ["id"] = new() { "Goal id must be unique." } });

        var validation = EnterpriseStrategyValidators.ValidateGoal(goal);
        if (validation.Count > 0) return Response<GoalDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, validation);
        var planningValidation = await ValidateGoalStrategyPeriodAssignmentAsync(goal, null, cancellationToken);
        if (planningValidation.Count > 0) return Response<GoalDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, planningValidation);

        var aggregate = ToAggregate(goal, actor);
        await _goals.AddAsync(aggregate, cancellationToken);
        await EmitAudit(actor, correlationId, aggregate.Id, EnterpriseStrategyEventNames.GoalCreated, "", aggregate.Name, cancellationToken);
        string? savedTemplateId = null;
        if (goal.SaveAsTemplate && goal.TemplateSave is not null && _goalTemplateSnapshot is not null)
        {
            try
            {
                savedTemplateId = await _goalTemplateSnapshot.WriteFromGoalAsync(aggregate, goal.TemplateSave, actor, cancellationToken);
            }
            catch
            {
                // Template snapshot is best-effort; goal create already succeeded.
            }
        }

        var createdDto = aggregate.ToDto();
        createdDto.SavedTemplateId = savedTemplateId;
        return Response<GoalDto>.Ok(createdDto);
    }

    public async Task<Response<GoalDto>> UpdateAsync(string goalId, GoalDto goal, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var aggregate = await _goals.GetByIdAsync(goalId, cancellationToken);
        if (aggregate is null) return Response<GoalDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (EnterpriseStrategyResult.IsStaleWrite(expectedVersion, aggregate.Version)) return EnterpriseStrategyResult.StaleVersion<GoalDto>();

        NormalizeGoalPlanningDates(goal);
        GoalContractNormalizer.Normalize(goal);
        if (string.IsNullOrWhiteSpace(goal.OwnerCompanyId) && !string.IsNullOrWhiteSpace(goal.StrategyPeriodId))
        {
            var strategyPeriod = await _strategyPeriods.GetByIdAsync(goal.StrategyPeriodId.Trim(), cancellationToken);
            if (strategyPeriod is not null && !string.IsNullOrWhiteSpace(strategyPeriod.CompanyId))
                goal.OwnerCompanyId = strategyPeriod.CompanyId.Trim();
        }

        var validation = EnterpriseStrategyValidators.ValidateGoal(goal);
        if (validation.Count > 0) return Response<GoalDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, validation);
        var planningValidation = await ValidateGoalStrategyPeriodAssignmentAsync(goal, aggregate.StrategyPeriodId, cancellationToken);
        if (planningValidation.Count > 0) return Response<GoalDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, planningValidation);

        var before = aggregate.Name;
        MapInto(aggregate, goal, actor);
        aggregate.Version++;
        aggregate.UpdatedAt = DateTime.UtcNow;
        await _goals.UpdateAsync(aggregate, cancellationToken);
        await EmitAudit(actor, correlationId, aggregate.Id, EnterpriseStrategyEventNames.GoalUpdated, before, aggregate.Name, cancellationToken);
        string? savedTemplateId = null;
        if (goal.SaveAsTemplate && goal.TemplateSave is not null && _goalTemplateSnapshot is not null)
        {
            try
            {
                savedTemplateId = await _goalTemplateSnapshot.WriteFromGoalAsync(aggregate, goal.TemplateSave, actor, cancellationToken);
            }
            catch
            {
                // Best-effort library snapshot
            }
        }

        var updatedDto = aggregate.ToDto();
        updatedDto.SavedTemplateId = savedTemplateId;
        return Response<GoalDto>.Ok(updatedDto);
    }

    public async Task<Response<GoalDto>> ChangeStatusAsync(string goalId, string status, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var aggregate = await _goals.GetByIdAsync(goalId, cancellationToken);
        if (aggregate is null) return Response<GoalDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (EnterpriseStrategyResult.IsStaleWrite(expectedVersion, aggregate.Version)) return EnterpriseStrategyResult.StaleVersion<GoalDto>();

        aggregate.Status = status;
        aggregate.Version++;
        aggregate.UpdatedBy = actor;
        aggregate.UpdatedAt = DateTime.UtcNow;
        var validation = EnterpriseStrategyValidators.ValidateGoal(aggregate.ToDto());
        if (validation.Count > 0) return Response<GoalDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, validation);

        await _goals.UpdateAsync(aggregate, cancellationToken);
        await EmitAudit(actor, correlationId, aggregate.Id, EnterpriseStrategyEventNames.GoalStatusChanged, "", status, cancellationToken);
        return Response<GoalDto>.Ok(aggregate.ToDto());
    }

    public async Task<Response<GoalDto>> ArchiveAsync(string goalId, int expectedVersion, bool archiveGuardEnabled, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var aggregate = await _goals.GetByIdAsync(goalId, cancellationToken);
        if (aggregate is null) return Response<GoalDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (EnterpriseStrategyResult.IsStaleWrite(expectedVersion, aggregate.Version)) return EnterpriseStrategyResult.StaleVersion<GoalDto>();

        if (archiveGuardEnabled)
        {
            var hasActiveObjectives = (await _objectives.ListAsync(cancellationToken)).Any(x => x.ParentGoalId == goalId && string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase));
            if (hasActiveObjectives)
                return Response<GoalDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, new() { ["status"] = new() { "Cannot archive a goal with active objectives." } });
        }

        aggregate.Status = "Archived";
        aggregate.ArchivedAt = DateTime.UtcNow;
        aggregate.Version++;
        aggregate.UpdatedBy = actor;
        aggregate.UpdatedAt = DateTime.UtcNow;
        await _goals.UpdateAsync(aggregate, cancellationToken);
        await EmitAudit(actor, correlationId, aggregate.Id, EnterpriseStrategyEventNames.GoalArchived, "", aggregate.Status, cancellationToken);
        return Response<GoalDto>.Ok(aggregate.ToDto());
    }

    public async Task<Response<GoalDto>> RestoreAsync(string goalId, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var aggregate = await _goals.GetByIdAsync(goalId, cancellationToken);
        if (aggregate is null) return Response<GoalDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (EnterpriseStrategyResult.IsStaleWrite(expectedVersion, aggregate.Version)) return EnterpriseStrategyResult.StaleVersion<GoalDto>();

        aggregate.Status = "Draft";
        aggregate.ArchivedAt = null;
        aggregate.Version++;
        aggregate.UpdatedBy = actor;
        aggregate.UpdatedAt = DateTime.UtcNow;
        await _goals.UpdateAsync(aggregate, cancellationToken);
        await EmitAudit(actor, correlationId, aggregate.Id, EnterpriseStrategyEventNames.GoalRestored, "", aggregate.Status, cancellationToken);
        return Response<GoalDto>.Ok(aggregate.ToDto());
    }

    public async Task<Response<IReadOnlyList<ObjectiveDto>>> GetObjectivesAsync(string goalId, CancellationToken cancellationToken = default)
    {
        var all = await _objectives.ListAsync(cancellationToken);
        return Response<IReadOnlyList<ObjectiveDto>>.Ok(all.Where(x => x.ParentGoalId == goalId).Select(x => x.ToDto()).ToList());
    }

    public async Task<Response<GoalSummaryDto>> GetSummaryAsync(string goalId, CancellationToken cancellationToken = default)
    {
        var goal = await _goals.GetByIdAsync(goalId, cancellationToken);
        if (goal is null) return Response<GoalSummaryDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        return Response<GoalSummaryDto>.Ok(await GetSummaryInternal(goalId, cancellationToken));
    }

    public async Task<Response<GoalPlanningContextDto>> ResolvePlanningContextAsync(string goalId, CancellationToken cancellationToken = default)
    {
        var goal = await _goals.GetByIdAsync(goalId, cancellationToken);
        if (goal is null)
            return Response<GoalPlanningContextDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);

        return await GoalPlanningContextResolver.ResolveForGoalAsync(goal, _strategyPeriods, cancellationToken);
    }

    private async Task<GoalSummaryDto> GetSummaryInternal(string goalId, CancellationToken cancellationToken)
    {
        var goal = await _goals.GetByIdAsync(goalId, cancellationToken) ?? new GoalAggregate();
        var objectiveRows = (await _objectives.ListAsync(cancellationToken)).Where(x => x.ParentGoalId == goalId).ToList();
        var initiatives = await _initiativeLinks.ListByGoalIdAsync(goalId, cancellationToken);
        var projects = await _projectLinks.ListByGoalIdAsync(goalId, cancellationToken);

        return new GoalSummaryDto
        {
            GoalId = goalId,
            MetricsCount = goal.Metrics.Count,
            ChildObjectivesSummary = new GoalObjectivesSummaryDto
            {
                TotalObjectives = objectiveRows.Count,
                ActiveObjectives = objectiveRows.Count(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase)),
                ArchivedObjectives = objectiveRows.Count(x => string.Equals(x.Status, "Archived", StringComparison.OrdinalIgnoreCase))
            },
            LinkedInitiativesCount = initiatives.Count,
            LinkedProjectsCount = projects.Count,
            DecisionReference = goal.DecisionReference,
            EvidenceReference = goal.EvidenceReference,
            Version = goal.Version,
            AuditSummary = $"v{goal.Version} updated {goal.UpdatedAt:O}"
        };
    }

    private async Task EmitAudit(string actor, string correlationId, string objectId, string action, string before, string after, CancellationToken cancellationToken)
    {
        await _audit.WriteMutationAsync(actor, "Goal", objectId, action, correlationId, "enterprise-strategy.goals", before, after, cancellationToken);
    }

    private static GoalAggregate ToAggregate(GoalDto dto, string actor)
    {
        var goalId = string.IsNullOrWhiteSpace(dto.GoalId) ? Guid.NewGuid().ToString("N") : dto.GoalId;
        return new GoalAggregate
        {
            GoalId = goalId,
            GoalTitle = string.IsNullOrWhiteSpace(dto.GoalTitle) ? dto.Name : dto.GoalTitle,
            Category = dto.Category,
            StrategicThemeId = dto.StrategicThemeId,
            GoalStatement = string.IsNullOrWhiteSpace(dto.GoalStatement) ? dto.Statement : dto.GoalStatement,
            OwnerRole = string.IsNullOrWhiteSpace(dto.OwnerRole) ? (string.IsNullOrWhiteSpace(dto.OwnerId) ? dto.Owner : dto.OwnerId) : dto.OwnerRole,
            OwnerCompanyId = !string.IsNullOrWhiteSpace(dto.OwnerCompanyId) ? dto.OwnerCompanyId : dto.PrimaryCompanyId ?? string.Empty,
            OwnerPersonId = dto.OwnerPersonId,
            Status = dto.Status,
            StartDate = dto.StartDate ?? dto.PlanningHorizonStart,
            EndDate = dto.EndDate ?? dto.PlanningHorizonEnd,
            Priority = dto.Priority,
            RelatedEntityScope = string.IsNullOrWhiteSpace(dto.RelatedEntityScope) ? dto.EntityScope : dto.RelatedEntityScope,
            ApplicabilityMode = string.IsNullOrWhiteSpace(dto.ApplicabilityMode) ? dto.ScopeMode : dto.ApplicabilityMode,
            StrategyPeriodId = string.IsNullOrWhiteSpace(dto.StrategyPeriodId) ? null : dto.StrategyPeriodId.Trim(),
            AppliesToAllCompanies = dto.AppliesToAllCompanies || string.Equals(dto.ApplicabilityMode, "Enterprise", StringComparison.OrdinalIgnoreCase) || dto.AppliesToAllCompaniesFlag,
            ApplicableCompanyIds = dto.ApplicableCompanyIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new(),
            DecisionReference = dto.DecisionReference,
            EvidenceLink = string.IsNullOrWhiteSpace(dto.EvidenceLink) ? dto.EvidenceReference : dto.EvidenceLink,
            ChangeLogRef = dto.ChangeLogRef,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = actor,
            UpdatedBy = actor,
            SourceTemplateType = dto.SourceTemplateType,
            SourceTemplateId = dto.SourceTemplateId,
            SourceTemplateVersion = dto.SourceTemplateVersion,
            SourceBlueprintPackId = dto.SourceBlueprintPackId,
            InstantiationBatchId = dto.InstantiationBatchId,
            CreatedFromLibrary = dto.CreatedFromLibrary,
            Version = 1,
            YearlyBudgets = (dto.BudgetEnvelopes ?? new()).Select(b => new GoalYearlyBudgetEnvelope
            {
                Year = b.Year,
                RevenueTarget = b.RevenueTarget,
                EbitdaTarget = b.EbitdaTarget,
                CapexEnvelope = b.CapexEnvelope,
                OpexEnvelope = b.OpexEnvelope,
                SavingsTarget = b.SavingsTarget,
                FundingPool = b.FundingPoolEnvelope ?? b.FundingPool,
                Commentary = b.Commentary
            }).ToList(),
            Metrics = (dto.Metrics ?? new()).Select((x, index) => new GoalMetric
            {
                Id = string.IsNullOrWhiteSpace(x.Id) ? Guid.NewGuid().ToString("N") : x.Id,
                MetricAssignmentId = string.IsNullOrWhiteSpace(x.MetricAssignmentId) ? (x.Id ?? string.Empty) : x.MetricAssignmentId,
                GoalId = goalId,
                MetricDefinitionId = string.IsNullOrWhiteSpace(x.MetricDefinitionId) ? x.MetricDefId : x.MetricDefinitionId,
                MetricName = x.MetricName,
                MetricType = x.MetricType,
                BaselineValue = ResolveBaselineValue(x),
                TargetValue = ResolveTargetValue(x),
                UnitOfMeasure = x.UnitOfMeasure,
                AggregationMethod = x.AggregationMethod,
                DirectionPolarity = x.DirectionPolarity,
                ThresholdModel = x.ThresholdModel,
                ReportingFrequency = x.ReportingFrequency,
                CascadeMetric = x.CascadeMetric,
                MetricOrigin = NormalizeIncomingMetricOrigin(x.MetricOrigin),
                MetricRole = string.IsNullOrWhiteSpace(x.MetricRole) ? "Strategic" : x.MetricRole.Trim(),
                RestrictionMode = string.IsNullOrWhiteSpace(x.RestrictionMode) ? "GoalGovernedStructure" : x.RestrictionMode,
                RollupEligible = x.RollupEligible,
                YearlyTargets = (x.YearlyValues ?? new()).OrderBy(y => y.Year).Select(y => new GoalMetricYearValue
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
                SortOrder = x.SortOrder <= 0 ? index + 1 : x.SortOrder,
                MetricBindingStatus = x.MetricBindingStatus,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList()
        };
    }

    private static void MapInto(GoalAggregate target, GoalDto dto, string actor)
    {
        target.GoalTitle = string.IsNullOrWhiteSpace(dto.GoalTitle) ? dto.Name : dto.GoalTitle;
        target.Category = dto.Category;
        target.StrategicThemeId = dto.StrategicThemeId;
        target.GoalStatement = string.IsNullOrWhiteSpace(dto.GoalStatement) ? dto.Statement : dto.GoalStatement;
        target.OwnerRole = string.IsNullOrWhiteSpace(dto.OwnerRole) ? (string.IsNullOrWhiteSpace(dto.OwnerId) ? dto.Owner : dto.OwnerId) : dto.OwnerRole;
        target.OwnerCompanyId = !string.IsNullOrWhiteSpace(dto.OwnerCompanyId) ? dto.OwnerCompanyId : dto.PrimaryCompanyId ?? string.Empty;
        target.OwnerPersonId = dto.OwnerPersonId;
        target.Status = dto.Status;
        target.StartDate = dto.StartDate ?? dto.PlanningHorizonStart;
        target.EndDate = dto.EndDate ?? dto.PlanningHorizonEnd;
        target.Priority = dto.Priority;
        target.RelatedEntityScope = string.IsNullOrWhiteSpace(dto.RelatedEntityScope) ? dto.EntityScope : dto.RelatedEntityScope;
        target.ApplicabilityMode = string.IsNullOrWhiteSpace(dto.ApplicabilityMode) ? dto.ScopeMode : dto.ApplicabilityMode;
        target.StrategyPeriodId = string.IsNullOrWhiteSpace(dto.StrategyPeriodId) ? null : dto.StrategyPeriodId.Trim();
        target.AppliesToAllCompanies = dto.AppliesToAllCompanies || string.Equals(dto.ApplicabilityMode, "Enterprise", StringComparison.OrdinalIgnoreCase) || dto.AppliesToAllCompaniesFlag;
        target.ApplicableCompanyIds = dto.ApplicableCompanyIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new();
        target.DecisionReference = dto.DecisionReference;
        target.EvidenceLink = string.IsNullOrWhiteSpace(dto.EvidenceLink) ? dto.EvidenceReference : dto.EvidenceLink;
        target.ChangeLogRef = dto.ChangeLogRef;
        target.UpdatedBy = actor;
        target.SourceTemplateType = dto.SourceTemplateType;
        target.SourceTemplateId = dto.SourceTemplateId;
        target.SourceTemplateVersion = dto.SourceTemplateVersion;
        target.SourceBlueprintPackId = dto.SourceBlueprintPackId;
        target.InstantiationBatchId = dto.InstantiationBatchId;
        target.CreatedFromLibrary = dto.CreatedFromLibrary;
        target.YearlyBudgets = (dto.BudgetEnvelopes ?? new()).Select(b => new GoalYearlyBudgetEnvelope
        {
            Year = b.Year,
            RevenueTarget = b.RevenueTarget,
            EbitdaTarget = b.EbitdaTarget,
            CapexEnvelope = b.CapexEnvelope,
            OpexEnvelope = b.OpexEnvelope,
            SavingsTarget = b.SavingsTarget,
            FundingPool = b.FundingPoolEnvelope ?? b.FundingPool,
            Commentary = b.Commentary
        }).ToList();
        target.Metrics = (dto.Metrics ?? new()).Select((x, index) => new GoalMetric
        {
            Id = string.IsNullOrWhiteSpace(x.Id) ? Guid.NewGuid().ToString("N") : x.Id,
            MetricAssignmentId = string.IsNullOrWhiteSpace(x.MetricAssignmentId) ? (x.Id ?? string.Empty) : x.MetricAssignmentId,
            GoalId = target.Id,
            MetricDefinitionId = string.IsNullOrWhiteSpace(x.MetricDefinitionId) ? x.MetricDefId : x.MetricDefinitionId,
            MetricName = x.MetricName,
            MetricType = x.MetricType,
            BaselineValue = ResolveBaselineValue(x),
            TargetValue = ResolveTargetValue(x),
            UnitOfMeasure = x.UnitOfMeasure,
            AggregationMethod = x.AggregationMethod,
            DirectionPolarity = x.DirectionPolarity,
            ThresholdModel = x.ThresholdModel,
            ReportingFrequency = x.ReportingFrequency,
            CascadeMetric = x.CascadeMetric,
            MetricOrigin = NormalizeIncomingMetricOrigin(x.MetricOrigin),
            MetricRole = string.IsNullOrWhiteSpace(x.MetricRole) ? "Strategic" : x.MetricRole.Trim(),
            RestrictionMode = string.IsNullOrWhiteSpace(x.RestrictionMode) ? "GoalGovernedStructure" : x.RestrictionMode,
            RollupEligible = x.RollupEligible,
            YearlyTargets = (x.YearlyValues ?? new()).OrderBy(y => y.Year).Select(y => new GoalMetricYearValue
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
            SortOrder = x.SortOrder <= 0 ? index + 1 : x.SortOrder,
            MetricBindingStatus = x.MetricBindingStatus,
            CreatedAt = x.CreatedAt == default ? DateTime.UtcNow : x.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        }).ToList();
    }

    private static decimal ResolveBaselineValue(GoalMetricDto metric)
    {
        var firstYear = (metric.YearlyValues ?? new()).OrderBy(x => x.Year).FirstOrDefault();
        return firstYear?.BaselineValue ?? firstYear?.TargetValue ?? metric.BaselineValue;
    }

    private static decimal ResolveTargetValue(GoalMetricDto metric)
    {
        var lastYear = (metric.YearlyValues ?? new()).OrderBy(x => x.Year).LastOrDefault();
        return lastYear?.TargetValue ?? metric.TargetValue;
    }

    private static string NormalizeIncomingMetricOrigin(string? origin)
    {
        var o = (origin ?? string.Empty).Trim();
        if (string.Equals(o, "Strategic", StringComparison.OrdinalIgnoreCase))
            return "Local";
        return string.IsNullOrWhiteSpace(o) ? "Local" : o;
    }

    private static void NormalizeGoalPlanningDates(GoalDto goal)
    {
        if (goal.StartDate.HasValue)
            goal.StartDate = DateTime.SpecifyKind(goal.StartDate.Value.Date, DateTimeKind.Utc);
        if (goal.EndDate.HasValue)
            goal.EndDate = DateTime.SpecifyKind(goal.EndDate.Value.Date, DateTimeKind.Utc);
    }

    private async Task<Dictionary<string, List<string>>> ValidateGoalStrategyPeriodAssignmentAsync(GoalDto goal, string? existingStrategyPeriodId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(goal.StrategyPeriodId))
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["strategyPeriodId"] = new() { "Strategy Period is required for Goal planning assignment." }
            };

        var period = await _strategyPeriods.GetByIdAsync(goal.StrategyPeriodId.Trim(), cancellationToken);
        if (period is null)
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["strategyPeriodId"] = new() { "Selected strategy period does not exist." }
            };

        if (!string.Equals(period.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["strategyPeriodId"] = new() { "Only Active strategy periods can be assigned to a goal." }
            };

        if (goal.StartDate.HasValue && goal.StartDate.Value.Date < period.StartDate.Date)
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["planning.startDate"] = new() { "Start Date must be on or after the Strategy Period Start Date." }
            };

        if (goal.EndDate.HasValue && goal.EndDate.Value.Date > period.EndDate.Date)
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["planning.endDate"] = new() { "End Date must be on or before the Strategy Period End Date." }
            };

        var companyScope = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(goal.OwnerCompanyId))
            companyScope.Add(goal.OwnerCompanyId.Trim());
        if (!string.IsNullOrWhiteSpace(goal.PrimaryCompanyId))
            companyScope.Add(goal.PrimaryCompanyId.Trim());
        foreach (var c in goal.ApplicableCompanyIds ?? new())
        {
            if (!string.IsNullOrWhiteSpace(c))
                companyScope.Add(c.Trim());
        }

        if (!string.IsNullOrWhiteSpace(period.CompanyId) && (companyScope.Count == 0 || !companyScope.Contains(period.CompanyId)))
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["companyScope"] = new() { "Goal company scope must include the selected strategy period company." }
            };

        return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class ObjectiveService : IObjectiveService
{
    private readonly IObjectiveRepository _objectives;
    private readonly IGoalRepository _goals;
    private readonly IStrategyPeriodRepository _strategyPeriods;
    private readonly IInitiativeStrategyLinkRepository _initiativeLinks;
    private readonly IProjectStrategyLinkRepository _projectLinks;
    private readonly IEnterpriseStrategyAuditSink _audit;
    private readonly bool _duplicateNameGuardEnabled = true;

    public ObjectiveService(
        IObjectiveRepository objectives,
        IGoalRepository goals,
        IStrategyPeriodRepository strategyPeriods,
        IInitiativeStrategyLinkRepository initiativeLinks,
        IProjectStrategyLinkRepository projectLinks,
        IEnterpriseStrategyAuditSink audit)
    {
        _objectives = objectives;
        _goals = goals;
        _strategyPeriods = strategyPeriods;
        _initiativeLinks = initiativeLinks;
        _projectLinks = projectLinks;
        _audit = audit;
    }

    public async Task<Response<PagedResponseDto<ObjectiveDto>>> ListAsync(PagedRequestDto request, CancellationToken cancellationToken = default)
    {
        var rows = await _objectives.ListAsync(cancellationToken);
        var initiatives = await _initiativeLinks.ListAsync(cancellationToken);
        var projects = await _projectLinks.ListAsync(cancellationToken);
        var initiativeCountsByObjective = initiatives
            .GroupBy(i => i.ParentObjectiveId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        var projectCountsByObjective = projects
            .GroupBy(i => i.ParentObjectiveId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        IEnumerable<ObjectiveAggregate> query = rows;
        var f = request.Filters;
        if (f.TryGetValue("parentGoalId", out var parentGoalId)) query = query.Where(x => x.ParentGoalId == parentGoalId);
        if (f.TryGetValue("owner", out var owner)) query = query.Where(x => string.Equals(x.Owner, owner, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("status", out var status)) query = query.Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("type", out var type)) query = query.Where(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("priority", out var priority)) query = query.Where(x => string.Equals(x.Priority, priority, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("inheritCompanyScope", out var inheritCompanyScope)) query = query.Where(x => string.Equals(x.InheritCompanyScope.ToString(), inheritCompanyScope, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("company", out var company))
            query = query.Where(x =>
                string.Equals(x.PrimaryCompanyId, company, StringComparison.OrdinalIgnoreCase) ||
                x.ApplicableCompanyIds.Any(c => string.Equals(c, company, StringComparison.OrdinalIgnoreCase)));
        if (f.TryGetValue("entityScope", out var scope)) query = query.Where(x => string.Equals(x.EntityScope, scope, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(x => x.Name.Contains(request.Search, StringComparison.OrdinalIgnoreCase) || x.Statement.Contains(request.Search, StringComparison.OrdinalIgnoreCase));

        Func<ObjectiveAggregate, object> sort = (request.SortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "name" => x => x.Name,
            "owner" => x => x.Owner,
            "priority" => x => x.Priority,
            "initiativecount" => x => initiativeCountsByObjective.TryGetValue(x.Id, out var count) ? count : 0,
            "projectcount" => x => projectCountsByObjective.TryGetValue(x.Id, out var count) ? count : 0,
            "contributionweight" => x => x.ContributionWeight,
            _ => x => x.UpdatedAt
        };
        query = string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase) ? query.OrderBy(sort) : query.OrderByDescending(sort);

        var total = query.Count();
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 10_000);
        var items = query.Skip((page - 1) * size).Take(size).Select(x => x.ToDto()).ToList();
        return Response<PagedResponseDto<ObjectiveDto>>.Ok(new PagedResponseDto<ObjectiveDto> { Page = page, PageSize = size, TotalCount = total, Items = items });
    }

    public async Task<Response<ObjectiveDetailDto>> GetAsync(string objectiveId, CancellationToken cancellationToken = default)
    {
        var objective = await _objectives.GetByIdAsync(objectiveId, cancellationToken);
        if (objective is null) return Response<ObjectiveDetailDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        var parentGoal = await _goals.GetByIdAsync(objective.ParentGoalId, cancellationToken);
        var initiatives = await _initiativeLinks.ListByObjectiveIdAsync(objectiveId, cancellationToken);
        var projects = await _projectLinks.ListByObjectiveIdAsync(objectiveId, cancellationToken);
        var alignment = await GetAlignmentSummaryAsync(objectiveId, cancellationToken);
        return Response<ObjectiveDetailDto>.Ok(new ObjectiveDetailDto
        {
            Objective = objective.ToDto(),
            ParentGoal = parentGoal?.ToDto(),
            LinkedInitiatives = initiatives.Select(MapInitiativeReference).ToList(),
            LinkedProjects = projects.Select(x => new ProjectStrategyLinkViewDto
            {
                LinkId = x.Id,
                ProjectId = x.ProjectId,
                SourceSystem = x.SourceSystem,
                SourceRecordId = x.SourceRecordId,
                ParentInitiativeId = x.ParentInitiativeId,
                ParentObjectiveId = x.ParentObjectiveId,
                ParentGoalId = x.ParentGoalId,
                StrategyLinkStatus = x.StrategyLinkStatus,
                ContributionNote = x.ContributionNote,
                MetricBindingsJson = x.MetricBindingsJson,
                DecisionReference = x.DecisionReference,
                EvidenceReference = x.EvidenceReference,
                DeliveryCompanyId = x.DeliveryCompanyId,
                FundingCompanyId = x.FundingCompanyId,
                Version = x.Version,
                SyncedAt = x.SyncedAt,
                SyncFreshness = x.SyncedAt.HasValue && x.SyncedAt.Value >= DateTime.UtcNow.AddHours(-24) ? "Fresh" : "Stale"
            }).ToList(),
            AlignmentSummary = alignment.Data ?? new ObjectiveAlignmentSummaryDto(),
            AuditSummary = $"v{objective.Version} updated {objective.UpdatedAt:O}"
        });
    }

    public async Task<Response<ObjectiveDto>> CreateAsync(ObjectiveDto objective, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        objective.Status = "Draft";
        if (string.IsNullOrWhiteSpace(objective.ApprovalStatus))
            objective.ApprovalStatus = "Draft";
        objective.ApprovedBy = null;
        objective.ApprovedOn = null;
        objective.CoOwnerIds = NormalizeIds(objective.CoOwnerIds);
        objective.ApplicableCompanyIds = NormalizeIds(objective.ApplicableCompanyIds);
        objective.LinkedInitiativeIds = NormalizeIds(objective.LinkedInitiativeIds);
        objective.LinkedProjectIds = NormalizeIds(objective.LinkedProjectIds);
        objective.LinkedRiskIssueIds = NormalizeIds(objective.LinkedRiskIssueIds);
        objective.LinkedDependencyIds = NormalizeIds(objective.LinkedDependencyIds);
        objective.DependencyLinks = NormalizeDependencyLinks(objective.DependencyLinks);
        NormalizeApprovalRouting(objective);
        NormalizeContributionWeight(objective);
        NormalizeTargetInheritance(objective);
        if (string.IsNullOrWhiteSpace(objective.Id))
        {
            for (var attempt = 0; attempt < 16; attempt++)
            {
                var existing = await _objectives.ListAsync(cancellationToken);
                var candidate = EnterpriseStrategyRuntimeIds.NextObjectiveId(existing.Select(x => x.Id));
                if (existing.All(x => !string.Equals(x.Id, candidate, StringComparison.OrdinalIgnoreCase)))
                {
                    objective.Id = candidate;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(objective.Id))
                return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.InternalError, new() { ["id"] = new() { "Could not allocate a unique objective id." } });
        }

        var parent = await _goals.GetByIdAsync(objective.ParentGoalId, cancellationToken);
        if (parent is null) return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["parentGoalId"] = new() { "Parent goal must exist." } });
        if (string.IsNullOrWhiteSpace(parent.StrategyPeriodId))
            return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["parentGoalId"] = new() { "Parent goal must be linked to an Active strategy period before creating objectives." } });

        var planningContextResult = await GoalPlanningContextResolver.ResolveForGoalAsync(parent, _strategyPeriods, cancellationToken);
        if (!planningContextResult.Success)
            return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, planningContextResult.Error?.Details);
        if (!string.Equals(planningContextResult.Data!.StrategyPeriodStatus, "Active", StringComparison.OrdinalIgnoreCase))
            return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["parentGoalId"] = new() { "Parent goal strategy period must be Active." } });

        ApplyParentDerivedObjectiveDefaults(parent, planningContextResult.Data!, objective);
        NormalizeObjectiveYearlyStructures(objective);
        var validation = EnterpriseStrategyValidators.ValidateObjective(objective);
        if (validation.Count > 0) return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, validation);
        if (!IsObjectiveTypeCompatibleWithGoal(parent, objective))
            return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["type"] = new() { "Objective type is not compatible with selected parent goal type." } });
        if (!IsObjectiveDatesWithinGoal(parent, objective))
            return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["timeHorizon"] = new() { "Objective Start Date and End Date must fall within the Parent Goal planning horizon." } });
        if (!IsObjectiveScopeWithinGoal(parent, objective))
            return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["companyScope"] = new() { "Objective company scope cannot exceed parent goal scope." } });
        if (_duplicateNameGuardEnabled)
        {
            var existing = await _objectives.ListAsync(cancellationToken);
            if (existing.Any(x => x.ParentGoalId == objective.ParentGoalId && string.Equals(x.Name, objective.Name, StringComparison.OrdinalIgnoreCase)))
                return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, new() { ["name"] = new() { "Duplicate objective name under same goal." } });
        }

        var aggregate = ToAggregate(objective, actor);
        await _objectives.AddAsync(aggregate, cancellationToken);
        await EmitAudit(actor, correlationId, aggregate.Id, EnterpriseStrategyEventNames.ObjectiveCreated, "", aggregate.Name, cancellationToken);
        return Response<ObjectiveDto>.Ok(aggregate.ToDto());
    }

    public async Task<Response<ObjectiveDto>> UpdateAsync(string objectiveId, ObjectiveDto objective, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var aggregate = await _objectives.GetByIdAsync(objectiveId, cancellationToken);
        if (aggregate is null) return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (EnterpriseStrategyResult.IsStaleWrite(expectedVersion, aggregate.Version)) return EnterpriseStrategyResult.StaleVersion<ObjectiveDto>();

        objective.CoOwnerIds = NormalizeIds(objective.CoOwnerIds);
        objective.ApplicableCompanyIds = NormalizeIds(objective.ApplicableCompanyIds);
        objective.LinkedInitiativeIds = NormalizeIds(objective.LinkedInitiativeIds);
        objective.LinkedProjectIds = NormalizeIds(objective.LinkedProjectIds);
        objective.LinkedRiskIssueIds = NormalizeIds(objective.LinkedRiskIssueIds);
        objective.LinkedDependencyIds = NormalizeIds(objective.LinkedDependencyIds);
        objective.DependencyLinks = NormalizeDependencyLinks(objective.DependencyLinks);
        NormalizeApprovalRouting(objective);
        NormalizeContributionWeight(objective);
        NormalizeTargetInheritance(objective);
        objective.ApprovedBy = aggregate.ApprovedBy;
        objective.ApprovedOn = aggregate.ApprovedOn;
        var parent = await _goals.GetByIdAsync(objective.ParentGoalId, cancellationToken);
        if (parent is null) return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["parentGoalId"] = new() { "Parent goal must exist." } });
        if (string.IsNullOrWhiteSpace(parent.StrategyPeriodId))
            return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["parentGoalId"] = new() { "Parent goal must be linked to an Active strategy period before updating objectives." } });

        var planningContextResult = await GoalPlanningContextResolver.ResolveForGoalAsync(parent, _strategyPeriods, cancellationToken);
        if (!planningContextResult.Success)
            return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, planningContextResult.Error?.Details);
        if (!string.Equals(planningContextResult.Data!.StrategyPeriodStatus, "Active", StringComparison.OrdinalIgnoreCase))
            return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["parentGoalId"] = new() { "Parent goal strategy period must be Active." } });

        ApplyParentDerivedObjectiveDefaults(parent, planningContextResult.Data!, objective);
        NormalizeObjectiveYearlyStructures(objective);
        var validation = EnterpriseStrategyValidators.ValidateObjective(objective);
        if (validation.Count > 0) return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, validation);
        if (!IsObjectiveTypeCompatibleWithGoal(parent, objective))
            return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["type"] = new() { "Objective type is not compatible with selected parent goal type." } });
        if (!IsObjectiveDatesWithinGoal(parent, objective))
            return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["timeHorizon"] = new() { "Objective Start Date and End Date must fall within the Parent Goal planning horizon." } });
        if (!IsObjectiveScopeWithinGoal(parent, objective))
            return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["companyScope"] = new() { "Objective company scope cannot exceed parent goal scope." } });
        if (_duplicateNameGuardEnabled)
        {
            var existing = await _objectives.ListAsync(cancellationToken);
            if (existing.Any(x => x.Id != objectiveId && x.ParentGoalId == objective.ParentGoalId && string.Equals(x.Name, objective.Name, StringComparison.OrdinalIgnoreCase)))
                return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, new() { ["name"] = new() { "Duplicate objective name under same goal." } });
        }
        if (!string.Equals(aggregate.ParentGoalId, objective.ParentGoalId, StringComparison.OrdinalIgnoreCase))
            await EmitAudit(actor, correlationId, aggregate.Id, EnterpriseStrategyEventNames.ObjectiveReparented, aggregate.ParentGoalId, objective.ParentGoalId, cancellationToken);

        MapInto(aggregate, objective, actor);
        aggregate.Version++;
        aggregate.UpdatedAt = DateTime.UtcNow;
        await _objectives.UpdateAsync(aggregate, cancellationToken);
        await EmitAudit(actor, correlationId, aggregate.Id, EnterpriseStrategyEventNames.ObjectiveUpdated, "", aggregate.Name, cancellationToken);
        return Response<ObjectiveDto>.Ok(aggregate.ToDto());
    }

    public async Task<Response<ObjectiveDto>> ChangeStatusAsync(string objectiveId, string status, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var aggregate = await _objectives.GetByIdAsync(objectiveId, cancellationToken);
        if (aggregate is null) return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (EnterpriseStrategyResult.IsStaleWrite(expectedVersion, aggregate.Version)) return EnterpriseStrategyResult.StaleVersion<ObjectiveDto>();
        if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var parent = await _goals.GetByIdAsync(aggregate.ParentGoalId, cancellationToken);
            if (parent is null || EnterpriseStrategyValidators.IsArchived(parent.Status))
                return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, new() { ["parentGoalId"] = new() { "Cannot activate objective under archived parent goal." } });
        }

        aggregate.Status = status;
        aggregate.UpdatedAt = DateTime.UtcNow;
        aggregate.UpdatedBy = actor;
        aggregate.Version++;
        await _objectives.UpdateAsync(aggregate, cancellationToken);
        await EmitAudit(actor, correlationId, aggregate.Id, EnterpriseStrategyEventNames.ObjectiveStatusChanged, "", status, cancellationToken);
        return Response<ObjectiveDto>.Ok(aggregate.ToDto());
    }

    public async Task<Response<ObjectiveDto>> ArchiveAsync(string objectiveId, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        return await ChangeStatusInternal(objectiveId, "Archived", expectedVersion, actor, EnterpriseStrategyEventNames.ObjectiveArchived, correlationId, cancellationToken);
    }

    public async Task<Response<ObjectiveDto>> RestoreAsync(string objectiveId, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        return await ChangeStatusInternal(objectiveId, "Draft", expectedVersion, actor, EnterpriseStrategyEventNames.ObjectiveRestored, correlationId, cancellationToken);
    }

    public async Task<Response<IReadOnlyList<InitiativeStrategyLinkViewDto>>> GetInitiativesAsync(string objectiveId, CancellationToken cancellationToken = default)
    {
        var rows = await _initiativeLinks.ListByObjectiveIdAsync(objectiveId, cancellationToken);
        return Response<IReadOnlyList<InitiativeStrategyLinkViewDto>>.Ok(rows.Select(MapInitiativeReference).ToList());
    }

    public async Task<Response<IReadOnlyList<ProjectStrategyLinkViewDto>>> GetProjectsAsync(string objectiveId, CancellationToken cancellationToken = default)
    {
        var rows = await _projectLinks.ListByObjectiveIdAsync(objectiveId, cancellationToken);
        return Response<IReadOnlyList<ProjectStrategyLinkViewDto>>.Ok(rows.Select(x => new ProjectStrategyLinkViewDto
        {
            LinkId = x.Id,
            ProjectId = x.ProjectId,
            SourceSystem = x.SourceSystem,
            SourceRecordId = x.SourceRecordId,
            ParentInitiativeId = x.ParentInitiativeId,
            ParentObjectiveId = x.ParentObjectiveId,
            ParentGoalId = x.ParentGoalId,
            StrategyLinkStatus = x.StrategyLinkStatus,
            ContributionNote = x.ContributionNote,
            MetricBindingsJson = x.MetricBindingsJson,
            DecisionReference = x.DecisionReference,
            EvidenceReference = x.EvidenceReference,
            DeliveryCompanyId = x.DeliveryCompanyId,
            FundingCompanyId = x.FundingCompanyId,
            Version = x.Version,
            SyncedAt = x.SyncedAt,
            SyncFreshness = x.SyncedAt.HasValue && x.SyncedAt.Value >= DateTime.UtcNow.AddHours(-24) ? "Fresh" : "Stale"
        }).ToList());
    }

    public async Task<Response<ObjectiveAlignmentSummaryDto>> GetAlignmentSummaryAsync(string objectiveId, CancellationToken cancellationToken = default)
    {
        var initiatives = await _initiativeLinks.ListByObjectiveIdAsync(objectiveId, cancellationToken);
        var projects = await _projectLinks.ListByObjectiveIdAsync(objectiveId, cancellationToken);
        return Response<ObjectiveAlignmentSummaryDto>.Ok(new ObjectiveAlignmentSummaryDto
        {
            ObjectiveId = objectiveId,
            LinkedInitiativesCount = initiatives.Count,
            LinkedProjectsCount = projects.Count,
            HasCoverageGap = initiatives.Count == 0 || projects.Count == 0,
            AuditSummary = $"initiativeLinks={initiatives.Count}; projectLinks={projects.Count}"
        });
    }

    private static InitiativeStrategyLinkViewDto MapInitiativeReference(InitiativeStrategyLinkAggregate aggregate)
    {
        var contributionPlanValues = aggregate.ContributionPlanValues?
            .Select(x => new InitiativeContributionPlanValueDto
            {
                PeriodKey = x.PeriodKey,
                PeriodLabel = x.PeriodLabel,
                PeriodStart = x.PeriodStart,
                PeriodEnd = x.PeriodEnd,
                PlannedValue = x.PlannedValue,
                ActualValue = x.ActualValue,
                ForecastValue = x.ForecastValue,
                Commentary = x.Commentary
            })
            .ToList() ?? new List<InitiativeContributionPlanValueDto>();

        return new InitiativeStrategyLinkViewDto
        {
            LinkId = aggregate.Id,
            InitiativeId = aggregate.InitiativeId,
            InitiativeName = aggregate.InitiativeName,
            Description = aggregate.Description,
            Owner = string.IsNullOrWhiteSpace(aggregate.Owner) ? aggregate.DeliveryOwnerPersonId : aggregate.Owner,
            DeliveryOwnerCompanyId = aggregate.DeliveryOwnerCompanyId,
            DeliveryOwnerPositionId = aggregate.DeliveryOwnerPositionId,
            DeliveryOwnerPersonId = aggregate.DeliveryOwnerPersonId,
            ExecutiveSponsor = aggregate.ExecutiveSponsor,
            AccountableSponsorRole = aggregate.AccountableSponsorRole,
            Status = aggregate.Status,
            Type = aggregate.Type,
            WaveOrPhase = aggregate.WaveOrPhase,
            Priority = aggregate.Priority,
            Complexity = aggregate.Complexity,
            Maturity = aggregate.Maturity,
            StartDate = aggregate.StartDate,
            EndDate = aggregate.EndDate,
            ReportingFrequency = aggregate.ReportingFrequency,
            ContributionMetricName = aggregate.ContributionMetricName,
            ContributionUnitOfMeasure = aggregate.ContributionUnitOfMeasure,
            ContributionPlanGranularity = aggregate.ContributionPlanGranularity,
            ContributionMethod = aggregate.ContributionMethod,
            ContributionTiming = aggregate.ContributionTiming,
            BenefitHypothesis = aggregate.BenefitHypothesis,
            BenefitRealizationStart = aggregate.BenefitRealizationStart,
            BenefitRealizationEnd = aggregate.BenefitRealizationEnd,
            ContributionPlanValues = contributionPlanValues,
            SourceSystem = aggregate.SourceSystem,
            SourceRecordId = aggregate.SourceRecordId,
            ParentObjectiveId = aggregate.ParentObjectiveId,
            ParentGoalId = aggregate.ParentGoalId,
            StrategyLinkStatus = aggregate.StrategyLinkStatus,
            ContributionType = aggregate.ContributionType,
            ContributionWeight = aggregate.ContributionWeight,
            MetricBindingsJson = aggregate.MetricBindingsJson,
            DecisionReference = aggregate.DecisionReference,
            EvidenceReference = aggregate.EvidenceReference,
            SponsoringCompanyId = aggregate.SponsoringCompanyId,
            ParticipatingCompanyIds = aggregate.ParticipatingCompanyIds,
            EntityScope = aggregate.EntityScope,
            InitiativeClass = aggregate.InitiativeClass,
            BudgetEnvelope = aggregate.BudgetEnvelope,
            BudgetAmount = aggregate.BudgetAmount,
            CurrencyCode = aggregate.CurrencyCode,
            FundingSource = aggregate.FundingSource,
            StrategyAlignmentNote = aggregate.StrategyAlignmentNote,
            GovernanceStage = aggregate.GovernanceStage,
            GovernanceNotes = aggregate.GovernanceNotes,
            DependencyFlag = aggregate.DependencyFlag,
            Notes = aggregate.Notes,
            Version = aggregate.Version,
            SyncedAt = aggregate.SyncedAt,
            SyncFreshness = aggregate.SyncedAt.HasValue && aggregate.SyncedAt.Value >= DateTime.UtcNow.AddHours(-24) ? "Fresh" : "Stale",
            ReadinessStatus = DeriveInitiativeReferenceReadinessStatus(aggregate)
        };
    }

    private static string DeriveInitiativeReferenceReadinessStatus(InitiativeStrategyLinkAggregate aggregate)
    {
        var missingCoreAnchor =
            string.IsNullOrWhiteSpace(aggregate.ParentObjectiveId) ||
            string.IsNullOrWhiteSpace(aggregate.InitiativeName) ||
            string.IsNullOrWhiteSpace(aggregate.Type) ||
            (string.IsNullOrWhiteSpace(aggregate.DeliveryOwnerPersonId) && string.IsNullOrWhiteSpace(aggregate.Owner)) ||
            string.IsNullOrWhiteSpace(aggregate.SponsoringCompanyId) ||
            !aggregate.StartDate.HasValue ||
            !aggregate.EndDate.HasValue;

        if (missingCoreAnchor)
            return "Blocked";

        var contributionReady =
            !string.IsNullOrWhiteSpace(aggregate.ContributionMetricName) &&
            !string.IsNullOrWhiteSpace(aggregate.ContributionMethod) &&
            !string.IsNullOrWhiteSpace(aggregate.BenefitHypothesis) &&
            (aggregate.ContributionPlanValues?.Count ?? 0) > 0 &&
            aggregate.ContributionPlanValues.All(x => x.PlannedValue.HasValue);

        return contributionReady ? "Planning Ready" : "Draft Ready";
    }

    private async Task<Response<ObjectiveDto>> ChangeStatusInternal(string objectiveId, string status, int expectedVersion, string actor, string eventName, string correlationId, CancellationToken cancellationToken)
    {
        var aggregate = await _objectives.GetByIdAsync(objectiveId, cancellationToken);
        if (aggregate is null) return Response<ObjectiveDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (EnterpriseStrategyResult.IsStaleWrite(expectedVersion, aggregate.Version)) return EnterpriseStrategyResult.StaleVersion<ObjectiveDto>();
        aggregate.Status = status;
        aggregate.UpdatedAt = DateTime.UtcNow;
        aggregate.ArchivedAt = string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null;
        aggregate.UpdatedBy = actor;
        aggregate.Version++;
        await _objectives.UpdateAsync(aggregate, cancellationToken);
        await EmitAudit(actor, correlationId, aggregate.Id, eventName, "", status, cancellationToken);
        return Response<ObjectiveDto>.Ok(aggregate.ToDto());
    }

    private async Task EmitAudit(string actor, string correlationId, string objectId, string action, string before, string after, CancellationToken cancellationToken)
    {
        await _audit.WriteMutationAsync(actor, "Objective", objectId, action, correlationId, "enterprise-strategy.objectives", before, after, cancellationToken);
    }

    private static bool IsObjectiveScopeWithinGoal(GoalAggregate parentGoal, ObjectiveDto objective)
    {
        if (objective.InheritCompanyScope) return true;
        var goalMode = (parentGoal.ScopeMode ?? "Enterprise").Trim();
        if (goalMode.Equals("Enterprise", StringComparison.OrdinalIgnoreCase)) return true;
        var goalSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(parentGoal.PrimaryCompanyId)) goalSet.Add(parentGoal.PrimaryCompanyId);
        (parentGoal.ApplicableCompanyIds ?? new()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList().ForEach(x => goalSet.Add(x));
        var objectiveSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(objective.PrimaryCompanyId)) objectiveSet.Add(objective.PrimaryCompanyId);
        (objective.ApplicableCompanyIds ?? new()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList().ForEach(x => objectiveSet.Add(x));
        if (objectiveSet.Count == 0) return false;
        return objectiveSet.All(goalSet.Contains);
    }

    private static bool IsObjectiveDatesWithinGoal(GoalAggregate parentGoal, ObjectiveDto objective)
    {
        if (!parentGoal.PlanningHorizonStart.HasValue || !parentGoal.PlanningHorizonEnd.HasValue)
            return true;
        if (!objective.TimeHorizonStart.HasValue || !objective.TimeHorizonEnd.HasValue)
            return true;
        return objective.TimeHorizonStart.Value.Date >= parentGoal.PlanningHorizonStart.Value.Date &&
               objective.TimeHorizonEnd.Value.Date <= parentGoal.PlanningHorizonEnd.Value.Date;
    }

    private static bool IsObjectiveDatesWithinStrategyPeriod(GoalPlanningContextDto context, ObjectiveDto objective)
    {
        if (!objective.TimeHorizonStart.HasValue || !objective.TimeHorizonEnd.HasValue)
            return true;

        return objective.TimeHorizonStart.Value.Date >= context.StartDate.Date &&
               objective.TimeHorizonEnd.Value.Date <= context.EndDate.Date;
    }

    private static bool IsObjectiveTypeCompatibleWithGoal(GoalAggregate parentGoal, ObjectiveDto objective)
    {
        var goalType = (parentGoal.Category ?? string.Empty).Trim();
        var objectiveType = (objective.Type ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(goalType) || string.IsNullOrWhiteSpace(objectiveType))
            return true;
        var compatibility = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Growth"] = new HashSet<string>(new[] { "Growth", "Financial", "Market", "Customer", "Portfolio" }, StringComparer.OrdinalIgnoreCase),
            ["Operations"] = new HashSet<string>(new[] { "Operations", "Capability", "Risk", "Transformation" }, StringComparer.OrdinalIgnoreCase),
            ["Transformation"] = new HashSet<string>(new[] { "Transformation", "Innovation", "Capability", "People", "Portfolio" }, StringComparer.OrdinalIgnoreCase),
            ["Risk"] = new HashSet<string>(new[] { "Risk", "Operations", "Capability" }, StringComparer.OrdinalIgnoreCase),
            ["Financial"] = new HashSet<string>(new[] { "Financial", "Growth", "Portfolio", "Risk" }, StringComparer.OrdinalIgnoreCase)
        };
        if (!compatibility.TryGetValue(goalType, out var allowed))
            return true;
        return allowed.Contains(objectiveType);
    }

    private static List<string> NormalizeIds(IEnumerable<string>? values)
    {
        return (values ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<ObjectiveDependencyLinkDto> NormalizeDependencyLinks(IEnumerable<ObjectiveDependencyLinkDto>? values)
    {
        return (values ?? Array.Empty<ObjectiveDependencyLinkDto>())
            .Where(x => !string.IsNullOrWhiteSpace(x.DependencyTypeId) ||
                        !string.IsNullOrWhiteSpace(x.DependencyObjectType) ||
                        !string.IsNullOrWhiteSpace(x.DependencyReferenceId) ||
                        !string.IsNullOrWhiteSpace(x.DependencyReferenceText))
            .Select(x => new ObjectiveDependencyLinkDto
            {
                Id = string.IsNullOrWhiteSpace(x.Id) ? Guid.NewGuid().ToString("N") : x.Id.Trim(),
                DependencyTypeId = x.DependencyTypeId?.Trim() ?? string.Empty,
                DependencyObjectType = x.DependencyObjectType?.Trim() ?? string.Empty,
                DependencyReferenceId = string.IsNullOrWhiteSpace(x.DependencyReferenceId) ? null : x.DependencyReferenceId.Trim(),
                DependencyReferenceText = string.IsNullOrWhiteSpace(x.DependencyReferenceText) ? null : x.DependencyReferenceText.Trim(),
                DependencyCriticality = string.IsNullOrWhiteSpace(x.DependencyCriticality) ? null : x.DependencyCriticality.Trim()
            })
            .ToList();
    }

    private static void NormalizeApprovalRouting(ObjectiveDto objective)
    {
        var route = (objective.ApprovalRouteType ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(route))
            route = "IndividualApprover";
        objective.ApprovalRouteType = route;
        if (route.Equals("ApprovalGroup", StringComparison.OrdinalIgnoreCase))
            objective.ApproverId = null;
        else
            objective.ApprovalGroupId = null;
    }

    private static void NormalizeContributionWeight(ObjectiveDto objective)
    {
        var type = (objective.ContributionTypeId ?? objective.ContributionType ?? string.Empty).Trim();
        var requiresWeight = type.Equals("Weighted", StringComparison.OrdinalIgnoreCase) ||
                             type.Equals("WeightedContribution", StringComparison.OrdinalIgnoreCase);
        if (!requiresWeight)
            objective.ContributionWeight = 0;
    }

    private static void NormalizeTargetInheritance(ObjectiveDto objective)
    {
        objective.Metrics ??= new List<ObjectiveMetricDto>();
        foreach (var row in objective.Metrics)
        {
            row.MetricClass = string.IsNullOrWhiteSpace(row.MetricClass) ? "Local" : row.MetricClass.Trim();
            row.MetricRole = string.IsNullOrWhiteSpace(row.MetricRole)
                ? (string.Equals(row.MetricClass, "Inherited", StringComparison.OrdinalIgnoreCase) ? "Contribution" : "Local")
                : row.MetricRole.Trim();
            row.PolarityCode = string.IsNullOrWhiteSpace(row.PolarityCode) ? row.Direction : row.PolarityCode.Trim();
            row.ThresholdModelCode = string.IsNullOrWhiteSpace(row.ThresholdModelCode) ? objective.ThresholdModelId ?? string.Empty : row.ThresholdModelCode.Trim();
            row.ReportingFrequencyCode = string.IsNullOrWhiteSpace(row.ReportingFrequencyCode) ? objective.ReportingFrequencyId : row.ReportingFrequencyCode.Trim();

            if (string.Equals(row.MetricClass, "Inherited", StringComparison.OrdinalIgnoreCase))
            {
                row.MetricRole = "Contribution";
                row.MetricId = objective.PrimaryMetricId;
                row.UnitOfMeasureId = objective.UnitOfMeasureId;
                row.Direction = objective.PerformanceDirection;
                row.PolarityCode = objective.PerformanceDirection;
                row.ReportingFrequencyCode = objective.ReportingFrequencyId;
                row.ThresholdModelCode = objective.ThresholdModelId ?? string.Empty;
                row.RollupEligibleFlag = true;
            }
            else
            {
                // Local metrics cannot roll up unless explicit approved mapping exists.
                row.RollupEligibleFlag = false;
            }
        }
        if (!objective.AllowMultipleTargetMetrics)
        {
            foreach (var row in objective.Metrics)
            {
                row.MetricId = objective.PrimaryMetricId;
                row.UnitOfMeasureId = objective.UnitOfMeasureId;
            }
        }
        if (!objective.AllowRowThresholdOverrides)
        {
            string? baselineThreshold = null;
            decimal? baselineThresholdValue = null;
            for (var i = 0; i < objective.Metrics.Count; i++)
            {
                var row = objective.Metrics[i];
                if (i == 0)
                {
                    baselineThreshold = row.ThresholdTolerance;
                    baselineThresholdValue = row.ThresholdValue;
                    continue;
                }
                row.ThresholdTolerance = baselineThreshold ?? string.Empty;
                row.ThresholdValue = baselineThresholdValue;
            }
        }
    }

    private static void NormalizeObjectiveYearlyStructures(ObjectiveDto objective)
    {
        if (!objective.TimeHorizonStart.HasValue || !objective.TimeHorizonEnd.HasValue)
            return;
        objective.TargetPlanGranularity = ObjectiveTargetPlanPeriodHelper.NormalizeGranularity(objective.TargetPlanGranularity);
        var startYear = objective.TimeHorizonStart.Value.Year;
        var endYear = objective.TimeHorizonEnd.Value.Year;
        if (endYear < startYear)
            return;
        var planPeriods = ObjectiveTargetPlanPeriodHelper.BuildPeriods(objective.TimeHorizonStart, objective.TimeHorizonEnd, objective.TargetPlanGranularity);

        objective.YearlyBudgets ??= new List<ObjectiveYearlyBudgetDto>();
        var budgetByYear = objective.YearlyBudgets
            .Where(x => x.Year > 0)
            .GroupBy(x => x.Year)
            .ToDictionary(g => g.Key, g => g.First());
        var normalizedBudgets = new List<ObjectiveYearlyBudgetDto>();
        for (var year = startYear; year <= endYear; year++)
        {
            if (!budgetByYear.TryGetValue(year, out var row))
                row = new ObjectiveYearlyBudgetDto { Year = year };
            row.Year = year;
            normalizedBudgets.Add(row);
        }
        objective.YearlyBudgets = normalizedBudgets;

        foreach (var metric in objective.Metrics ?? new List<ObjectiveMetricDto>())
        {
            metric.YearlyValues ??= new List<ObjectiveMetricYearValueDto>();
            var valuesByPeriodKey = metric.YearlyValues
                .Select(x =>
                {
                    x.PeriodGranularity = ObjectiveTargetPlanPeriodHelper.NormalizeGranularity(string.IsNullOrWhiteSpace(x.PeriodGranularity) ? objective.TargetPlanGranularity : x.PeriodGranularity);
                    if (string.IsNullOrWhiteSpace(x.PeriodKey) && x.Year > 0)
                        x.PeriodKey = x.Year.ToString();
                    if (string.IsNullOrWhiteSpace(x.PeriodLabel) && x.Year > 0)
                        x.PeriodLabel = x.Year.ToString();
                    return x;
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.PeriodKey) || x.Year > 0)
                .GroupBy(x => string.IsNullOrWhiteSpace(x.PeriodKey) ? x.Year.ToString() : x.PeriodKey)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var normalizedValues = new List<ObjectiveMetricYearValueDto>();
            foreach (var period in planPeriods)
            {
                if (!valuesByPeriodKey.TryGetValue(period.PeriodKey, out var row))
                    row = new ObjectiveMetricYearValueDto();
                row.Year = period.Year;
                row.PeriodKey = period.PeriodKey;
                row.PeriodLabel = period.PeriodLabel;
                row.PeriodStart = period.PeriodStart;
                row.PeriodEnd = period.PeriodEnd;
                row.PeriodGranularity = objective.TargetPlanGranularity;
                row.SortOrder = period.SortOrder;
                normalizedValues.Add(row);
            }
            metric.YearlyValues = normalizedValues;
        }
    }

    private static void ApplyParentDerivedObjectiveDefaults(GoalAggregate parentGoal, GoalPlanningContextDto planningContext, ObjectiveDto objective)
    {
        var inheritedStrategicThemeId = string.IsNullOrWhiteSpace(parentGoal.StrategicThemeId)
            ? (parentGoal.Category ?? string.Empty).Trim()
            : parentGoal.StrategicThemeId.Trim();
        if (string.IsNullOrWhiteSpace(objective.StrategicThemeId))
            objective.StrategicThemeId = inheritedStrategicThemeId;
        if (string.IsNullOrWhiteSpace(objective.StrategicTheme))
            objective.StrategicTheme = objective.StrategicThemeId;
        if (string.IsNullOrWhiteSpace(objective.StrategyPeriodId))
            objective.StrategyPeriodId = planningContext.StrategyPeriodId;
        if (string.IsNullOrWhiteSpace(objective.OwnerCompanyId))
            objective.OwnerCompanyId = parentGoal.PrimaryCompanyId ?? string.Empty;
        if (objective.TimeHorizonStart is null)
            objective.TimeHorizonStart = parentGoal.PlanningHorizonStart;
        if (objective.TimeHorizonEnd is null)
            objective.TimeHorizonEnd = parentGoal.PlanningHorizonEnd;
        if (objective.InheritCompanyScope)
        {
            objective.PrimaryCompanyId = parentGoal.PrimaryCompanyId;
            objective.ApplicableCompanyIds = NormalizeIds(parentGoal.ApplicableCompanyIds);
            if (string.IsNullOrWhiteSpace(objective.EntityScope))
                objective.EntityScope = parentGoal.EntityScope;
        }
        if (string.IsNullOrWhiteSpace(objective.ReviewCadence))
            objective.ReviewCadence = "Quarterly";
    }

    private static ObjectiveAggregate ToAggregate(ObjectiveDto dto, string actor) => new()
    {
        Id = string.IsNullOrWhiteSpace(dto.Id) ? Guid.NewGuid().ToString("N") : dto.Id,
        ParentGoalId = dto.ParentGoalId,
        Name = dto.Name,
        Statement = dto.Statement,
        StrategicTheme = dto.StrategicTheme,
        OwnerCompanyId = dto.OwnerCompanyId,
        OwnerPositionId = dto.OwnerPositionId,
        CurrentOwnerPersonId = dto.CurrentOwnerPersonId,
        Owner = dto.Owner,
        ExecutiveSponsor = dto.ExecutiveSponsor,
        ApproverId = dto.ApproverId,
        CoOwnerIds = NormalizeIds(dto.CoOwnerIds),
        ApprovalGroup = dto.ApprovalGroup,
        ReviewOwner = dto.ReviewOwner,
        ApprovalRouteType = dto.ApprovalRouteType,
        ApprovalStatus = dto.ApprovalStatus,
        Status = dto.Status,
        Type = dto.Type,
        TimeHorizonStart = dto.TimeHorizonStart,
        TimeHorizonEnd = dto.TimeHorizonEnd,
        PlanningCycle = dto.PlanningCycle,
        Priority = dto.Priority,
        ContributionType = dto.ContributionType,
        ContributionWeight = dto.ContributionWeight,
        DependencyType = dto.DependencyType,
        EntityScope = dto.EntityScope,
        BusinessUnit = dto.BusinessUnit,
        Region = dto.Region,
        InheritCompanyScope = dto.InheritCompanyScope,
        PrimaryCompanyId = dto.PrimaryCompanyId,
        ApplicableCompanyIds = NormalizeIds(dto.ApplicableCompanyIds),
        DependencyNotes = dto.DependencyNotes,
        PrimaryKpiMetric = dto.PrimaryKpiMetric,
        UnitOfMeasure = dto.UnitOfMeasure,
        DirectionOfPerformance = dto.DirectionOfPerformance,
        ReportingFrequency = dto.ReportingFrequency,
        ThresholdModel = dto.ThresholdModel,
        DecisionReference = dto.DecisionReference,
        EvidenceReference = dto.EvidenceReference,
        LinkedInitiativeIds = NormalizeIds(dto.LinkedInitiativeIds),
        LinkedProjectIds = NormalizeIds(dto.LinkedProjectIds),
        LinkedRiskIssueIds = NormalizeIds(dto.LinkedRiskIssueIds),
        LinkedDependencyIds = NormalizeIds(dto.LinkedDependencyIds),
        DependencyLinks = NormalizeDependencyLinks(dto.DependencyLinks).Select(x => new ObjectiveDependencyLink
        {
            Id = x.Id,
            DependencyTypeId = x.DependencyTypeId,
            DependencyObjectType = x.DependencyObjectType,
            DependencyReferenceId = x.DependencyReferenceId,
            DependencyReferenceText = x.DependencyReferenceText,
            DependencyCriticality = x.DependencyCriticality
        }).ToList(),
        ApprovedBy = dto.ApprovedBy,
        ApprovedOn = dto.ApprovedOn,
        EffectiveDate = dto.EffectiveDate,
        ReviewCadence = dto.ReviewCadence,
        NextReviewDate = dto.NextReviewDate,
        ChangeReason = dto.ChangeReason,
        TargetPlanGranularity = ObjectiveTargetPlanPeriodHelper.NormalizeGranularity(dto.TargetPlanGranularity),
        Version = Math.Max(1, dto.Version),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        CreatedBy = actor,
        UpdatedBy = actor,
        SourceTemplateType = dto.SourceTemplateType,
        SourceTemplateId = dto.SourceTemplateId,
        SourceTemplateVersion = dto.SourceTemplateVersion,
        SourceBlueprintPackId = dto.SourceBlueprintPackId,
        InstantiationBatchId = dto.InstantiationBatchId,
        CreatedFromLibrary = dto.CreatedFromLibrary,
        AllowMultipleTargetMetrics = dto.AllowMultipleTargetMetrics,
        AllowRowThresholdOverrides = dto.AllowRowThresholdOverrides,
        YearlyBudgets = dto.YearlyBudgets.Select(b => new ObjectiveYearlyBudget
        {
            Year = b.Year,
            RequestedBudget = b.RequestedBudget,
            ApprovedBudget = b.ApprovedBudget,
            ForecastBudget = b.ForecastBudget,
            ActualBudget = b.ActualBudget,
            VarianceAmount = b.VarianceAmount,
            Commentary = b.Commentary
        }).ToList(),
        Metrics = dto.Metrics.Select(x => new ObjectiveMetric
        {
            Id = string.IsNullOrWhiteSpace(x.Id) ? Guid.NewGuid().ToString("N") : x.Id,
            ObjectiveId = dto.Id,
            ParentMetricAssignmentId = x.ParentMetricAssignmentId,
            MetricDefId = x.MetricDefId,
            MetricClass = x.MetricClass,
            MetricRole = x.MetricRole,
            MetricName = x.MetricName,
            BaselineValue = x.BaselineValue,
            BaselineDate = x.BaselineDate,
            TargetValue = x.TargetValue,
            TargetPeriod = x.TargetPeriod,
            Direction = x.Direction,
            AggregationMethod = x.AggregationMethod,
            ThresholdTolerance = x.ThresholdTolerance,
            PolarityCode = x.PolarityCode,
            RollupEligibleFlag = x.RollupEligibleFlag,
            ThresholdModelCode = x.ThresholdModelCode,
            ReportingFrequencyCode = x.ReportingFrequencyCode,
            ContributionWeight = x.ContributionWeight,
            YearlyValues = x.YearlyValues.Select(y => new ObjectiveMetricYearValue
            {
                Year = y.Year,
                PeriodKey = y.PeriodKey,
                PeriodLabel = y.PeriodLabel,
                PeriodStart = y.PeriodStart,
                PeriodEnd = y.PeriodEnd,
                PeriodGranularity = y.PeriodGranularity,
                SortOrder = y.SortOrder,
                TargetValue = y.TargetValue,
                ActualValue = y.ActualValue,
                ForecastValue = y.ForecastValue,
                ThresholdMin = y.ThresholdMin,
                ThresholdMax = y.ThresholdMax,
                Commentary = y.Commentary
            }).ToList(),
            TargetDate = x.TargetDate,
            FiscalPeriodId = x.FiscalPeriodId,
            ThresholdValue = x.ThresholdValue,
            Notes = x.Notes,
            UnitOfMeasure = x.UnitOfMeasure,
            MetricBindingStatus = x.MetricBindingStatus,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToList()
    };

    private static void MapInto(ObjectiveAggregate target, ObjectiveDto dto, string actor)
    {
        target.ParentGoalId = dto.ParentGoalId;
        target.Name = dto.Name;
        target.Statement = dto.Statement;
        target.StrategicTheme = dto.StrategicTheme;
        target.OwnerCompanyId = dto.OwnerCompanyId;
        target.OwnerPositionId = dto.OwnerPositionId;
        target.CurrentOwnerPersonId = dto.CurrentOwnerPersonId;
        target.Owner = dto.Owner;
        target.ExecutiveSponsor = dto.ExecutiveSponsor;
        target.ApproverId = dto.ApproverId;
        target.CoOwnerIds = NormalizeIds(dto.CoOwnerIds);
        target.ApprovalGroup = dto.ApprovalGroup;
        target.ReviewOwner = dto.ReviewOwner;
        target.ApprovalRouteType = dto.ApprovalRouteType;
        target.ApprovalStatus = dto.ApprovalStatus;
        target.Status = dto.Status;
        target.Type = dto.Type;
        target.TimeHorizonStart = dto.TimeHorizonStart;
        target.TimeHorizonEnd = dto.TimeHorizonEnd;
        target.PlanningCycle = dto.PlanningCycle;
        target.Priority = dto.Priority;
        target.ContributionType = dto.ContributionType;
        target.ContributionWeight = dto.ContributionWeight;
        target.DependencyType = dto.DependencyType;
        target.EntityScope = dto.EntityScope;
        target.BusinessUnit = dto.BusinessUnit;
        target.Region = dto.Region;
        target.InheritCompanyScope = dto.InheritCompanyScope;
        target.PrimaryCompanyId = dto.PrimaryCompanyId;
        target.ApplicableCompanyIds = NormalizeIds(dto.ApplicableCompanyIds);
        target.DependencyNotes = dto.DependencyNotes;
        target.PrimaryKpiMetric = dto.PrimaryKpiMetric;
        target.UnitOfMeasure = dto.UnitOfMeasure;
        target.DirectionOfPerformance = dto.DirectionOfPerformance;
        target.ReportingFrequency = dto.ReportingFrequency;
        target.ThresholdModel = dto.ThresholdModel;
        target.DecisionReference = dto.DecisionReference;
        target.EvidenceReference = dto.EvidenceReference;
        target.LinkedInitiativeIds = NormalizeIds(dto.LinkedInitiativeIds);
        target.LinkedProjectIds = NormalizeIds(dto.LinkedProjectIds);
        target.LinkedRiskIssueIds = NormalizeIds(dto.LinkedRiskIssueIds);
        target.LinkedDependencyIds = NormalizeIds(dto.LinkedDependencyIds);
        target.DependencyLinks = NormalizeDependencyLinks(dto.DependencyLinks).Select(x => new ObjectiveDependencyLink
        {
            Id = x.Id,
            DependencyTypeId = x.DependencyTypeId,
            DependencyObjectType = x.DependencyObjectType,
            DependencyReferenceId = x.DependencyReferenceId,
            DependencyReferenceText = x.DependencyReferenceText,
            DependencyCriticality = x.DependencyCriticality
        }).ToList();
        target.ApprovedBy = dto.ApprovedBy;
        target.ApprovedOn = dto.ApprovedOn;
        target.EffectiveDate = dto.EffectiveDate;
        target.ReviewCadence = dto.ReviewCadence;
        target.NextReviewDate = dto.NextReviewDate;
        target.ChangeReason = dto.ChangeReason;
        target.TargetPlanGranularity = ObjectiveTargetPlanPeriodHelper.NormalizeGranularity(dto.TargetPlanGranularity);
        target.UpdatedBy = actor;
        target.SourceTemplateType = dto.SourceTemplateType;
        target.SourceTemplateId = dto.SourceTemplateId;
        target.SourceTemplateVersion = dto.SourceTemplateVersion;
        target.SourceBlueprintPackId = dto.SourceBlueprintPackId;
        target.InstantiationBatchId = dto.InstantiationBatchId;
        target.CreatedFromLibrary = dto.CreatedFromLibrary;
        target.AllowMultipleTargetMetrics = dto.AllowMultipleTargetMetrics;
        target.AllowRowThresholdOverrides = dto.AllowRowThresholdOverrides;
        target.YearlyBudgets = dto.YearlyBudgets.Select(b => new ObjectiveYearlyBudget
        {
            Year = b.Year,
            RequestedBudget = b.RequestedBudget,
            ApprovedBudget = b.ApprovedBudget,
            ForecastBudget = b.ForecastBudget,
            ActualBudget = b.ActualBudget,
            VarianceAmount = b.VarianceAmount,
            Commentary = b.Commentary
        }).ToList();
        target.Metrics = dto.Metrics.Select(x => new ObjectiveMetric
        {
            Id = string.IsNullOrWhiteSpace(x.Id) ? Guid.NewGuid().ToString("N") : x.Id,
            ObjectiveId = target.Id,
            ParentMetricAssignmentId = x.ParentMetricAssignmentId,
            MetricDefId = x.MetricDefId,
            MetricClass = x.MetricClass,
            MetricRole = x.MetricRole,
            MetricName = x.MetricName,
            BaselineValue = x.BaselineValue,
            BaselineDate = x.BaselineDate,
            TargetValue = x.TargetValue,
            TargetPeriod = x.TargetPeriod,
            Direction = x.Direction,
            AggregationMethod = x.AggregationMethod,
            ThresholdTolerance = x.ThresholdTolerance,
            PolarityCode = x.PolarityCode,
            RollupEligibleFlag = x.RollupEligibleFlag,
            ThresholdModelCode = x.ThresholdModelCode,
            ReportingFrequencyCode = x.ReportingFrequencyCode,
            ContributionWeight = x.ContributionWeight,
            YearlyValues = x.YearlyValues.Select(y => new ObjectiveMetricYearValue
            {
                Year = y.Year,
                PeriodKey = y.PeriodKey,
                PeriodLabel = y.PeriodLabel,
                PeriodStart = y.PeriodStart,
                PeriodEnd = y.PeriodEnd,
                PeriodGranularity = y.PeriodGranularity,
                SortOrder = y.SortOrder,
                TargetValue = y.TargetValue,
                ActualValue = y.ActualValue,
                ForecastValue = y.ForecastValue,
                ThresholdMin = y.ThresholdMin,
                ThresholdMax = y.ThresholdMax,
                Commentary = y.Commentary
            }).ToList(),
            TargetDate = x.TargetDate,
            FiscalPeriodId = x.FiscalPeriodId,
            ThresholdValue = x.ThresholdValue,
            Notes = x.Notes,
            UnitOfMeasure = x.UnitOfMeasure,
            MetricBindingStatus = x.MetricBindingStatus,
            CreatedAt = x.CreatedAt == default ? DateTime.UtcNow : x.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        }).ToList();
    }
}

internal static class GoalPlanningContextResolver
{
    public static async Task<Response<GoalPlanningContextDto>> ResolveForGoalAsync(
        GoalAggregate goal,
        IStrategyPeriodRepository strategyPeriods,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(goal.StrategyPeriodId))
        {
            return Response<GoalPlanningContextDto>.Fail(
                EnterpriseStrategyErrorCodes.ValidationError,
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["strategyPeriodId"] = new() { "Goal does not have an assigned strategy period." }
                });
        }

        var period = await strategyPeriods.GetByIdAsync(goal.StrategyPeriodId.Trim(), cancellationToken);
        if (period is null)
        {
            return Response<GoalPlanningContextDto>.Fail(
                EnterpriseStrategyErrorCodes.ValidationError,
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["strategyPeriodId"] = new() { "Assigned strategy period no longer exists." }
                });
        }

        return Response<GoalPlanningContextDto>.Ok(ToContext(period));
    }

    public static async Task<GoalPlanningContextDto?> TryResolveForGoalAsync(
        GoalAggregate goal,
        IStrategyPeriodRepository strategyPeriods,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(goal.StrategyPeriodId))
            return null;

        var period = await strategyPeriods.GetByIdAsync(goal.StrategyPeriodId.Trim(), cancellationToken);
        return period is null ? null : ToContext(period);
    }

    private static GoalPlanningContextDto ToContext(StrategyPeriodAggregate period) => new()
    {
        StrategyPeriodId = period.Id,
        StrategyPeriodStatus = period.Status,
        PlanningCycleId = period.PlanningCycleId,
        StartDate = period.StartDate,
        EndDate = period.EndDate,
        CompanyId = period.CompanyId,
        BusinessUnitId = period.BusinessUnitId,
        RegionId = period.RegionId,
        ReviewCadence = period.ReviewCadence
    };
}

public sealed class ConnectionService : IConnectionService
{
    private readonly IStrategyConnectionRepository _connections;
    private readonly IGoalRepository _goals;
    private readonly IObjectiveRepository _objectives;
    private readonly IEnterpriseStrategyAuditSink _audit;
    private readonly bool _allowDirectGoalLinks = false;
    private readonly bool _evidenceRequiredForSensitiveChanges = false;

    public ConnectionService(
        IStrategyConnectionRepository connections,
        IGoalRepository goals,
        IObjectiveRepository objectives,
        IEnterpriseStrategyAuditSink audit)
    {
        _connections = connections;
        _goals = goals;
        _objectives = objectives;
        _audit = audit;
    }

    public async Task<Response<PagedResponseDto<StrategyConnectionDto>>> ListAsync(PagedRequestDto request, CancellationToken cancellationToken = default)
    {
        var rows = await _connections.ListAsync(cancellationToken);
        IEnumerable<StrategyConnectionAggregate> query = rows;
        var f = request.Filters;
        if (f.TryGetValue("companyScopeMode", out var companyScopeMode))
            query = query.Where(x => string.Equals(x.CompanyScopeMode, companyScopeMode, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("company", out var company))
            query = query.Where(x => string.Equals(x.CompanyId, company, StringComparison.OrdinalIgnoreCase));
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 200);
        var total = query.Count();
        var items = query.Skip((page - 1) * size).Take(size).Select(x => x.ToDto()).ToList();
        return Response<PagedResponseDto<StrategyConnectionDto>>.Ok(new PagedResponseDto<StrategyConnectionDto> { Page = page, PageSize = size, TotalCount = total, Items = items });
    }

    public async Task<Response<StrategyConnectionDto>> GetAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        var row = await _connections.GetByIdAsync(connectionId, cancellationToken);
        return row is null ? Response<StrategyConnectionDto>.Fail(EnterpriseStrategyErrorCodes.NotFound) : Response<StrategyConnectionDto>.Ok(row.ToDto());
    }

    public async Task<Response<StrategyConnectionDto>> CreateAsync(StrategyConnectionDto connection, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var validation = EnterpriseStrategyValidators.ValidateConnection(connection, _allowDirectGoalLinks);
        if (_evidenceRequiredForSensitiveChanges && string.IsNullOrWhiteSpace(connection.EvidenceReferencesJson))
            validation["evidence"] = new() { "Evidence reference is required for sensitive changes." };
        if (validation.Count > 0) return Response<StrategyConnectionDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, validation);

        var duplicate = await _connections.GetByEdgeAsync(connection.FromType, connection.FromId, connection.ToType, connection.ToId, cancellationToken);
        if (duplicate is not null) return Response<StrategyConnectionDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, new() { ["edge"] = new() { "Duplicate edge." } });

        if (!await IsEndpointValid(connection, cancellationToken))
            return Response<StrategyConnectionDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["edge"] = new() { "From/To entity does not exist." } });

        var aggregate = ToAggregate(connection, actor);
        var all = await _connections.ListAsync(cancellationToken);
        if (EnterpriseStrategyValidators.IsCircularEdge(aggregate, all))
            return Response<StrategyConnectionDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["graph"] = new() { "Circular lineage detected." } });

        await _connections.AddAsync(aggregate, cancellationToken);
        await EmitAudit(actor, correlationId, aggregate.Id, EnterpriseStrategyEventNames.ConnectionCreated, "", $"{aggregate.FromType}:{aggregate.FromId}->{aggregate.ToType}:{aggregate.ToId}", cancellationToken);
        return Response<StrategyConnectionDto>.Ok(aggregate.ToDto());
    }

    public async Task<Response<StrategyConnectionDto>> UpdateAsync(string connectionId, StrategyConnectionDto connection, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var aggregate = await _connections.GetByIdAsync(connectionId, cancellationToken);
        if (aggregate is null) return Response<StrategyConnectionDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (EnterpriseStrategyResult.IsStaleWrite(expectedVersion, aggregate.Version)) return EnterpriseStrategyResult.StaleVersion<StrategyConnectionDto>();

        var validation = EnterpriseStrategyValidators.ValidateConnection(connection, _allowDirectGoalLinks);
        if (_evidenceRequiredForSensitiveChanges && string.IsNullOrWhiteSpace(connection.EvidenceReferencesJson))
            validation["evidence"] = new() { "Evidence reference is required for sensitive changes." };
        if (validation.Count > 0)
        {
            await EmitAudit(actor, correlationId, connectionId, EnterpriseStrategyEventNames.ConnectionValidationFailed, "", string.Join("; ", validation.SelectMany(x => x.Value)), cancellationToken);
            return Response<StrategyConnectionDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, validation);
        }

        aggregate.FromType = connection.FromType;
        aggregate.FromId = connection.FromId;
        aggregate.ToType = connection.ToType;
        aggregate.ToId = connection.ToId;
        aggregate.RelationshipType = connection.RelationshipType;
        aggregate.ContributionType = connection.ContributionType;
        aggregate.ContributionWeight = connection.ContributionWeight;
        aggregate.MetricBindingsJson = connection.MetricBindingsJson;
        aggregate.DecisionReferencesJson = connection.DecisionReferencesJson;
        aggregate.EvidenceReferencesJson = connection.EvidenceReferencesJson;
        aggregate.CompanyScopeMode = connection.CompanyScopeMode;
        aggregate.CompanyId = connection.CompanyId;
        aggregate.Status = connection.Status;
        aggregate.UpdatedBy = actor;
        aggregate.UpdatedAt = DateTime.UtcNow;
        aggregate.Version++;
        await _connections.UpdateAsync(aggregate, cancellationToken);
        await EmitAudit(actor, correlationId, connectionId, EnterpriseStrategyEventNames.ConnectionUpdated, "", aggregate.Status, cancellationToken);
        return Response<StrategyConnectionDto>.Ok(aggregate.ToDto());
    }

    public async Task<Response<StrategyConnectionDto>> ChangeStatusAsync(string connectionId, string status, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var aggregate = await _connections.GetByIdAsync(connectionId, cancellationToken);
        if (aggregate is null) return Response<StrategyConnectionDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (EnterpriseStrategyResult.IsStaleWrite(expectedVersion, aggregate.Version)) return EnterpriseStrategyResult.StaleVersion<StrategyConnectionDto>();
        aggregate.Status = status;
        aggregate.UpdatedAt = DateTime.UtcNow;
        aggregate.UpdatedBy = actor;
        aggregate.Version++;
        await _connections.UpdateAsync(aggregate, cancellationToken);
        await EmitAudit(actor, correlationId, connectionId, EnterpriseStrategyEventNames.ConnectionStatusChanged, "", status, cancellationToken);
        return Response<StrategyConnectionDto>.Ok(aggregate.ToDto());
    }

    public async Task<Response<bool>> DeleteAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        await _connections.DeleteAsync(connectionId, cancellationToken);
        return Response<bool>.Ok(true);
    }

    public async Task<Response<IReadOnlyList<ConnectionTreeNodeDto>>> TreeAsync(CancellationToken cancellationToken = default)
    {
        var goals = await _goals.ListAsync(cancellationToken);
        var objectives = await _objectives.ListAsync(cancellationToken);
        var connections = await _connections.ListAsync(cancellationToken);

        var root = goals.Select(g => new ConnectionTreeNodeDto
        {
            Type = "Goal",
            Id = g.Id,
            Name = g.Name,
            Status = g.Status,
            Children = objectives.Where(o => o.ParentGoalId == g.Id).Select(o => new ConnectionTreeNodeDto
            {
                Type = "Objective",
                Id = o.Id,
                Name = o.Name,
                Status = o.Status
            }).ToList()
        }).ToList();
        _ = connections;
        return Response<IReadOnlyList<ConnectionTreeNodeDto>>.Ok(root);
    }

    public async Task<Response<ConnectionGraphViewDto>> GraphAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _connections.ListAsync(cancellationToken);
        var nodes = rows.SelectMany(x => new[]
        {
            new ConnectionNodeDto { Id = x.FromId, Type = x.FromType, Label = $"{x.FromType}:{x.FromId}" },
            new ConnectionNodeDto { Id = x.ToId, Type = x.ToType, Label = $"{x.ToType}:{x.ToId}" }
        }).GroupBy(x => $"{x.Type}:{x.Id}", StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();

        var edges = rows.Select(x => new ConnectionEdgeDto { Id = x.Id, FromId = x.FromId, ToId = x.ToId, Status = x.Status }).ToList();
        return Response<ConnectionGraphViewDto>.Ok(new ConnectionGraphViewDto { Nodes = nodes, Edges = edges });
    }

    public async Task<Response<IReadOnlyList<ConnectionMatrixCellDto>>> MatrixAsync(string mode, CancellationToken cancellationToken = default)
    {
        var rows = await _connections.ListAsync(cancellationToken);
        var cells = rows.Select(x => new ConnectionMatrixCellDto { RowId = $"{x.FromType}:{x.FromId}", ColumnId = $"{x.ToType}:{x.ToId}", State = string.Equals(x.Status, "Needs Review", StringComparison.OrdinalIgnoreCase) ? "needs review" : "linked" }).ToList();
        _ = mode;
        return Response<IReadOnlyList<ConnectionMatrixCellDto>>.Ok(cells);
    }

    public async Task<Response<IReadOnlyList<CoverageGapDto>>> CoverageGapsAsync(CancellationToken cancellationToken = default)
    {
        var goals = await _goals.ListAsync(cancellationToken);
        var objectives = await _objectives.ListAsync(cancellationToken);
        var edges = await _connections.ListAsync(cancellationToken);

        var gaps = new List<CoverageGapDto>();
        gaps.AddRange(goals.Where(g => objectives.All(o => o.ParentGoalId != g.Id)).Select(g => new CoverageGapDto { GapType = "goal_without_objective", EntityId = g.Id, Message = "Goal has no objectives." }));
        gaps.AddRange(objectives.Where(o => edges.All(e => !(e.FromType == "Objective" && e.FromId == o.Id && e.ToType == "Initiative"))).Select(o => new CoverageGapDto { GapType = "objective_without_initiative", EntityId = o.Id, Message = "Objective has no initiative links." }));
        return Response<IReadOnlyList<CoverageGapDto>>.Ok(gaps);
    }

    public async Task<Response<ConnectionGraphViewDto>> ValidateGraphAsync(CancellationToken cancellationToken = default)
    {
        var edges = await _connections.ListAsync(cancellationToken);
        foreach (var edge in edges)
        {
            var subset = edges.Where(x => x.Id != edge.Id).ToList();
            if (EnterpriseStrategyValidators.IsCircularEdge(edge, subset))
                return Response<ConnectionGraphViewDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["graph"] = new() { $"Circular edge at {edge.Id}" } });
        }

        return await GraphAsync(cancellationToken);
    }

    private async Task<bool> IsEndpointValid(StrategyConnectionDto connection, CancellationToken cancellationToken)
    {
        async Task<bool> Exists(string type, string id) => type switch
        {
            "Goal" => await _goals.ExistsAsync(id, cancellationToken),
            "Objective" => (await _objectives.GetByIdAsync(id, cancellationToken)) is not null,
            "Initiative" => true,
            "Project" => true,
            _ => false
        };

        return await Exists(connection.FromType, connection.FromId) && await Exists(connection.ToType, connection.ToId);
    }

    private async Task EmitAudit(string actor, string correlationId, string objectId, string action, string before, string after, CancellationToken cancellationToken)
    {
        await _audit.WriteMutationAsync(actor, "Connection", objectId, action, correlationId, "enterprise-strategy.connections", before, after, cancellationToken);
    }

    private static StrategyConnectionAggregate ToAggregate(StrategyConnectionDto dto, string actor) => new()
    {
        Id = string.IsNullOrWhiteSpace(dto.Id) ? Guid.NewGuid().ToString("N") : dto.Id,
        FromType = dto.FromType,
        FromId = dto.FromId,
        ToType = dto.ToType,
        ToId = dto.ToId,
        RelationshipType = dto.RelationshipType,
        ContributionType = dto.ContributionType,
        ContributionWeight = dto.ContributionWeight,
        MetricBindingsJson = dto.MetricBindingsJson,
        DecisionReferencesJson = dto.DecisionReferencesJson,
        EvidenceReferencesJson = dto.EvidenceReferencesJson,
        CompanyScopeMode = dto.CompanyScopeMode,
        CompanyId = dto.CompanyId,
        Status = dto.Status,
        Version = Math.Max(1, dto.Version),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        CreatedBy = actor,
        UpdatedBy = actor
    };
}
