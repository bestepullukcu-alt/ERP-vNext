using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Mappers;
using Diten.Application.EnterpriseStrategy.Repositories;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Domain.Aggregates.EnterpriseStrategy;

namespace Diten.Application.EnterpriseStrategy.Services;

public interface IPlanningCycleService
{
    Task<Response<IReadOnlyList<PlanningCycleDto>>> ListPlanningCyclesAsync(string? search = null, string? status = null, CancellationToken cancellationToken = default);
    Task<Response<PlanningCycleDto>> GetPlanningCycleAsync(string planningCycleId, CancellationToken cancellationToken = default);
    Task<Response<PlanningCycleDto>> CreatePlanningCycleAsync(PlanningCycleDto input, string actor, CancellationToken cancellationToken = default);
    Task<Response<PlanningCycleDto>> UpdatePlanningCycleAsync(string planningCycleId, PlanningCycleDto input, string actor, CancellationToken cancellationToken = default);
    Task<Response<PlanningCycleDto>> ChangePlanningCycleStatusAsync(string planningCycleId, string newStatus, string actor, CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<StrategyPeriodDto>>> ListStrategyPeriodsAsync(string? planningCycleId = null, string? search = null, string? status = null, CancellationToken cancellationToken = default);
    Task<Response<StrategyPeriodDto>> GetStrategyPeriodAsync(string strategyPeriodId, CancellationToken cancellationToken = default);
    Task<Response<StrategyPeriodDto>> CreateStrategyPeriodAsync(StrategyPeriodDto input, string actor, CancellationToken cancellationToken = default);
    Task<Response<StrategyPeriodDto>> UpdateStrategyPeriodAsync(string strategyPeriodId, StrategyPeriodDto input, string actor, CancellationToken cancellationToken = default);
    Task<Response<StrategyPeriodDto>> ChangeStrategyPeriodStatusAsync(string strategyPeriodId, string newStatus, string actor, CancellationToken cancellationToken = default);
    Task<Response<StrategyPeriodDto>> ResolveDefaultForScopeAsync(string companyId, string? businessUnitId, string? regionId, CancellationToken cancellationToken = default);
    Task<Response<StrategyPeriodUsageSummaryDto>> GetStrategyPeriodUsageSummaryAsync(string strategyPeriodId, CancellationToken cancellationToken = default);
}

public sealed class PlanningCycleService : IPlanningCycleService
{
    private readonly IPlanningCycleRepository _planningCycles;
    private readonly IStrategyPeriodRepository _strategyPeriods;
    private readonly IGoalRepository _goals;
    private readonly IObjectiveRepository _objectives;
    private readonly IInitiativeStrategyLinkRepository _initiativeLinks;

    public PlanningCycleService(
        IPlanningCycleRepository planningCycles,
        IStrategyPeriodRepository strategyPeriods,
        IGoalRepository goals,
        IObjectiveRepository objectives,
        IInitiativeStrategyLinkRepository initiativeLinks)
    {
        _planningCycles = planningCycles;
        _strategyPeriods = strategyPeriods;
        _goals = goals;
        _objectives = objectives;
        _initiativeLinks = initiativeLinks;
    }

    public async Task<Response<IReadOnlyList<PlanningCycleDto>>> ListPlanningCyclesAsync(string? search = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var rows = await _planningCycles.ListAsync(cancellationToken);
        IEnumerable<PlanningCycleAggregate> query = rows;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(x =>
                x.Code.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => string.Equals(x.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));

        query = query.OrderByDescending(x => x.UpdatedOn);
        return Response<IReadOnlyList<PlanningCycleDto>>.Ok(query.Select(x => x.ToDto()).ToList());
    }

    public async Task<Response<PlanningCycleDto>> GetPlanningCycleAsync(string planningCycleId, CancellationToken cancellationToken = default)
    {
        var row = await _planningCycles.GetByIdAsync(planningCycleId, cancellationToken);
        if (row is null)
            return Response<PlanningCycleDto>.Fail(EnterpriseStrategyErrorCodes.NotFound, NewError("planningCycleId", "PlanningCycle was not found."));

        return Response<PlanningCycleDto>.Ok(row.ToDto());
    }

    public async Task<Response<PlanningCycleDto>> CreatePlanningCycleAsync(PlanningCycleDto input, string actor, CancellationToken cancellationToken = default)
    {
        input.EffectiveFrom = NormalizeDateOnlyUtc(input.EffectiveFrom);
        input.EffectiveTo = NormalizeDateOnlyUtc(input.EffectiveTo);
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "Draft" : input.Status.Trim();
        input.OwnerCompanyId = input.OwnerCompanyId?.Trim() ?? string.Empty;
        input.OwnerPositionId = string.IsNullOrWhiteSpace(input.OwnerPositionId) ? null : input.OwnerPositionId.Trim();
        input.CurrentOwnerPersonId = string.IsNullOrWhiteSpace(input.CurrentOwnerPersonId) ? null : input.CurrentOwnerPersonId.Trim();
        var errors = ValidatePlanningCycle(input);
        if (errors.Count > 0)
            return Response<PlanningCycleDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, errors);

        var normalizedCode = input.Code.Trim().ToUpperInvariant();
        var duplicate = await _planningCycles.GetByCodeAsync(normalizedCode, cancellationToken);
        if (duplicate is not null)
            return Response<PlanningCycleDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, NewError("code", "PlanningCycle Code must be unique."));

        var now = DateTime.UtcNow;
        var aggregate = new PlanningCycleAggregate
        {
            Id = string.IsNullOrWhiteSpace(input.Id) ? Guid.NewGuid().ToString("N") : input.Id.Trim(),
            Code = normalizedCode,
            Name = input.Name.Trim(),
            PlanningCycleType = input.PlanningCycleType.Trim(),
            Description = input.Description?.Trim() ?? string.Empty,
            Status = input.Status.Trim(),
            OwnerCompanyId = input.OwnerCompanyId,
            OwnerPositionId = input.OwnerPositionId,
            CurrentOwnerPersonId = input.CurrentOwnerPersonId,
            EffectiveFrom = input.EffectiveFrom,
            EffectiveTo = input.EffectiveTo,
            CreatedOn = now,
            CreatedBy = actor,
            UpdatedOn = now,
            UpdatedBy = actor
        };

        await _planningCycles.AddAsync(aggregate, cancellationToken);
        return Response<PlanningCycleDto>.Ok(aggregate.ToDto());
    }

    public async Task<Response<PlanningCycleDto>> UpdatePlanningCycleAsync(string planningCycleId, PlanningCycleDto input, string actor, CancellationToken cancellationToken = default)
    {
        var existing = await _planningCycles.GetByIdAsync(planningCycleId, cancellationToken);
        if (existing is null)
            return Response<PlanningCycleDto>.Fail(EnterpriseStrategyErrorCodes.NotFound, NewError("planningCycleId", "PlanningCycle was not found."));

        input.EffectiveFrom = NormalizeDateOnlyUtc(input.EffectiveFrom);
        input.EffectiveTo = NormalizeDateOnlyUtc(input.EffectiveTo);
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? existing.Status : input.Status.Trim();
        input.OwnerCompanyId = input.OwnerCompanyId?.Trim() ?? string.Empty;
        input.OwnerPositionId = string.IsNullOrWhiteSpace(input.OwnerPositionId) ? null : input.OwnerPositionId.Trim();
        input.CurrentOwnerPersonId = string.IsNullOrWhiteSpace(input.CurrentOwnerPersonId) ? null : input.CurrentOwnerPersonId.Trim();
        var errors = ValidatePlanningCycle(input);
        if (errors.Count > 0)
            return Response<PlanningCycleDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, errors);

        var normalizedCode = input.Code.Trim().ToUpperInvariant();
        var duplicate = await _planningCycles.GetByCodeAsync(normalizedCode, cancellationToken);
        if (duplicate is not null && !string.Equals(duplicate.Id, planningCycleId, StringComparison.OrdinalIgnoreCase))
            return Response<PlanningCycleDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, NewError("code", "PlanningCycle Code must be unique."));

        var shrinkErrors = await ValidatePlanningCycleDateShrinkAsync(planningCycleId, input.EffectiveFrom, input.EffectiveTo, cancellationToken);
        if (shrinkErrors.Count > 0)
            return Response<PlanningCycleDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, shrinkErrors);

        if (string.Equals(input.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            var archiveErrors = await ValidatePlanningCycleArchiveUsageGuardAsync(planningCycleId, cancellationToken);
            if (archiveErrors.Count > 0)
                return Response<PlanningCycleDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, archiveErrors);
        }

        existing.Code = normalizedCode;
        existing.Name = input.Name.Trim();
        existing.PlanningCycleType = input.PlanningCycleType.Trim();
        existing.Description = input.Description?.Trim() ?? string.Empty;
        existing.Status = input.Status.Trim();
        existing.OwnerCompanyId = input.OwnerCompanyId;
        existing.OwnerPositionId = input.OwnerPositionId;
        existing.CurrentOwnerPersonId = input.CurrentOwnerPersonId;
        existing.EffectiveFrom = input.EffectiveFrom;
        existing.EffectiveTo = input.EffectiveTo;
        existing.UpdatedOn = DateTime.UtcNow;
        existing.UpdatedBy = actor;
        existing.ArchivedAt = string.Equals(existing.Status, "Archived", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null;

        await _planningCycles.UpdateAsync(existing, cancellationToken);
        return Response<PlanningCycleDto>.Ok(existing.ToDto());
    }

    public async Task<Response<PlanningCycleDto>> ChangePlanningCycleStatusAsync(string planningCycleId, string newStatus, string actor, CancellationToken cancellationToken = default)
    {
        var existing = await _planningCycles.GetByIdAsync(planningCycleId, cancellationToken);
        if (existing is null)
            return Response<PlanningCycleDto>.Fail(EnterpriseStrategyErrorCodes.NotFound, NewError("planningCycleId", "PlanningCycle was not found."));

        var status = (newStatus ?? string.Empty).Trim();
        if (!EnterpriseStrategyPlanningLookupCatalog.LifecycleStatuses.Contains(status))
            return Response<PlanningCycleDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, NewError("status", "Status must be Draft, Active, or Archived."));

        if (!IsAllowedTransition(existing.Status, status))
            return Response<PlanningCycleDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, NewError("status", $"Status transition {existing.Status} -> {status} is not allowed."));

        // Draft -> Active must still satisfy required invariants.
        if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var errors = ValidatePlanningCycle(existing.ToDto());
            if (errors.Count > 0)
                return Response<PlanningCycleDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, errors);
        }

        if (string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            var archiveErrors = await ValidatePlanningCycleArchiveUsageGuardAsync(planningCycleId, cancellationToken);
            if (archiveErrors.Count > 0)
                return Response<PlanningCycleDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, archiveErrors);
        }

        existing.Status = status;
        existing.UpdatedOn = DateTime.UtcNow;
        existing.UpdatedBy = actor;
        existing.ArchivedAt = string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null;
        await _planningCycles.UpdateAsync(existing, cancellationToken);
        return Response<PlanningCycleDto>.Ok(existing.ToDto());
    }

    public async Task<Response<IReadOnlyList<StrategyPeriodDto>>> ListStrategyPeriodsAsync(string? planningCycleId = null, string? search = null, string? status = null, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<StrategyPeriodAggregate> rows = string.IsNullOrWhiteSpace(planningCycleId)
            ? await _strategyPeriods.ListAsync(cancellationToken)
            : await _strategyPeriods.ListByPlanningCycleIdAsync(planningCycleId, cancellationToken);

        IEnumerable<StrategyPeriodAggregate> query = rows;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(x =>
                x.Code.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => string.Equals(x.Status, status.Trim(), StringComparison.OrdinalIgnoreCase));

        query = query.OrderByDescending(x => x.UpdatedOn);
        return Response<IReadOnlyList<StrategyPeriodDto>>.Ok(query.Select(x => x.ToDto()).ToList());
    }

    public async Task<Response<StrategyPeriodDto>> GetStrategyPeriodAsync(string strategyPeriodId, CancellationToken cancellationToken = default)
    {
        var row = await _strategyPeriods.GetByIdAsync(strategyPeriodId, cancellationToken);
        if (row is null)
            return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.NotFound, NewError("strategyPeriodId", "StrategyPeriod was not found."));

        return Response<StrategyPeriodDto>.Ok(row.ToDto());
    }

    public async Task<Response<StrategyPeriodDto>> CreateStrategyPeriodAsync(StrategyPeriodDto input, string actor, CancellationToken cancellationToken = default)
    {
        input.StartDate = NormalizeDateOnlyUtc(input.StartDate);
        input.EndDate = NormalizeDateOnlyUtc(input.EndDate);
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? "Draft" : input.Status.Trim();
        input.OwnerCompanyId = string.IsNullOrWhiteSpace(input.OwnerCompanyId) ? input.CompanyId?.Trim() ?? string.Empty : input.OwnerCompanyId.Trim();
        input.CurrentOwnerPersonId = string.IsNullOrWhiteSpace(input.CurrentOwnerPersonId) ? input.OwnerEmployeeId?.Trim() ?? string.Empty : input.CurrentOwnerPersonId.Trim();
        input.OwnerEmployeeId = input.CurrentOwnerPersonId;
        input.OwnerPositionId = string.IsNullOrWhiteSpace(input.OwnerPositionId) ? null : input.OwnerPositionId.Trim();
        var errors = await ValidateStrategyPeriodAsync(input, cancellationToken);
        if (errors.Count > 0)
            return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, errors);

        var normalizedCode = input.Code.Trim().ToUpperInvariant();
        var duplicate = await _strategyPeriods.GetByCodeAsync(normalizedCode, cancellationToken);
        if (duplicate is not null)
            return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, NewError("code", "StrategyPeriod Code must be unique."));

        var now = DateTime.UtcNow;
        var aggregate = new StrategyPeriodAggregate
        {
            Id = string.IsNullOrWhiteSpace(input.Id) ? Guid.NewGuid().ToString("N") : input.Id.Trim(),
            PlanningCycleId = input.PlanningCycleId.Trim(),
            Code = normalizedCode,
            Name = input.Name.Trim(),
            OwnerEmployeeId = input.OwnerEmployeeId.Trim(),
            OwnerCompanyId = input.OwnerCompanyId.Trim(),
            OwnerPositionId = string.IsNullOrWhiteSpace(input.OwnerPositionId) ? null : input.OwnerPositionId.Trim(),
            CompanyId = input.CompanyId.Trim(),
            BusinessUnitId = string.IsNullOrWhiteSpace(input.BusinessUnitId) ? null : input.BusinessUnitId.Trim(),
            RegionId = string.IsNullOrWhiteSpace(input.RegionId) ? null : input.RegionId.Trim(),
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            ReviewCadence = input.ReviewCadence.Trim(),
            ScenarioType = string.IsNullOrWhiteSpace(input.ScenarioType) ? null : input.ScenarioType.Trim(),
            VersionLabel = string.IsNullOrWhiteSpace(input.VersionLabel) ? null : input.VersionLabel.Trim(),
            Status = input.Status.Trim(),
            IsDefaultForScope = input.IsDefaultForScope,
            Notes = input.Notes?.Trim() ?? string.Empty,
            CreatedOn = now,
            CreatedBy = actor,
            UpdatedOn = now,
            UpdatedBy = actor
        };

        if (string.Equals(aggregate.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var overlapErrors = await ValidateActivePeriodOverlapAsync(aggregate, cancellationToken);
            if (overlapErrors.Count > 0)
                return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, overlapErrors);

            var defaultErrors = await ValidateSingleDefaultForScopeAsync(aggregate, cancellationToken);
            if (defaultErrors.Count > 0)
                return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, defaultErrors);
        }

        await _strategyPeriods.AddAsync(aggregate, cancellationToken);
        return Response<StrategyPeriodDto>.Ok(aggregate.ToDto());
    }

    public async Task<Response<StrategyPeriodDto>> UpdateStrategyPeriodAsync(string strategyPeriodId, StrategyPeriodDto input, string actor, CancellationToken cancellationToken = default)
    {
        var existing = await _strategyPeriods.GetByIdAsync(strategyPeriodId, cancellationToken);
        if (existing is null)
            return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.NotFound, NewError("strategyPeriodId", "StrategyPeriod was not found."));

        input.StartDate = NormalizeDateOnlyUtc(input.StartDate);
        input.EndDate = NormalizeDateOnlyUtc(input.EndDate);
        input.Status = string.IsNullOrWhiteSpace(input.Status) ? existing.Status : input.Status.Trim();
        input.OwnerCompanyId = string.IsNullOrWhiteSpace(input.OwnerCompanyId) ? input.CompanyId?.Trim() ?? string.Empty : input.OwnerCompanyId.Trim();
        input.CurrentOwnerPersonId = string.IsNullOrWhiteSpace(input.CurrentOwnerPersonId) ? input.OwnerEmployeeId?.Trim() ?? string.Empty : input.CurrentOwnerPersonId.Trim();
        input.OwnerEmployeeId = input.CurrentOwnerPersonId;
        input.OwnerPositionId = string.IsNullOrWhiteSpace(input.OwnerPositionId) ? null : input.OwnerPositionId.Trim();
        var errors = await ValidateStrategyPeriodAsync(input, cancellationToken);
        if (errors.Count > 0)
            return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, errors);

        var normalizedCode = input.Code.Trim().ToUpperInvariant();
        var duplicate = await _strategyPeriods.GetByCodeAsync(normalizedCode, cancellationToken);
        if (duplicate is not null && !string.Equals(duplicate.Id, strategyPeriodId, StringComparison.OrdinalIgnoreCase))
            return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, NewError("code", "StrategyPeriod Code must be unique."));

        if (!IsAllowedTransition(existing.Status, input.Status))
            return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, NewError("status", $"Status transition {existing.Status} -> {input.Status} is not allowed."));

        var candidate = new StrategyPeriodAggregate
        {
            Id = existing.Id,
            PlanningCycleId = input.PlanningCycleId.Trim(),
            Code = normalizedCode,
            Name = input.Name.Trim(),
            OwnerEmployeeId = input.OwnerEmployeeId.Trim(),
            OwnerCompanyId = input.OwnerCompanyId.Trim(),
            OwnerPositionId = string.IsNullOrWhiteSpace(input.OwnerPositionId) ? null : input.OwnerPositionId.Trim(),
            CompanyId = input.CompanyId.Trim(),
            BusinessUnitId = string.IsNullOrWhiteSpace(input.BusinessUnitId) ? null : input.BusinessUnitId.Trim(),
            RegionId = string.IsNullOrWhiteSpace(input.RegionId) ? null : input.RegionId.Trim(),
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            ReviewCadence = input.ReviewCadence.Trim(),
            ScenarioType = string.IsNullOrWhiteSpace(input.ScenarioType) ? null : input.ScenarioType.Trim(),
            VersionLabel = string.IsNullOrWhiteSpace(input.VersionLabel) ? null : input.VersionLabel.Trim(),
            Status = input.Status.Trim(),
            IsDefaultForScope = input.IsDefaultForScope,
            Notes = input.Notes?.Trim() ?? string.Empty,
            CreatedOn = existing.CreatedOn,
            CreatedBy = existing.CreatedBy,
            UpdatedOn = DateTime.UtcNow,
            UpdatedBy = actor,
            ArchivedAt = string.Equals(input.Status, "Archived", StringComparison.OrdinalIgnoreCase) ? (existing.ArchivedAt ?? DateTime.UtcNow) : null
        };

        // ─── Usage-Aware Governing Guards ───
        var structuralLockErrors = await ValidateStructuralFieldLockAsync(existing, candidate, cancellationToken);
        if (structuralLockErrors.Count > 0)
            return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, structuralLockErrors);

        if (existing.StartDate.Date != candidate.StartDate.Date || existing.EndDate.Date != candidate.EndDate.Date)
        {
            var dateShrinkErrors = await ValidateDateShrinkAgainstUsageAsync(existing.Id, candidate.StartDate, candidate.EndDate, cancellationToken);
            if (dateShrinkErrors.Count > 0)
                return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, dateShrinkErrors);
        }

        if (string.Equals(candidate.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var overlapErrors = await ValidateActivePeriodOverlapAsync(candidate, cancellationToken);
            if (overlapErrors.Count > 0)
                return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, overlapErrors);

            var defaultErrors = await ValidateSingleDefaultForScopeAsync(candidate, cancellationToken);
            if (defaultErrors.Count > 0)
                return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, defaultErrors);
        }

        if (string.Equals(candidate.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            var archiveErrors = await ValidateArchiveUsageGuardAsync(candidate.Id, cancellationToken);
            if (archiveErrors.Count > 0)
                return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, archiveErrors);
        }

        await _strategyPeriods.UpdateAsync(candidate, cancellationToken);
        return Response<StrategyPeriodDto>.Ok(candidate.ToDto());
    }

    public async Task<Response<StrategyPeriodDto>> ChangeStrategyPeriodStatusAsync(string strategyPeriodId, string newStatus, string actor, CancellationToken cancellationToken = default)
    {
        var existing = await _strategyPeriods.GetByIdAsync(strategyPeriodId, cancellationToken);
        if (existing is null)
            return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.NotFound, NewError("strategyPeriodId", "StrategyPeriod was not found."));

        var status = (newStatus ?? string.Empty).Trim();
        if (!EnterpriseStrategyPlanningLookupCatalog.LifecycleStatuses.Contains(status))
            return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, NewError("status", "Status must be Draft, Active, or Archived."));

        if (!IsAllowedTransition(existing.Status, status))
            return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, NewError("status", $"Status transition {existing.Status} -> {status} is not allowed."));

        if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var validationErrors = await ValidateStrategyPeriodAsync(existing.ToDto(), cancellationToken);
            if (validationErrors.Count > 0)
                return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, validationErrors);

            var overlapErrors = await ValidateActivePeriodOverlapAsync(existing, cancellationToken);
            if (overlapErrors.Count > 0)
                return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, overlapErrors);

            var defaultErrors = await ValidateSingleDefaultForScopeAsync(existing, cancellationToken);
            if (defaultErrors.Count > 0)
                return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, defaultErrors);
        }

        if (string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            var archiveErrors = await ValidateArchiveUsageGuardAsync(existing.Id, cancellationToken);
            if (archiveErrors.Count > 0)
                return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, archiveErrors);
        }

        existing.Status = status;
        existing.UpdatedOn = DateTime.UtcNow;
        existing.UpdatedBy = actor;
        existing.ArchivedAt = string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : null;

        await _strategyPeriods.UpdateAsync(existing, cancellationToken);
        return Response<StrategyPeriodDto>.Ok(existing.ToDto());
    }

    public async Task<Response<StrategyPeriodDto>> ResolveDefaultForScopeAsync(string companyId, string? businessUnitId, string? regionId, CancellationToken cancellationToken = default)
    {
        var rows = await _strategyPeriods.ListAsync(cancellationToken);
        var scopedRows = rows
            .Where(x => x.IsDefaultForScope)
            .Where(x => string.Equals(x.CompanyId, companyId, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(businessUnitId) || string.Equals(x.BusinessUnitId, businessUnitId, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(regionId) || string.Equals(x.RegionId, regionId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.StartDate)
            .ToList();

        var resolved = scopedRows.FirstOrDefault();
        if (resolved is null)
            return Response<StrategyPeriodDto>.Fail(EnterpriseStrategyErrorCodes.NotFound, NewError("scope", "No default StrategyPeriod was found for the requested scope."));

        return Response<StrategyPeriodDto>.Ok(resolved.ToDto());
    }

    private static Dictionary<string, List<string>> ValidatePlanningCycle(PlanningCycleDto input)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        AddIf(errors, string.IsNullOrWhiteSpace(input.Code), "code", "Code is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(input.Name), "name", "Name is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(input.PlanningCycleType), "planningCycleType", "PlanningCycleType is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(input.OwnerCompanyId), "ownerCompanyId", "Owner Company / Org is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(input.OwnerPositionId), "ownerPositionId", "Owner Position is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(input.Status), "status", "Status is required.");
        AddIf(errors, input.EffectiveTo < input.EffectiveFrom, "effectiveTo", "EffectiveTo must be greater than or equal to EffectiveFrom.");

        if (!string.IsNullOrWhiteSpace(input.PlanningCycleType))
            AddIf(errors, !EnterpriseStrategyPlanningLookupCatalog.PlanningCycleTypes.Contains(input.PlanningCycleType.Trim()), "planningCycleType", "PlanningCycleType is invalid.");
        if (!string.IsNullOrWhiteSpace(input.Status))
            AddIf(errors, !EnterpriseStrategyPlanningLookupCatalog.LifecycleStatuses.Contains(input.Status.Trim()), "status", "Status must be Draft, Active, or Archived.");

        return errors;
    }

    private async Task<Dictionary<string, List<string>>> ValidateStrategyPeriodAsync(StrategyPeriodDto input, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        AddIf(errors, string.IsNullOrWhiteSpace(input.PlanningCycleId), "planningCycleId", "PlanningCycleId is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(input.Code), "code", "Code is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(input.Name), "name", "Name is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(input.OwnerCompanyId), "ownerCompanyId", "Owner Company / Org is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(input.OwnerPositionId), "ownerPositionId", "Owner Position is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(input.CompanyId), "companyId", "CompanyId is required for MVP.");
        AddIf(errors, string.IsNullOrWhiteSpace(input.ReviewCadence), "reviewCadence", "ReviewCadence is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(input.Status), "status", "Status is required.");
        AddIf(errors, input.EndDate < input.StartDate, "endDate", "EndDate must be greater than or equal to StartDate.");

        if (!string.IsNullOrWhiteSpace(input.Status))
            AddIf(errors, !EnterpriseStrategyPlanningLookupCatalog.LifecycleStatuses.Contains(input.Status.Trim()), "status", "Status must be Draft, Active, or Archived.");
        if (!string.IsNullOrWhiteSpace(input.ReviewCadence))
            AddIf(errors, !EnterpriseStrategyPlanningLookupCatalog.ReviewCadences.Contains(input.ReviewCadence.Trim()), "reviewCadence", "ReviewCadence is invalid.");
        if (!string.IsNullOrWhiteSpace(input.ScenarioType))
            AddIf(errors, !EnterpriseStrategyPlanningLookupCatalog.ScenarioTypes.Contains(input.ScenarioType.Trim()), "scenarioType", "ScenarioType is invalid.");

        if (!string.IsNullOrWhiteSpace(input.PlanningCycleId))
        {
            var planningCycle = await _planningCycles.GetByIdAsync(input.PlanningCycleId.Trim(), cancellationToken);
            AddIf(errors, planningCycle is null, "planningCycleId", "StrategyPeriod must reference a valid PlanningCycle.");
            if (planningCycle is not null)
            {
                AddIf(
                    errors,
                    input.StartDate.Date < planningCycle.EffectiveFrom.Date,
                    "startDate",
                    "StrategyPeriod StartDate must be on or after parent PlanningCycle EffectiveFrom.");
                AddIf(
                    errors,
                    input.EndDate.Date > planningCycle.EffectiveTo.Date,
                    "endDate",
                    "StrategyPeriod EndDate must be on or before parent PlanningCycle EffectiveTo.");
            }
        }
        AddIf(
            errors,
            !string.IsNullOrWhiteSpace(input.OwnerCompanyId) &&
            !string.IsNullOrWhiteSpace(input.CompanyId) &&
            !string.Equals(input.OwnerCompanyId.Trim(), input.CompanyId.Trim(), StringComparison.OrdinalIgnoreCase),
            "ownerCompanyId",
            "Owner Company / Org must stay within the selected scope.");

        return errors;
    }

    private async Task<Dictionary<string, List<string>>> ValidateActivePeriodOverlapAsync(StrategyPeriodAggregate candidate, CancellationToken cancellationToken)
    {
        var activeRows = (await _strategyPeriods.ListAsync(cancellationToken))
            .Where(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .Where(x => !string.Equals(x.Id, candidate.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var conflict = activeRows.FirstOrDefault(existing =>
            string.Equals(existing.CompanyId, candidate.CompanyId, StringComparison.OrdinalIgnoreCase) &&
            ScopeMatchesOrBothNull(existing.BusinessUnitId, candidate.BusinessUnitId) &&
            ScopeMatchesOrBothNull(existing.RegionId, candidate.RegionId) &&
            DateRangesOverlap(existing.StartDate, existing.EndDate, candidate.StartDate, candidate.EndDate));

        if (conflict is null)
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        return NewError(
            "overlap",
            $"Another active Strategy Period ('{conflict.Code}') already overlaps this date range for the same Company/Business Unit/Region scope.");
    }

    private async Task<Dictionary<string, List<string>>> ValidateSingleDefaultForScopeAsync(StrategyPeriodAggregate candidate, CancellationToken cancellationToken)
    {
        if (!candidate.IsDefaultForScope || !string.Equals(candidate.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var conflict = (await _strategyPeriods.ListAsync(cancellationToken))
            .Where(x => x.IsDefaultForScope)
            .Where(x => string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .Where(x => !string.Equals(x.Id, candidate.Id, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(existing =>
                string.Equals(existing.CompanyId, candidate.CompanyId, StringComparison.OrdinalIgnoreCase) &&
                ScopeMatchesOrBothNull(existing.BusinessUnitId, candidate.BusinessUnitId) &&
                ScopeMatchesOrBothNull(existing.RegionId, candidate.RegionId));

        if (conflict is null)
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        return NewError(
            "isDefaultForScope",
            $"Scope already has an active default Strategy Period ('{conflict.Code}'). Keep only one active default per Company/Business Unit/Region scope.");
    }
    public async Task<Response<StrategyPeriodUsageSummaryDto>> GetStrategyPeriodUsageSummaryAsync(string strategyPeriodId, CancellationToken cancellationToken = default)
    {
        var period = await _strategyPeriods.GetByIdAsync(strategyPeriodId, cancellationToken);
        if (period is null)
            return Response<StrategyPeriodUsageSummaryDto>.Fail(EnterpriseStrategyErrorCodes.NotFound, NewError("strategyPeriodId", "StrategyPeriod was not found."));

        var goals = await _goals.ListAsync(cancellationToken);
        var scopedGoals = goals
            .Where(x => string.Equals((x.StrategyPeriodId ?? string.Empty).Trim(), strategyPeriodId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var goalIds = new HashSet<string>(scopedGoals.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
        var objectives = (await _objectives.ListAsync(cancellationToken))
            .Where(o => goalIds.Contains((o.ParentGoalId ?? string.Empty).Trim()))
            .ToList();

        var dto = new StrategyPeriodUsageSummaryDto
        {
            StrategyPeriodId = strategyPeriodId,
            GoalCount = scopedGoals.Count,
            ObjectiveCount = objectives.Count,
            Goals = scopedGoals.Select(g => new StrategyPeriodUsageGoalRef
            {
                GoalId = g.Id,
                GoalTitle = g.GoalTitle,
                ObjectiveCount = objectives.Count(o => string.Equals((o.ParentGoalId ?? string.Empty).Trim(), g.Id, StringComparison.OrdinalIgnoreCase))
            }).ToList()
        };

        return Response<StrategyPeriodUsageSummaryDto>.Ok(dto);
    }

    private async Task<Dictionary<string, List<string>>> ValidateStructuralFieldLockAsync(StrategyPeriodAggregate existing, StrategyPeriodAggregate candidate, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (!string.Equals(existing.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return errors;

        var usage = await GetStrategyPeriodUsageCountsAsync(existing.Id, cancellationToken);
        if (usage.Goals == 0 && usage.Objectives == 0)
            return errors;

        if (!string.Equals(existing.PlanningCycleId, candidate.PlanningCycleId, StringComparison.OrdinalIgnoreCase))
            AddIf(errors, true, "planningCycleId", $"Parent Planning Cycle, {usage.Goals} goal(s) ve {usage.Objectives} objective(s) bağlı olduğu için değiştirilemez.");

        if (!string.Equals(existing.CompanyId, candidate.CompanyId, StringComparison.OrdinalIgnoreCase))
            AddIf(errors, true, "companyId", "Company, bağlı kayıtlar mevcut olduğu için değiştirilemez. Önce Goal atamalarını kaldırın.");

        if (!ScopeMatchesOrBothNull(existing.BusinessUnitId, candidate.BusinessUnitId))
            AddIf(errors, true, "businessUnitId", "Business Unit, bağlı kayıtlar mevcut olduğu için değiştirilemez.");

        if (!ScopeMatchesOrBothNull(existing.RegionId, candidate.RegionId))
            AddIf(errors, true, "regionId", "Region, bağlı kayıtlar mevcut olduğu için değiştirilemez.");

        return errors;
    }

    private async Task<Dictionary<string, List<string>>> ValidateDateShrinkAgainstUsageAsync(string strategyPeriodId, DateTime candidateStart, DateTime candidateEnd, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var goals = await _goals.ListAsync(cancellationToken);
        var goalList = goals
            .Where(g => string.Equals((g.StrategyPeriodId ?? string.Empty).Trim(), strategyPeriodId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (goalList.Count == 0)
            return errors;

        foreach (var goal in goalList)
        {
            if (goal.StartDate.HasValue && goal.StartDate.Value.Date < candidateStart.Date)
                AddIf(errors, true, "startDate", $"Goal '{goal.GoalTitle}' başlangıç tarihi ({goal.StartDate.Value:yyyy-MM-dd}), yeni period başlangıcından ({candidateStart:yyyy-MM-dd}) öncedir.");
            if (goal.EndDate.HasValue && goal.EndDate.Value.Date > candidateEnd.Date)
                AddIf(errors, true, "endDate", $"Goal '{goal.GoalTitle}' bitiş tarihi ({goal.EndDate.Value:yyyy-MM-dd}), yeni period bitişinden ({candidateEnd:yyyy-MM-dd}) sonradır.");
        }

        var goalIds = new HashSet<string>(goalList.Select(g => g.Id), StringComparer.OrdinalIgnoreCase);
        var objectives = await _objectives.ListAsync(cancellationToken);
        var objList = objectives
            .Where(o => goalIds.Contains((o.ParentGoalId ?? string.Empty).Trim()))
            .ToList();

        foreach (var obj in objList)
        {
            if (obj.TimeHorizonStart.HasValue && obj.TimeHorizonStart.Value.Date < candidateStart.Date)
                AddIf(errors, true, "startDate", $"Objective '{obj.Name}' başlangıç tarihi, yeni period başlangıcından öncedir.");
            if (obj.TimeHorizonEnd.HasValue && obj.TimeHorizonEnd.Value.Date > candidateEnd.Date)
                AddIf(errors, true, "endDate", $"Objective '{obj.Name}' bitiş tarihi, yeni period bitişinden sonradır.");
        }

        return errors;
    }

    private async Task<Dictionary<string, List<string>>> ValidateArchiveUsageGuardAsync(string strategyPeriodId, CancellationToken cancellationToken)
    {
        var usage = await GetStrategyPeriodUsageCountsAsync(strategyPeriodId, cancellationToken);
        if (usage.Goals == 0 && usage.Objectives == 0)
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        return NewError(
            "inUse",
            $"Strategy Period is currently in use by {usage.Goals} goal(s) and {usage.Objectives} objective(s). Remove assignments before archiving.");
    }

    private async Task<Dictionary<string, List<string>>> ValidatePlanningCycleArchiveUsageGuardAsync(string planningCycleId, CancellationToken cancellationToken)
    {
        var usage = await GetPlanningCycleUsageCountsAsync(planningCycleId, cancellationToken);
        if (usage.Periods == 0 && usage.Goals == 0 && usage.Objectives == 0 && usage.Initiatives == 0)
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        return NewError(
            "inUse",
            $"Planning Cycle is currently in use by {usage.Periods} strategy period(s), {usage.Goals} goal(s), {usage.Objectives} objective(s), and {usage.Initiatives} initiative(s). Remove or re-home downstream assignments before archiving.");
    }

    private async Task<Dictionary<string, List<string>>> ValidatePlanningCycleDateShrinkAsync(
        string planningCycleId,
        DateTime candidateEffectiveFrom,
        DateTime candidateEffectiveTo,
        CancellationToken cancellationToken)
    {
        var periods = (await _strategyPeriods.ListByPlanningCycleIdAsync(planningCycleId, cancellationToken)).ToList();
        if (periods.Count == 0)
            return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var earliestPeriodStart = periods.Min(x => x.StartDate).Date;
        var latestPeriodEnd = periods.Max(x => x.EndDate).Date;
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        AddIf(
            errors,
            candidateEffectiveFrom.Date > earliestPeriodStart,
            "effectiveFrom",
            $"Effective From cannot be moved later than {earliestPeriodStart:yyyy-MM-dd} while linked Strategy Periods exist.");
        AddIf(
            errors,
            candidateEffectiveTo.Date < latestPeriodEnd,
            "effectiveTo",
            $"Effective To cannot be moved earlier than {latestPeriodEnd:yyyy-MM-dd} while linked Strategy Periods exist.");

        return errors;
    }

    private async Task<(int Goals, int Objectives)> GetStrategyPeriodUsageCountsAsync(string strategyPeriodId, CancellationToken cancellationToken)
    {
        var goals = await _goals.ListAsync(cancellationToken);
        var scopedGoals = goals
            .Where(x => string.Equals((x.StrategyPeriodId ?? string.Empty).Trim(), strategyPeriodId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (scopedGoals.Count == 0)
            return (0, 0);

        var goalIds = new HashSet<string>(scopedGoals.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
        var objectives = await _objectives.ListAsync(cancellationToken);
        var objectiveCount = objectives.Count(x => goalIds.Contains((x.ParentGoalId ?? string.Empty).Trim()));

        return (scopedGoals.Count, objectiveCount);
    }

    private async Task<(int Periods, int Goals, int Objectives, int Initiatives)> GetPlanningCycleUsageCountsAsync(string planningCycleId, CancellationToken cancellationToken)
    {
        var periods = (await _strategyPeriods.ListByPlanningCycleIdAsync(planningCycleId, cancellationToken)).ToList();
        if (periods.Count == 0)
            return (0, 0, 0, 0);

        var periodIds = periods.Select(x => x.Id).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var goals = (await _goals.ListAsync(cancellationToken))
            .Where(x => periodIds.Contains((x.StrategyPeriodId ?? string.Empty).Trim()))
            .ToList();
        var goalIds = goals.Select(x => x.Id).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var objectives = (await _objectives.ListAsync(cancellationToken))
            .Where(x => goalIds.Contains((x.ParentGoalId ?? string.Empty).Trim()))
            .ToList();
        var objectiveIds = objectives.Select(x => x.Id).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var initiatives = (await _initiativeLinks.ListAsync(cancellationToken))
            .Where(x =>
                goalIds.Contains((x.ParentGoalId ?? string.Empty).Trim()) ||
                objectiveIds.Contains((x.ParentObjectiveId ?? string.Empty).Trim()))
            .Select(x => string.IsNullOrWhiteSpace(x.InitiativeId) ? x.Id : x.InitiativeId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return (periods.Count, goals.Count, objectives.Count, initiatives);
    }

    private static bool ScopeMatchesOrBothNull(string? a, string? b)
    {
        var av = string.IsNullOrWhiteSpace(a) ? null : a.Trim();
        var bv = string.IsNullOrWhiteSpace(b) ? null : b.Trim();
        if (av is null && bv is null)
            return true;

        return string.Equals(av, bv, StringComparison.OrdinalIgnoreCase);
    }

    private static bool DateRangesOverlap(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) =>
        aStart.Date <= bEnd.Date && bStart.Date <= aEnd.Date;

    private static DateTime NormalizeDateOnlyUtc(DateTime value)
        => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    private static bool IsAllowedTransition(string currentStatus, string nextStatus)
    {
        var current = (currentStatus ?? string.Empty).Trim();
        var next = (nextStatus ?? string.Empty).Trim();
        if (string.Equals(current, next, StringComparison.OrdinalIgnoreCase))
            return true;

        return (string.Equals(current, "Draft", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(next, "Active", StringComparison.OrdinalIgnoreCase)) ||
               (string.Equals(current, "Draft", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(next, "Archived", StringComparison.OrdinalIgnoreCase)) ||
               (string.Equals(current, "Active", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(next, "Archived", StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, List<string>> NewError(string key, string message) =>
        new(StringComparer.OrdinalIgnoreCase) { [key] = new() { message } };

    private static void AddIf(IDictionary<string, List<string>> errors, bool condition, string key, string message)
    {
        if (!condition)
            return;
        if (!errors.TryGetValue(key, out var messages))
        {
            messages = new List<string>();
            errors[key] = messages;
        }

        messages.Add(message);
    }
}
