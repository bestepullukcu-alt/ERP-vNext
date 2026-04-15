using Diten.Application.Common.Models;
using Diten.Application.DeliveryExecutionManagement.Shared;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Adapters.Ppm;
using Diten.Application.EnterpriseStrategy.Mappers;
using Diten.Application.EnterpriseStrategy.Repositories;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Domain.Aggregates.EnterpriseStrategy;

namespace Diten.Application.EnterpriseStrategy.Services;

public interface IInitiativeOrchestrationService
{
    Task<Response<PagedResponseDto<InitiativeStrategyLinkViewDto>>> ListAsync(PagedRequestDto request, CancellationToken cancellationToken = default);
    Task<Response<InitiativeDetailDto>> GetAsync(string initiativeId, CancellationToken cancellationToken = default);
    Task<Response<InitiativeStrategyLinkViewDto>> CreateAsync(InitiativeStrategyLinkViewDto initiative, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<InitiativeStrategyLinkViewDto>> UpdateAsync(string initiativeId, InitiativeStrategyLinkViewDto initiative, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<InitiativeStrategyLinkViewDto>> UpsertStrategyLinkAsync(string initiativeId, InitiativeStrategyLinkViewDto link, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<InitiativeStrategyLinkViewDto>> ChangeStrategyLinkStatusAsync(string initiativeId, string status, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<bool>> DeleteStrategyLinkAsync(string initiativeId, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<SyncResultDto>> SyncAsync(string correlationId, string actor, CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<ProjectStrategyLinkViewDto>>> ProjectsAsync(string initiativeId, CancellationToken cancellationToken = default);
    Task<Response<string>> TraceabilityAsync(string initiativeId, CancellationToken cancellationToken = default);
}

public interface IProjectOrchestrationService
{
    Task<Response<PagedResponseDto<ProjectStrategyLinkViewDto>>> ListAsync(PagedRequestDto request, CancellationToken cancellationToken = default);
    Task<Response<ProjectDetailDto>> GetAsync(string projectId, CancellationToken cancellationToken = default);
    Task<Response<ProjectStrategyLinkViewDto>> CreateAsync(ProjectStrategyLinkViewDto project, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<ProjectStrategyLinkViewDto>> UpdateAsync(string projectId, ProjectStrategyLinkViewDto project, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<ProjectStrategyLinkViewDto>> UpsertStrategyLinkAsync(string projectId, ProjectStrategyLinkViewDto link, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<ProjectStrategyLinkViewDto>> ChangeStrategyLinkStatusAsync(string projectId, string status, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<bool>> DeleteStrategyLinkAsync(string projectId, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<SyncResultDto>> SyncAsync(string correlationId, string actor, CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<ProjectCreationTemplateDto>>> GetCompatibleTemplatesAsync(string parentType, string entityScope, CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<EnterpriseStrategyAuditEventDto>>> GetAuditTrailAsync(string projectId, CancellationToken cancellationToken = default);
    Task<Response<string>> TraceabilityAsync(string projectId, CancellationToken cancellationToken = default);
    Task<Response<string>> UpstreamLineageAsync(string projectId, CancellationToken cancellationToken = default);
    Task<Response<ProjectStrategyLinkViewDto>> CreateStrategyLinkedAsync(
        Commands.EnterpriseStrategyCommands.CreateStrategyLinkedProjectPayloadDto project,
        Commands.EnterpriseStrategyCommands.CreateStrategyLinkedContextDto strategyContext,
        string actor, string correlationId, CancellationToken cancellationToken = default);
}

public sealed class InitiativeOrchestrationService : IInitiativeOrchestrationService
{
    private const string LocalInitiativeSource = "delivery";
    private readonly IPpmInitiativeReadAdapter _ppmInitiatives;
    private readonly IPpmInitiativeCacheRepository _initiativeCache;
    private readonly IInitiativeStrategyLinkRepository _initiativeLinks;
    private readonly IProjectStrategyLinkRepository _projectLinks;
    private readonly IObjectiveRepository _objectives;
    private readonly IGoalRepository _goals;
    private readonly IEnterpriseStrategyAuditSink _audit;

    public InitiativeOrchestrationService(
        IPpmInitiativeReadAdapter ppmInitiatives,
        IPpmInitiativeCacheRepository initiativeCache,
        IInitiativeStrategyLinkRepository initiativeLinks,
        IProjectStrategyLinkRepository projectLinks,
        IObjectiveRepository objectives,
        IGoalRepository goals,
        IEnterpriseStrategyAuditSink audit)
    {
        _ppmInitiatives = ppmInitiatives;
        _initiativeCache = initiativeCache;
        _initiativeLinks = initiativeLinks;
        _projectLinks = projectLinks;
        _objectives = objectives;
        _goals = goals;
        _audit = audit;
    }

    public async Task<Response<PagedResponseDto<InitiativeStrategyLinkViewDto>>> ListAsync(PagedRequestDto request, CancellationToken cancellationToken = default)
    {
        var ppm = await GetPpmInitiativesSafeAsync(cancellationToken);
        var links = await _initiativeLinks.ListAsync(cancellationToken);
        var objectives = (await _objectives.ListAsync(cancellationToken)).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var goals = (await _goals.ListAsync(cancellationToken)).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var ppmById = ppm.ToDictionary(x => x.InitiativeId, StringComparer.OrdinalIgnoreCase);
        var linksById = links.ToDictionary(x => x.InitiativeId, StringComparer.OrdinalIgnoreCase);

        IEnumerable<InitiativeStrategyLinkViewDto> query = ppmById.Keys
            .Union(linksById.Keys, StringComparer.OrdinalIgnoreCase)
            .Select(id =>
            {
                ppmById.TryGetValue(id, out var ppmRow);
                linksById.TryGetValue(id, out var link);
                objectives.TryGetValue(link?.ParentObjectiveId ?? string.Empty, out var objective);
                goals.TryGetValue(link?.ParentGoalId ?? objective?.ParentGoalId ?? string.Empty, out var goal);
                return BuildInitiativeView(ppmRow, link, objective, goal);
            });

        var f = request.Filters;
        if (f.TryGetValue("owner", out var owner)) query = query.Where(x => string.Equals(x.Owner, owner, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("status", out var status)) query = query.Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("type", out var type)) query = query.Where(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("waveOrPhase", out var wave)) query = query.Where(x => string.Equals(x.WaveOrPhase, wave, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("priority", out var priority)) query = query.Where(x => string.Equals(x.Priority, priority, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("complexity", out var complexity)) query = query.Where(x => string.Equals(x.Complexity, complexity, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("parentGoal", out var parentGoal)) query = query.Where(x => string.Equals(x.ParentGoalId, parentGoal, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("parentObjective", out var parentObjective)) query = query.Where(x => string.Equals(x.ParentObjectiveId, parentObjective, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("sponsoringCompany", out var sponsoringCompany)) query = query.Where(x => string.Equals(x.SponsoringCompanyId, sponsoringCompany, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("participatingCompany", out var participatingCompany)) query = query.Where(x => x.ParticipatingCompanyIds.Any(c => string.Equals(c, participatingCompany, StringComparison.OrdinalIgnoreCase)));
        if (f.TryGetValue("initiativeClass", out var initiativeClass)) query = query.Where(x => string.Equals(x.InitiativeClass, initiativeClass, StringComparison.OrdinalIgnoreCase));
        if (f.TryGetValue("scope", out var scope)) query = query.Where(x => x.EntityScope.Contains(scope, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(x =>
                x.InitiativeName.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ||
                x.ParentObjectiveName.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ||
                x.ParentGoalName.Contains(request.Search, StringComparison.OrdinalIgnoreCase));
        }
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 10_000);
        var total = query.Count();
        var rows = query.Skip((page - 1) * size).Take(size).ToList();

        return Response<PagedResponseDto<InitiativeStrategyLinkViewDto>>.Ok(new PagedResponseDto<InitiativeStrategyLinkViewDto>
        {
            Page = page,
            PageSize = size,
            TotalCount = total,
            Items = rows
        });
    }

    public async Task<Response<InitiativeDetailDto>> GetAsync(string initiativeId, CancellationToken cancellationToken = default)
    {
        var initiative = await GetInitiativeAsync(initiativeId, cancellationToken);
        var link = await _initiativeLinks.GetByInitiativeIdAsync(initiativeId, cancellationToken);
        if (initiative is null && link is null)
            return Response<InitiativeDetailDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);

        var projects = await _projectLinks.ListByInitiativeIdAsync(initiativeId, cancellationToken);
        ObjectiveAggregate? objective = null;
        GoalAggregate? goal = null;
        if (!string.IsNullOrWhiteSpace(link?.ParentObjectiveId))
        {
            objective = await _objectives.GetByIdAsync(link.ParentObjectiveId, cancellationToken);
            if (objective is not null && !string.IsNullOrWhiteSpace(objective.ParentGoalId))
                goal = await _goals.GetByIdAsync(objective.ParentGoalId, cancellationToken);
        }

        var linkDto = BuildInitiativeView(initiative, link, objective, goal);
        return Response<InitiativeDetailDto>.Ok(new InitiativeDetailDto
        {
            Initiative = BuildInitiativeRuntimeModel(initiative, linkDto),
            StrategyLink = linkDto,
            ParentObjective = objective?.ToDto(),
            ParentGoal = goal?.ToDto(),
            Readiness = linkDto.Readiness ?? new InitiativeReadinessDto(),
            Projects = projects.Select(x => x.ToViewDto()).ToList(),
            TraceabilitySummary = $"goal={linkDto.ParentGoalId ?? "-"}; objective={linkDto.ParentObjectiveId ?? "-"}; projects={projects.Count}"
        });
    }

    public async Task<Response<InitiativeStrategyLinkViewDto>> CreateAsync(InitiativeStrategyLinkViewDto initiative, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var initiativeId = string.IsNullOrWhiteSpace(initiative.InitiativeId) ? $"init-{Guid.NewGuid():N}"[..15] : initiative.InitiativeId.Trim();
        var existing = await _initiativeLinks.GetByInitiativeIdAsync(initiativeId, cancellationToken);
        if (existing is not null)
            return Response<InitiativeStrategyLinkViewDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, new() { ["initiativeId"] = new() { "Initiative id already exists." } });

        return await SaveInitiativeAsync(initiativeId, initiative, null, actor, correlationId, cancellationToken);
    }

    public Task<Response<InitiativeStrategyLinkViewDto>> UpdateAsync(string initiativeId, InitiativeStrategyLinkViewDto initiative, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default) =>
        SaveInitiativeAsync(initiativeId, initiative, expectedVersion, actor, correlationId, cancellationToken);

    public async Task<Response<InitiativeStrategyLinkViewDto>> UpsertStrategyLinkAsync(string initiativeId, InitiativeStrategyLinkViewDto link, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var ppm = await GetInitiativeAsync(initiativeId, cancellationToken);
        var existing = await _initiativeLinks.GetByInitiativeIdAsync(initiativeId, cancellationToken);
        if (ppm is null && existing is null)
            return Response<InitiativeStrategyLinkViewDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["initiativeId"] = new() { "Initiative must exist in Delivery or PPM/cache." } });

        if (string.IsNullOrWhiteSpace(link.ParentObjectiveId) && existing is not null)
            link.ParentObjectiveId = existing.ParentObjectiveId;
        if (string.Equals(link.StrategyLinkStatus, "Linked", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(link.ParentObjectiveId))
            return Response<InitiativeStrategyLinkViewDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["parentObjectiveId"] = new() { "Parent objective is required for Linked status." } });

        var objective = await _objectives.GetByIdAsync(link.ParentObjectiveId, cancellationToken);
        if (objective is null)
            return Response<InitiativeStrategyLinkViewDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["parentObjectiveId"] = new() { "Parent objective must exist." } });
        if (string.IsNullOrWhiteSpace(link.SponsoringCompanyId))
            return Response<InitiativeStrategyLinkViewDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["sponsoringCompanyId"] = new() { "Sponsoring company is required." } });

        if (existing is not null && EnterpriseStrategyResult.IsStaleWrite(expectedVersion, existing.Version))
            return EnterpriseStrategyResult.StaleVersion<InitiativeStrategyLinkViewDto>();

        var objectiveGoalId = objective.ParentGoalId;
        var aggregate = new InitiativeStrategyLinkAggregate
        {
            Id = existing?.Id ?? Guid.NewGuid().ToString("N"),
            InitiativeId = initiativeId,
            InitiativeName = !string.IsNullOrWhiteSpace(link.InitiativeName) ? link.InitiativeName : existing?.InitiativeName ?? ppm?.InitiativeName ?? string.Empty,
            Description = !string.IsNullOrWhiteSpace(link.Description) ? link.Description : existing?.Description ?? ppm?.Description ?? string.Empty,
            Owner = !string.IsNullOrWhiteSpace(link.Owner) ? link.Owner : existing?.Owner ?? ppm?.Owner ?? string.Empty,
            DeliveryOwnerCompanyId = !string.IsNullOrWhiteSpace(link.DeliveryOwnerCompanyId) ? link.DeliveryOwnerCompanyId : existing?.DeliveryOwnerCompanyId ?? string.Empty,
            DeliveryOwnerPositionId = !string.IsNullOrWhiteSpace(link.DeliveryOwnerPositionId) ? link.DeliveryOwnerPositionId : existing?.DeliveryOwnerPositionId ?? string.Empty,
            DeliveryOwnerPersonId = !string.IsNullOrWhiteSpace(link.DeliveryOwnerPersonId) ? link.DeliveryOwnerPersonId : existing?.DeliveryOwnerPersonId ?? string.Empty,
            ExecutiveSponsor = !string.IsNullOrWhiteSpace(link.ExecutiveSponsor) ? link.ExecutiveSponsor : existing?.ExecutiveSponsor ?? string.Empty,
            Status = !string.IsNullOrWhiteSpace(link.Status) ? link.Status : existing?.Status ?? ppm?.Status ?? "Draft",
            Type = !string.IsNullOrWhiteSpace(link.Type) ? link.Type : existing?.Type ?? ppm?.Type ?? string.Empty,
            StartDate = link.StartDate ?? existing?.StartDate ?? ppm?.StartDate,
            EndDate = link.EndDate ?? existing?.EndDate ?? ppm?.EndDate,
            WaveOrPhase = !string.IsNullOrWhiteSpace(link.WaveOrPhase) ? link.WaveOrPhase : existing?.WaveOrPhase ?? ppm?.WaveOrPhase ?? string.Empty,
            Priority = !string.IsNullOrWhiteSpace(link.Priority) ? link.Priority : existing?.Priority ?? ppm?.Priority ?? string.Empty,
            Complexity = !string.IsNullOrWhiteSpace(link.Complexity) ? link.Complexity : existing?.Complexity ?? ppm?.Complexity ?? string.Empty,
            PrimaryKpi = !string.IsNullOrWhiteSpace(link.PrimaryKpi) ? link.PrimaryKpi : existing?.PrimaryKpi ?? ppm?.PrimaryKpi ?? string.Empty,
            ReportingFrequency = !string.IsNullOrWhiteSpace(link.ReportingFrequency) ? link.ReportingFrequency : existing?.ReportingFrequency ?? string.Empty,
            ContributionMetricName = !string.IsNullOrWhiteSpace(link.ContributionMetricName) ? link.ContributionMetricName : existing?.ContributionMetricName ?? string.Empty,
            ContributionUnitOfMeasure = !string.IsNullOrWhiteSpace(link.ContributionUnitOfMeasure) ? link.ContributionUnitOfMeasure : existing?.ContributionUnitOfMeasure ?? string.Empty,
            ContributionPlanGranularity = !string.IsNullOrWhiteSpace(link.ContributionPlanGranularity) ? link.ContributionPlanGranularity : existing?.ContributionPlanGranularity ?? "InheritFromObjective",
            ContributionTiming = !string.IsNullOrWhiteSpace(link.ContributionTiming) ? link.ContributionTiming : existing?.ContributionTiming ?? string.Empty,
            BenefitHypothesis = !string.IsNullOrWhiteSpace(link.BenefitHypothesis) ? link.BenefitHypothesis : existing?.BenefitHypothesis ?? string.Empty,
            SourceSystem = !string.IsNullOrWhiteSpace(existing?.SourceSystem) ? existing.SourceSystem : ppm?.SourceSystem ?? LocalInitiativeSource,
            SourceRecordId = !string.IsNullOrWhiteSpace(existing?.SourceRecordId) ? existing.SourceRecordId : ppm?.InitiativeId ?? initiativeId,
            ParentObjectiveId = link.ParentObjectiveId,
            ParentGoalId = objectiveGoalId,
            StrategyLinkStatus = string.IsNullOrWhiteSpace(link.StrategyLinkStatus) ? existing?.StrategyLinkStatus ?? "Linked" : link.StrategyLinkStatus,
            ContributionType = link.ContributionType,
            ContributionWeight = link.ContributionWeight,
            MetricBindingsJson = string.IsNullOrWhiteSpace(link.MetricBindingsJson) ? "[]" : link.MetricBindingsJson,
            DecisionReference = link.DecisionReference,
            EvidenceReference = link.EvidenceReference,
            SponsoringCompanyId = link.SponsoringCompanyId,
            ParticipatingCompanyIds = link.ParticipatingCompanyIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new(),
            EntityScope = !string.IsNullOrWhiteSpace(link.EntityScope) ? link.EntityScope : existing?.EntityScope ?? string.Empty,
            InitiativeClass = !string.IsNullOrWhiteSpace(link.InitiativeClass) ? link.InitiativeClass : existing?.InitiativeClass ?? string.Empty,
            BudgetEnvelope = !string.IsNullOrWhiteSpace(link.BudgetEnvelope) ? link.BudgetEnvelope : existing?.BudgetEnvelope ?? ppm?.BudgetEnvelope ?? string.Empty,
            BudgetAmount = link.BudgetAmount ?? existing?.BudgetAmount,
            CurrencyCode = !string.IsNullOrWhiteSpace(link.CurrencyCode) ? link.CurrencyCode : existing?.CurrencyCode ?? string.Empty,
            GovernanceStage = !string.IsNullOrWhiteSpace(link.GovernanceStage) ? link.GovernanceStage : existing?.GovernanceStage ?? string.Empty,
            GovernanceNotes = !string.IsNullOrWhiteSpace(link.GovernanceNotes) ? link.GovernanceNotes : existing?.GovernanceNotes ?? string.Empty,
            ContributionPlanValues = link.ContributionPlanValues?.Select(ToAggregate).ToList() ?? existing?.ContributionPlanValues ?? new(),
            Notes = link.Notes,
            Version = existing is null ? 1 : existing.Version + 1,
            SyncedAt = DateTime.UtcNow,
            CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = existing?.CreatedBy ?? actor,
            UpdatedBy = actor,
            SourceTemplateType = link.SourceTemplateType,
            SourceTemplateId = link.SourceTemplateId,
            SourceTemplateVersion = link.SourceTemplateVersion,
            SourceBlueprintPackId = link.SourceBlueprintPackId,
            InstantiationBatchId = link.InstantiationBatchId,
            CreatedFromLibrary = link.CreatedFromLibrary
        };

        await _initiativeLinks.AddOrUpdateAsync(aggregate, cancellationToken);
        await _audit.WriteMutationAsync(
            actor,
            "InitiativeStrategyLink",
            aggregate.Id,
            existing is null ? EnterpriseStrategyEventNames.InitiativeStrategyLinked : EnterpriseStrategyEventNames.InitiativeStrategyUpdated,
            correlationId,
            DeliveryExecutionManagementModules.Initiatives,
            existing?.ParentObjectiveId ?? string.Empty,
            aggregate.ParentObjectiveId,
            cancellationToken);

        var goal = await _goals.GetByIdAsync(objective.ParentGoalId, cancellationToken);
        var output = BuildInitiativeView(ppm, aggregate, objective, goal);
        return Response<InitiativeStrategyLinkViewDto>.Ok(output);
    }

    public async Task<Response<InitiativeStrategyLinkViewDto>> ChangeStrategyLinkStatusAsync(string initiativeId, string status, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var existing = await _initiativeLinks.GetByInitiativeIdAsync(initiativeId, cancellationToken);
        if (existing is null)
            return Response<InitiativeStrategyLinkViewDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (EnterpriseStrategyResult.IsStaleWrite(expectedVersion, existing.Version))
            return EnterpriseStrategyResult.StaleVersion<InitiativeStrategyLinkViewDto>();

        existing.StrategyLinkStatus = status;
        existing.Version++;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = actor;
        await _initiativeLinks.AddOrUpdateAsync(existing, cancellationToken);
        await _audit.WriteMutationAsync(actor, "InitiativeStrategyLink", existing.Id, EnterpriseStrategyEventNames.InitiativeStrategyUpdated, correlationId, DeliveryExecutionManagementModules.Initiatives, "", status, cancellationToken);

        var ppm = await GetInitiativeAsync(initiativeId, cancellationToken);
        var objective = await _objectives.GetByIdAsync(existing.ParentObjectiveId, cancellationToken);
        var goal = objective is null ? null : await _goals.GetByIdAsync(objective.ParentGoalId, cancellationToken);
        var dto = BuildInitiativeView(ppm, existing, objective, goal);
        return Response<InitiativeStrategyLinkViewDto>.Ok(dto);
    }

    public async Task<Response<bool>> DeleteStrategyLinkAsync(string initiativeId, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var existing = await _initiativeLinks.GetByInitiativeIdAsync(initiativeId, cancellationToken);
        await _initiativeLinks.DeleteByInitiativeIdAsync(initiativeId, cancellationToken);
        if (existing is not null)
        {
            await _audit.WriteMutationAsync(actor, "InitiativeStrategyLink", existing.Id, EnterpriseStrategyEventNames.InitiativeStrategyUnlinked, correlationId, DeliveryExecutionManagementModules.Initiatives, existing.ParentObjectiveId, "Unlinked", cancellationToken);
        }
        return Response<bool>.Ok(true);
    }

    public async Task<Response<SyncResultDto>> SyncAsync(string correlationId, string actor, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await _ppmInitiatives.SyncAsync(correlationId, cancellationToken);
            await _initiativeCache.UpsertManyAsync(rows.Select(x => new PpmInitiativeReadModelAggregate
            {
                InitiativeId = x.InitiativeId,
                InitiativeName = x.InitiativeName,
                Description = x.Description,
                Owner = x.Owner,
                Status = x.Status,
                Type = x.Type,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                WaveOrPhase = x.WaveOrPhase,
                Priority = x.Priority,
                Complexity = x.Complexity,
                PrimaryKpi = x.PrimaryKpi,
                BudgetEnvelope = x.BudgetEnvelope,
                Maturity = x.Maturity,
                SourceSystem = x.SourceSystem,
                SourceUpdatedAt = x.SourceUpdatedAt,
                CachedAt = DateTime.UtcNow
            }).ToList(), cancellationToken);

            await _audit.WriteMutationAsync(actor, "InitiativeSync", "ppm-initiatives", EnterpriseStrategyEventNames.InitiativeSyncCompleted, correlationId, DeliveryExecutionManagementModules.Initiatives, "", rows.Count.ToString(), cancellationToken);

            return Response<SyncResultDto>.Ok(new SyncResultDto
            {
                CorrelationId = correlationId,
                ImportedCount = rows.Count,
                DegradedMode = false,
                EventName = EnterpriseStrategyEventNames.InitiativeSyncCompleted
            });
        }
        catch (Exception ex)
        {
            await _audit.WriteMutationAsync(actor, "InitiativeSync", "ppm-initiatives", EnterpriseStrategyEventNames.InitiativeSyncFailed, correlationId, DeliveryExecutionManagementModules.Initiatives, "", ex.Message, cancellationToken);
            return Response<SyncResultDto>.Fail(EnterpriseStrategyErrorCodes.DependencyUnavailable, new() { ["ppm"] = new() { ex.Message } });
        }
    }

    public async Task<Response<IReadOnlyList<ProjectStrategyLinkViewDto>>> ProjectsAsync(string initiativeId, CancellationToken cancellationToken = default)
    {
        var rows = await _projectLinks.ListByInitiativeIdAsync(initiativeId, cancellationToken);
        return Response<IReadOnlyList<ProjectStrategyLinkViewDto>>.Ok(rows.Select(x => x.ToViewDto()).ToList());
    }

    public async Task<Response<string>> TraceabilityAsync(string initiativeId, CancellationToken cancellationToken = default)
    {
        var link = await _initiativeLinks.GetByInitiativeIdAsync(initiativeId, cancellationToken);
        if (link is null)
            return Response<string>.Ok("Unlinked");
        return Response<string>.Ok($"Goal {link.ParentGoalId} -> Objective {link.ParentObjectiveId} -> Initiative {initiativeId}");
    }

    private async Task<Response<InitiativeStrategyLinkViewDto>> SaveInitiativeAsync(
        string initiativeId,
        InitiativeStrategyLinkViewDto initiative,
        int? expectedVersion,
        string actor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var existing = await _initiativeLinks.GetByInitiativeIdAsync(initiativeId, cancellationToken);
        if (expectedVersion.HasValue && existing is not null && EnterpriseStrategyResult.IsStaleWrite(expectedVersion.Value, existing.Version))
            return EnterpriseStrategyResult.StaleVersion<InitiativeStrategyLinkViewDto>();

        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(initiative.ParentObjectiveId))
            errors["parentObjectiveId"] = new() { "Parent objective is required." };
        if (string.IsNullOrWhiteSpace(initiative.InitiativeName))
            errors["initiativeName"] = new() { "Initiative name is required." };
        if (errors.Count > 0)
            return Response<InitiativeStrategyLinkViewDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, errors);

        var objective = await _objectives.GetByIdAsync(initiative.ParentObjectiveId, cancellationToken);
        if (objective is null)
            return Response<InitiativeStrategyLinkViewDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["parentObjectiveId"] = new() { "Parent objective must exist." } });

        var goal = await _goals.GetByIdAsync(objective.ParentGoalId, cancellationToken);
        var aggregate = BuildAggregateForSave(existing, initiativeId, initiative, objective, actor);
        var validationErrors = ValidateInitiativeAggregate(aggregate, objective, goal);
        if (validationErrors.Count > 0)
            return Response<InitiativeStrategyLinkViewDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, validationErrors);

        await _initiativeLinks.AddOrUpdateAsync(aggregate, cancellationToken);
        await _audit.WriteMutationAsync(
            actor,
            "InitiativeStrategyLink",
            aggregate.Id,
            existing is null ? EnterpriseStrategyEventNames.InitiativeStrategyLinked : EnterpriseStrategyEventNames.InitiativeStrategyUpdated,
            correlationId,
            DeliveryExecutionManagementModules.Initiatives,
            existing?.ParentObjectiveId ?? string.Empty,
            aggregate.ParentObjectiveId,
            cancellationToken);

        var ppm = await GetInitiativeAsync(initiativeId, cancellationToken);
        return Response<InitiativeStrategyLinkViewDto>.Ok(BuildInitiativeView(ppm, aggregate, objective, goal));
    }

    private async Task<List<PpmInitiativeReadModelDto>> GetPpmInitiativesSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _ppmInitiatives.ListAsync(1, 1000, cancellationToken);
            await _initiativeCache.UpsertManyAsync(rows.Select(x => new PpmInitiativeReadModelAggregate
            {
                InitiativeId = x.InitiativeId,
                InitiativeName = x.InitiativeName,
                Description = x.Description,
                Owner = x.Owner,
                Status = x.Status,
                Type = x.Type,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                WaveOrPhase = x.WaveOrPhase,
                Priority = x.Priority,
                Complexity = x.Complexity,
                PrimaryKpi = x.PrimaryKpi,
                BudgetEnvelope = x.BudgetEnvelope,
                Maturity = x.Maturity,
                SourceSystem = x.SourceSystem,
                SourceUpdatedAt = x.SourceUpdatedAt,
                CachedAt = DateTime.UtcNow
            }).ToList(), cancellationToken);

            return rows.Select(x => new PpmInitiativeReadModelDto
            {
                InitiativeId = x.InitiativeId,
                InitiativeName = x.InitiativeName,
                Description = x.Description,
                Owner = x.Owner,
                Status = x.Status,
                Type = x.Type,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                WaveOrPhase = x.WaveOrPhase,
                Priority = x.Priority,
                Complexity = x.Complexity,
                PrimaryKpi = x.PrimaryKpi,
                BudgetEnvelope = x.BudgetEnvelope,
                Maturity = x.Maturity,
                SourceSystem = x.SourceSystem,
                SourceUpdatedAt = x.SourceUpdatedAt,
                CachedAt = DateTime.UtcNow
            }).ToList();
        }
        catch
        {
            var cached = await _initiativeCache.ListAsync(cancellationToken);
            return cached.Select(x => new PpmInitiativeReadModelDto
            {
                InitiativeId = x.InitiativeId,
                InitiativeName = x.InitiativeName,
                Description = x.Description,
                Owner = x.Owner,
                Status = x.Status,
                Type = x.Type,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                WaveOrPhase = x.WaveOrPhase,
                Priority = x.Priority,
                Complexity = x.Complexity,
                PrimaryKpi = x.PrimaryKpi,
                BudgetEnvelope = x.BudgetEnvelope,
                Maturity = x.Maturity,
                SourceSystem = x.SourceSystem,
                SourceUpdatedAt = x.SourceUpdatedAt,
                CachedAt = x.CachedAt,
                DegradedMode = true
            }).ToList();
        }
    }

    private async Task<PpmInitiativeReadModelDto?> GetInitiativeAsync(string initiativeId, CancellationToken cancellationToken)
    {
        var all = await GetPpmInitiativesSafeAsync(cancellationToken);
        return all.FirstOrDefault(x => string.Equals(x.InitiativeId, initiativeId, StringComparison.OrdinalIgnoreCase));
    }

    private static InitiativeStrategyLinkAggregate BuildAggregateForSave(
        InitiativeStrategyLinkAggregate? existing,
        string initiativeId,
        InitiativeStrategyLinkViewDto initiative,
        ObjectiveAggregate objective,
        string actor)
    {
        return new InitiativeStrategyLinkAggregate
        {
            Id = existing?.Id ?? Guid.NewGuid().ToString("N"),
            InitiativeId = initiativeId,
            InitiativeName = initiative.InitiativeName.Trim(),
            Description = (initiative.Description ?? string.Empty).Trim(),
            Owner = (initiative.Owner ?? string.Empty).Trim(),
            DeliveryOwnerCompanyId = (initiative.DeliveryOwnerCompanyId ?? string.Empty).Trim(),
            DeliveryOwnerPositionId = (initiative.DeliveryOwnerPositionId ?? string.Empty).Trim(),
            DeliveryOwnerPersonId = (initiative.DeliveryOwnerPersonId ?? string.Empty).Trim(),
            ExecutiveSponsor = (initiative.ExecutiveSponsor ?? string.Empty).Trim(),
            AccountableSponsorRole = (initiative.AccountableSponsorRole ?? string.Empty).Trim(),
            Status = string.IsNullOrWhiteSpace(initiative.Status) ? existing?.Status ?? "Draft" : initiative.Status.Trim(),
            Type = (initiative.Type ?? string.Empty).Trim(),
            NormalizedType = NormalizeText(initiative.NormalizedType, initiative.Type),
            StartDate = initiative.StartDate,
            EndDate = initiative.EndDate,
            WaveOrPhase = (initiative.WaveOrPhase ?? string.Empty).Trim(),
            Priority = (initiative.Priority ?? string.Empty).Trim(),
            Complexity = (initiative.Complexity ?? string.Empty).Trim(),
            Maturity = (initiative.Maturity ?? string.Empty).Trim(),
            PrimaryKpi = (initiative.PrimaryKpi ?? string.Empty).Trim(),
            ReportingFrequency = (initiative.ReportingFrequency ?? string.Empty).Trim(),
            ContributionMetricName = (initiative.ContributionMetricName ?? string.Empty).Trim(),
            ContributionUnitOfMeasure = (initiative.ContributionUnitOfMeasure ?? string.Empty).Trim(),
            ContributionPlanGranularity = NormalizeInitiativeGranularity(initiative.ContributionPlanGranularity),
            ContributionMethod = (initiative.ContributionMethod ?? string.Empty).Trim(),
            ContributionTiming = (initiative.ContributionTiming ?? string.Empty).Trim(),
            BenefitHypothesis = (initiative.BenefitHypothesis ?? string.Empty).Trim(),
            BenefitRealizationStart = initiative.BenefitRealizationStart,
            BenefitRealizationEnd = initiative.BenefitRealizationEnd,
            SourceSystem = string.IsNullOrWhiteSpace(existing?.SourceSystem) ? LocalInitiativeSource : existing.SourceSystem,
            SourceRecordId = string.IsNullOrWhiteSpace(existing?.SourceRecordId) ? initiativeId : existing.SourceRecordId,
            ParentObjectiveId = objective.Id,
            ParentGoalId = objective.ParentGoalId,
            StrategyLinkStatus = string.IsNullOrWhiteSpace(initiative.StrategyLinkStatus) ? "Linked" : initiative.StrategyLinkStatus.Trim(),
            ContributionType = string.IsNullOrWhiteSpace(initiative.ContributionType) ? existing?.ContributionType ?? "Supports" : initiative.ContributionType.Trim(),
            ContributionWeight = initiative.ContributionWeight,
            MetricBindingsJson = string.IsNullOrWhiteSpace(initiative.MetricBindingsJson) ? existing?.MetricBindingsJson ?? "[]" : initiative.MetricBindingsJson,
            DecisionReference = initiative.DecisionReference,
            EvidenceReference = initiative.EvidenceReference,
            SponsoringCompanyId = (initiative.SponsoringCompanyId ?? string.Empty).Trim(),
            ParticipatingCompanyIds = initiative.ParticipatingCompanyIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new(),
            EntityScope = (initiative.EntityScope ?? string.Empty).Trim(),
            InitiativeClass = (initiative.InitiativeClass ?? string.Empty).Trim(),
            BudgetEnvelope = (initiative.BudgetEnvelope ?? string.Empty).Trim(),
            BudgetAmount = initiative.BudgetAmount,
            CurrencyCode = (initiative.CurrencyCode ?? string.Empty).Trim(),
            FundingSource = (initiative.FundingSource ?? string.Empty).Trim(),
            StrategyAlignmentNote = (initiative.StrategyAlignmentNote ?? string.Empty).Trim(),
            GovernanceStage = (initiative.GovernanceStage ?? string.Empty).Trim(),
            GovernanceNotes = (initiative.GovernanceNotes ?? string.Empty).Trim(),
            DependencyFlag = initiative.DependencyFlag,
            ContributionPlanValues = initiative.ContributionPlanValues?.Select(ToAggregate).ToList() ?? new(),
            Notes = (initiative.Notes ?? string.Empty).Trim(),
            Version = existing is null ? 1 : existing.Version + 1,
            SyncedAt = existing?.SyncedAt,
            CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = existing?.CreatedBy ?? actor,
            UpdatedBy = actor,
            SourceTemplateType = initiative.SourceTemplateType,
            SourceTemplateId = initiative.SourceTemplateId,
            SourceTemplateVersion = initiative.SourceTemplateVersion,
            SourceBlueprintPackId = initiative.SourceBlueprintPackId,
            InstantiationBatchId = initiative.InstantiationBatchId,
            CreatedFromLibrary = initiative.CreatedFromLibrary
        };
    }

    private static Dictionary<string, List<string>> ValidateInitiativeAggregate(
        InitiativeStrategyLinkAggregate aggregate,
        ObjectiveAggregate objective,
        GoalAggregate? goal)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(aggregate.ParentObjectiveId))
            AddValidationError(errors, "parentObjectiveId", "Parent objective is required.");
        if (string.IsNullOrWhiteSpace(aggregate.InitiativeName))
            AddValidationError(errors, "initiativeName", "Initiative name is required.");
        if (string.IsNullOrWhiteSpace(aggregate.Type))
            AddValidationError(errors, "type", "Initiative type is required.");
        if (string.IsNullOrWhiteSpace(aggregate.DeliveryOwnerPersonId) && string.IsNullOrWhiteSpace(aggregate.Owner))
            AddValidationError(errors, "deliveryOwnerPersonId", "Initiative owner is required.");
        if (string.IsNullOrWhiteSpace(aggregate.SponsoringCompanyId))
            AddValidationError(errors, "sponsoringCompanyId", "Sponsoring company is required.");
        if (!aggregate.StartDate.HasValue)
            AddValidationError(errors, "startDate", "Start period is required.");
        if (!aggregate.EndDate.HasValue)
            AddValidationError(errors, "endDate", "End period is required.");

        if (aggregate.StartDate.HasValue && aggregate.EndDate.HasValue && aggregate.EndDate.Value.Date < aggregate.StartDate.Value.Date)
            AddValidationError(errors, "endDate", "End date must be on or after start date.");

        if (aggregate.StartDate.HasValue && objective.TimeHorizonStart.HasValue && aggregate.StartDate.Value.Date < objective.TimeHorizonStart.Value.Date)
            AddValidationError(errors, "startDate", "Initiative start date must sit inside the parent objective horizon.");
        if (aggregate.EndDate.HasValue && objective.TimeHorizonEnd.HasValue && aggregate.EndDate.Value.Date > objective.TimeHorizonEnd.Value.Date)
            AddValidationError(errors, "endDate", "Initiative end date must sit inside the parent objective horizon.");

        if (goal is not null)
        {
            if (aggregate.StartDate.HasValue && goal.StartDate.HasValue && aggregate.StartDate.Value.Date < goal.StartDate.Value.Date)
                AddValidationError(errors, "startDate", "Initiative start date must sit inside the parent strategy period.");

            if (aggregate.EndDate.HasValue && goal.EndDate.HasValue && aggregate.EndDate.Value.Date > goal.EndDate.Value.Date)
                AddValidationError(errors, "endDate", "Initiative end date must sit inside the parent strategy period.");
        }

        var objectiveGranularity = NormalizeGranularityForComparison(objective.TargetPlanGranularity);
        var initiativeGranularity = ResolveEffectiveInitiativeGranularity(aggregate.ContributionPlanGranularity, objective.TargetPlanGranularity);
        if (GranularityRank(initiativeGranularity) > GranularityRank(objectiveGranularity))
            errors["contributionPlanGranularity"] = new() { "Contribution plan granularity cannot be coarser than the parent objective target granularity." };

        if (aggregate.BenefitRealizationStart.HasValue && aggregate.BenefitRealizationEnd.HasValue &&
            aggregate.BenefitRealizationEnd.Value.Date < aggregate.BenefitRealizationStart.Value.Date)
            AddValidationError(errors, "benefitRealizationEnd", "Benefit realization end must be on or after benefit realization start.");

        if (aggregate.BenefitRealizationStart.HasValue && aggregate.StartDate.HasValue && aggregate.BenefitRealizationStart.Value.Date < aggregate.StartDate.Value.Date)
            AddValidationError(errors, "benefitRealizationStart", "Benefit realization start must sit inside the initiative period.");
        if (aggregate.BenefitRealizationEnd.HasValue && aggregate.EndDate.HasValue && aggregate.BenefitRealizationEnd.Value.Date > aggregate.EndDate.Value.Date)
            AddValidationError(errors, "benefitRealizationEnd", "Benefit realization end must sit inside the initiative period.");

        var permittedPeriods = BuildInitiativePeriods(aggregate, objective, goal, initiativeGranularity);
        var periodKeys = permittedPeriods.Select(x => x.PeriodKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in aggregate.ContributionPlanValues ?? new())
        {
            if (periodKeys.Count > 0 && !periodKeys.Contains(row.PeriodKey))
                AddValidationError(errors, "contributionPlanValues", $"Contribution row {row.PeriodLabel} falls outside the allowed initiative/objective/strategy horizon.");
            if (row.PlannedValue is null)
                AddValidationError(errors, "contributionPlanValues", $"Contribution row {row.PeriodLabel} is missing a planned value.");
        }

        return errors;
    }

    private static void AddValidationError(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var bucket))
        {
            bucket = new List<string>();
            errors[key] = bucket;
        }

        if (!bucket.Contains(message, StringComparer.OrdinalIgnoreCase))
            bucket.Add(message);
    }

    private static InitiativeStrategyLinkViewDto BuildInitiativeView(
        PpmInitiativeReadModelDto? ppm,
        InitiativeStrategyLinkAggregate? link,
        ObjectiveAggregate? objective,
        GoalAggregate? goal)
    {
        var view = link?.ToViewDto() ?? new InitiativeStrategyLinkViewDto
        {
            InitiativeId = ppm?.InitiativeId ?? string.Empty,
            SourceSystem = ppm?.SourceSystem ?? LocalInitiativeSource,
            SourceRecordId = ppm?.InitiativeId ?? string.Empty,
            StrategyLinkStatus = "Unlinked",
            SyncFreshness = ppm?.DegradedMode == true ? "Degraded" : "Fresh",
            ContributionPlanGranularity = "InheritFromObjective"
        };

        view.InitiativeName = FirstText(view.InitiativeName, ppm?.InitiativeName);
        view.Description = FirstText(view.Description, ppm?.Description);
        view.Owner = FirstText(view.Owner, ppm?.Owner);
        view.Status = FirstText(view.Status, ppm?.Status, "Draft");
        view.Type = FirstText(view.Type, ppm?.Type);
        view.NormalizedType = NormalizeText(view.NormalizedType, view.Type);
        view.WaveOrPhase = FirstText(view.WaveOrPhase, ppm?.WaveOrPhase);
        view.Priority = FirstText(view.Priority, ppm?.Priority);
        view.Complexity = FirstText(view.Complexity, ppm?.Complexity);
        view.Maturity = FirstText(view.Maturity, ppm?.Maturity);
        view.PrimaryKpi = FirstText(view.PrimaryKpi, ppm?.PrimaryKpi);
        view.StartDate ??= ppm?.StartDate;
        view.EndDate ??= ppm?.EndDate;
        view.BudgetEnvelope = FirstText(view.BudgetEnvelope, ppm?.BudgetEnvelope);
        view.EntityScope = FirstText(view.EntityScope, objective?.EntityScope, goal?.EntityScope);
        view.ParentObjectiveName = objective?.Name ?? string.Empty;
        view.ParentGoalName = goal?.Name ?? string.Empty;
        view.ObjectiveTargetGranularity = objective?.TargetPlanGranularity ?? string.Empty;
        view.SyncFreshness = ppm?.DegradedMode == true ? "Degraded" : view.SyncFreshness;
        view.Warnings = BuildInitiativeWarnings(ppm, link, objective, goal);
        view.Readiness = EvaluateReadiness(link, objective, goal);
        view.ReadinessStatus = view.Readiness.ReadinessStatus;
        return view;
    }

    private static PpmInitiativeReadModelDto BuildInitiativeRuntimeModel(PpmInitiativeReadModelDto? ppm, InitiativeStrategyLinkViewDto merged) => new()
    {
        InitiativeId = merged.InitiativeId,
        InitiativeName = merged.InitiativeName,
        Description = merged.Description,
        Owner = merged.Owner,
        Status = merged.Status,
        Type = merged.Type,
        StartDate = merged.StartDate,
        EndDate = merged.EndDate,
        WaveOrPhase = merged.WaveOrPhase,
        Priority = merged.Priority,
        Complexity = merged.Complexity,
        PrimaryKpi = merged.PrimaryKpi,
        BudgetEnvelope = merged.BudgetEnvelope,
        Maturity = FirstText(merged.Maturity, ppm?.Maturity, merged.ReadinessStatus),
        SourceSystem = merged.SourceSystem,
        SourceUpdatedAt = ppm?.SourceUpdatedAt,
        CachedAt = ppm?.CachedAt,
        DegradedMode = ppm?.DegradedMode ?? false
    };

    private static InitiativeReadinessDto EvaluateReadiness(
        InitiativeStrategyLinkAggregate? link,
        ObjectiveAggregate? objective,
        GoalAggregate? goal)
    {
        if (link is null)
        {
            return new InitiativeReadinessDto
            {
                DraftReady = false,
                PlanningReady = false,
                PublishReady = false,
                ReadinessStatus = "Blocked",
                Missing = new[] { "Save the initiative record in Delivery to establish a planning baseline." }
            };
        }

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(link.InitiativeName)) missing.Add("Initiative Name");
        if (string.IsNullOrWhiteSpace(link.ParentObjectiveId)) missing.Add("Parent Objective");
        if (string.IsNullOrWhiteSpace(link.Type)) missing.Add("Initiative Type");
        if (string.IsNullOrWhiteSpace(link.DeliveryOwnerPersonId) && string.IsNullOrWhiteSpace(link.Owner)) missing.Add("Initiative Owner");
        if (string.IsNullOrWhiteSpace(link.SponsoringCompanyId)) missing.Add("Sponsoring Company");
        if (!link.StartDate.HasValue) missing.Add("Start Period");
        if (!link.EndDate.HasValue) missing.Add("End Period");

        var blockers = new List<string>();
        if (link.StartDate.HasValue && link.EndDate.HasValue && link.EndDate.Value.Date < link.StartDate.Value.Date)
            blockers.Add("End Date must be on or after Start Date.");
        if (objective is not null)
        {
            if (link.StartDate.HasValue && objective.TimeHorizonStart.HasValue && link.StartDate.Value.Date < objective.TimeHorizonStart.Value.Date)
                blockers.Add("Initiative Start Date sits before the parent Objective horizon.");
            if (link.EndDate.HasValue && objective.TimeHorizonEnd.HasValue && link.EndDate.Value.Date > objective.TimeHorizonEnd.Value.Date)
                blockers.Add("Initiative End Date sits after the parent Objective horizon.");
        }
        if (goal is not null)
        {
            if (link.StartDate.HasValue && goal.StartDate.HasValue && link.StartDate.Value.Date < goal.StartDate.Value.Date)
                blockers.Add("Initiative Start Date sits before the parent Strategy Period.");
            if (link.EndDate.HasValue && goal.EndDate.HasValue && link.EndDate.Value.Date > goal.EndDate.Value.Date)
                blockers.Add("Initiative End Date sits after the parent Strategy Period.");
        }

        var objectiveGranularity = NormalizeGranularityForComparison(objective?.TargetPlanGranularity);
        var effectiveGranularity = ResolveEffectiveInitiativeGranularity(link.ContributionPlanGranularity, objective?.TargetPlanGranularity);
        if (GranularityRank(effectiveGranularity) > GranularityRank(objectiveGranularity))
            blockers.Add("Contribution Plan Granularity is coarser than the parent Objective target plan.");

        if (link.BenefitRealizationStart.HasValue && link.BenefitRealizationEnd.HasValue &&
            link.BenefitRealizationEnd.Value.Date < link.BenefitRealizationStart.Value.Date)
            blockers.Add("Benefit realization end must be on or after benefit realization start.");
        if (link.BenefitRealizationStart.HasValue && link.StartDate.HasValue && link.BenefitRealizationStart.Value.Date < link.StartDate.Value.Date)
            blockers.Add("Benefit realization start sits before the initiative period.");
        if (link.BenefitRealizationEnd.HasValue && link.EndDate.HasValue && link.BenefitRealizationEnd.Value.Date > link.EndDate.Value.Date)
            blockers.Add("Benefit realization end sits after the initiative period.");

        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(link.AccountableSponsorRole))
            warnings.Add("Accountable sponsor role is still blank.");
        if ((link.ParticipatingCompanyIds?.Count ?? 0) == 0)
            warnings.Add("Participating companies are not selected yet.");
        if (string.IsNullOrWhiteSpace(link.ReportingFrequency))
            warnings.Add("Reporting Frequency is still blank.");
        if (string.IsNullOrWhiteSpace(link.FundingSource))
            warnings.Add("Funding source is still blank.");
        if (string.IsNullOrWhiteSpace(link.StrategyAlignmentNote))
            warnings.Add("Strategy alignment note is still blank.");
        if (string.IsNullOrWhiteSpace(link.GovernanceNotes))
            warnings.Add("Governance / evidence note is still blank.");

        var permittedPeriods = BuildInitiativePeriods(link, objective, goal, effectiveGranularity);
        var periodMap = permittedPeriods.ToDictionary(x => x.PeriodKey, StringComparer.OrdinalIgnoreCase);
        var planRows = link.ContributionPlanValues ?? new();
        var missingValues = planRows.Count(x => !x.PlannedValue.HasValue);
        if (string.IsNullOrWhiteSpace(link.ContributionMetricName))
            blockers.Add("Contribution metric / success measure is required for planning readiness.");
        if (string.IsNullOrWhiteSpace(link.ContributionMethod))
            blockers.Add("Contribution method / aggregation method is required for planning readiness.");
        if (string.IsNullOrWhiteSpace(link.BenefitHypothesis))
            blockers.Add("Expected contribution / benefit hypothesis is required for planning readiness.");
        foreach (var row in planRows)
        {
            if (!periodMap.ContainsKey(row.PeriodKey))
                blockers.Add($"Contribution row {row.PeriodLabel} falls outside the allowed initiative/objective/strategy horizon.");
        }
        if (permittedPeriods.Count > 0 && !planRows.Any())
            blockers.Add("Generate or add contribution plan rows inside the allowed horizon.");
        if (missingValues > 0)
            blockers.Add("Contribution plan rows are missing planned values.");

        var draftReady = missing.Count == 0;
        var planningReady = draftReady && blockers.Count == 0 && planRows.Any();
        var publishReady = planningReady
            && !string.IsNullOrWhiteSpace(link.ReportingFrequency)
            && !string.IsNullOrWhiteSpace(link.StrategyAlignmentNote)
            && !string.IsNullOrWhiteSpace(link.GovernanceNotes);
        if (!publishReady && planningReady)
            warnings.Add("Publish readiness still needs Reporting Frequency, Strategy Alignment Note, and Governance / evidence note.");

        return new InitiativeReadinessDto
        {
            DraftReady = draftReady,
            PlanningReady = planningReady,
            PublishReady = publishReady,
            ReadinessStatus = publishReady ? "Ready" : planningReady ? "Planning Ready" : draftReady ? "Draft Ready" : "Blocked",
            ContributionPlanRowsCount = planRows.Count,
            MissingContributionValuesCount = missingValues,
            Missing = missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Blockers = blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings = warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static IReadOnlyList<ObjectiveTargetPlanPeriodDefinition> BuildInitiativePeriods(
        InitiativeStrategyLinkAggregate link,
        ObjectiveAggregate? objective,
        GoalAggregate? goal,
        string effectiveGranularity)
    {
        if (!link.StartDate.HasValue || !link.EndDate.HasValue)
            return Array.Empty<ObjectiveTargetPlanPeriodDefinition>();

        var start = link.StartDate.Value.Date;
        var end = link.EndDate.Value.Date;
        if (objective?.TimeHorizonStart is not null && objective.TimeHorizonStart.Value.Date > start) start = objective.TimeHorizonStart.Value.Date;
        if (objective?.TimeHorizonEnd is not null && objective.TimeHorizonEnd.Value.Date < end) end = objective.TimeHorizonEnd.Value.Date;
        if (goal?.StartDate is not null && goal.StartDate.Value.Date > start) start = goal.StartDate.Value.Date;
        if (goal?.EndDate is not null && goal.EndDate.Value.Date < end) end = goal.EndDate.Value.Date;
        if (end < start)
            return Array.Empty<ObjectiveTargetPlanPeriodDefinition>();

        return ObjectiveTargetPlanPeriodHelper.BuildPeriods(start, end, effectiveGranularity == "TotalInitiativeHorizon"
            ? ObjectiveTargetPlanPeriodHelper.GranularityTotalStrategyPeriod
            : effectiveGranularity);
    }

    private static InitiativeContributionPlanValue ToAggregate(InitiativeContributionPlanValueDto dto) => new()
    {
        PeriodKey = dto.PeriodKey,
        PeriodLabel = dto.PeriodLabel,
        PeriodStart = dto.PeriodStart,
        PeriodEnd = dto.PeriodEnd,
        PlannedValue = dto.PlannedValue,
        ActualValue = dto.ActualValue,
        ForecastValue = dto.ForecastValue,
        Commentary = dto.Commentary ?? string.Empty
    };

    private static string FirstText(params string?[] values) =>
        values.Select(x => (x ?? string.Empty).Trim()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static string NormalizeInitiativeGranularity(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Replace(" ", string.Empty);
        return normalized switch
        {
            "inheritfromobjective" => "InheritFromObjective",
            "monthly" => "Monthly",
            "quarterly" => "Quarterly",
            "yearly" => "Yearly",
            "totalinitiativehorizon" => "TotalInitiativeHorizon",
            "totalstrategyperiod" => "TotalInitiativeHorizon",
            "total" => "TotalInitiativeHorizon",
            _ => "InheritFromObjective"
        };
    }

    private static string ResolveEffectiveInitiativeGranularity(string? value, string? objectiveGranularity)
    {
        var normalized = NormalizeInitiativeGranularity(value);
        if (string.Equals(normalized, "InheritFromObjective", StringComparison.OrdinalIgnoreCase))
        {
            var objectiveNormalized = NormalizeGranularityForComparison(objectiveGranularity);
            return string.Equals(objectiveNormalized, ObjectiveTargetPlanPeriodHelper.GranularityTotalStrategyPeriod, StringComparison.OrdinalIgnoreCase)
                ? "TotalInitiativeHorizon"
                : objectiveNormalized;
        }

        return normalized;
    }

    private static string NormalizeGranularityForComparison(string? value)
    {
        var normalized = ObjectiveTargetPlanPeriodHelper.NormalizeGranularity(value);
        return string.Equals(normalized, ObjectiveTargetPlanPeriodHelper.GranularityTotalStrategyPeriod, StringComparison.OrdinalIgnoreCase)
            ? "TotalInitiativeHorizon"
            : normalized;
    }

    private static int GranularityRank(string? value) => NormalizeInitiativeGranularity(value) switch
    {
        "Monthly" => 1,
        "Quarterly" => 2,
        "Yearly" => 3,
        "TotalInitiativeHorizon" => 4,
        _ => 4
    };

    private static IReadOnlyList<string> BuildInitiativeWarnings(
        PpmInitiativeReadModelDto? ppm,
        InitiativeStrategyLinkAggregate? link,
        ObjectiveAggregate? objective,
        GoalAggregate? goal)
    {
        var warnings = new List<string>();
        if (ppm?.DegradedMode == true)
            warnings.Add("PPM dependency unavailable; showing cached data.");
        if (link is not null && string.IsNullOrWhiteSpace(link.MetricBindingsJson))
            warnings.Add("No legacy metric binding exists. Use the contribution plan instead.");
        if (objective is not null && link is not null && (link.StartDate.HasValue || link.EndDate.HasValue))
            warnings.Add("Contribution periods must remain inside initiative dates, objective horizon, and strategy period.");
        if (goal is not null && string.IsNullOrWhiteSpace(goal.StrategyPeriodId))
            warnings.Add("Parent goal is missing a Strategy Period id; horizon checks rely on date bounds only.");
        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string NormalizeText(params string?[] values) =>
        string.Concat(FirstText(values).Where(char.IsLetterOrDigit)).ToLowerInvariant();
}

public sealed class ProjectOrchestrationService : IProjectOrchestrationService
{
    private const string LocalProjectSource = "delivery";
    private static readonly string[] NonDraftStatuses = ["planned", "approved", "active", "onhold", "closed"];
    private readonly IPpmProjectReadAdapter _ppmProjects;
    private readonly IPpmProjectCacheRepository _projectCache;
    private readonly IProjectStrategyLinkRepository _projectLinks;
    private readonly IInitiativeStrategyLinkRepository _initiativeLinks;
    private readonly IObjectiveRepository _objectives;
    private readonly IGoalRepository _goals;
    private readonly IStrategyLibraryRepository _library;
    private readonly IEnterpriseStrategyAuditStore _audit;

    public ProjectOrchestrationService(
        IPpmProjectReadAdapter ppmProjects,
        IPpmProjectCacheRepository projectCache,
        IProjectStrategyLinkRepository projectLinks,
        IInitiativeStrategyLinkRepository initiativeLinks,
        IObjectiveRepository objectives,
        IGoalRepository goals,
        IStrategyLibraryRepository library,
        IEnterpriseStrategyAuditStore audit)
    {
        _ppmProjects = ppmProjects;
        _projectCache = projectCache;
        _projectLinks = projectLinks;
        _initiativeLinks = initiativeLinks;
        _objectives = objectives;
        _goals = goals;
        _library = library;
        _audit = audit;
    }

    public async Task<Response<PagedResponseDto<ProjectStrategyLinkViewDto>>> ListAsync(PagedRequestDto request, CancellationToken cancellationToken = default)
    {
        var ppm = await GetPpmProjectsSafeAsync(cancellationToken);
        var links = await _projectLinks.ListAsync(cancellationToken);
        var initiatives = (await _initiativeLinks.ListAsync(cancellationToken)).ToDictionary(x => x.InitiativeId, StringComparer.OrdinalIgnoreCase);
        var objectives = (await _objectives.ListAsync(cancellationToken)).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var goals = (await _goals.ListAsync(cancellationToken)).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var ppmById = ppm.ToDictionary(x => x.ProjectId, StringComparer.OrdinalIgnoreCase);
        var linksById = links.ToDictionary(x => x.ProjectId, StringComparer.OrdinalIgnoreCase);

        IEnumerable<ProjectStrategyLinkViewDto> query = ppmById.Keys
            .Union(linksById.Keys, StringComparer.OrdinalIgnoreCase)
            .Select(id =>
            {
                ppmById.TryGetValue(id, out var ppmRow);
                linksById.TryGetValue(id, out var link);
                initiatives.TryGetValue(link?.ParentInitiativeId ?? string.Empty, out var initiative);
                objectives.TryGetValue(link?.ParentObjectiveId ?? initiative?.ParentObjectiveId ?? string.Empty, out var objective);
                goals.TryGetValue(link?.ParentGoalId ?? objective?.ParentGoalId ?? string.Empty, out var goal);
                return BuildProjectView(ppmRow, link, initiative, objective, goal);
            });

        var filters = request.Filters;
        if (filters.TryGetValue("status", out var status))
            query = query.Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase));
        if (filters.TryGetValue("phase", out var phase))
            query = query.Where(x => string.Equals(x.Phase, phase, StringComparison.OrdinalIgnoreCase));
        if (filters.TryGetValue("parentGoal", out var parentGoal))
            query = query.Where(x => string.Equals(x.ParentGoalId, parentGoal, StringComparison.OrdinalIgnoreCase));
        if (filters.TryGetValue("parentObjective", out var parentObjective))
            query = query.Where(x => string.Equals(x.ParentObjectiveId, parentObjective, StringComparison.OrdinalIgnoreCase));
        if (filters.TryGetValue("parentInitiative", out var parentInitiative))
            query = query.Where(x => string.Equals(x.ParentInitiativeId, parentInitiative, StringComparison.OrdinalIgnoreCase));
        if (filters.TryGetValue("deliveryCompany", out var deliveryCompany))
            query = query.Where(x => string.Equals(x.DeliveryCompanyId, deliveryCompany, StringComparison.OrdinalIgnoreCase));
        if (filters.TryGetValue("fundingCompany", out var fundingCompany))
            query = query.Where(x => string.Equals(x.FundingCompanyId, fundingCompany, StringComparison.OrdinalIgnoreCase));
        if (filters.TryGetValue("ownerPm", out var ownerPm))
            query = query.Where(x => string.Equals(x.OwnerPm, ownerPm, StringComparison.OrdinalIgnoreCase));
        if (filters.TryGetValue("sponsor", out var sponsor))
            query = query.Where(x => string.Equals(x.Sponsor, sponsor, StringComparison.OrdinalIgnoreCase));
        if (filters.TryGetValue("deliveryType", out var deliveryType))
            query = query.Where(x => string.Equals(x.DeliveryType, deliveryType, StringComparison.OrdinalIgnoreCase));
        if (filters.TryGetValue("readinessStatus", out var readinessStatus))
            query = query.Where(x => string.Equals(x.ReadinessStatus, readinessStatus, StringComparison.OrdinalIgnoreCase));
        if (filters.TryGetValue("scope", out var scope))
            query = query.Where(x => (x.EntityScope ?? string.Empty).Contains(scope, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(x =>
                x.ProjectName.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ||
                x.ParentInitiativeName.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ||
                x.ParentObjectiveName.Contains(request.Search, StringComparison.OrdinalIgnoreCase) ||
                x.ParentGoalName.Contains(request.Search, StringComparison.OrdinalIgnoreCase));
        }

        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 10_000);
        var total = query.Count();
        var items = query.Skip((page - 1) * size).Take(size).ToList();

        return Response<PagedResponseDto<ProjectStrategyLinkViewDto>>.Ok(new PagedResponseDto<ProjectStrategyLinkViewDto>
        {
            Page = page,
            PageSize = size,
            TotalCount = total,
            Items = items
        });
    }

    public async Task<Response<ProjectDetailDto>> GetAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var ppm = await GetProjectAsync(projectId, cancellationToken);
        var link = await _projectLinks.GetByProjectIdAsync(projectId, cancellationToken);
        if (ppm is null && link is null)
            return Response<ProjectDetailDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);

        InitiativeStrategyLinkAggregate? initiative = null;
        ObjectiveAggregate? objective = null;
        GoalAggregate? goal = null;
        if (link is not null)
        {
            initiative = await _initiativeLinks.GetByInitiativeIdAsync(link.ParentInitiativeId, cancellationToken);
            objective = await _objectives.GetByIdAsync(link.ParentObjectiveId, cancellationToken);
            goal = await _goals.GetByIdAsync(link.ParentGoalId, cancellationToken);
        }

        var project = BuildProjectView(ppm, link, initiative, objective, goal);
        var auditTrail = await GetAuditTrailAsync(projectId, cancellationToken);

        return Response<ProjectDetailDto>.Ok(new ProjectDetailDto
        {
            Project = project,
            StrategyLink = link?.ToViewDto(),
            TraceabilitySummary = link is null ? "Unlinked" : $"Goal {link.ParentGoalId} -> Objective {link.ParentObjectiveId} -> Initiative {link.ParentInitiativeId} -> Project {projectId}",
            UpstreamLineage = link is null ? "Missing upstream strategy link." : $"Goal:{link.ParentGoalId}; Objective:{link.ParentObjectiveId}; Initiative:{link.ParentInitiativeId}",
            AuditTrail = auditTrail.Data ?? Array.Empty<EnterpriseStrategyAuditEventDto>()
        });
    }

    public Task<Response<ProjectStrategyLinkViewDto>> CreateAsync(ProjectStrategyLinkViewDto project, string actor, string correlationId, CancellationToken cancellationToken = default)
        => SaveProjectAsync(string.IsNullOrWhiteSpace(project.ProjectId) ? NextProjectId() : project.ProjectId.Trim(), project, null, actor, correlationId, cancellationToken);

    public Task<Response<ProjectStrategyLinkViewDto>> UpdateAsync(string projectId, ProjectStrategyLinkViewDto project, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default)
        => SaveProjectAsync(projectId, project, expectedVersion, actor, correlationId, cancellationToken);

    public async Task<Response<ProjectStrategyLinkViewDto>> UpsertStrategyLinkAsync(string projectId, ProjectStrategyLinkViewDto link, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var ppm = await GetProjectAsync(projectId, cancellationToken);
        var existing = await _projectLinks.GetByProjectIdAsync(projectId, cancellationToken);
        if (ppm is null && existing is null)
        {
            return Response<ProjectStrategyLinkViewDto>.Fail(
                EnterpriseStrategyErrorCodes.ValidationError,
                new() { ["projectId"] = new() { "Project must exist in Delivery or PPM/cache." } });
        }

        var current = existing?.ToViewDto() ?? BuildProjectView(ppm, null, null, null, null);
        current.ParentInitiativeId = link.ParentInitiativeId;
        current.DeliveryCompanyId = string.IsNullOrWhiteSpace(link.DeliveryCompanyId) ? current.DeliveryCompanyId : link.DeliveryCompanyId;
        current.FundingCompanyId = string.IsNullOrWhiteSpace(link.FundingCompanyId) ? current.FundingCompanyId : link.FundingCompanyId;
        current.DecisionReference = link.DecisionReference;
        current.EvidenceReference = link.EvidenceReference;
        current.ContributionNote = link.ContributionNote;
        current.SourceTemplateId = link.SourceTemplateId;
        current.SourceTemplateType = link.SourceTemplateType;
        current.SourceTemplateVersion = link.SourceTemplateVersion;
        current.SourceTemplateName = link.SourceTemplateName;
        current.CreationMode = string.IsNullOrWhiteSpace(link.CreationMode) ? current.CreationMode : link.CreationMode;
        current.StrategyLinkStatus = "Linked";
        return await SaveProjectAsync(projectId, current, expectedVersion, actor, correlationId, cancellationToken);
    }

    public async Task<Response<ProjectStrategyLinkViewDto>> ChangeStrategyLinkStatusAsync(string projectId, string status, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var existing = await _projectLinks.GetByProjectIdAsync(projectId, cancellationToken);
        if (existing is null)
            return Response<ProjectStrategyLinkViewDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);

        var dto = existing.ToViewDto();
        dto.Status = string.IsNullOrWhiteSpace(status) ? dto.Status : status.Trim();
        dto.StrategyLinkStatus = "Linked";
        return await SaveProjectAsync(projectId, dto, expectedVersion, actor, correlationId, cancellationToken);
    }

    public async Task<Response<bool>> DeleteStrategyLinkAsync(string projectId, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var existing = await _projectLinks.GetByProjectIdAsync(projectId, cancellationToken);
        await _projectLinks.DeleteByProjectIdAsync(projectId, cancellationToken);
        if (existing is not null)
        {
            await _audit.WriteMutationAsync(
                actor,
                "Project",
                projectId,
                EnterpriseStrategyEventNames.ProjectStrategyUnlinked,
                correlationId,
                DeliveryExecutionManagementModules.Projects,
                existing.ParentInitiativeId,
                "Deleted",
                cancellationToken);
        }

        return Response<bool>.Ok(true);
    }

    public async Task<Response<SyncResultDto>> SyncAsync(string correlationId, string actor, CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await _ppmProjects.SyncAsync(correlationId, cancellationToken);
            await _projectCache.UpsertManyAsync(rows.Select(x => new PpmProjectReadModelAggregate
            {
                ProjectId = x.ProjectId,
                ProjectName = x.ProjectName,
                Description = x.Description,
                OwnerPm = x.OwnerPm,
                Sponsor = x.Sponsor,
                Status = x.Status,
                Phase = x.Phase,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                DeliveryType = x.DeliveryType,
                SuccessMetric = x.SuccessMetric,
                RiskRating = x.RiskRating,
                ReadinessStatus = x.ReadinessStatus,
                BudgetSummary = x.BudgetSummary,
                SourceSystem = x.SourceSystem,
                SourceUpdatedAt = x.SourceUpdatedAt,
                CachedAt = DateTime.UtcNow
            }).ToList(), cancellationToken);
            await _audit.WriteMutationAsync(actor, "ProjectSync", "ppm-projects", EnterpriseStrategyEventNames.ProjectSyncCompleted, correlationId, DeliveryExecutionManagementModules.Projects, "", rows.Count.ToString(), cancellationToken);
            return Response<SyncResultDto>.Ok(new SyncResultDto { CorrelationId = correlationId, ImportedCount = rows.Count, DegradedMode = false, EventName = EnterpriseStrategyEventNames.ProjectSyncCompleted });
        }
        catch (Exception ex)
        {
            await _audit.WriteMutationAsync(actor, "ProjectSync", "ppm-projects", EnterpriseStrategyEventNames.ProjectSyncFailed, correlationId, DeliveryExecutionManagementModules.Projects, "", ex.Message, cancellationToken);
            return Response<SyncResultDto>.Fail(EnterpriseStrategyErrorCodes.DependencyUnavailable, new() { ["ppm"] = new() { ex.Message } });
        }
    }

    public async Task<Response<IReadOnlyList<ProjectCreationTemplateDto>>> GetCompatibleTemplatesAsync(string parentType, string entityScope, CancellationToken cancellationToken = default)
    {
        var rows = await BuildCompatibleProjectTemplatesAsync(parentType, entityScope, cancellationToken);
        return Response<IReadOnlyList<ProjectCreationTemplateDto>>.Ok(rows);
    }

    public async Task<Response<IReadOnlyList<EnterpriseStrategyAuditEventDto>>> GetAuditTrailAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var events = await _audit.ListAsync("Project", projectId, cancellationToken);
        return Response<IReadOnlyList<EnterpriseStrategyAuditEventDto>>.Ok(events.Select(x => new EnterpriseStrategyAuditEventDto
        {
            Id = x.Id,
            Actor = x.Actor,
            TimestampUtc = x.TimestampUtc,
            ObjectType = x.ObjectType,
            ObjectId = x.ObjectId,
            Action = x.Action,
            CorrelationId = x.CorrelationId,
            SourceModule = x.SourceModule,
            BeforeSummary = x.BeforeSummary,
            AfterSummary = x.AfterSummary
        }).ToList());
    }

    public async Task<Response<string>> TraceabilityAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var link = await _projectLinks.GetByProjectIdAsync(projectId, cancellationToken);
        return Response<string>.Ok(link is null ? "Unlinked" : $"Goal {link.ParentGoalId} -> Objective {link.ParentObjectiveId} -> Initiative {link.ParentInitiativeId} -> Project {projectId}");
    }

    public async Task<Response<string>> UpstreamLineageAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var link = await _projectLinks.GetByProjectIdAsync(projectId, cancellationToken);
        return Response<string>.Ok(link is null ? "Missing upstream strategy link." : $"Goal:{link.ParentGoalId}; Objective:{link.ParentObjectiveId}; Initiative:{link.ParentInitiativeId}");
    }

    public async Task<Response<ProjectStrategyLinkViewDto>> CreateStrategyLinkedAsync(
        Commands.EnterpriseStrategyCommands.CreateStrategyLinkedProjectPayloadDto project,
        Commands.EnterpriseStrategyCommands.CreateStrategyLinkedContextDto ctx,
        string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var payload = new ProjectStrategyLinkViewDto
        {
            ProjectName = project.ProjectName,
            Description = project.Description ?? string.Empty,
            OwnerPm = project.OwnerPm ?? string.Empty,
            Sponsor = project.Sponsor ?? string.Empty,
            Status = string.IsNullOrWhiteSpace(project.Status) ? "Draft" : project.Status,
            Phase = project.Phase ?? string.Empty,
            DeliveryType = project.DeliveryType ?? string.Empty,
            Priority = project.PriorityCode ?? string.Empty,
            StartDate = DateTime.TryParse(project.StartDate, out var startDate) ? startDate : null,
            EndDate = DateTime.TryParse(project.EndDate, out var endDate) ? endDate : null,
            SuccessMetric = project.SuccessMetric ?? string.Empty,
            MetricBaseline = project.MetricBaseline ?? string.Empty,
            MetricTarget = project.MetricTarget ?? string.Empty,
            RiskRating = project.RiskRating ?? string.Empty,
            ReadinessStatus = project.ReadinessStatus ?? string.Empty,
            DeliveryCompanyId = project.DeliveryCompanyId ?? string.Empty,
            FundingCompanyId = project.FundingCompanyId,
            ParentInitiativeId = ctx.ParentInitiativeId,
            CreationMode = string.IsNullOrWhiteSpace(ctx.SourceTemplateId) ? "Blank" : "Template",
            SourceTemplateType = string.IsNullOrWhiteSpace(ctx.SourceTemplateId) ? null : "ProjectTemplate",
            SourceTemplateId = ctx.SourceTemplateId,
            SourceTemplateVersion = ctx.SourceTemplateVersion,
            CreatedFromLibrary = !string.IsNullOrWhiteSpace(ctx.SourceTemplateId),
            ContributionNote = ctx.StrategyTraceabilityNote ?? string.Empty,
            StrategyLinkStatus = "Linked",
            EntityScope = project.EntityScopeCode ?? string.Empty,
            BudgetAmount = project.BudgetAmount,
            CurrencyCode = project.CurrencyCode ?? string.Empty,
            BudgetType = project.BudgetTypeCode ?? string.Empty,
            BudgetBasis = project.BudgetBasisCode ?? string.Empty,
            FundingSource = string.Empty
        };
        return await CreateAsync(payload, actor, correlationId, cancellationToken);
    }

    private async Task<Response<ProjectStrategyLinkViewDto>> SaveProjectAsync(
        string projectId,
        ProjectStrategyLinkViewDto project,
        int? expectedVersion,
        string actor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var existing = await _projectLinks.GetByProjectIdAsync(projectId, cancellationToken);
        if (expectedVersion.HasValue && existing is not null && EnterpriseStrategyResult.IsStaleWrite(expectedVersion.Value, existing.Version))
            return EnterpriseStrategyResult.StaleVersion<ProjectStrategyLinkViewDto>();

        var initiativeId = (project.ParentInitiativeId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(initiativeId))
        {
            return Response<ProjectStrategyLinkViewDto>.Fail(
                EnterpriseStrategyErrorCodes.ValidationError,
                new() { ["parentInitiativeId"] = new() { "Parent Initiative is required before a project can be saved." } });
        }

        var initiative = await _initiativeLinks.GetByInitiativeIdAsync(initiativeId, cancellationToken);
        if (initiative is null)
        {
            return Response<ProjectStrategyLinkViewDto>.Fail(
                EnterpriseStrategyErrorCodes.ValidationError,
                new() { ["parentInitiativeId"] = new() { "Selected Parent Initiative must already exist in Delivery." } });
        }

        var objective = await _objectives.GetByIdAsync(initiative.ParentObjectiveId, cancellationToken);
        if (objective is null)
        {
            return Response<ProjectStrategyLinkViewDto>.Fail(
                EnterpriseStrategyErrorCodes.ValidationError,
                new() { ["parentObjectiveId"] = new() { "Derived Parent Objective could not be resolved." } });
        }

        var goal = await _goals.GetByIdAsync(objective.ParentGoalId, cancellationToken);
        if (goal is null)
        {
            return Response<ProjectStrategyLinkViewDto>.Fail(
                EnterpriseStrategyErrorCodes.ValidationError,
                new() { ["parentGoalId"] = new() { "Derived Parent Goal could not be resolved." } });
        }

        var parentType = ResolveProjectParentType(initiative, goal);
        var entityScope = ResolveEntityScope(initiative, objective, goal);
        var template = await ResolveCompatibleTemplateAsync(project.SourceTemplateId, parentType, entityScope, cancellationToken);
        if (!string.IsNullOrWhiteSpace(project.SourceTemplateId) && template is null)
        {
            return Response<ProjectStrategyLinkViewDto>.Fail(
                EnterpriseStrategyErrorCodes.ValidationError,
                new() { ["sourceTemplateId"] = new() { "Selected Project Template is incompatible with the selected Parent Initiative type." } });
        }

        var aggregate = BuildAggregateForSave(existing, projectId, project, initiative, objective, goal, template, actor);
        var validationErrors = ValidateProjectAggregate(aggregate);
        if (validationErrors.Count > 0)
            return Response<ProjectStrategyLinkViewDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, validationErrors);

        await _projectLinks.AddOrUpdateAsync(aggregate, cancellationToken);
        await EmitProjectAuditAsync(existing, aggregate, project.TemplateApplicationMode, actor, correlationId, cancellationToken);

        var ppm = await GetProjectAsync(projectId, cancellationToken);
        var dto = BuildProjectView(ppm, aggregate, initiative, objective, goal);
        return Response<ProjectStrategyLinkViewDto>.Ok(dto);
    }

    private static ProjectStrategyLinkAggregate BuildAggregateForSave(
        ProjectStrategyLinkAggregate? existing,
        string projectId,
        ProjectStrategyLinkViewDto project,
        InitiativeStrategyLinkAggregate initiative,
        ObjectiveAggregate objective,
        GoalAggregate goal,
        ProjectTemplate? template,
        string actor)
    {
        var status = string.IsNullOrWhiteSpace(project.Status) ? existing?.Status ?? "Draft" : project.Status.Trim();
        var creationMode = NormalizeCreationMode(project.CreationMode, project.SourceTemplateId);
        var parentType = ResolveProjectParentType(initiative, goal);
        return new ProjectStrategyLinkAggregate
        {
            Id = existing?.Id ?? Guid.NewGuid().ToString("N"),
            ProjectId = projectId,
            ProjectName = (project.ProjectName ?? string.Empty).Trim(),
            Description = (project.Description ?? string.Empty).Trim(),
            OwnerPm = (project.OwnerPm ?? string.Empty).Trim(),
            Sponsor = FirstText(project.Sponsor, initiative.ExecutiveSponsor),
            BusinessOwner = (project.BusinessOwner ?? string.Empty).Trim(),
            Status = status,
            Phase = FirstText(project.Phase, template?.Phase),
            DeliveryType = FirstText(project.DeliveryType, template?.DeliveryType),
            DeliveryMethodology = FirstText(project.DeliveryMethodology, template?.DeliveryMethodology),
            Priority = (project.Priority ?? string.Empty).Trim(),
            ComplexitySize = FirstText(project.ComplexitySize, template?.ComplexitySize),
            StartDate = project.StartDate,
            EndDate = project.EndDate,
            GoLiveDate = project.GoLiveDate,
            ReportingCadence = FirstText(project.ReportingCadence, initiative.ReportingFrequency, template?.ReportingCadence),
            SuccessMetric = (project.SuccessMetric ?? string.Empty).Trim(),
            MetricBaseline = (project.MetricBaseline ?? string.Empty).Trim(),
            MetricTarget = (project.MetricTarget ?? string.Empty).Trim(),
            RiskRating = FirstText(project.RiskRating, template?.RiskRating),
            ReadinessStatus = FirstText(project.ReadinessStatus, template?.ReadinessStatus),
            OverallHealth = (project.OverallHealth ?? string.Empty).Trim(),
            ComplianceRegulatoryImpact = FirstText(project.ComplianceRegulatoryImpact, initiative.GovernanceNotes),
            DependencyFlag = project.DependencyFlag,
            EvidenceRequiredFlag = project.EvidenceRequiredFlag,
            SourceSystem = string.IsNullOrWhiteSpace(existing?.SourceSystem) ? LocalProjectSource : existing.SourceSystem,
            SourceRecordId = string.IsNullOrWhiteSpace(existing?.SourceRecordId) ? projectId : existing.SourceRecordId,
            ParentInitiativeId = initiative.InitiativeId,
            ParentInitiativeName = initiative.InitiativeName,
            ParentObjectiveId = objective.Id,
            ParentObjectiveName = objective.Name,
            ParentGoalId = goal.Id,
            ParentGoalName = goal.Name,
            ParentType = parentType,
            EntityScope = ResolveEntityScope(initiative, objective, goal),
            CreationMode = creationMode,
            StrategyLinkStatus = "Linked",
            ContributionNote = (project.ContributionNote ?? string.Empty).Trim(),
            MetricBindingsJson = string.IsNullOrWhiteSpace(project.MetricBindingsJson) ? existing?.MetricBindingsJson ?? "[]" : project.MetricBindingsJson.Trim(),
            DecisionReference = string.IsNullOrWhiteSpace(project.DecisionReference) ? null : project.DecisionReference.Trim(),
            EvidenceReference = string.IsNullOrWhiteSpace(project.EvidenceReference) ? null : project.EvidenceReference.Trim(),
            DeliveryCompanyId = FirstText(project.DeliveryCompanyId, initiative.DeliveryOwnerCompanyId, initiative.SponsoringCompanyId),
            FundingCompanyId = string.IsNullOrWhiteSpace(FirstText(project.FundingCompanyId, initiative.SponsoringCompanyId))
                ? null
                : FirstText(project.FundingCompanyId, initiative.SponsoringCompanyId),
            OwningFunctionDepartment = FirstText(project.OwningFunctionDepartment, initiative.AccountableSponsorRole),
            DeliveryPartnerVendor = (project.DeliveryPartnerVendor ?? string.Empty).Trim(),
            ScopeSummary = FirstText(project.ScopeSummary, template?.ScopeSummaryTemplate, template?.Description),
            OutOfScopeNote = (project.OutOfScopeNote ?? string.Empty).Trim(),
            BudgetRequired = project.BudgetRequired,
            BudgetAmount = project.BudgetAmount,
            CurrencyCode = (project.CurrencyCode ?? string.Empty).Trim(),
            BudgetType = FirstText(project.BudgetType, template?.BudgetType),
            BudgetBasis = FirstText(project.BudgetBasis, template?.BudgetBasis),
            FundingSource = FirstText(project.FundingSource, template?.FundingSource),
            CostCenter = FirstText(project.CostCenter, template?.CostCenter),
            BudgetOwner = (project.BudgetOwner ?? string.Empty).Trim(),
            ApprovalRoute = FirstText(project.ApprovalRoute, template?.ApprovalRoute, template?.DecisionReference),
            FinancialNotes = (project.FinancialNotes ?? string.Empty).Trim(),
            NoBudgetReason = (project.NoBudgetReason ?? string.Empty).Trim(),
            Version = existing is null ? 1 : existing.Version + 1,
            SyncedAt = existing?.SyncedAt,
            CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = existing?.CreatedBy ?? actor,
            UpdatedBy = actor,
            SourceTemplateType = template is null ? null : "ProjectTemplate",
            SourceTemplateId = template?.Id,
            SourceTemplateName = template?.Name,
            SourceTemplateVersion = template?.Version,
            SourceBlueprintPackId = project.SourceBlueprintPackId,
            InstantiationBatchId = project.InstantiationBatchId,
            CreatedFromLibrary = template is not null
        };
    }

    private static Dictionary<string, List<string>> ValidateProjectAggregate(ProjectStrategyLinkAggregate aggregate)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(aggregate.ParentInitiativeId))
            AddValidationError(errors, "parentInitiativeId", "Parent Initiative is required.");
        if (IsTemplateMode(aggregate.CreationMode) && string.IsNullOrWhiteSpace(aggregate.SourceTemplateId))
            AddValidationError(errors, "sourceTemplateId", "Project Template is required when Creation Mode is Template.");

        if (aggregate.StartDate.HasValue && aggregate.EndDate.HasValue && aggregate.EndDate.Value.Date < aggregate.StartDate.Value.Date)
            AddValidationError(errors, "endDate", "End Date must be on or after Start Date.");
        if (aggregate.GoLiveDate.HasValue && aggregate.StartDate.HasValue && aggregate.GoLiveDate.Value.Date < aggregate.StartDate.Value.Date)
            AddValidationError(errors, "goLiveDate", "Go-Live / Target Milestone must be on or after Start Date.");

        if (!IsNonDraftStatus(aggregate.Status))
            return errors;

        if (string.IsNullOrWhiteSpace(aggregate.ProjectName))
            AddValidationError(errors, "projectName", "Project Name is required for non-draft Projects.");
        if (string.IsNullOrWhiteSpace(aggregate.Description))
            AddValidationError(errors, "description", "Project Description is required for non-draft Projects.");
        if (string.IsNullOrWhiteSpace(aggregate.OwnerPm))
            AddValidationError(errors, "ownerPm", "Project Owner / PM is required for non-draft Projects.");
        if (string.IsNullOrWhiteSpace(aggregate.Sponsor))
            AddValidationError(errors, "sponsor", "Executive Sponsor is required for non-draft Projects.");
        if (string.IsNullOrWhiteSpace(aggregate.DeliveryCompanyId))
            AddValidationError(errors, "deliveryCompanyId", "Delivery Company is required for non-draft Projects.");
        if (string.IsNullOrWhiteSpace(aggregate.ScopeSummary))
            AddValidationError(errors, "scopeSummary", "Scope Summary is required for non-draft Projects.");
        if (string.IsNullOrWhiteSpace(aggregate.Phase))
            AddValidationError(errors, "phase", "Stage / Phase is required for non-draft Projects.");
        if (string.IsNullOrWhiteSpace(aggregate.DeliveryType))
            AddValidationError(errors, "deliveryType", "Delivery Type is required for non-draft Projects.");
        if (string.IsNullOrWhiteSpace(aggregate.DeliveryMethodology))
            AddValidationError(errors, "deliveryMethodology", "Delivery Methodology is required for non-draft Projects.");
        if (string.IsNullOrWhiteSpace(aggregate.Priority))
            AddValidationError(errors, "priority", "Priority is required for non-draft Projects.");
        if (!aggregate.StartDate.HasValue)
            AddValidationError(errors, "startDate", "Start Date is required for non-draft Projects.");
        if (!aggregate.EndDate.HasValue)
            AddValidationError(errors, "endDate", "End Date is required for non-draft Projects.");
        if (string.IsNullOrWhiteSpace(aggregate.ReadinessStatus))
            AddValidationError(errors, "readinessStatus", "Readiness Status is required for non-draft Projects.");
        if (string.IsNullOrWhiteSpace(aggregate.RiskRating))
            AddValidationError(errors, "riskRating", "Risk Rating is required for non-draft Projects.");
        if (!aggregate.BudgetRequired.HasValue)
            AddValidationError(errors, "budgetRequired", "Budget Required must be set for non-draft Projects.");

        if (aggregate.BudgetRequired == true)
        {
            if (!aggregate.BudgetAmount.HasValue || aggregate.BudgetAmount.Value <= 0)
                AddValidationError(errors, "budgetAmount", "Budget Amount is required when Budget Required is Yes.");
            if (string.IsNullOrWhiteSpace(aggregate.CurrencyCode))
                AddValidationError(errors, "currencyCode", "Currency is required when Budget Required is Yes.");
            if (string.IsNullOrWhiteSpace(aggregate.BudgetType))
                AddValidationError(errors, "budgetType", "Budget Type is required when Budget Required is Yes.");
            if (string.IsNullOrWhiteSpace(aggregate.BudgetBasis))
                AddValidationError(errors, "budgetBasis", "Budget Basis is required when Budget Required is Yes.");
            if (string.IsNullOrWhiteSpace(aggregate.BudgetOwner))
                AddValidationError(errors, "budgetOwner", "Budget Owner is required when Budget Required is Yes.");
            if (string.IsNullOrWhiteSpace(aggregate.ApprovalRoute))
                AddValidationError(errors, "approvalRoute", "Approval Route is required when Budget Required is Yes.");
        }

        if (aggregate.BudgetRequired == false && string.IsNullOrWhiteSpace(aggregate.NoBudgetReason))
            AddValidationError(errors, "noBudgetReason", "No-Budget Reason is required when Budget Required is No.");

        return errors;
    }

    private async Task EmitProjectAuditAsync(
        ProjectStrategyLinkAggregate? existing,
        ProjectStrategyLinkAggregate next,
        string? templateApplicationMode,
        string actor,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (existing is null)
        {
            await _audit.WriteMutationAsync(actor, "Project", next.ProjectId, EnterpriseStrategyEventNames.ProjectCreated, correlationId, DeliveryExecutionManagementModules.Projects, "", next.ProjectName, cancellationToken);
            await _audit.WriteMutationAsync(actor, "Project", next.ProjectId, EnterpriseStrategyEventNames.ProjectAnchorChanged, correlationId, DeliveryExecutionManagementModules.Projects, "", next.ParentInitiativeId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(next.SourceTemplateId))
                await _audit.WriteMutationAsync(actor, "Project", next.ProjectId, EnterpriseStrategyEventNames.ProjectTemplateApplied, correlationId, DeliveryExecutionManagementModules.Projects, "", next.SourceTemplateId ?? string.Empty, cancellationToken);
            if (!string.IsNullOrWhiteSpace(next.Status))
                await _audit.WriteMutationAsync(actor, "Project", next.ProjectId, EnterpriseStrategyEventNames.ProjectStatusChanged, correlationId, DeliveryExecutionManagementModules.Projects, "Draft", next.Status, cancellationToken);
            if (HasBudgetSignal(next))
                await _audit.WriteMutationAsync(actor, "Project", next.ProjectId, EnterpriseStrategyEventNames.ProjectBudgetChanged, correlationId, DeliveryExecutionManagementModules.Projects, "", DescribeBudget(next), cancellationToken);
            return;
        }

        await _audit.WriteMutationAsync(actor, "Project", next.ProjectId, EnterpriseStrategyEventNames.ProjectStrategyUpdated, correlationId, DeliveryExecutionManagementModules.Projects, existing.ProjectName, next.ProjectName, cancellationToken);

        if (!string.Equals(existing.ParentInitiativeId, next.ParentInitiativeId, StringComparison.OrdinalIgnoreCase))
            await _audit.WriteMutationAsync(actor, "Project", next.ProjectId, EnterpriseStrategyEventNames.ProjectAnchorChanged, correlationId, DeliveryExecutionManagementModules.Projects, existing.ParentInitiativeId, next.ParentInitiativeId, cancellationToken);

        if (string.Equals(templateApplicationMode, "Cleared", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(existing.SourceTemplateId) &&
            string.IsNullOrWhiteSpace(next.SourceTemplateId))
        {
            await _audit.WriteMutationAsync(actor, "Project", next.ProjectId, EnterpriseStrategyEventNames.ProjectTemplateCleared, correlationId, DeliveryExecutionManagementModules.Projects, existing.SourceTemplateId ?? string.Empty, "", cancellationToken);
        }
        else if (string.Equals(templateApplicationMode, "Reapplied", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(next.SourceTemplateId))
        {
            await _audit.WriteMutationAsync(actor, "Project", next.ProjectId, EnterpriseStrategyEventNames.ProjectTemplateReapplied, correlationId, DeliveryExecutionManagementModules.Projects, next.SourceTemplateId ?? string.Empty, next.SourceTemplateId ?? string.Empty, cancellationToken);
        }
        else if (string.IsNullOrWhiteSpace(existing.SourceTemplateId) && !string.IsNullOrWhiteSpace(next.SourceTemplateId))
            await _audit.WriteMutationAsync(actor, "Project", next.ProjectId, EnterpriseStrategyEventNames.ProjectTemplateApplied, correlationId, DeliveryExecutionManagementModules.Projects, "", next.SourceTemplateId ?? string.Empty, cancellationToken);
        else if (!string.Equals(existing.SourceTemplateId, next.SourceTemplateId, StringComparison.OrdinalIgnoreCase))
            await _audit.WriteMutationAsync(actor, "Project", next.ProjectId, EnterpriseStrategyEventNames.ProjectTemplateChanged, correlationId, DeliveryExecutionManagementModules.Projects, existing.SourceTemplateId ?? string.Empty, next.SourceTemplateId ?? string.Empty, cancellationToken);

        if (!string.Equals(existing.Status, next.Status, StringComparison.OrdinalIgnoreCase))
            await _audit.WriteMutationAsync(actor, "Project", next.ProjectId, EnterpriseStrategyEventNames.ProjectStatusChanged, correlationId, DeliveryExecutionManagementModules.Projects, existing.Status, next.Status, cancellationToken);

        if (IsBudgetChanged(existing, next))
            await _audit.WriteMutationAsync(actor, "Project", next.ProjectId, EnterpriseStrategyEventNames.ProjectBudgetChanged, correlationId, DeliveryExecutionManagementModules.Projects, DescribeBudget(existing), DescribeBudget(next), cancellationToken);
    }

    private async Task<IReadOnlyList<ProjectCreationTemplateDto>> BuildCompatibleProjectTemplatesAsync(string parentType, string entityScope, CancellationToken cancellationToken)
    {
        _ = entityScope;
        if (string.IsNullOrWhiteSpace(parentType))
            return Array.Empty<ProjectCreationTemplateDto>();

        var goalTemplates = (await _library.ListGoalTemplatesAsync(cancellationToken)).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var initiativeTemplates = (await _library.ListInitiativeTemplatesAsync(cancellationToken)).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var templates = await _library.ListProjectTemplatesAsync(cancellationToken);
        var compatible = new List<ProjectTemplate>();

        foreach (var template in templates)
        {
            var templateParentType = ResolveTemplateParentType(template, initiativeTemplates, goalTemplates);
            if (!string.Equals(NormalizeText(templateParentType), NormalizeText(parentType), StringComparison.Ordinal))
                continue;

            compatible.Add(template);
        }

        var selected = compatible.Where(x => IsActiveTemplateStatus(x.LifecycleStatus)).ToList();
        if (selected.Count == 0)
            selected = compatible.Where(x => IsDraftFallbackTemplateStatus(x.LifecycleStatus)).ToList();

        var results = new List<ProjectCreationTemplateDto>();
        foreach (var template in selected)
        {
            var templateParentType = ResolveTemplateParentType(template, initiativeTemplates, goalTemplates);

            var metric = (await _library.ListProjectTemplateMetricsAsync(template.Id, cancellationToken))
                .OrderBy(x => x.DisplayOrder)
                .FirstOrDefault();

            results.Add(new ProjectCreationTemplateDto
            {
                TemplateId = template.Id,
                Name = template.Name,
                Description = template.Description,
                ParentType = templateParentType,
                EntityScope = template.EntityScope,
                LifecycleStatus = template.LifecycleStatus,
                Version = template.Version,
                OwnerPm = template.OwnerPm,
                Sponsor = template.Sponsor,
                Status = template.Status,
                Phase = template.Phase,
                DeliveryType = template.DeliveryType,
                DeliveryMethodology = template.DeliveryMethodology,
                ComplexitySize = template.ComplexitySize,
                ReportingCadence = template.ReportingCadence,
                RiskRating = template.RiskRating,
                ReadinessStatus = template.ReadinessStatus,
                ScopeSummaryTemplate = FirstText(template.ScopeSummaryTemplate, template.Description),
                ApprovalRoute = FirstText(template.ApprovalRoute, template.DecisionReference),
                BudgetType = template.BudgetType,
                BudgetBasis = template.BudgetBasis,
                FundingSource = template.FundingSource,
                CostCenter = template.CostCenter,
                SuccessMetric = metric?.SuccessMetric ?? string.Empty,
                MetricBaseline = metric?.BaselineValue.ToString() ?? string.Empty,
                MetricTarget = metric?.TargetValue.ToString() ?? string.Empty
            });
        }

        return results
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<ProjectTemplate?> ResolveCompatibleTemplateAsync(string? templateId, string parentType, string entityScope, CancellationToken cancellationToken)
    {
        _ = entityScope;
        if (string.IsNullOrWhiteSpace(templateId))
            return null;

        var template = await _library.GetProjectTemplateAsync(templateId.Trim(), cancellationToken);
        if (template is null)
            return null;

        InitiativeTemplate? initiativeTemplate = null;
        if (!string.IsNullOrWhiteSpace(template.ParentInitiativeTemplateId))
            initiativeTemplate = await _library.GetInitiativeTemplateAsync(template.ParentInitiativeTemplateId, cancellationToken);
        GoalTemplate? goalTemplate = null;
        if (initiativeTemplate is null && !string.IsNullOrWhiteSpace(template.ParentGoalTemplateId))
            goalTemplate = await _library.GetGoalTemplateAsync(template.ParentGoalTemplateId ?? string.Empty, cancellationToken);
        var templateParentType = FirstText(template.NormalizedParentType, initiativeTemplate?.Type, initiativeTemplate?.NormalizedType, goalTemplate?.Category);
        if (!string.Equals(NormalizeText(templateParentType), NormalizeText(parentType), StringComparison.Ordinal))
            return null;

        if (IsActiveTemplateStatus(template.LifecycleStatus))
            return template;

        if (!IsDraftFallbackTemplateStatus(template.LifecycleStatus))
            return null;

        var goalTemplates = (await _library.ListGoalTemplatesAsync(cancellationToken)).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var initiativeTemplates = (await _library.ListInitiativeTemplatesAsync(cancellationToken)).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var templates = await _library.ListProjectTemplatesAsync(cancellationToken);
        var hasActiveMatch = templates.Any(candidate =>
        {
            if (!IsActiveTemplateStatus(candidate.LifecycleStatus))
                return false;
            var candidateParentType = ResolveTemplateParentType(candidate, initiativeTemplates, goalTemplates);
            return string.Equals(NormalizeText(candidateParentType), NormalizeText(parentType), StringComparison.Ordinal);
        });

        if (hasActiveMatch)
            return null;

        return template;
    }

    private async Task<List<PpmProjectReadModelDto>> GetPpmProjectsSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _ppmProjects.ListAsync(1, 1000, cancellationToken);
            await _projectCache.UpsertManyAsync(rows.Select(x => new PpmProjectReadModelAggregate
            {
                ProjectId = x.ProjectId,
                ProjectName = x.ProjectName,
                Description = x.Description,
                OwnerPm = x.OwnerPm,
                Sponsor = x.Sponsor,
                Status = x.Status,
                Phase = x.Phase,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                DeliveryType = x.DeliveryType,
                SuccessMetric = x.SuccessMetric,
                RiskRating = x.RiskRating,
                ReadinessStatus = x.ReadinessStatus,
                BudgetSummary = x.BudgetSummary,
                SourceSystem = x.SourceSystem,
                SourceUpdatedAt = x.SourceUpdatedAt,
                CachedAt = DateTime.UtcNow
            }).ToList(), cancellationToken);

            return rows.Select(x => new PpmProjectReadModelDto
            {
                ProjectId = x.ProjectId,
                ProjectName = x.ProjectName,
                Description = x.Description,
                OwnerPm = x.OwnerPm,
                Sponsor = x.Sponsor,
                Status = x.Status,
                Phase = x.Phase,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                DeliveryType = x.DeliveryType,
                SuccessMetric = x.SuccessMetric,
                RiskRating = x.RiskRating,
                ReadinessStatus = x.ReadinessStatus,
                BudgetSummary = x.BudgetSummary,
                SourceSystem = x.SourceSystem,
                SourceUpdatedAt = x.SourceUpdatedAt,
                CachedAt = DateTime.UtcNow
            }).ToList();
        }
        catch
        {
            var cached = await _projectCache.ListAsync(cancellationToken);
            return cached.Select(x => new PpmProjectReadModelDto
            {
                ProjectId = x.ProjectId,
                ProjectName = x.ProjectName,
                Description = x.Description,
                OwnerPm = x.OwnerPm,
                Sponsor = x.Sponsor,
                Status = x.Status,
                Phase = x.Phase,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                DeliveryType = x.DeliveryType,
                SuccessMetric = x.SuccessMetric,
                RiskRating = x.RiskRating,
                ReadinessStatus = x.ReadinessStatus,
                BudgetSummary = x.BudgetSummary,
                SourceSystem = x.SourceSystem,
                SourceUpdatedAt = x.SourceUpdatedAt,
                CachedAt = x.CachedAt,
                DegradedMode = true
            }).ToList();
        }
    }

    private async Task<PpmProjectReadModelDto?> GetProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        var all = await GetPpmProjectsSafeAsync(cancellationToken);
        return all.FirstOrDefault(x => string.Equals(x.ProjectId, projectId, StringComparison.OrdinalIgnoreCase));
    }

    private static ProjectStrategyLinkViewDto BuildProjectView(
        PpmProjectReadModelDto? ppm,
        ProjectStrategyLinkAggregate? link,
        InitiativeStrategyLinkAggregate? initiative,
        ObjectiveAggregate? objective,
        GoalAggregate? goal)
    {
        var view = link?.ToViewDto() ?? new ProjectStrategyLinkViewDto
        {
            ProjectId = ppm?.ProjectId ?? string.Empty,
            SourceSystem = ppm?.SourceSystem ?? LocalProjectSource,
            SourceRecordId = ppm?.ProjectId ?? string.Empty,
            StrategyLinkStatus = link is null ? "Unlinked" : "Linked",
            SyncFreshness = ppm?.DegradedMode == true ? "Degraded" : "Fresh"
        };

        view.ProjectName = FirstText(view.ProjectName, ppm?.ProjectName);
        view.Description = FirstText(view.Description, ppm?.Description);
        view.OwnerPm = FirstText(view.OwnerPm, ppm?.OwnerPm);
        view.Sponsor = FirstText(view.Sponsor, ppm?.Sponsor);
        view.Status = FirstText(view.Status, ppm?.Status, "Draft");
        view.Phase = FirstText(view.Phase, ppm?.Phase);
        view.DeliveryType = FirstText(view.DeliveryType, ppm?.DeliveryType);
        view.StartDate ??= ppm?.StartDate;
        view.EndDate ??= ppm?.EndDate;
        view.SuccessMetric = FirstText(view.SuccessMetric, ppm?.SuccessMetric);
        view.RiskRating = FirstText(view.RiskRating, ppm?.RiskRating);
        view.ReadinessStatus = FirstText(view.ReadinessStatus, ppm?.ReadinessStatus);
        view.ParentInitiativeName = FirstText(view.ParentInitiativeName, initiative?.InitiativeName);
        view.ParentObjectiveName = FirstText(view.ParentObjectiveName, objective?.Name);
        view.ParentGoalName = FirstText(view.ParentGoalName, goal?.Name);
        view.ParentType = FirstText(view.ParentType, ResolveProjectParentType(initiative, goal));
        view.EntityScope = FirstText(view.EntityScope, initiative?.EntityScope, objective?.EntityScope, goal?.EntityScope);
        view.BudgetSummary = FirstText(view.BudgetSummary, BuildBudgetSummary(view.BudgetRequired, view.BudgetAmount, view.CurrencyCode, view.BudgetType, view.BudgetBasis, view.NoBudgetReason), ppm?.BudgetSummary);
        view.SyncFreshness = ppm?.DegradedMode == true ? "Degraded" : view.SyncFreshness;
        view.Warnings = BuildProjectWarnings(ppm, link);
        return view;
    }

    private static IReadOnlyList<string> BuildProjectWarnings(PpmProjectReadModelDto? project, ProjectStrategyLinkAggregate? link)
    {
        var warnings = new List<string>();
        if (link is null && string.Equals(project?.Status, "Active", StringComparison.OrdinalIgnoreCase))
            warnings.Add("Project is active but has no anchored Parent Initiative.");
        if (link is not null && string.IsNullOrWhiteSpace(link.MetricBindingsJson))
            warnings.Add("Success metric is not mapped upstream.");
        if (link is not null && IsNonDraftStatus(link.Status))
        {
            if (!link.BudgetRequired.HasValue)
                warnings.Add("Budget governance is incomplete for the current non-draft status.");
            else if (link.BudgetRequired == true)
            {
                if (!link.BudgetAmount.HasValue || link.BudgetAmount.Value <= 0)
                    warnings.Add("Budget Amount is still missing for a budget-required non-draft Project.");
                if (string.IsNullOrWhiteSpace(link.CurrencyCode))
                    warnings.Add("Currency is still missing for a budget-required non-draft Project.");
                if (string.IsNullOrWhiteSpace(link.BudgetType) || string.IsNullOrWhiteSpace(link.BudgetBasis))
                    warnings.Add("Budget Type and Budget Basis must be governed before this Project can progress beyond Draft.");
                if (string.IsNullOrWhiteSpace(link.BudgetOwner) || string.IsNullOrWhiteSpace(link.ApprovalRoute))
                    warnings.Add("Budget Owner and Approval Route must be governed before this Project can progress beyond Draft.");
            }
            else if (string.IsNullOrWhiteSpace(link.NoBudgetReason))
            {
                warnings.Add("No-Budget Reason is required when a non-draft Project is marked as not requiring budget.");
            }
        }
        if (project?.DegradedMode == true)
            warnings.Add("PPM dependency unavailable; showing cached project data.");
        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string ResolveEntityScope(
        InitiativeStrategyLinkAggregate initiative,
        ObjectiveAggregate objective,
        GoalAggregate goal)
        => FirstText(initiative.EntityScope, objective.EntityScope, goal.EntityScope);

    private static string ResolveProjectParentType(InitiativeStrategyLinkAggregate? initiative, GoalAggregate? goal)
        => FirstText(initiative?.Type, initiative?.NormalizedType, goal?.Category);

    private static string ResolveTemplateParentType(
        ProjectTemplate template,
        IReadOnlyDictionary<string, InitiativeTemplate> initiativeTemplates,
        IReadOnlyDictionary<string, GoalTemplate> goalTemplates)
    {
        if (!string.IsNullOrWhiteSpace(template.NormalizedParentType))
            return template.NormalizedParentType.Trim();

        if (initiativeTemplates.TryGetValue(template.ParentInitiativeTemplateId ?? string.Empty, out var initiativeTemplate))
            return FirstText(initiativeTemplate.Type, initiativeTemplate.NormalizedType);

        if (goalTemplates.TryGetValue(template.ParentGoalTemplateId ?? string.Empty, out var goalTemplate))
            return (goalTemplate.Category ?? string.Empty).Trim();

        return string.Empty;
    }

    private static string NormalizeCreationMode(string? creationMode, string? templateId)
    {
        var value = (creationMode ?? string.Empty).Trim();
        if (string.Equals(value, "From Project Template", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "Template", StringComparison.OrdinalIgnoreCase))
            return "Template";
        if (string.IsNullOrWhiteSpace(value))
            return string.IsNullOrWhiteSpace(templateId) ? "Blank" : "Template";
        return value;
    }

    private static bool IsTemplateMode(string? creationMode) =>
        string.Equals(NormalizeCreationMode(creationMode, null), "Template", StringComparison.OrdinalIgnoreCase);

    private static bool IsNonDraftStatus(string? status)
    {
        var normalized = NormalizeText(status);
        return NonDraftStatuses.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeText(params string?[] values) =>
        string.Concat(FirstText(values).Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private static string FirstText(params string?[] values) =>
        values.Select(x => (x ?? string.Empty).Trim()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static bool IsActiveTemplateStatus(string? status)
    {
        var normalized = NormalizeText(status);
        return normalized is "published" or "active" or "approved" or "released" or "live";
    }

    private static bool IsDraftFallbackTemplateStatus(string? status)
    {
        var normalized = NormalizeText(status);
        return string.IsNullOrWhiteSpace(normalized) || normalized == "draft";
    }

    private static void AddValidationError(Dictionary<string, List<string>> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var list))
        {
            list = new List<string>();
            errors[key] = list;
        }

        if (!list.Contains(message, StringComparer.OrdinalIgnoreCase))
            list.Add(message);
    }

    private static string DescribeBudget(ProjectStrategyLinkAggregate aggregate)
        => $"required={aggregate.BudgetRequired};amount={aggregate.BudgetAmount};currency={aggregate.CurrencyCode};type={aggregate.BudgetType};basis={aggregate.BudgetBasis};owner={aggregate.BudgetOwner};route={aggregate.ApprovalRoute};reason={aggregate.NoBudgetReason}";

    private static bool HasBudgetSignal(ProjectStrategyLinkAggregate aggregate)
        => aggregate.BudgetRequired.HasValue
           || aggregate.BudgetAmount.HasValue
           || !string.IsNullOrWhiteSpace(aggregate.CurrencyCode)
           || !string.IsNullOrWhiteSpace(aggregate.BudgetType)
           || !string.IsNullOrWhiteSpace(aggregate.BudgetBasis)
           || !string.IsNullOrWhiteSpace(aggregate.BudgetOwner)
           || !string.IsNullOrWhiteSpace(aggregate.ApprovalRoute)
           || !string.IsNullOrWhiteSpace(aggregate.FundingSource)
           || !string.IsNullOrWhiteSpace(aggregate.CostCenter)
           || !string.IsNullOrWhiteSpace(aggregate.NoBudgetReason);

    private static string BuildBudgetSummary(
        bool? budgetRequired,
        decimal? budgetAmount,
        string? currencyCode,
        string? budgetType,
        string? budgetBasis,
        string? noBudgetReason)
    {
        if (budgetRequired == false)
            return string.IsNullOrWhiteSpace(noBudgetReason)
                ? "No budget required"
                : $"No budget required: {noBudgetReason.Trim()}";

        if (budgetRequired == true)
        {
            var parts = new List<string>();
            if (budgetAmount.HasValue)
                parts.Add($"{(currencyCode ?? string.Empty).Trim()} {budgetAmount.Value:N2}".Trim());
            if (!string.IsNullOrWhiteSpace(budgetType))
                parts.Add(budgetType.Trim());
            if (!string.IsNullOrWhiteSpace(budgetBasis))
                parts.Add(budgetBasis.Trim());
            return parts.Count > 0 ? string.Join(" | ", parts) : "Budget required";
        }

        return "Pending budget decision";
    }

    private static bool IsBudgetChanged(ProjectStrategyLinkAggregate existing, ProjectStrategyLinkAggregate next)
        => existing.BudgetRequired != next.BudgetRequired
           || existing.BudgetAmount != next.BudgetAmount
           || !string.Equals(existing.CurrencyCode, next.CurrencyCode, StringComparison.OrdinalIgnoreCase)
           || !string.Equals(existing.BudgetType, next.BudgetType, StringComparison.OrdinalIgnoreCase)
           || !string.Equals(existing.BudgetBasis, next.BudgetBasis, StringComparison.OrdinalIgnoreCase)
           || !string.Equals(existing.FundingSource, next.FundingSource, StringComparison.OrdinalIgnoreCase)
           || !string.Equals(existing.CostCenter, next.CostCenter, StringComparison.OrdinalIgnoreCase)
           || !string.Equals(existing.BudgetOwner, next.BudgetOwner, StringComparison.OrdinalIgnoreCase)
           || !string.Equals(existing.ApprovalRoute, next.ApprovalRoute, StringComparison.OrdinalIgnoreCase)
           || !string.Equals(existing.FinancialNotes, next.FinancialNotes, StringComparison.OrdinalIgnoreCase)
           || !string.Equals(existing.NoBudgetReason, next.NoBudgetReason, StringComparison.OrdinalIgnoreCase);

    private string NextProjectId() => $"PRJ-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}