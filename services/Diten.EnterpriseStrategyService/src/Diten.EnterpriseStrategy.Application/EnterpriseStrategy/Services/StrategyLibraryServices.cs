using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Repositories;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Domain.Aggregates.EnterpriseStrategy;
using System.Text.Json;

namespace Diten.Application.EnterpriseStrategy.Services;

public interface IStrategyLibraryImportService
{
    Task<Response<StrategyLibraryImportBatchDto>> ImportAsync(StrategyLibraryImportPayloadDto payload, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<StrategyLibraryImportBatchDto>> GetImportBatchAsync(string batchId, CancellationToken cancellationToken = default);
    Task<Response<StrategyLibraryImportBatchDto>> ApproveImportAsync(string batchId, string actor, string correlationId, CancellationToken cancellationToken = default);
}

public interface IStrategyLibraryQueryService
{
    Task<Response<PagedResponseDto<StrategyLibraryCatalogItemDto>>> CatalogAsync(StrategyLibraryCatalogRequestDto request, CancellationToken cancellationToken = default);
    Task<Response<PagedResponseDto<ProjectLibraryRowDto>>> ProjectsLibraryAsync(ProjectLibraryCatalogRequestDto request, CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<ProjectTemplateMetricDto>>> ProjectLibraryMetricsAsync(string projectTemplateId, CancellationToken cancellationToken = default);
    Task<Response<StrategyTemplateDetailDto>> GetTemplateAsync(string id, CancellationToken cancellationToken = default);
    Task<Response<StrategyBlueprintDetailDto>> GetBlueprintAsync(string id, CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<StrategyTemplateVersionDto>>> GetTemplateVersionsAsync(string id, CancellationToken cancellationToken = default);
}

public interface IStrategyLibraryGovernanceService
{
    Task<Response<bool>> SubmitReviewTemplateAsync(string id, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<bool>> ApproveTemplateAsync(string id, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<bool>> PublishTemplateAsync(string id, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<bool>> RetireTemplateAsync(string id, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<bool>> PublishBlueprintAsync(string id, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<bool>> RetireBlueprintAsync(string id, string actor, string correlationId, CancellationToken cancellationToken = default);
}

public interface IStrategyInstantiationService
{
    Task<Response<StrategyInstantiationResultDto>> InstantiateTemplateAsync(string templateId, StrategyTemplateInstantiateRequestDto request, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<StrategyInstantiationResultDto>> InstantiateBlueprintAsync(string blueprintId, StrategyBlueprintInstantiateRequestDto request, string actor, string correlationId, CancellationToken cancellationToken = default);
}

public interface IStrategyLibraryUsageService
{
    Task<Response<StrategyLibraryUsageSummaryDto>> SummaryAsync(CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<StrategyLibraryUsageItemDto>>> TemplateUsageAsync(CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<StrategyLibraryUsageItemDto>>> BlueprintUsageAsync(CancellationToken cancellationToken = default);
}

public sealed class StrategyLibraryService :
    IStrategyLibraryImportService,
    IStrategyLibraryQueryService,
    IStrategyLibraryGovernanceService,
    IStrategyInstantiationService,
    IStrategyLibraryUsageService
{
    private static readonly string[] RequiredSheets = { "Goals_List", "Objectives_List", "Initiatives_List", "Projects_List", "Connection_Map" };
    private static readonly SemaphoreSlim EmbeddedSeedGate = new(1, 1);
    private static bool _embeddedSeedApplied;
    private static bool _embeddedProjectsSeedApplied;
    private const string EmbeddedSeedPath = "/Users/natig/Downloads/Cursor_Prompt_Goals_Objectives_Initiatives_Embedded_Seed.md";
    private const string EmbeddedProjectsSeedPath = "/Users/natig/Downloads/Cursor_Prompt_Projects_Library_Embedded_Seed.md";
    private readonly IStrategyLibraryRepository _library;
    private readonly IGoalRepository _goals;
    private readonly IObjectiveRepository _objectives;
    private readonly IInitiativeStrategyLinkRepository _initiativeLinks;
    private readonly IProjectStrategyLinkRepository _projectLinks;
    private readonly IPpmInitiativeCacheRepository _initiativeCache;
    private readonly IPpmProjectCacheRepository _projectCache;
    private readonly IEnterpriseStrategyAuditSink _audit;

    public StrategyLibraryService(
        IStrategyLibraryRepository library,
        IGoalRepository goals,
        IObjectiveRepository objectives,
        IInitiativeStrategyLinkRepository initiativeLinks,
        IProjectStrategyLinkRepository projectLinks,
        IPpmInitiativeCacheRepository initiativeCache,
        IPpmProjectCacheRepository projectCache,
        IEnterpriseStrategyAuditSink audit)
    {
        _library = library;
        _goals = goals;
        _objectives = objectives;
        _initiativeLinks = initiativeLinks;
        _projectLinks = projectLinks;
        _initiativeCache = initiativeCache;
        _projectCache = projectCache;
        _audit = audit;
    }

    public async Task<Response<StrategyLibraryImportBatchDto>> ImportAsync(StrategyLibraryImportPayloadDto payload, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var issues = new List<TemplateImportIssue>();
        var missingSheets = RequiredSheets.Where(s => !payload.Sheets.ContainsKey(s)).ToList();
        if (missingSheets.Count > 0)
        {
            foreach (var sheet in missingSheets)
            {
                issues.Add(NewIssue("", "Fatal", sheet, 0, "MISSING_SHEET", $"Required sheet '{sheet}' is missing."));
            }

            var failed = new TemplateImportBatch
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(payload.BatchName) ? $"Import {DateTime.UtcNow:yyyyMMddHHmmss}" : payload.BatchName.Trim(),
                Status = "Failed",
                ImportedAt = DateTime.UtcNow,
                ImportedBy = actor
            };
            await _library.UpsertImportBatchAsync(failed, cancellationToken);
            foreach (var issue in issues) issue.BatchId = failed.Id;
            await _library.UpsertImportIssuesAsync(issues, cancellationToken);
            return Response<StrategyLibraryImportBatchDto>.Ok(ToImportBatchDto(failed, issues));
        }

        var batchId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var batch = new TemplateImportBatch
        {
            Id = batchId,
            Name = string.IsNullOrWhiteSpace(payload.BatchName) ? $"Import {now:yyyyMMddHHmmss}" : payload.BatchName.Trim(),
            Status = "Draft",
            ImportedAt = now,
            ImportedBy = actor
        };

        await _audit.WriteMutationAsync(actor, "StrategyLibraryImport", batchId, EnterpriseStrategyEventNames.LibraryImportStarted, correlationId, "enterprise-strategy.library", "", batch.Name, cancellationToken);

        var goalsRows = payload.Sheets["Goals_List"];
        var objectivesRows = payload.Sheets["Objectives_List"];
        var initiativesRows = payload.Sheets["Initiatives_List"];
        var projectsRows = payload.Sheets["Projects_List"];
        var connectionRows = payload.Sheets["Connection_Map"];
        var totalRowsRead = goalsRows.Count + objectivesRows.Count + initiativesRows.Count + projectsRows.Count + connectionRows.Count;

        var incomingGoals = NormalizeGoalTemplates(goalsRows, issues);
        var incomingObjectives = NormalizeObjectiveTemplates(objectivesRows, issues);
        var incomingInitiatives = NormalizeInitiativeTemplates(initiativesRows, issues);
        var incomingProjects = NormalizeProjectTemplates(projectsRows, issues);
        var incomingPacks = NormalizeBlueprintPacks(connectionRows, batchId, issues);

        var existingGoals = (await _library.ListGoalTemplatesAsync(cancellationToken)).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var existingObjectives = (await _library.ListObjectiveTemplatesAsync(cancellationToken)).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var existingInitiatives = (await _library.ListInitiativeTemplatesAsync(cancellationToken)).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var existingProjects = (await _library.ListProjectTemplatesAsync(cancellationToken)).ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        var versionConflicts = 0;
        foreach (var item in incomingGoals.Goals.ToList())
        {
            if (existingGoals.TryGetValue(item.Id, out var existing) && item.Version < existing.Version)
            {
                versionConflicts++;
                issues.Add(NewIssue(batchId, "Warning", "Goals_List", 0, "VERSION_CONFLICT", $"GoalTemplate '{item.Id}' incoming version {item.Version} < existing {existing.Version}; skipped."));
                incomingGoals.Goals.Remove(item);
                incomingGoals.Metrics.Remove(item.Id);
            }
        }
        foreach (var item in incomingObjectives.Objectives.ToList())
        {
            if (existingObjectives.TryGetValue(item.Id, out var existing) && item.Version < existing.Version)
            {
                versionConflicts++;
                issues.Add(NewIssue(batchId, "Warning", "Objectives_List", 0, "VERSION_CONFLICT", $"ObjectiveTemplate '{item.Id}' incoming version {item.Version} < existing {existing.Version}; skipped."));
                incomingObjectives.Objectives.Remove(item);
                incomingObjectives.Metrics.Remove(item.Id);
            }
        }
        foreach (var item in incomingInitiatives.Initiatives.ToList())
        {
            if (existingInitiatives.TryGetValue(item.Id, out var existing) && item.Version < existing.Version)
            {
                versionConflicts++;
                issues.Add(NewIssue(batchId, "Warning", "Initiatives_List", 0, "VERSION_CONFLICT", $"InitiativeTemplate '{item.Id}' incoming version {item.Version} < existing {existing.Version}; skipped."));
                incomingInitiatives.Initiatives.Remove(item);
                incomingInitiatives.Metrics.Remove(item.Id);
            }
        }
        foreach (var item in incomingProjects.Projects.ToList())
        {
            if (existingProjects.TryGetValue(item.Id, out var existing) && item.Version < existing.Version)
            {
                versionConflicts++;
                issues.Add(NewIssue(batchId, "Warning", "Projects_List", 0, "VERSION_CONFLICT", $"ProjectTemplate '{item.Id}' incoming version {item.Version} < existing {existing.Version}; skipped."));
                incomingProjects.Projects.Remove(item);
                incomingProjects.Metrics.Remove(item.Id);
            }
        }

        await _library.UpsertGoalTemplatesAsync(incomingGoals.Goals, cancellationToken);
        foreach (var kv in incomingGoals.Metrics)
            await _library.ReplaceGoalTemplateMetricsAsync(kv.Key, kv.Value, cancellationToken);

        await _library.UpsertObjectiveTemplatesAsync(incomingObjectives.Objectives, cancellationToken);
        foreach (var kv in incomingObjectives.Metrics)
            await _library.ReplaceObjectiveTemplateMetricsAsync(kv.Key, kv.Value, cancellationToken);

        await _library.UpsertInitiativeTemplatesAsync(incomingInitiatives.Initiatives, cancellationToken);
        foreach (var kv in incomingInitiatives.Metrics)
            await _library.ReplaceInitiativeTemplateMetricsAsync(kv.Key, kv.Value, cancellationToken);

        await _library.UpsertProjectTemplatesAsync(incomingProjects.Projects, cancellationToken);
        foreach (var kv in incomingProjects.Metrics)
            await _library.ReplaceProjectTemplateMetricsAsync(kv.Key, kv.Value, cancellationToken);

        await _library.UpsertBlueprintPacksAsync(incomingPacks.Packs, cancellationToken);
        foreach (var kv in incomingPacks.Items)
            await _library.ReplaceBlueprintPackItemsAsync(kv.Key, kv.Value, cancellationToken);

        batch.TotalRowsRead = totalRowsRead;
        batch.UniqueTemplatesCreated = incomingGoals.Goals.Count + incomingObjectives.Objectives.Count + incomingInitiatives.Initiatives.Count + incomingProjects.Projects.Count + incomingPacks.Packs.Count;
        batch.DuplicateRowsCollapsed = incomingGoals.DuplicatesCollapsed + incomingObjectives.DuplicatesCollapsed + incomingInitiatives.DuplicatesCollapsed + incomingProjects.DuplicatesCollapsed + incomingPacks.DuplicatesCollapsed;
        batch.InvalidParentReferences = issues.Count(x => x.Code == "INVALID_PARENT");
        batch.MissingIds = issues.Count(x => x.Code == "MISSING_ID");
        batch.RepeatedMetricsDetected = incomingGoals.RepeatedMetricRows + incomingObjectives.RepeatedMetricRows + incomingInitiatives.RepeatedMetricRows + incomingProjects.RepeatedMetricRows;
        batch.OrphanRows = issues.Count(x => x.Code == "ORPHAN_ROW");
        batch.VersionConflicts = versionConflicts;

        foreach (var issue in issues) issue.BatchId = batchId;
        await _library.UpsertImportBatchAsync(batch, cancellationToken);
        await _library.UpsertImportIssuesAsync(issues, cancellationToken);

        await _audit.WriteMutationAsync(actor, "StrategyLibraryImport", batchId, EnterpriseStrategyEventNames.LibraryImportCompleted, correlationId, "enterprise-strategy.library", "", $"rows={totalRowsRead};templates={batch.UniqueTemplatesCreated}", cancellationToken);
        return Response<StrategyLibraryImportBatchDto>.Ok(ToImportBatchDto(batch, issues));
    }

    public async Task<Response<StrategyLibraryImportBatchDto>> GetImportBatchAsync(string batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _library.GetImportBatchAsync(batchId, cancellationToken);
        if (batch is null) return Response<StrategyLibraryImportBatchDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        var issues = await _library.ListImportIssuesAsync(batchId, cancellationToken);
        return Response<StrategyLibraryImportBatchDto>.Ok(ToImportBatchDto(batch, issues));
    }

    public async Task<Response<StrategyLibraryImportBatchDto>> ApproveImportAsync(string batchId, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var batch = await _library.GetImportBatchAsync(batchId, cancellationToken);
        if (batch is null) return Response<StrategyLibraryImportBatchDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        batch.Status = "Approved";
        await _library.UpsertImportBatchAsync(batch, cancellationToken);
        var issues = await _library.ListImportIssuesAsync(batchId, cancellationToken);
        await _audit.WriteMutationAsync(actor, "StrategyLibraryImport", batchId, EnterpriseStrategyEventNames.LibraryImportApproved, correlationId, "enterprise-strategy.library", "", "Approved", cancellationToken);
        return Response<StrategyLibraryImportBatchDto>.Ok(ToImportBatchDto(batch, issues));
    }

    public async Task<Response<PagedResponseDto<StrategyLibraryCatalogItemDto>>> CatalogAsync(StrategyLibraryCatalogRequestDto request, CancellationToken cancellationToken = default)
    {
        await EnsureEmbeddedStrategyLibrarySeedsAsync(cancellationToken);
        var rows = new List<StrategyLibraryCatalogItemDto>();
        var objectiveTemplates = (await SafeListAsync(
            () => _library.ListObjectiveTemplatesAsync(cancellationToken),
            () => Array.Empty<ObjectiveTemplate>())).ToList();
        var objectiveTemplateNamesById = objectiveTemplates
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        var goalTemplates = await SafeListAsync(
            () => _library.ListGoalTemplatesAsync(cancellationToken),
            () => EnterpriseStrategyLibraryFallbackStore.ListGoalTemplates());
        rows.AddRange(goalTemplates.Select(x => new StrategyLibraryCatalogItemDto
        {
            ItemType = "Template",
            TemplateType = "Goal",
            Id = x.Id,
            Name = x.Name,
            Owner = x.Owner,
            Status = x.LifecycleStatus,
            Version = x.Version,
            CategoryOrType = x.Category,
            Category = x.Category,
            Statement = x.Statement,
            EntityScope = x.EntityScope,
            Priority = x.Priority,
            UpdatedAt = x.UpdatedAt
        }));
        rows.AddRange(objectiveTemplates.Select(x => new StrategyLibraryCatalogItemDto
        {
            ItemType = "Template",
            TemplateType = "Objective",
            Id = x.Id,
            Name = x.Name,
            Owner = x.Owner,
            Status = x.LifecycleStatus,
            Version = x.Version,
            CategoryOrType = x.Type,
            Category = x.Type,
            ParentGoalTemplateId = x.ParentGoalTemplateId,
            Statement = x.Statement,
            EntityScope = x.EntityScope,
            Priority = x.Priority,
            TimeHorizonStart = x.TimeHorizonStart,
            TimeHorizonEnd = x.TimeHorizonEnd,
            UpdatedAt = x.UpdatedAt
        }));
        rows.AddRange((await SafeListAsync(
            () => _library.ListInitiativeTemplatesAsync(cancellationToken),
            () => Array.Empty<InitiativeTemplate>())).Select(x => new StrategyLibraryCatalogItemDto
        {
            ItemType = "Template",
            TemplateType = "Initiative",
            Id = x.Id,
            Name = x.Name,
            Owner = x.Owner,
            Status = x.LifecycleStatus,
            Version = x.Version,
            CategoryOrType = x.Type,
            Category = x.Type,
            ParentObjectiveTemplateId = x.ParentObjectiveTemplateId,
            ParentObjectiveName = objectiveTemplateNamesById.TryGetValue(x.ParentObjectiveTemplateId ?? string.Empty, out var parentObjectiveName)
                ? parentObjectiveName
                : string.Empty,
            Statement = x.Description,
            EntityScope = x.EntityScope,
            Priority = x.Priority,
            UpdatedAt = x.UpdatedAt
        }));
        rows.AddRange((await SafeListAsync(
            () => _library.ListProjectTemplatesAsync(cancellationToken),
            () => Array.Empty<ProjectTemplate>())).Select(x => new StrategyLibraryCatalogItemDto
        {
            ItemType = "Template",
            TemplateType = "Project",
            Id = x.Id,
            Name = x.Name,
            Owner = x.OwnerPm,
            Status = x.LifecycleStatus,
            Version = x.Version,
            CategoryOrType = x.DeliveryType,
            Category = x.DeliveryType,
            ParentObjectiveTemplateId = x.ParentObjectiveTemplateId,
            ParentObjectiveName = objectiveTemplateNamesById.TryGetValue(x.ParentObjectiveTemplateId ?? string.Empty, out var parentObjectiveName)
                ? parentObjectiveName
                : string.Empty,
            Statement = x.Description,
            EntityScope = x.EntityScope,
            Priority = x.RiskRating,
            UpdatedAt = x.UpdatedAt
        }));
        rows.AddRange((await SafeListAsync(
            () => _library.ListBlueprintPacksAsync(cancellationToken),
            () => Array.Empty<StrategyBlueprintPack>())).Select(x => new StrategyLibraryCatalogItemDto
        {
            ItemType = "BlueprintPack",
            TemplateType = "BlueprintPack",
            Id = x.Id,
            Name = x.Name,
            Owner = x.Owner,
            Status = x.Status,
            Version = x.Version,
            CategoryOrType = "Pack",
            Category = "Pack",
            Statement = x.Description,
            EntityScope = x.EntityScope,
            Priority = x.Priority,
            UpdatedAt = x.UpdatedAt
        }));

        var usage = await SafeListAsync(
            () => _library.ListUsageStatsAsync(cancellationToken),
            () => Array.Empty<TemplateUsageStat>());
        var usageMap = usage.ToDictionary(x => $"{x.ItemType}:{x.ItemId}", StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (usageMap.TryGetValue($"{row.ItemType}:{row.Id}", out var stat))
                row.UsageCount = stat.UsageCount;
        }

        IEnumerable<StrategyLibraryCatalogItemDto> query = rows;
        if (!string.IsNullOrWhiteSpace(request.TemplateType))
            query = query.Where(x => string.Equals(x.TemplateType, request.TemplateType, StringComparison.OrdinalIgnoreCase));
        if (request.PublishedOnly)
            query = query.Where(x => string.Equals(x.Status, "Published", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => $"{x.Id} {x.Name} {x.Owner} {x.CategoryOrType} {x.Statement} {x.ParentGoalTemplateId}".Contains(request.Search, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(request.CategoryOrType))
            query = query.Where(x => string.Equals(x.CategoryOrType, request.CategoryOrType, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.Status))
            query = query.Where(x => string.Equals(x.Status, request.Status, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.ParentGoalTemplateId))
            query = query.Where(x => string.Equals(x.ParentGoalTemplateId, request.ParentGoalTemplateId, StringComparison.OrdinalIgnoreCase));

        if (request.Filters.TryGetValue("status", out var status))
            query = query.Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase));
        if (request.Filters.TryGetValue("owner", out var owner))
            query = query.Where(x => string.Equals(x.Owner, owner, StringComparison.OrdinalIgnoreCase));
        if (request.Filters.TryGetValue("entityScope", out var entityScope))
            query = query.Where(x => string.Equals(x.EntityScope, entityScope, StringComparison.OrdinalIgnoreCase));
        if (request.Filters.TryGetValue("priority", out var priority))
            query = query.Where(x => string.Equals(x.Priority, priority, StringComparison.OrdinalIgnoreCase));

        query = query.OrderByDescending(x => x.UpdatedAt);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 5000);
        var total = query.Count();
        var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Response<PagedResponseDto<StrategyLibraryCatalogItemDto>>.Ok(new PagedResponseDto<StrategyLibraryCatalogItemDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items
        });
    }

    public async Task<Response<PagedResponseDto<ProjectLibraryRowDto>>> ProjectsLibraryAsync(ProjectLibraryCatalogRequestDto request, CancellationToken cancellationToken = default)
    {
        await EnsureEmbeddedStrategyLibrarySeedsAsync(cancellationToken);
        var templates = await _library.ListProjectTemplatesAsync(cancellationToken);
        var counts = await _library.CountProjectTemplateMetricsByProjectAsync(cancellationToken);
        IEnumerable<ProjectLibraryRowDto> query = templates.Select(p => new ProjectLibraryRowDto
        {
            ProjectId = p.Id,
            Name = p.Name,
            OwnerPm = p.OwnerPm,
            Sponsor = p.Sponsor,
            Status = p.Status,
            Phase = p.Phase,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            DeliveryType = p.DeliveryType,
            EntityScope = p.EntityScope,
            RiskRating = p.RiskRating,
            ReadinessStatus = p.ReadinessStatus,
            Version = p.Version,
            MetricCount = counts.TryGetValue(p.Id, out var c) ? c : 0,
            LifecycleStatus = p.LifecycleStatus,
            UpdatedAt = p.UpdatedAt
        });

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(x =>
                x.ProjectId.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                x.OwnerPm.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                x.Sponsor.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                x.DeliveryType.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                x.EntityScope.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.ProjectStatus))
            query = query.Where(x => string.Equals(x.Status, request.ProjectStatus.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.Phase))
            query = query.Where(x => string.Equals(x.Phase, request.Phase.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.OwnerPm))
            query = query.Where(x => string.Equals(x.OwnerPm, request.OwnerPm.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.Sponsor))
            query = query.Where(x => string.Equals(x.Sponsor, request.Sponsor.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.DeliveryType))
            query = query.Where(x => string.Equals(x.DeliveryType, request.DeliveryType.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.EntityScope))
            query = query.Where(x => string.Equals(x.EntityScope, request.EntityScope.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.RiskRating))
            query = query.Where(x => string.Equals(x.RiskRating, request.RiskRating.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(request.ReadinessStatus))
            query = query.Where(x => string.Equals(x.ReadinessStatus, request.ReadinessStatus.Trim(), StringComparison.OrdinalIgnoreCase));
        if (request.Version is { } ver)
            query = query.Where(x => x.Version == ver);

        var list = query.ToList();
        var desc = !string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var sortKey = (request.SortBy ?? string.Empty).Trim().ToLowerInvariant();
        IEnumerable<ProjectLibraryRowDto> ordered = sortKey switch
        {
            "projectid" => desc ? list.OrderByDescending(x => x.ProjectId) : list.OrderBy(x => x.ProjectId),
            "name" => desc ? list.OrderByDescending(x => x.Name) : list.OrderBy(x => x.Name),
            "status" => desc ? list.OrderByDescending(x => x.Status) : list.OrderBy(x => x.Status),
            "phase" => desc ? list.OrderByDescending(x => x.Phase) : list.OrderBy(x => x.Phase),
            "ownerpm" => desc ? list.OrderByDescending(x => x.OwnerPm) : list.OrderBy(x => x.OwnerPm),
            "sponsor" => desc ? list.OrderByDescending(x => x.Sponsor) : list.OrderBy(x => x.Sponsor),
            "deliverytype" => desc ? list.OrderByDescending(x => x.DeliveryType) : list.OrderBy(x => x.DeliveryType),
            "entityscope" => desc ? list.OrderByDescending(x => x.EntityScope) : list.OrderBy(x => x.EntityScope),
            "riskrating" => desc ? list.OrderByDescending(x => x.RiskRating) : list.OrderBy(x => x.RiskRating),
            "readinessstatus" => desc ? list.OrderByDescending(x => x.ReadinessStatus) : list.OrderBy(x => x.ReadinessStatus),
            "version" => desc ? list.OrderByDescending(x => x.Version) : list.OrderBy(x => x.Version),
            "metriccount" => desc ? list.OrderByDescending(x => x.MetricCount) : list.OrderBy(x => x.MetricCount),
            "startdate" => desc ? list.OrderByDescending(x => x.StartDate) : list.OrderBy(x => x.StartDate),
            "enddate" => desc ? list.OrderByDescending(x => x.EndDate) : list.OrderBy(x => x.EndDate),
            _ => desc ? list.OrderByDescending(x => x.UpdatedAt) : list.OrderBy(x => x.UpdatedAt)
        };
        var sorted = ordered.ToList();

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);
        var total = sorted.Count;
        var items = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Response<PagedResponseDto<ProjectLibraryRowDto>>.Ok(new PagedResponseDto<ProjectLibraryRowDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items
        });
    }

    public async Task<Response<IReadOnlyList<ProjectTemplateMetricDto>>> ProjectLibraryMetricsAsync(string projectTemplateId, CancellationToken cancellationToken = default)
    {
        await EnsureEmbeddedStrategyLibrarySeedsAsync(cancellationToken);
        var template = await _library.GetProjectTemplateAsync(projectTemplateId, cancellationToken);
        if (template is null)
            return Response<IReadOnlyList<ProjectTemplateMetricDto>>.Fail(EnterpriseStrategyErrorCodes.NotFound);

        var metrics = await _library.ListProjectTemplateMetricsAsync(projectTemplateId, cancellationToken);
        var dtos = metrics
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.SuccessMetric, StringComparer.OrdinalIgnoreCase)
            .Select(x => new ProjectTemplateMetricDto
            {
                Id = x.Id,
                ProjectTemplateId = x.ProjectTemplateId,
                SuccessMetric = x.SuccessMetric,
                MetricType = x.MetricType,
                BaselineValue = x.BaselineValue,
                TargetValue = x.TargetValue,
                UnitOfMeasure = x.UnitOfMeasure,
                AggregationMethod = x.AggregationMethod,
                DisplayOrder = x.DisplayOrder
            })
            .ToList();
        return Response<IReadOnlyList<ProjectTemplateMetricDto>>.Ok(dtos);
    }

    public async Task<Response<StrategyTemplateDetailDto>> GetTemplateAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureEmbeddedStrategyLibrarySeedsAsync(cancellationToken);
        var goal = await SafeGetAsync(
            () => _library.GetGoalTemplateAsync(id, cancellationToken),
            () => EnterpriseStrategyLibraryFallbackStore.GetGoalTemplate(id));
        if (goal is not null)
        {
            var metrics = await SafeListAsync(
                () => _library.ListGoalTemplateMetricsAsync(id, cancellationToken),
                () => EnterpriseStrategyLibraryFallbackStore.ListGoalTemplateMetrics(id));
            return Response<StrategyTemplateDetailDto>.Ok(new StrategyTemplateDetailDto
            {
                TemplateType = "Goal",
                Id = goal.Id,
                Name = goal.Name,
                Owner = goal.Owner,
                Status = goal.LifecycleStatus,
                Version = goal.Version,
                EntityScope = goal.EntityScope,
                Priority = goal.Priority,
                Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Category"] = goal.Category,
                    ["Statement"] = goal.Statement,
                    ["DecisionReference"] = goal.DecisionReference,
                    ["EvidenceReference"] = goal.EvidenceReference,
                    ["Tags"] = goal.Tags ?? string.Empty
                },
                GoalPrefill = new GoalTemplatePrefillDto
                {
                    TemplateId = goal.Id,
                    Version = goal.Version,
                    Name = goal.Name,
                    Category = goal.Category,
                    Statement = goal.Statement,
                    Owner = goal.Owner,
                    Priority = goal.Priority,
                    EntityScope = goal.EntityScope,
                    LifecycleStatus = goal.LifecycleStatus,
                    PlanningStartYear = goal.PlanningHorizonStart?.Year,
                    PlanningEndYear = goal.PlanningHorizonEnd?.Year,
                    DecisionReference = string.IsNullOrWhiteSpace(goal.DecisionReference) ? null : goal.DecisionReference,
                    EvidenceReference = string.IsNullOrWhiteSpace(goal.EvidenceReference) ? null : goal.EvidenceReference,
                    ChangeLogRef = string.IsNullOrWhiteSpace(goal.ChangeLogRef) ? null : goal.ChangeLogRef,
                    Tags = goal.Tags
                },
                Metrics = metrics.Select(x => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["MetricName"] = x.MetricName,
                    ["MetricType"] = x.MetricType,
                    ["BaselineValue"] = x.BaselineValue.ToString(),
                    ["TargetValue"] = x.TargetValue.ToString(),
                    ["UnitOfMeasure"] = x.UnitOfMeasure,
                    ["AggregationMethod"] = x.AggregationMethod
                }).ToList(),
                GoalMetrics = metrics.Select(x => new GoalTemplateMetricSnapshotDto
                {
                    TemplateMetricId = x.Id,
                    MetricName = x.MetricName,
                    MetricType = x.MetricType,
                    BaselineValue = x.BaselineValue,
                    TargetValue = x.TargetValue,
                    UnitOfMeasure = x.UnitOfMeasure,
                    AggregationMethod = x.AggregationMethod,
                    CascadeMetric = x.CascadeMetric,
                    MetricOrigin = string.IsNullOrWhiteSpace(x.MetricOrigin) ? "Local" : x.MetricOrigin,
                    MetricRole = string.IsNullOrWhiteSpace(x.MetricRole) ? "Strategic" : x.MetricRole,
                    RestrictionMode = string.IsNullOrWhiteSpace(x.RestrictionMode) ? "GoalGovernedStructure" : x.RestrictionMode,
                    RollupEligible = x.RollupEligible,
                    YearlyValues = (x.YearlyTargets ?? new()).OrderBy(y => y.Year).Select(y => new GoalMetricYearValueDto
                    {
                        Year = y.Year,
                        TargetValue = y.TargetValue,
                        ActualValue = y.ActualValue,
                        ForecastValue = y.ForecastValue,
                        ThresholdCommentary = y.ThresholdCommentary
                    }).ToList()
                }).ToList(),
                GoalYearlyBudgets = (goal.YearlyBudgets ?? new()).OrderBy(x => x.Year).Select(x => new GoalYearlyBudgetEnvelopeDto
                {
                    Year = x.Year,
                    RevenueTarget = x.RevenueTarget,
                    EbitdaTarget = x.EbitdaTarget,
                    CapexEnvelope = x.CapexEnvelope,
                    OpexEnvelope = x.OpexEnvelope,
                    SavingsTarget = x.SavingsTarget,
                    FundingPoolEnvelope = x.FundingPoolEnvelope
                }).ToList()
            });
        }

        var objective = await SafeGetAsync(
            () => _library.GetObjectiveTemplateAsync(id, cancellationToken),
            () => null as ObjectiveTemplate);
        if (objective is not null)
        {
            var metrics = await SafeListAsync(
                () => _library.ListObjectiveTemplateMetricsAsync(id, cancellationToken),
                () => Array.Empty<ObjectiveTemplateMetric>());
            return Response<StrategyTemplateDetailDto>.Ok(new StrategyTemplateDetailDto
            {
                TemplateType = "Objective",
                Id = objective.Id,
                Name = objective.Name,
                Owner = objective.Owner,
                Status = objective.LifecycleStatus,
                Version = objective.Version,
                EntityScope = objective.EntityScope,
                Priority = objective.Priority,
                Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ParentGoalTemplateId"] = objective.ParentGoalTemplateId,
                    ["Type"] = objective.Type,
                    ["Statement"] = objective.Statement,
                    ["Owner"] = objective.Owner,
                    ["Priority"] = objective.Priority,
                    ["EntityScope"] = objective.EntityScope,
                    ["LifecycleStatus"] = objective.LifecycleStatus,
                    ["Status"] = objective.Status,
                    ["TimeHorizonStart"] = objective.TimeHorizonStart?.ToString("yyyy-MM-dd") ?? string.Empty,
                    ["TimeHorizonEnd"] = objective.TimeHorizonEnd?.ToString("yyyy-MM-dd") ?? string.Empty,
                    ["DependencyNotes"] = objective.DependencyNotes,
                    ["DecisionReference"] = objective.DecisionReference,
                    ["EvidenceReference"] = objective.EvidenceReference,
                    ["ContributionType"] = objective.ContributionType,
                    ["ContributionWeight"] = objective.ContributionWeight.ToString()
                },
                ObjectivePrefill = new ObjectiveTemplatePrefillDto
                {
                    TemplateId = objective.Id,
                    Version = objective.Version,
                    ParentGoalTemplateId = objective.ParentGoalTemplateId,
                    Name = objective.Name,
                    Statement = objective.Statement,
                    Owner = objective.Owner,
                    Type = objective.Type,
                    Priority = objective.Priority,
                    EntityScope = objective.EntityScope,
                    LifecycleStatus = objective.LifecycleStatus,
                    TimeHorizonStart = objective.TimeHorizonStart,
                    TimeHorizonEnd = objective.TimeHorizonEnd,
                    DependencyNotes = string.IsNullOrWhiteSpace(objective.DependencyNotes) ? null : objective.DependencyNotes,
                    DecisionReference = string.IsNullOrWhiteSpace(objective.DecisionReference) ? null : objective.DecisionReference,
                    EvidenceReference = string.IsNullOrWhiteSpace(objective.EvidenceReference) ? null : objective.EvidenceReference
                },
                Metrics = metrics.Select(x => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["MetricName"] = x.MetricName,
                    ["BaselineValue"] = x.BaselineValue.ToString(),
                    ["TargetValue"] = x.TargetValue.ToString(),
                    ["UnitOfMeasure"] = x.UnitOfMeasure,
                    ["AggregationMethod"] = x.AggregationMethod
                }).ToList()
            });
        }

        var initiative = await SafeGetAsync(
            () => _library.GetInitiativeTemplateAsync(id, cancellationToken),
            () => null as InitiativeTemplate);
        if (initiative is not null)
        {
            var metrics = await SafeListAsync(
                () => _library.ListInitiativeTemplateMetricsAsync(id, cancellationToken),
                () => Array.Empty<InitiativeTemplateMetric>());
            return Response<StrategyTemplateDetailDto>.Ok(new StrategyTemplateDetailDto
            {
                TemplateType = "Initiative",
                Id = initiative.Id,
                Name = initiative.Name,
                Owner = initiative.Owner,
                Status = initiative.LifecycleStatus,
                Version = initiative.Version,
                EntityScope = initiative.EntityScope,
                Priority = initiative.Priority,
                Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ParentGoalTemplateId"] = initiative.ParentGoalTemplateId,
                    ["ParentObjectiveTemplateId"] = initiative.ParentObjectiveTemplateId,
                    ["Type"] = initiative.Type,
                    ["WaveOrPhase"] = initiative.WaveOrPhase,
                    ["Complexity"] = initiative.Complexity,
                    ["Description"] = initiative.Description,
                    ["BudgetEnvelope"] = initiative.BudgetEnvelope,
                    ["MaturityReadiness"] = initiative.MaturityReadiness,
                    ["InitiativeClass"] = initiative.InitiativeClass,
                    ["LifecycleStatus"] = initiative.LifecycleStatus,
                    ["StartDate"] = initiative.StartDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                    ["EndDate"] = initiative.EndDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                    ["DecisionReference"] = initiative.DecisionReference,
                    ["EvidenceReference"] = initiative.EvidenceReference
                },
                InitiativePrefill = new InitiativeTemplatePrefillDto
                {
                    TemplateId = initiative.Id,
                    Version = initiative.Version,
                    ParentGoalTemplateId = initiative.ParentGoalTemplateId,
                    ParentObjectiveTemplateId = initiative.ParentObjectiveTemplateId,
                    Name = initiative.Name,
                    Description = initiative.Description,
                    Owner = initiative.Owner,
                    OwnerRole = initiative.Owner,
                    Type = initiative.Type,
                    Priority = initiative.Priority,
                    Complexity = initiative.Complexity,
                    EntityScope = initiative.EntityScope,
                    BudgetEnvelope = initiative.BudgetEnvelope,
                    MaturityReadiness = initiative.MaturityReadiness,
                    InitiativeClass = initiative.InitiativeClass,
                    LifecycleStatus = initiative.LifecycleStatus,
                    StartDate = initiative.StartDate,
                    EndDate = initiative.EndDate,
                    DecisionReference = string.IsNullOrWhiteSpace(initiative.DecisionReference) ? null : initiative.DecisionReference,
                    EvidenceReference = string.IsNullOrWhiteSpace(initiative.EvidenceReference) ? null : initiative.EvidenceReference
                },
                Metrics = metrics.Select(x => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["SuccessMeasure"] = x.SuccessMeasure,
                    ["BaselineValue"] = x.BaselineValue.ToString(),
                    ["TargetValue"] = x.TargetValue.ToString()
                }).ToList()
            });
        }

        var project = await SafeGetAsync(
            () => _library.GetProjectTemplateAsync(id, cancellationToken),
            () => null as ProjectTemplate);
        if (project is not null)
        {
            var metrics = await SafeListAsync(
                () => _library.ListProjectTemplateMetricsAsync(id, cancellationToken),
                () => Array.Empty<ProjectTemplateMetric>());
            return Response<StrategyTemplateDetailDto>.Ok(new StrategyTemplateDetailDto
            {
                TemplateType = "Project",
                Id = project.Id,
                Name = project.Name,
                Owner = project.OwnerPm,
                Status = project.LifecycleStatus,
                Version = project.Version,
                EntityScope = project.EntityScope,
                Priority = project.RiskRating,
                Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ParentGoalTemplateId"] = project.ParentGoalTemplateId,
                    ["ParentObjectiveTemplateId"] = project.ParentObjectiveTemplateId,
                    ["ParentInitiativeTemplateId"] = project.ParentInitiativeTemplateId,
                    ["DeliveryType"] = project.DeliveryType,
                    ["Sponsor"] = project.Sponsor
                },
                Metrics = metrics.Select(x => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["SuccessMetric"] = x.SuccessMetric,
                    ["MetricType"] = x.MetricType,
                    ["BaselineValue"] = x.BaselineValue.ToString(),
                    ["TargetValue"] = x.TargetValue.ToString(),
                    ["UnitOfMeasure"] = x.UnitOfMeasure,
                    ["AggregationMethod"] = x.AggregationMethod,
                    ["DisplayOrder"] = x.DisplayOrder.ToString()
                }).ToList()
            });
        }

        return Response<StrategyTemplateDetailDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
    }

    public async Task<Response<StrategyBlueprintDetailDto>> GetBlueprintAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureEmbeddedStrategyLibrarySeedsAsync(cancellationToken);
        var pack = await SafeGetAsync(
            () => _library.GetBlueprintPackAsync(id, cancellationToken),
            () => null as StrategyBlueprintPack);
        if (pack is null) return Response<StrategyBlueprintDetailDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        var items = await SafeListAsync(
            () => _library.ListBlueprintPackItemsAsync(id, cancellationToken),
            () => Array.Empty<StrategyBlueprintPackItem>());
        var detail = new StrategyBlueprintDetailDto
        {
            BlueprintPackId = pack.Id,
            Name = pack.Name,
            Description = pack.Description,
            Status = pack.Status,
            Version = pack.Version,
            HierarchyRows = items.Select(x => new StrategyBlueprintHierarchyRowDto
            {
                GoalTemplateId = x.GoalTemplateId,
                ObjectiveTemplateId = x.ObjectiveTemplateId,
                InitiativeTemplateId = x.InitiativeTemplateId,
                ProjectTemplateId = x.ProjectTemplateId,
                AggregationMethod = x.AggregationMethod,
                PlanningYearStart = x.PlanningYearStart,
                PlanningYearEnd = x.PlanningYearEnd
            }).ToList()
        };
        return Response<StrategyBlueprintDetailDto>.Ok(detail);
    }

    public async Task<Response<IReadOnlyList<StrategyTemplateVersionDto>>> GetTemplateVersionsAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureEmbeddedStrategyLibrarySeedsAsync(cancellationToken);
        var types = new[] { "Goal", "Objective", "Initiative", "Project", "BlueprintPack" };
        var all = new List<StrategyTemplateVersionDto>();
        foreach (var t in types)
        {
            var rows = await _library.ListTemplateVersionsAsync(t, id, cancellationToken);
            all.AddRange(rows.Select(x => new StrategyTemplateVersionDto
            {
                Id = x.Id,
                TemplateType = x.TemplateType,
                TemplateId = x.TemplateId,
                VersionNumber = x.VersionNumber,
                Status = x.Status,
                ChangeSummary = x.ChangeSummary,
                ChangedBy = x.ChangedBy,
                ChangedAt = x.ChangedAt
            }));
        }
        return Response<IReadOnlyList<StrategyTemplateVersionDto>>.Ok(all.OrderByDescending(x => x.VersionNumber).ToList());
    }

    public async Task<Response<bool>> SubmitReviewTemplateAsync(string id, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureEmbeddedStrategyLibrarySeedsAsync(cancellationToken);
        return await TransitionTemplateState(id, "In Review", actor, correlationId, "submit-review", cancellationToken);
    }

    public async Task<Response<bool>> ApproveTemplateAsync(string id, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureEmbeddedStrategyLibrarySeedsAsync(cancellationToken);
        return await TransitionTemplateState(id, "Approved", actor, correlationId, "approve", cancellationToken);
    }

    public async Task<Response<bool>> PublishTemplateAsync(string id, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureEmbeddedStrategyLibrarySeedsAsync(cancellationToken);
        return await TransitionTemplateState(id, "Published", actor, correlationId, "publish", cancellationToken);
    }

    public async Task<Response<bool>> RetireTemplateAsync(string id, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureEmbeddedStrategyLibrarySeedsAsync(cancellationToken);
        return await TransitionTemplateState(id, "Retired", actor, correlationId, "retire", cancellationToken);
    }

    public async Task<Response<bool>> PublishBlueprintAsync(string id, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var pack = await _library.GetBlueprintPackAsync(id, cancellationToken);
        if (pack is null) return Response<bool>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        pack.Status = "Published";
        pack.Version += 1;
        pack.PublishedAt = DateTime.UtcNow;
        pack.UpdatedAt = DateTime.UtcNow;
        pack.UpdatedBy = actor;
        await _library.UpsertBlueprintPacksAsync(new[] { pack }, cancellationToken);
        await _library.AddTemplateVersionAsync(new TemplateVersion
        {
            TemplateType = "BlueprintPack",
            TemplateId = id,
            VersionNumber = pack.Version,
            Status = pack.Status,
            ChangeSummary = "Published",
            ChangedBy = actor,
            ChangedAt = DateTime.UtcNow
        }, cancellationToken);
        await _library.AddPublishHistoryAsync(new TemplatePublishHistory
        {
            TemplateType = "BlueprintPack",
            TemplateId = id,
            VersionNumber = pack.Version,
            Action = "publish",
            Actor = actor,
            At = DateTime.UtcNow
        }, cancellationToken);
        await _audit.WriteMutationAsync(actor, "StrategyBlueprintPack", id, EnterpriseStrategyEventNames.LibraryPackPublished, correlationId, "enterprise-strategy.library", "", "Published", cancellationToken);
        return Response<bool>.Ok(true);
    }

    public async Task<Response<bool>> RetireBlueprintAsync(string id, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        var pack = await _library.GetBlueprintPackAsync(id, cancellationToken);
        if (pack is null) return Response<bool>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        pack.Status = "Retired";
        pack.Version += 1;
        pack.UpdatedAt = DateTime.UtcNow;
        pack.UpdatedBy = actor;
        await _library.UpsertBlueprintPacksAsync(new[] { pack }, cancellationToken);
        await _library.AddTemplateVersionAsync(new TemplateVersion
        {
            TemplateType = "BlueprintPack",
            TemplateId = id,
            VersionNumber = pack.Version,
            Status = pack.Status,
            ChangeSummary = "Retired",
            ChangedBy = actor,
            ChangedAt = DateTime.UtcNow
        }, cancellationToken);
        await _library.AddPublishHistoryAsync(new TemplatePublishHistory
        {
            TemplateType = "BlueprintPack",
            TemplateId = id,
            VersionNumber = pack.Version,
            Action = "retire",
            Actor = actor,
            At = DateTime.UtcNow
        }, cancellationToken);
        await _audit.WriteMutationAsync(actor, "StrategyBlueprintPack", id, EnterpriseStrategyEventNames.LibraryPackRetired, correlationId, "enterprise-strategy.library", "", "Retired", cancellationToken);
        return Response<bool>.Ok(true);
    }

    public async Task<Response<StrategyInstantiationResultDto>> InstantiateTemplateAsync(string templateId, StrategyTemplateInstantiateRequestDto request, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureEmbeddedStrategyLibrarySeedsAsync(cancellationToken);
        var templateType = string.IsNullOrWhiteSpace(request.TemplateType) ? InferTemplateType(templateId) : request.TemplateType;
        if (string.IsNullOrWhiteSpace(templateType))
            return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["templateType"] = new() { "Unknown template type." } });

        var batch = new InstantiationBatch
        {
            Id = Guid.NewGuid().ToString("N"),
            SourceType = "Template",
            SourceId = templateId,
            FullChain = request.FullChain,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor
        };
        await _library.AddInstantiationBatchAsync(batch, cancellationToken);

        var result = templateType switch
        {
            "Goal" when request.FullChain => await InstantiateGoalTemplateFullChainAsync(templateId, batch, request.DefaultOverrides, request.AllowDuplicates, actor, cancellationToken),
            "Goal" => await InstantiateGoalAsync(templateId, batch, request.DefaultOverrides, request.AllowDuplicates, actor, cancellationToken),
            "Objective" when request.FullChain => await InstantiateObjectiveTemplateFullChainAsync(templateId, batch, request.DefaultOverrides, request.AllowDuplicates, actor, cancellationToken),
            "Objective" => await InstantiateObjectiveAsync(templateId, batch, request.DefaultOverrides, request.AllowDuplicates, actor, cancellationToken),
            "Initiative" when request.FullChain => await InstantiateInitiativeTemplateFullChainAsync(templateId, batch, request.DefaultOverrides, request.AllowDuplicates, actor, cancellationToken),
            "Initiative" => await InstantiateInitiativeAsync(templateId, batch, request.DefaultOverrides, request.AllowDuplicates, actor, cancellationToken),
            "Project" => await InstantiateProjectAsync(templateId, batch, request.DefaultOverrides, request.AllowDuplicates, actor, cancellationToken),
            _ => Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["templateType"] = new() { "Unsupported template type." } })
        };

        if (!result.Success) return result;
        await _audit.WriteMutationAsync(actor, "StrategyInstantiationBatch", batch.Id, EnterpriseStrategyEventNames.LibraryInstantiationCompleted, correlationId, "enterprise-strategy.library", "", $"{result.Data?.CreatedCount ?? 0}", cancellationToken);
        return result;
    }

    public async Task<Response<StrategyInstantiationResultDto>> InstantiateBlueprintAsync(string blueprintId, StrategyBlueprintInstantiateRequestDto request, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureEmbeddedStrategyLibrarySeedsAsync(cancellationToken);
        var pack = await _library.GetBlueprintPackAsync(blueprintId, cancellationToken);
        if (pack is null) return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (!string.Equals(pack.Status, "Published", StringComparison.OrdinalIgnoreCase))
            return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, new() { ["status"] = new() { "Only Published blueprint packs can be instantiated." } });

        var rows = await _library.ListBlueprintPackItemsAsync(blueprintId, cancellationToken);
        if (!request.FullChain && request.SelectedPackItemIds.Count > 0)
            rows = rows.Where(x => request.SelectedPackItemIds.Contains(x.Id, StringComparer.OrdinalIgnoreCase)).ToList();

        var batch = new InstantiationBatch
        {
            Id = Guid.NewGuid().ToString("N"),
            SourceType = "BlueprintPack",
            SourceId = blueprintId,
            FullChain = request.FullChain,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor
        };
        await _library.AddInstantiationBatchAsync(batch, cancellationToken);

        var created = new List<InstantiatedLiveRecordDto>();
        var warnings = new List<string>();
        var goalRuntimeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var objectiveRuntimeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var initiativeRuntimeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var duplicateWarnings = 0;

        foreach (var goalId in rows.Select(x => x.GoalTemplateId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var r = await InstantiateGoalAsync(goalId, batch, request.DefaultOverrides, request.AllowDuplicates, actor, cancellationToken, blueprintId);
            if (!r.Success) { warnings.Add($"GoalTemplate {goalId}: {FlattenErrors(r.Error?.Details)}"); continue; }
            created.AddRange(r.Data!.CreatedRecords);
            duplicateWarnings += r.Data.DuplicateWarnings;
            var createdGoal = r.Data.CreatedRecords.FirstOrDefault(x => x.RuntimeObjectType == "Goal");
            if (createdGoal is not null) goalRuntimeMap[goalId] = createdGoal.RuntimeObjectId;
        }

        foreach (var objectiveId in rows.Select(x => x.ObjectiveTemplateId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var r = await InstantiateObjectiveAsync(objectiveId, batch, request.DefaultOverrides, request.AllowDuplicates, actor, cancellationToken, blueprintId, goalRuntimeMap);
            if (!r.Success) { warnings.Add($"ObjectiveTemplate {objectiveId}: {FlattenErrors(r.Error?.Details)}"); continue; }
            created.AddRange(r.Data!.CreatedRecords);
            duplicateWarnings += r.Data.DuplicateWarnings;
            var createdObjective = r.Data.CreatedRecords.FirstOrDefault(x => x.RuntimeObjectType == "Objective");
            if (createdObjective is not null) objectiveRuntimeMap[objectiveId] = createdObjective.RuntimeObjectId;
        }

        foreach (var initiativeId in rows.Select(x => x.InitiativeTemplateId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var r = await InstantiateInitiativeAsync(initiativeId, batch, request.DefaultOverrides, request.AllowDuplicates, actor, cancellationToken, blueprintId, objectiveRuntimeMap, goalRuntimeMap);
            if (!r.Success) { warnings.Add($"InitiativeTemplate {initiativeId}: {FlattenErrors(r.Error?.Details)}"); continue; }
            created.AddRange(r.Data!.CreatedRecords);
            duplicateWarnings += r.Data.DuplicateWarnings;
            var createdInitiative = r.Data.CreatedRecords.FirstOrDefault(x => x.RuntimeObjectType == "Initiative");
            if (createdInitiative is not null) initiativeRuntimeMap[initiativeId] = createdInitiative.RuntimeObjectId;
        }

        foreach (var projectId in rows.Select(x => x.ProjectTemplateId).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var r = await InstantiateProjectAsync(projectId, batch, request.DefaultOverrides, request.AllowDuplicates, actor, cancellationToken, blueprintId, initiativeRuntimeMap, objectiveRuntimeMap, goalRuntimeMap);
            if (!r.Success) { warnings.Add($"ProjectTemplate {projectId}: {FlattenErrors(r.Error?.Details)}"); continue; }
            created.AddRange(r.Data!.CreatedRecords);
            duplicateWarnings += r.Data.DuplicateWarnings;
        }

        await UpsertUsageAsync("BlueprintPack", blueprintId, pack.Name, actor, cancellationToken);

        await _audit.WriteMutationAsync(actor, "StrategyInstantiationBatch", batch.Id, EnterpriseStrategyEventNames.LibraryInstantiationCompleted, correlationId, "enterprise-strategy.library", "", $"{created.Count}", cancellationToken);
        return Response<StrategyInstantiationResultDto>.Ok(new StrategyInstantiationResultDto
        {
            InstantiationBatchId = batch.Id,
            SourceType = "BlueprintPack",
            SourceId = blueprintId,
            CreatedCount = created.Count,
            DuplicateWarnings = duplicateWarnings,
            Warnings = warnings,
            CreatedRecords = created
        });
    }

    public async Task<Response<StrategyLibraryUsageSummaryDto>> SummaryAsync(CancellationToken cancellationToken = default)
    {
        await EnsureEmbeddedStrategyLibrarySeedsAsync(cancellationToken);
        var goals = await _library.ListGoalTemplatesAsync(cancellationToken);
        var objectives = await _library.ListObjectiveTemplatesAsync(cancellationToken);
        var initiatives = await _library.ListInitiativeTemplatesAsync(cancellationToken);
        var projects = await _library.ListProjectTemplatesAsync(cancellationToken);
        var packs = await _library.ListBlueprintPacksAsync(cancellationToken);
        var usage = await _library.ListUsageStatsAsync(cancellationToken);
        var instantiations = await _library.ListInstantiationBatchesAsync(cancellationToken);
        var last = instantiations.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        return Response<StrategyLibraryUsageSummaryDto>.Ok(new StrategyLibraryUsageSummaryDto
        {
            TotalTemplates = goals.Count + objectives.Count + initiatives.Count + projects.Count,
            PublishedTemplates = goals.Count(x => x.LifecycleStatus == "Published")
                + objectives.Count(x => x.LifecycleStatus == "Published")
                + initiatives.Count(x => x.LifecycleStatus == "Published")
                + projects.Count(x => x.LifecycleStatus == "Published"),
            RetiredTemplates = goals.Count(x => x.LifecycleStatus == "Retired")
                + objectives.Count(x => x.LifecycleStatus == "Retired")
                + initiatives.Count(x => x.LifecycleStatus == "Retired")
                + projects.Count(x => x.LifecycleStatus == "Retired"),
            TotalBlueprintPacks = packs.Count,
            PublishedBlueprintPacks = packs.Count(x => x.Status == "Published"),
            TotalInstantiations = instantiations.Count,
            LastInstantiatedBy = last?.CreatedBy ?? "",
            LastInstantiatedAt = last?.CreatedAt
        });
    }

    public async Task<Response<IReadOnlyList<StrategyLibraryUsageItemDto>>> TemplateUsageAsync(CancellationToken cancellationToken = default)
    {
        await EnsureEmbeddedStrategyLibrarySeedsAsync(cancellationToken);
        var rows = await _library.ListUsageStatsAsync(cancellationToken);
        return Response<IReadOnlyList<StrategyLibraryUsageItemDto>>.Ok(rows
            .Where(x => x.ItemType == "Template")
            .OrderByDescending(x => x.UsageCount)
            .Select(x => new StrategyLibraryUsageItemDto
            {
                Id = x.ItemId,
                Name = x.ItemName,
                ItemType = x.ItemType,
                UsageCount = x.UsageCount,
                LastInstantiatedBy = x.LastInstantiatedBy,
                LastInstantiatedAt = x.LastInstantiatedAt
            }).ToList());
    }

    public async Task<Response<IReadOnlyList<StrategyLibraryUsageItemDto>>> BlueprintUsageAsync(CancellationToken cancellationToken = default)
    {
        await EnsureEmbeddedStrategyLibrarySeedsAsync(cancellationToken);
        var rows = await _library.ListUsageStatsAsync(cancellationToken);
        return Response<IReadOnlyList<StrategyLibraryUsageItemDto>>.Ok(rows
            .Where(x => x.ItemType == "BlueprintPack")
            .OrderByDescending(x => x.UsageCount)
            .Select(x => new StrategyLibraryUsageItemDto
            {
                Id = x.ItemId,
                Name = x.ItemName,
                ItemType = x.ItemType,
                UsageCount = x.UsageCount,
                LastInstantiatedBy = x.LastInstantiatedBy,
                LastInstantiatedAt = x.LastInstantiatedAt
            }).ToList());
    }

    private async Task EnsureEmbeddedStrategyLibrarySeedsAsync(CancellationToken cancellationToken)
    {
        await EnsureEmbeddedGoalObjectiveInitiativeSeedAsync(cancellationToken);
        await EnsureEmbeddedProjectsSeedAsync(cancellationToken);
    }

    private async Task EnsureEmbeddedGoalObjectiveInitiativeSeedAsync(CancellationToken cancellationToken)
    {
        if (_embeddedSeedApplied) return;
        await EmbeddedSeedGate.WaitAsync(cancellationToken);
        try
        {
            if (_embeddedSeedApplied) return;
            if (!File.Exists(EmbeddedSeedPath))
            {
                _embeddedSeedApplied = true;
                return;
            }

            try
            {
                var markdown = await File.ReadAllTextAsync(EmbeddedSeedPath, cancellationToken);
                var goalRows = ParseEmbeddedJsonRows(markdown, "### Goals_List");
                var objectiveRows = ParseEmbeddedJsonRows(markdown, "### Objectives_List");
                var initiativeRows = ParseEmbeddedJsonRows(markdown, "### Initiatives_List");

                var goals = NormalizeEmbeddedGoals(goalRows);
                var objectives = NormalizeEmbeddedObjectives(objectiveRows);
                var initiatives = NormalizeEmbeddedInitiatives(initiativeRows);

                EnterpriseStrategyLibraryFallbackStore.UpsertGoalTemplates(goals.Parents);
                foreach (var metricGroup in goals.Metrics)
                    EnterpriseStrategyLibraryFallbackStore.ReplaceGoalTemplateMetrics(metricGroup.Key, metricGroup.Value);

                await _library.UpsertGoalTemplatesAsync(goals.Parents, cancellationToken);
                foreach (var metricGroup in goals.Metrics)
                    await _library.ReplaceGoalTemplateMetricsAsync(metricGroup.Key, metricGroup.Value, cancellationToken);

                await _library.UpsertObjectiveTemplatesAsync(objectives.Parents, cancellationToken);
                foreach (var metricGroup in objectives.Metrics)
                    await _library.ReplaceObjectiveTemplateMetricsAsync(metricGroup.Key, metricGroup.Value, cancellationToken);

                await _library.UpsertInitiativeTemplatesAsync(initiatives.Parents, cancellationToken);
                foreach (var metricGroup in initiatives.Metrics)
                    await _library.ReplaceInitiativeTemplateMetricsAsync(metricGroup.Key, metricGroup.Value, cancellationToken);
            }
            catch
            {
                // Embedded seed data is optional and must never block runtime catalog reads.
            }

            _embeddedSeedApplied = true;
        }
        finally
        {
            EmbeddedSeedGate.Release();
        }
    }

    private async Task EnsureEmbeddedProjectsSeedAsync(CancellationToken cancellationToken)
    {
        if (_embeddedProjectsSeedApplied) return;
        await EmbeddedSeedGate.WaitAsync(cancellationToken);
        try
        {
            if (_embeddedProjectsSeedApplied) return;
            if (!File.Exists(EmbeddedProjectsSeedPath))
            {
                _embeddedProjectsSeedApplied = true;
                return;
            }

            try
            {
                var markdown = await File.ReadAllTextAsync(EmbeddedProjectsSeedPath, cancellationToken);
                var projectRows = ParseEmbeddedJsonRows(markdown, "### Embedded Projects_List source-of-truth payload");
                var projects = NormalizeEmbeddedProjects(projectRows);

                await _library.UpsertProjectTemplatesAsync(projects.Parents, cancellationToken);
                foreach (var parent in projects.Parents)
                {
                    if (!projects.Metrics.TryGetValue(parent.Id, out var metricList))
                        metricList = new List<ProjectTemplateMetric>();
                    await _library.ReplaceProjectTemplateMetricsAsync(parent.Id, metricList, cancellationToken);
                }
            }
            catch
            {
                // Embedded seed data is optional and must never block runtime catalog reads.
            }

            _embeddedProjectsSeedApplied = true;
        }
        finally
        {
            EmbeddedSeedGate.Release();
        }
    }

    private static SeedNormalizeResponse<GoalTemplate, GoalTemplateMetric> NormalizeEmbeddedGoals(IReadOnlyList<Dictionary<string, string>> rows)
    {
        var parents = new Dictionary<string, GoalTemplate>(StringComparer.OrdinalIgnoreCase);
        var metrics = new Dictionary<string, List<GoalTemplateMetric>>(StringComparer.OrdinalIgnoreCase);
        var metricKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var id = ReadValue(row, "Goal ID");
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!parents.ContainsKey(id))
            {
                parents[id] = new GoalTemplate
                {
                    Id = id,
                    Name = ReadValue(row, "Goal"),
                    Category = GoalTemplateTypeCatalog.NormalizeOrDefault(EmptyAs(ReadValue(row, "Goal Type"), ReadValue(row, "Goal Category"))),
                    Statement = ReadValue(row, "Goal Statement"),
                    Owner = ReadValue(row, "Goal Owner"),
                    Status = ReadValue(row, "Goal Status", "Draft"),
                    PlanningHorizonStart = ParseDateOrYear(ReadValue(row, "Planning Horizon Start")),
                    PlanningHorizonEnd = ParseDateOrYear(ReadValue(row, "Planning Horizon End")),
                    Priority = ReadValue(row, "Priority"),
                    EntityScope = ReadValue(row, "Related Entity Scope"),
                    DecisionReference = ReadValue(row, "Decision Reference"),
                    EvidenceReference = ReadValue(row, "Evidence Link"),
                    ChangeLogRef = ReadValue(row, "Change Log Ref"),
                    Version = ParseVersion(ReadValue(row, "Version"), 1),
                    LifecycleStatus = NormalizeLifecycle(ReadValue(row, "Goal Status")),
                    UpdatedAt = DateTime.UtcNow
                };
            }

            var metricName = ReadValue(row, "Goal Metric");
            if (string.IsNullOrWhiteSpace(metricName)) continue;
            if (!metrics.TryGetValue(id, out var metricRows))
            {
                metricRows = new List<GoalTemplateMetric>();
                metrics[id] = metricRows;
                metricKeys[id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var metricType = ReadValue(row, "Goal Metric Type");
            var key = $"{metricName}|{metricType}|{ReadValue(row, "Aggregation Method")}";
            if (!metricKeys[id].Add(key)) continue;

            metricRows.Add(new GoalTemplateMetric
            {
                GoalTemplateId = id,
                MetricName = metricName,
                MetricType = metricType,
                BaselineValue = ParseDecimal(ReadValue(row, "Baseline Value")),
                TargetValue = ParseDecimal(ReadValue(row, "Target Value")),
                UnitOfMeasure = ReadValue(row, "Unit of Measure"),
                AggregationMethod = ReadValue(row, "Aggregation Method")
            });
        }

        return new SeedNormalizeResponse<GoalTemplate, GoalTemplateMetric>(parents.Values.ToList(), metrics);
    }

    private static async Task<IReadOnlyList<T>> SafeListAsync<T>(Func<Task<IReadOnlyList<T>>> source, Func<IReadOnlyList<T>> fallback)
    {
        try
        {
            return await source();
        }
        catch
        {
            return fallback();
        }
    }

    private static async Task<T?> SafeGetAsync<T>(Func<Task<T?>> source, Func<T?> fallback) where T : class
    {
        try
        {
            return await source();
        }
        catch
        {
            return fallback();
        }
    }

    private static SeedNormalizeResponse<ObjectiveTemplate, ObjectiveTemplateMetric> NormalizeEmbeddedObjectives(IReadOnlyList<Dictionary<string, string>> rows)
    {
        var parents = new Dictionary<string, ObjectiveTemplate>(StringComparer.OrdinalIgnoreCase);
        var metrics = new Dictionary<string, List<ObjectiveTemplateMetric>>(StringComparer.OrdinalIgnoreCase);
        var metricKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var id = ReadValue(row, "Objective ID");
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!parents.ContainsKey(id))
            {
                parents[id] = new ObjectiveTemplate
                {
                    Id = id,
                    ParentGoalTemplateId = ReadValue(row, "Parent Goal ID"),
                    Name = ReadValue(row, "Objective"),
                    Statement = ReadValue(row, "Objective Statement"),
                    Owner = ReadValue(row, "Objective Owner"),
                    Status = ReadValue(row, "Objective Status", "Draft"),
                    Type = ReadValue(row, "Objective Type"),
                    TimeHorizonStart = ParseDateOrYear(ReadValue(row, "Time Horizon Start")),
                    TimeHorizonEnd = ParseDateOrYear(ReadValue(row, "Time Horizon End")),
                    Priority = ReadValue(row, "Priority"),
                    ContributionType = ReadValue(row, "Contribution Type"),
                    ContributionWeight = ParseDecimal(ReadValue(row, "Contribution Weight %")),
                    EntityScope = ReadValue(row, "Entity Scope"),
                    DependencyNotes = ReadValue(row, "Dependency Notes"),
                    DecisionReference = ReadValue(row, "Decision Ref"),
                    EvidenceReference = ReadValue(row, "Evidence Ref"),
                    Version = ParseVersion(ReadValue(row, "Version"), 1),
                    LifecycleStatus = NormalizeLifecycle(ReadValue(row, "Objective Status")),
                    UpdatedAt = DateTime.UtcNow
                };
            }

            if (!metrics.TryGetValue(id, out var metricRows))
            {
                metricRows = new List<ObjectiveTemplateMetric>();
                metrics[id] = metricRows;
                metricKeys[id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var measureName = ReadValue(row, "Objective Measure", "Measure", "Contribution Type", "Objective Type");
            var baseline = ParseDecimal(ReadValue(row, "Baseline Value"));
            var target = ParseDecimal(ReadValue(row, "Target Value"));
            var aggregation = ReadValue(row, "Aggregation Method");
            var signature = $"{measureName}|{ReadValue(row, "Contribution Type")}|{ReadValue(row, "Contribution Weight %")}|{baseline}|{target}|{aggregation}";
            if (!metricKeys[id].Add(signature)) continue;

            metricRows.Add(new ObjectiveTemplateMetric
            {
                ObjectiveTemplateId = id,
                MetricName = measureName,
                BaselineValue = baseline,
                TargetValue = target,
                AggregationMethod = aggregation,
                UnitOfMeasure = ReadValue(row, "Unit of Measure")
            });
        }

        return new SeedNormalizeResponse<ObjectiveTemplate, ObjectiveTemplateMetric>(parents.Values.ToList(), metrics);
    }

    private static SeedNormalizeResponse<InitiativeTemplate, InitiativeTemplateMetric> NormalizeEmbeddedInitiatives(IReadOnlyList<Dictionary<string, string>> rows)
    {
        var parents = new Dictionary<string, InitiativeTemplate>(StringComparer.OrdinalIgnoreCase);
        var metrics = new Dictionary<string, List<InitiativeTemplateMetric>>(StringComparer.OrdinalIgnoreCase);
        var metricKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var id = ReadValue(row, "Initiative ID");
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!parents.ContainsKey(id))
            {
                parents[id] = new InitiativeTemplate
                {
                    Id = id,
                    ParentObjectiveTemplateId = ReadValue(row, "Parent Objective ID"),
                    ParentGoalTemplateId = ReadValue(row, "Parent Goal ID"),
                    Name = ReadValue(row, "Initiative"),
                    Description = ReadValue(row, "Initiative Description"),
                    Owner = ReadValue(row, "Initiative Owner"),
                    Status = ReadValue(row, "Initiative Status", "Draft"),
                    Type = ReadValue(row, "Initiative Type"),
                    StartDate = ParseDateOrYear(ReadValue(row, "Start Date")),
                    EndDate = ParseDateOrYear(ReadValue(row, "End Date")),
                    WaveOrPhase = ReadValue(row, "Planning Wave / Phase"),
                    Priority = ReadValue(row, "Priority"),
                    Complexity = ReadValue(row, "Complexity"),
                    DependencyIds = ReadValue(row, "Dependency IDs"),
                    EntityScope = ReadValue(row, "Entity Scope"),
                    BudgetEnvelope = ReadValue(row, "Budget Envelope"),
                    MaturityReadiness = ReadValue(row, "Maturity / Readiness"),
                    DecisionReference = ReadValue(row, "Decision Ref"),
                    EvidenceReference = ReadValue(row, "Evidence Ref"),
                    InitiativeClass = ReadValue(row, "Initiative Class"),
                    Version = ParseVersion(ReadValue(row, "Version"), 1),
                    LifecycleStatus = NormalizeLifecycle(ReadValue(row, "Initiative Status")),
                    UpdatedAt = DateTime.UtcNow
                };
            }

            var successMeasure = ReadValue(row, "Primary KPI / Success Measure");
            if (string.IsNullOrWhiteSpace(successMeasure)) continue;
            if (!metrics.TryGetValue(id, out var metricRows))
            {
                metricRows = new List<InitiativeTemplateMetric>();
                metrics[id] = metricRows;
                metricKeys[id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var baseline = ParseDecimal(ReadValue(row, "Baseline"));
            var target = ParseDecimal(ReadValue(row, "Target"));
            var signature = $"{successMeasure}|{baseline}|{target}";
            if (!metricKeys[id].Add(signature)) continue;
            metricRows.Add(new InitiativeTemplateMetric
            {
                InitiativeTemplateId = id,
                SuccessMeasure = successMeasure,
                BaselineValue = baseline,
                TargetValue = target
            });
        }

        return new SeedNormalizeResponse<InitiativeTemplate, InitiativeTemplateMetric>(parents.Values.ToList(), metrics);
    }

    private static SeedNormalizeResponse<ProjectTemplate, ProjectTemplateMetric> NormalizeEmbeddedProjects(IReadOnlyList<Dictionary<string, string>> rows)
    {
        var parents = new Dictionary<string, ProjectTemplate>(StringComparer.OrdinalIgnoreCase);
        var metrics = new Dictionary<string, List<ProjectTemplateMetric>>(StringComparer.OrdinalIgnoreCase);
        var metricKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var id = ReadValue(row, "Project ID");
            if (string.IsNullOrWhiteSpace(id)) continue;

            if (!parents.ContainsKey(id))
            {
                parents[id] = new ProjectTemplate
                {
                    Id = id,
                    ParentInitiativeTemplateId = ReadValue(row, "Parent Initiative ID"),
                    ParentObjectiveTemplateId = ReadValue(row, "Parent Objective ID"),
                    ParentGoalTemplateId = ReadValue(row, "Parent Goal ID"),
                    Name = ReadValue(row, "Project"),
                    Description = ReadValue(row, "Project Description"),
                    OwnerPm = ReadValue(row, "Project Owner / PM"),
                    Sponsor = ReadValue(row, "Project Sponsor"),
                    Status = ReadValue(row, "Project Status", "Draft"),
                    Phase = ReadValue(row, "Stage / Phase"),
                    StartDate = ParseDateOrYear(ReadValue(row, "Start Date")),
                    EndDate = ParseDateOrYear(ReadValue(row, "End Date")),
                    MilestoneFlag = ReadValue(row, "Milestone Flag / Key Deliverable"),
                    DependencyIds = ReadValue(row, "Dependency IDs"),
                    DeliveryType = ReadValue(row, "Delivery Type"),
                    EntityScope = ReadValue(row, "Entity Scope"),
                    BudgetSummary = ReadValue(row, "Budget / CapEx / OpEx"),
                    RiskRating = ReadValue(row, "Risk Rating"),
                    ReadinessStatus = ReadValue(row, "Readiness Status"),
                    DecisionReference = ReadValue(row, "Decision Ref"),
                    EvidenceReference = ReadValue(row, "Evidence Ref"),
                    Version = ParseVersion(ReadValue(row, "Version"), 1),
                    LifecycleStatus = NormalizeLifecycle(ReadValue(row, "Project Status")),
                    UpdatedAt = DateTime.UtcNow
                };
            }

            var successMetric = ReadValue(row, "Project Success Metric");
            var baseline = ParseDecimal(ReadValue(row, "Metric Baseline"));
            var target = ParseDecimal(ReadValue(row, "Metric Target"));
            var metricType = ReadValue(row, "Metric Type");
            var uom = ReadValue(row, "Unit of Measure");
            var aggregation = ReadValue(row, "Aggregation Method");
            if (string.IsNullOrWhiteSpace(successMetric) && baseline == 0m && target == 0m) continue;

            if (!metrics.TryGetValue(id, out var metricRows))
            {
                metricRows = new List<ProjectTemplateMetric>();
                metrics[id] = metricRows;
                metricKeys[id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var key = $"{successMetric}|{metricType}|{uom}|{aggregation}|{baseline}|{target}";
            if (!metricKeys[id].Add(key)) continue;

            var explicitOrder = ReadValue(row, "Display Order");
            var displayOrder = int.TryParse(explicitOrder, out var d) ? d : metricRows.Count;

            metricRows.Add(new ProjectTemplateMetric
            {
                ProjectTemplateId = id,
                SuccessMetric = successMetric,
                MetricType = metricType,
                BaselineValue = baseline,
                TargetValue = target,
                UnitOfMeasure = uom,
                AggregationMethod = aggregation,
                DisplayOrder = displayOrder
            });
        }

        return new SeedNormalizeResponse<ProjectTemplate, ProjectTemplateMetric>(parents.Values.ToList(), metrics);
    }

    private static List<Dictionary<string, string>> ParseEmbeddedJsonRows(string markdown, string heading)
    {
        var searchFrom = 0;
        var headingIndex = -1;
        var jsonFence = -1;
        while (true)
        {
            headingIndex = markdown.IndexOf(heading, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (headingIndex < 0) return new List<Dictionary<string, string>>();
            var nextSection = markdown.IndexOf("\n### ", headingIndex + 1, StringComparison.OrdinalIgnoreCase);
            if (nextSection < 0) nextSection = markdown.Length;
            jsonFence = markdown.IndexOf("```json", headingIndex, StringComparison.OrdinalIgnoreCase);
            if (jsonFence >= 0 && jsonFence < nextSection) break;
            searchFrom = headingIndex + heading.Length;
        }

        var jsonStart = markdown.IndexOf('[', jsonFence);
        var jsonEndFence = markdown.IndexOf("```", jsonStart, StringComparison.OrdinalIgnoreCase);
        if (jsonStart < 0 || jsonEndFence <= jsonStart) return new List<Dictionary<string, string>>();

        var json = markdown.Substring(jsonStart, jsonEndFence - jsonStart).Trim();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return new List<Dictionary<string, string>>();

        var rows = new List<Dictionary<string, string>>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in item.EnumerateObject())
            {
                row[property.Name] = property.Value.ValueKind == JsonValueKind.Null ? string.Empty : property.Value.ToString();
            }
            rows.Add(row);
        }
        return rows;
    }

    private static string ReadValue(Dictionary<string, string> row, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (row.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return string.Empty;
    }

    private static int ParseVersion(string value, int fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (int.TryParse(value, out var parsed)) return parsed;
        var started = false;
        var digits = new List<char>();
        foreach (var c in value)
        {
            if (char.IsDigit(c))
            {
                digits.Add(c);
                started = true;
                continue;
            }
            if (started) break;
        }
        return digits.Count > 0 && int.TryParse(new string(digits.ToArray()), out var extracted) ? extracted : fallback;
    }

    private static string NormalizeLifecycle(string status)
    {
        var normalized = (status ?? string.Empty).Trim();
        if (normalized.Equals("Published", StringComparison.OrdinalIgnoreCase)) return "Published";
        if (normalized.Equals("Retired", StringComparison.OrdinalIgnoreCase)) return "Retired";
        if (normalized.Equals("In Review", StringComparison.OrdinalIgnoreCase)) return "In Review";
        if (normalized.Equals("Approved", StringComparison.OrdinalIgnoreCase)) return "Approved";
        if (normalized.Equals("Active", StringComparison.OrdinalIgnoreCase)) return "Published";
        // Embedded workbook rows often use execution-style labels; treat as published library templates so Instantiate-to-Live works without a manual publish pass.
        if (normalized.Equals("Planned", StringComparison.OrdinalIgnoreCase)) return "Published";
        if (normalized.Equals("In-flight", StringComparison.OrdinalIgnoreCase) || normalized.Equals("In Flight", StringComparison.OrdinalIgnoreCase)) return "Published";
        if (normalized.Equals("On Track", StringComparison.OrdinalIgnoreCase)) return "Published";
        if (normalized.Equals("Complete", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Completed", StringComparison.OrdinalIgnoreCase)) return "Published";
        return "Draft";
    }

    private sealed class SeedNormalizeResponse<TParent, TMetric>
    {
        public SeedNormalizeResponse(List<TParent> parents, Dictionary<string, List<TMetric>> metrics)
        {
            Parents = parents;
            Metrics = metrics;
        }

        public List<TParent> Parents { get; }
        public Dictionary<string, List<TMetric>> Metrics { get; }
    }

    private async Task<Response<bool>> TransitionTemplateState(string id, string targetState, string actor, string correlationId, string action, CancellationToken cancellationToken)
    {
        var goal = await _library.GetGoalTemplateAsync(id, cancellationToken);
        if (goal is not null)
        {
            goal.LifecycleStatus = targetState;
            goal.Version += 1;
            goal.UpdatedAt = DateTime.UtcNow;
            goal.UpdatedBy = actor;
            await _library.UpsertGoalTemplatesAsync(new[] { goal }, cancellationToken);
            await AddVersionAndPublishRows("Goal", goal.Id, goal.Version, targetState, action, actor, cancellationToken);
            await EmitTemplateAudit(goal.Id, action, targetState, actor, correlationId, cancellationToken);
            return Response<bool>.Ok(true);
        }

        var objective = await _library.GetObjectiveTemplateAsync(id, cancellationToken);
        if (objective is not null)
        {
            objective.LifecycleStatus = targetState;
            objective.Version += 1;
            objective.UpdatedAt = DateTime.UtcNow;
            objective.UpdatedBy = actor;
            await _library.UpsertObjectiveTemplatesAsync(new[] { objective }, cancellationToken);
            await AddVersionAndPublishRows("Objective", objective.Id, objective.Version, targetState, action, actor, cancellationToken);
            await EmitTemplateAudit(objective.Id, action, targetState, actor, correlationId, cancellationToken);
            return Response<bool>.Ok(true);
        }

        var initiative = await _library.GetInitiativeTemplateAsync(id, cancellationToken);
        if (initiative is not null)
        {
            initiative.LifecycleStatus = targetState;
            initiative.Version += 1;
            initiative.UpdatedAt = DateTime.UtcNow;
            initiative.UpdatedBy = actor;
            await _library.UpsertInitiativeTemplatesAsync(new[] { initiative }, cancellationToken);
            await AddVersionAndPublishRows("Initiative", initiative.Id, initiative.Version, targetState, action, actor, cancellationToken);
            await EmitTemplateAudit(initiative.Id, action, targetState, actor, correlationId, cancellationToken);
            return Response<bool>.Ok(true);
        }

        var project = await _library.GetProjectTemplateAsync(id, cancellationToken);
        if (project is not null)
        {
            project.LifecycleStatus = targetState;
            project.Version += 1;
            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedBy = actor;
            await _library.UpsertProjectTemplatesAsync(new[] { project }, cancellationToken);
            await AddVersionAndPublishRows("Project", project.Id, project.Version, targetState, action, actor, cancellationToken);
            await EmitTemplateAudit(project.Id, action, targetState, actor, correlationId, cancellationToken);
            return Response<bool>.Ok(true);
        }

        return Response<bool>.Fail(EnterpriseStrategyErrorCodes.NotFound);
    }

    private async Task AddVersionAndPublishRows(string templateType, string templateId, int version, string status, string action, string actor, CancellationToken cancellationToken)
    {
        await _library.AddTemplateVersionAsync(new TemplateVersion
        {
            TemplateType = templateType,
            TemplateId = templateId,
            VersionNumber = version,
            Status = status,
            ChangeSummary = action,
            ChangedBy = actor,
            ChangedAt = DateTime.UtcNow
        }, cancellationToken);
        await _library.AddPublishHistoryAsync(new TemplatePublishHistory
        {
            TemplateType = templateType,
            TemplateId = templateId,
            VersionNumber = version,
            Action = action,
            Actor = actor,
            At = DateTime.UtcNow
        }, cancellationToken);
    }

    private async Task EmitTemplateAudit(string id, string action, string targetState, string actor, string correlationId, CancellationToken cancellationToken)
    {
        var eventName = action switch
        {
            "publish" => EnterpriseStrategyEventNames.LibraryTemplatePublished,
            "retire" => EnterpriseStrategyEventNames.LibraryTemplateRetired,
            _ => EnterpriseStrategyEventNames.LibraryTemplateUpdated
        };
        await _audit.WriteMutationAsync(actor, "StrategyTemplate", id, eventName, correlationId, "enterprise-strategy.library", "", targetState, cancellationToken);
    }

    /// <summary>Instantiate goal template plus all published objective and initiative templates in the library that reference this goal (or its child objectives).</summary>
    private async Task<Response<StrategyInstantiationResultDto>> InstantiateGoalTemplateFullChainAsync(string templateId, InstantiationBatch batch, Dictionary<string, string> overrides, bool allowDuplicates, string actor, CancellationToken cancellationToken)
    {
        var goalRes = await InstantiateGoalAsync(templateId, batch, overrides, allowDuplicates, actor, cancellationToken);
        if (!goalRes.Success) return goalRes;

        var allCreated = goalRes.Data!.CreatedRecords.ToList();
        var warnings = goalRes.Data.Warnings.ToList();
        var duplicateWarnings = goalRes.Data.DuplicateWarnings;

        var goalRec = allCreated.FirstOrDefault(x => string.Equals(x.RuntimeObjectType, "Goal", StringComparison.OrdinalIgnoreCase));
        if (goalRec is null) return goalRes;

        var goalRuntimeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [templateId] = goalRec.RuntimeObjectId };
        var objectiveRuntimeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var objectiveTemplates = (await _library.ListObjectiveTemplatesAsync(cancellationToken))
            .Where(o => string.Equals(o.ParentGoalTemplateId, templateId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(o => o.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var ot in objectiveTemplates)
        {
            if (!string.Equals(ot.LifecycleStatus, "Published", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"Skipped objective template '{ot.Id}' (not published).");
                continue;
            }

            var or = await InstantiateObjectiveAsync(ot.Id, batch, overrides, allowDuplicates, actor, cancellationToken, blueprintPackId: null, goalRuntimeMap: goalRuntimeMap);
            if (!or.Success)
            {
                warnings.Add($"ObjectiveTemplate {ot.Id}: {FlattenErrors(or.Error?.Details)}");
                continue;
            }

            allCreated.AddRange(or.Data!.CreatedRecords);
            warnings.AddRange(or.Data.Warnings);
            duplicateWarnings += or.Data.DuplicateWarnings;
            var objRec = or.Data.CreatedRecords.FirstOrDefault(x => string.Equals(x.RuntimeObjectType, "Objective", StringComparison.OrdinalIgnoreCase));
            if (objRec is not null) objectiveRuntimeMap[ot.Id] = objRec.RuntimeObjectId;
        }

        var childObjectiveIds = new HashSet<string>(objectiveTemplates.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
        var initiativeTemplates = (await _library.ListInitiativeTemplatesAsync(cancellationToken))
            .Where(i => childObjectiveIds.Contains(i.ParentObjectiveTemplateId))
            .OrderBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var it in initiativeTemplates)
        {
            if (!string.Equals(it.LifecycleStatus, "Published", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"Skipped initiative template '{it.Id}' (not published).");
                continue;
            }

            var ir = await InstantiateInitiativeAsync(it.Id, batch, overrides, allowDuplicates, actor, cancellationToken, blueprintPackId: null, objectiveRuntimeMap: objectiveRuntimeMap, goalRuntimeMap: goalRuntimeMap);
            if (!ir.Success)
            {
                warnings.Add($"InitiativeTemplate {it.Id}: {FlattenErrors(ir.Error?.Details)}");
                continue;
            }

            allCreated.AddRange(ir.Data!.CreatedRecords);
            warnings.AddRange(ir.Data.Warnings);
            duplicateWarnings += ir.Data.DuplicateWarnings;
        }

        return Response<StrategyInstantiationResultDto>.Ok(new StrategyInstantiationResultDto
        {
            InstantiationBatchId = batch.Id,
            SourceType = "Template",
            SourceId = templateId,
            CreatedCount = allCreated.Count,
            DuplicateWarnings = duplicateWarnings,
            Warnings = warnings,
            CreatedRecords = allCreated
        });
    }

    private async Task<Response<StrategyInstantiationResultDto>> InstantiateObjectiveTemplateFullChainAsync(string templateId, InstantiationBatch batch, Dictionary<string, string> overrides, bool allowDuplicates, string actor, CancellationToken cancellationToken)
    {
        var objRes = await InstantiateObjectiveAsync(templateId, batch, overrides, allowDuplicates, actor, cancellationToken);
        if (!objRes.Success) return objRes;

        var allCreated = objRes.Data!.CreatedRecords.ToList();
        var warnings = objRes.Data.Warnings.ToList();
        var duplicateWarnings = objRes.Data.DuplicateWarnings;

        var objRec = allCreated.FirstOrDefault(x => string.Equals(x.RuntimeObjectType, "Objective", StringComparison.OrdinalIgnoreCase));
        if (objRec is null) return objRes;

        var objectiveRuntimeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [templateId] = objRec.RuntimeObjectId };
        var goalRuntimeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var tmpl = await _library.GetObjectiveTemplateAsync(templateId, cancellationToken);
        if (tmpl is not null)
        {
            var goals = await _goals.ListAsync(cancellationToken);
            var rg = goals.FirstOrDefault(x => string.Equals(x.SourceTemplateId, tmpl.ParentGoalTemplateId, StringComparison.OrdinalIgnoreCase));
            if (rg is not null) goalRuntimeMap[tmpl.ParentGoalTemplateId] = rg.Id;
        }

        var initiativeTemplates = (await _library.ListInitiativeTemplatesAsync(cancellationToken))
            .Where(i => string.Equals(i.ParentObjectiveTemplateId, templateId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var it in initiativeTemplates)
        {
            if (!string.Equals(it.LifecycleStatus, "Published", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"Skipped initiative template '{it.Id}' (not published).");
                continue;
            }

            var ir = await InstantiateInitiativeAsync(it.Id, batch, overrides, allowDuplicates, actor, cancellationToken, blueprintPackId: null, objectiveRuntimeMap: objectiveRuntimeMap, goalRuntimeMap: goalRuntimeMap.Count > 0 ? goalRuntimeMap : null);
            if (!ir.Success)
            {
                warnings.Add($"InitiativeTemplate {it.Id}: {FlattenErrors(ir.Error?.Details)}");
                continue;
            }

            allCreated.AddRange(ir.Data!.CreatedRecords);
            warnings.AddRange(ir.Data.Warnings);
            duplicateWarnings += ir.Data.DuplicateWarnings;
        }

        return Response<StrategyInstantiationResultDto>.Ok(new StrategyInstantiationResultDto
        {
            InstantiationBatchId = batch.Id,
            SourceType = "Template",
            SourceId = templateId,
            CreatedCount = allCreated.Count,
            DuplicateWarnings = duplicateWarnings,
            Warnings = warnings,
            CreatedRecords = allCreated
        });
    }

    private async Task<Response<StrategyInstantiationResultDto>> InstantiateInitiativeTemplateFullChainAsync(string templateId, InstantiationBatch batch, Dictionary<string, string> overrides, bool allowDuplicates, string actor, CancellationToken cancellationToken)
    {
        var initRes = await InstantiateInitiativeAsync(templateId, batch, overrides, allowDuplicates, actor, cancellationToken);
        if (!initRes.Success) return initRes;

        var allCreated = initRes.Data!.CreatedRecords.ToList();
        var warnings = initRes.Data.Warnings.ToList();
        var duplicateWarnings = initRes.Data.DuplicateWarnings;

        var initRec = allCreated.FirstOrDefault(x => string.Equals(x.RuntimeObjectType, "Initiative", StringComparison.OrdinalIgnoreCase));
        if (initRec is null) return initRes;

        var initiativeRuntimeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [templateId] = initRec.RuntimeObjectId };

        var projectTemplates = (await _library.ListProjectTemplatesAsync(cancellationToken))
            .Where(p => string.Equals(p.ParentInitiativeTemplateId, templateId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var pt in projectTemplates)
        {
            if (!string.Equals(pt.LifecycleStatus, "Published", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"Skipped project template '{pt.Id}' (not published).");
                continue;
            }

            var pr = await InstantiateProjectAsync(pt.Id, batch, overrides, allowDuplicates, actor, cancellationToken, blueprintPackId: null, initiativeRuntimeMap: initiativeRuntimeMap);
            if (!pr.Success)
            {
                warnings.Add($"ProjectTemplate {pt.Id}: {FlattenErrors(pr.Error?.Details)}");
                continue;
            }

            allCreated.AddRange(pr.Data!.CreatedRecords);
            warnings.AddRange(pr.Data.Warnings);
            duplicateWarnings += pr.Data.DuplicateWarnings;
        }

        return Response<StrategyInstantiationResultDto>.Ok(new StrategyInstantiationResultDto
        {
            InstantiationBatchId = batch.Id,
            SourceType = "Template",
            SourceId = templateId,
            CreatedCount = allCreated.Count,
            DuplicateWarnings = duplicateWarnings,
            Warnings = warnings,
            CreatedRecords = allCreated
        });
    }

    private async Task<Response<StrategyInstantiationResultDto>> InstantiateGoalAsync(string templateId, InstantiationBatch batch, Dictionary<string, string> overrides, bool allowDuplicates, string actor, CancellationToken cancellationToken, string? blueprintPackId = null)
    {
        var template = await _library.GetGoalTemplateAsync(templateId, cancellationToken);
        if (template is null) return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (!string.Equals(template.LifecycleStatus, "Published", StringComparison.OrdinalIgnoreCase))
            return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, new() { ["status"] = new() { "Template is not published." } });

        var warnings = new List<string>();
        var duplicateWarnings = 0;
        var goals = await _goals.ListAsync(cancellationToken);
        var duplicate = goals.Any(x =>
            string.Equals(x.SourceTemplateId, template.Id, StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(x.Name, template.Name, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase)));
        if (duplicate)
        {
            duplicateWarnings++;
            warnings.Add($"Potential duplicate goal for template '{template.Id}'.");
            if (!allowDuplicates)
                return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, new() { ["duplicate"] = new() { "Equivalent live goal exists. Enable duplicate override to continue." } });
        }

        var runtimeId = $"goal-{Guid.NewGuid():N}".Substring(0, 16);
        var aggregate = new GoalAggregate
        {
            Id = runtimeId,
            Category = template.Category,
            Name = ApplyOverride(template.Name, overrides, "name"),
            Statement = ApplyOverride(template.Statement, overrides, "statement"),
            Status = ApplyOverride(string.IsNullOrWhiteSpace(template.Status) ? "Draft" : template.Status, overrides, "status"),
            Owner = ApplyOverride(template.Owner, overrides, "owner"),
            OwnerRole = ApplyOverride(template.Owner, overrides, "owner"),
            PlanningHorizonStart = template.PlanningHorizonStart,
            PlanningHorizonEnd = template.PlanningHorizonEnd,
            Priority = ApplyOverride(template.Priority, overrides, "priority"),
            EntityScope = ApplyOverride(template.EntityScope, overrides, "entityScope"),
            DecisionReference = template.DecisionReference,
            EvidenceReference = template.EvidenceReference,
            ChangeLogRef = template.ChangeLogRef,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = actor,
            UpdatedBy = actor,
            SourceTemplateType = "Goal",
            SourceTemplateId = template.Id,
            SourceTemplateVersion = template.Version,
            SourceBlueprintPackId = blueprintPackId,
            InstantiationBatchId = batch.Id,
            CreatedFromLibrary = true,
            YearlyBudgets = (template.YearlyBudgets ?? new()).Select(b => new GoalYearlyBudgetEnvelope
            {
                Year = b.Year,
                RevenueTarget = b.RevenueTarget,
                EbitdaTarget = b.EbitdaTarget,
                CapexEnvelope = b.CapexEnvelope,
                OpexEnvelope = b.OpexEnvelope,
                SavingsTarget = b.SavingsTarget,
                FundingPoolEnvelope = b.FundingPoolEnvelope
            }).ToList()
        };

        var metrics = await _library.ListGoalTemplateMetricsAsync(template.Id, cancellationToken);
        aggregate.Metrics = metrics.Select(m => new GoalMetric
        {
            Id = Guid.NewGuid().ToString("N"),
            GoalId = runtimeId,
            MetricName = m.MetricName,
            MetricType = m.MetricType,
            BaselineValue = m.BaselineValue,
            TargetValue = m.TargetValue,
            UnitOfMeasure = m.UnitOfMeasure,
            AggregationMethod = m.AggregationMethod,
            CascadeMetric = m.CascadeMetric,
            MetricOrigin = string.IsNullOrWhiteSpace(m.MetricOrigin) ? "Local" : m.MetricOrigin,
            MetricRole = string.IsNullOrWhiteSpace(m.MetricRole) ? "Strategic" : m.MetricRole,
            RestrictionMode = string.IsNullOrWhiteSpace(m.RestrictionMode) ? "GoalGovernedStructure" : m.RestrictionMode,
            RollupEligible = m.RollupEligible,
            YearlyTargets = (m.YearlyTargets ?? new()).Select(y => new GoalMetricYearValue
            {
                Year = y.Year,
                TargetValue = y.TargetValue,
                ActualValue = y.ActualValue,
                ForecastValue = y.ForecastValue,
                ThresholdCommentary = y.ThresholdCommentary
            }).ToList(),
            MetricBindingStatus = "Bound",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        var logs = BuildOverrideLogs(overrides, batch.Id, "Goal", runtimeId, actor, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["owner"] = template.Owner,
            ["status"] = template.Status,
            ["priority"] = template.Priority,
            ["entityScope"] = template.EntityScope,
            ["name"] = template.Name,
            ["statement"] = template.Statement
        });

        await _goals.AddAsync(aggregate, cancellationToken);
        await _library.AddInstantiationRecordsAsync(new[]
        {
            new InstantiationRecord
            {
                InstantiationBatchId = batch.Id,
                RuntimeObjectType = "Goal",
                RuntimeObjectId = runtimeId,
                SourceTemplateType = "Goal",
                SourceTemplateId = template.Id,
                SourceTemplateVersion = template.Version,
                SourceBlueprintPackId = blueprintPackId,
                CreatedAt = DateTime.UtcNow
            }
        }, cancellationToken);
        await _library.AddOverrideLogsAsync(logs, cancellationToken);
        await UpsertUsageAsync("Template", template.Id, template.Name, actor, cancellationToken);

        return Response<StrategyInstantiationResultDto>.Ok(new StrategyInstantiationResultDto
        {
            InstantiationBatchId = batch.Id,
            SourceType = "Template",
            SourceId = template.Id,
            CreatedCount = 1,
            DuplicateWarnings = duplicateWarnings,
            Warnings = warnings,
            CreatedRecords = new[]
            {
                new InstantiatedLiveRecordDto
                {
                    RuntimeObjectType = "Goal",
                    RuntimeObjectId = runtimeId,
                    SourceTemplateType = "Goal",
                    SourceTemplateId = template.Id,
                    SourceTemplateVersion = template.Version
                }
            }
        });
    }

    private async Task<Response<StrategyInstantiationResultDto>> InstantiateObjectiveAsync(string templateId, InstantiationBatch batch, Dictionary<string, string> overrides, bool allowDuplicates, string actor, CancellationToken cancellationToken, string? blueprintPackId = null, Dictionary<string, string>? goalRuntimeMap = null)
    {
        var template = await _library.GetObjectiveTemplateAsync(templateId, cancellationToken);
        if (template is null) return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (!string.Equals(template.LifecycleStatus, "Published", StringComparison.OrdinalIgnoreCase))
            return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, new() { ["status"] = new() { "Template is not published." } });

        var goals = await _goals.ListAsync(cancellationToken);
        var runtimeParentGoalId = goalRuntimeMap is not null && goalRuntimeMap.TryGetValue(template.ParentGoalTemplateId, out var mappedGoal)
            ? mappedGoal
            : goals.FirstOrDefault(x => string.Equals(x.SourceTemplateId, template.ParentGoalTemplateId, StringComparison.OrdinalIgnoreCase))?.Id ?? template.ParentGoalTemplateId;
        if (!goals.Any(x => x.Id == runtimeParentGoalId))
            return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["parentGoalId"] = new() { $"Runtime parent goal for template parent '{template.ParentGoalTemplateId}' not found." } });

        var objectives = await _objectives.ListAsync(cancellationToken);
        var duplicate = objectives.Any(x =>
            string.Equals(x.SourceTemplateId, template.Id, StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(x.Name, template.Name, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(x.ParentGoalId, runtimeParentGoalId, StringComparison.OrdinalIgnoreCase)));
        var warnings = new List<string>();
        var duplicateWarnings = 0;
        if (duplicate)
        {
            duplicateWarnings++;
            warnings.Add($"Potential duplicate objective for template '{template.Id}'.");
            if (!allowDuplicates)
                return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, new() { ["duplicate"] = new() { "Equivalent live objective exists. Enable duplicate override to continue." } });
        }

        var runtimeId = $"obj-{Guid.NewGuid():N}".Substring(0, 15);
        var aggregate = new ObjectiveAggregate
        {
            Id = runtimeId,
            ParentGoalId = runtimeParentGoalId,
            Name = ApplyOverride(template.Name, overrides, "name"),
            Statement = ApplyOverride(template.Statement, overrides, "statement"),
            Owner = ApplyOverride(template.Owner, overrides, "owner"),
            Status = ApplyOverride(string.IsNullOrWhiteSpace(template.Status) ? "Draft" : template.Status, overrides, "status"),
            Type = template.Type,
            TimeHorizonStart = template.TimeHorizonStart,
            TimeHorizonEnd = template.TimeHorizonEnd,
            Priority = ApplyOverride(template.Priority, overrides, "priority"),
            ContributionType = template.ContributionType,
            ContributionWeight = template.ContributionWeight,
            EntityScope = ApplyOverride(template.EntityScope, overrides, "entityScope"),
            DependencyNotes = template.DependencyNotes,
            DecisionReference = template.DecisionReference,
            EvidenceReference = template.EvidenceReference,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = actor,
            UpdatedBy = actor,
            SourceTemplateType = "Objective",
            SourceTemplateId = template.Id,
            SourceTemplateVersion = template.Version,
            SourceBlueprintPackId = blueprintPackId,
            InstantiationBatchId = batch.Id,
            CreatedFromLibrary = true
        };
        var metrics = await _library.ListObjectiveTemplateMetricsAsync(template.Id, cancellationToken);
        aggregate.Metrics = metrics.Select(m => new ObjectiveMetric
        {
            Id = Guid.NewGuid().ToString("N"),
            ObjectiveId = runtimeId,
            MetricName = m.MetricName,
            BaselineValue = m.BaselineValue,
            TargetValue = m.TargetValue,
            UnitOfMeasure = m.UnitOfMeasure,
            AggregationMethod = m.AggregationMethod,
            MetricBindingStatus = "Bound",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ToList();

        await _objectives.AddAsync(aggregate, cancellationToken);
        await _library.AddInstantiationRecordsAsync(new[]
        {
            new InstantiationRecord
            {
                InstantiationBatchId = batch.Id,
                RuntimeObjectType = "Objective",
                RuntimeObjectId = runtimeId,
                SourceTemplateType = "Objective",
                SourceTemplateId = template.Id,
                SourceTemplateVersion = template.Version,
                SourceBlueprintPackId = blueprintPackId,
                CreatedAt = DateTime.UtcNow
            }
        }, cancellationToken);
        await _library.AddOverrideLogsAsync(BuildOverrideLogs(overrides, batch.Id, "Objective", runtimeId, actor, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["owner"] = template.Owner,
            ["status"] = template.Status,
            ["priority"] = template.Priority,
            ["entityScope"] = template.EntityScope,
            ["name"] = template.Name,
            ["statement"] = template.Statement
        }), cancellationToken);
        await UpsertUsageAsync("Template", template.Id, template.Name, actor, cancellationToken);

        return Response<StrategyInstantiationResultDto>.Ok(new StrategyInstantiationResultDto
        {
            InstantiationBatchId = batch.Id,
            SourceType = "Template",
            SourceId = template.Id,
            CreatedCount = 1,
            DuplicateWarnings = duplicateWarnings,
            Warnings = warnings,
            CreatedRecords = new[]
            {
                new InstantiatedLiveRecordDto
                {
                    RuntimeObjectType = "Objective",
                    RuntimeObjectId = runtimeId,
                    SourceTemplateType = "Objective",
                    SourceTemplateId = template.Id,
                    SourceTemplateVersion = template.Version
                }
            }
        });
    }

    private async Task<Response<StrategyInstantiationResultDto>> InstantiateInitiativeAsync(string templateId, InstantiationBatch batch, Dictionary<string, string> overrides, bool allowDuplicates, string actor, CancellationToken cancellationToken, string? blueprintPackId = null, Dictionary<string, string>? objectiveRuntimeMap = null, Dictionary<string, string>? goalRuntimeMap = null)
    {
        var template = await _library.GetInitiativeTemplateAsync(templateId, cancellationToken);
        if (template is null) return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (!string.Equals(template.LifecycleStatus, "Published", StringComparison.OrdinalIgnoreCase))
            return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, new() { ["status"] = new() { "Template is not published." } });

        var objectiveRuntimeId = objectiveRuntimeMap is not null && objectiveRuntimeMap.TryGetValue(template.ParentObjectiveTemplateId, out var mappedObjective)
            ? mappedObjective
            : (await _objectives.ListAsync(cancellationToken)).FirstOrDefault(x => string.Equals(x.SourceTemplateId, template.ParentObjectiveTemplateId, StringComparison.OrdinalIgnoreCase))?.Id ?? template.ParentObjectiveTemplateId;
        var objective = await _objectives.GetByIdAsync(objectiveRuntimeId, cancellationToken);
        if (objective is null)
            return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["parentObjectiveId"] = new() { $"Runtime parent objective for template parent '{template.ParentObjectiveTemplateId}' not found." } });
        var goalRuntimeId = goalRuntimeMap is not null && goalRuntimeMap.TryGetValue(template.ParentGoalTemplateId, out var mappedGoal)
            ? mappedGoal
            : objective.ParentGoalId;

        var existingLinks = await _initiativeLinks.ListAsync(cancellationToken);
        var duplicate = existingLinks.Any(x =>
            string.Equals(x.SourceTemplateId, template.Id, StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(x.ParentObjectiveId, objectiveRuntimeId, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(x.StrategyLinkStatus, "Linked", StringComparison.OrdinalIgnoreCase)));
        var warnings = new List<string>();
        var duplicateWarnings = 0;
        if (duplicate)
        {
            duplicateWarnings++;
            warnings.Add($"Potential duplicate initiative for template '{template.Id}'.");
            if (!allowDuplicates)
                return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, new() { ["duplicate"] = new() { "Equivalent live initiative exists. Enable duplicate override to continue." } });
        }

        var runtimeId = $"init-{Guid.NewGuid():N}".Substring(0, 16);
        var metric = (await _library.ListInitiativeTemplateMetricsAsync(template.Id, cancellationToken)).FirstOrDefault();
        var link = new InitiativeStrategyLinkAggregate
        {
            Id = Guid.NewGuid().ToString("N"),
            InitiativeId = runtimeId,
            SourceSystem = "strategy-library",
            SourceRecordId = template.Id,
            ParentObjectiveId = objectiveRuntimeId,
            ParentGoalId = goalRuntimeId,
            StrategyLinkStatus = ApplyOverride("Linked", overrides, "status"),
            ContributionType = "Direct",
            ContributionWeight = objective.ContributionWeight,
            MetricBindingsJson = metric is null ? "[]" : $"{{\"successMeasure\":\"{Escape(metric.SuccessMeasure)}\",\"baseline\":{metric.BaselineValue},\"target\":{metric.TargetValue}}}",
            DecisionReference = template.DecisionReference,
            EvidenceReference = template.EvidenceReference,
            SponsoringCompanyId = ApplyOverride("enterprise", overrides, "sponsoringCompanyId"),
            ParticipatingCompanyIds = new List<string>(),
            Notes = ApplyOverride(template.Description, overrides, "notes"),
            Version = 1,
            SyncedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = actor,
            UpdatedBy = actor,
            SourceTemplateType = "Initiative",
            SourceTemplateId = template.Id,
            SourceTemplateVersion = template.Version,
            SourceBlueprintPackId = blueprintPackId,
            InstantiationBatchId = batch.Id,
            CreatedFromLibrary = true
        };
        await _initiativeLinks.AddOrUpdateAsync(link, cancellationToken);
        await _initiativeCache.UpsertManyAsync(new[]
        {
            new PpmInitiativeReadModelAggregate
            {
                Id = Guid.NewGuid().ToString("N"),
                InitiativeId = runtimeId,
                InitiativeName = ApplyOverride(template.Name, overrides, "name"),
                Description = template.Description,
                Owner = ApplyOverride(template.Owner, overrides, "owner"),
                Status = ApplyOverride(template.Status, overrides, "status"),
                Type = template.Type,
                StartDate = template.StartDate,
                EndDate = template.EndDate,
                WaveOrPhase = template.WaveOrPhase,
                Priority = template.Priority,
                Complexity = template.Complexity,
                PrimaryKpi = metric?.SuccessMeasure ?? "",
                BudgetEnvelope = template.BudgetEnvelope,
                Maturity = template.MaturityReadiness,
                SourceSystem = "strategy-library",
                SourceUpdatedAt = DateTime.UtcNow,
                CachedAt = DateTime.UtcNow
            }
        }, cancellationToken);

        await _library.AddInstantiationRecordsAsync(new[]
        {
            new InstantiationRecord
            {
                InstantiationBatchId = batch.Id,
                RuntimeObjectType = "Initiative",
                RuntimeObjectId = runtimeId,
                SourceTemplateType = "Initiative",
                SourceTemplateId = template.Id,
                SourceTemplateVersion = template.Version,
                SourceBlueprintPackId = blueprintPackId,
                CreatedAt = DateTime.UtcNow
            }
        }, cancellationToken);
        await _library.AddOverrideLogsAsync(BuildOverrideLogs(overrides, batch.Id, "Initiative", runtimeId, actor, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["owner"] = template.Owner,
            ["status"] = template.Status,
            ["name"] = template.Name
        }), cancellationToken);
        await UpsertUsageAsync("Template", template.Id, template.Name, actor, cancellationToken);

        return Response<StrategyInstantiationResultDto>.Ok(new StrategyInstantiationResultDto
        {
            InstantiationBatchId = batch.Id,
            SourceType = "Template",
            SourceId = template.Id,
            CreatedCount = 1,
            DuplicateWarnings = duplicateWarnings,
            Warnings = warnings,
            CreatedRecords = new[]
            {
                new InstantiatedLiveRecordDto
                {
                    RuntimeObjectType = "Initiative",
                    RuntimeObjectId = runtimeId,
                    SourceTemplateType = "Initiative",
                    SourceTemplateId = template.Id,
                    SourceTemplateVersion = template.Version
                }
            }
        });
    }

    private async Task<Response<StrategyInstantiationResultDto>> InstantiateProjectAsync(string templateId, InstantiationBatch batch, Dictionary<string, string> overrides, bool allowDuplicates, string actor, CancellationToken cancellationToken, string? blueprintPackId = null, Dictionary<string, string>? initiativeRuntimeMap = null, Dictionary<string, string>? objectiveRuntimeMap = null, Dictionary<string, string>? goalRuntimeMap = null)
    {
        var template = await _library.GetProjectTemplateAsync(templateId, cancellationToken);
        if (template is null) return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (!string.Equals(template.LifecycleStatus, "Published", StringComparison.OrdinalIgnoreCase))
            return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, new() { ["status"] = new() { "Template is not published." } });

        var initiativeRuntimeId = initiativeRuntimeMap is not null && initiativeRuntimeMap.TryGetValue(template.ParentInitiativeTemplateId, out var mappedInitiative)
            ? mappedInitiative
            : (await _initiativeLinks.ListAsync(cancellationToken)).FirstOrDefault(x => string.Equals(x.SourceTemplateId, template.ParentInitiativeTemplateId, StringComparison.OrdinalIgnoreCase))?.InitiativeId ?? template.ParentInitiativeTemplateId;
        var parentInitiative = await _initiativeLinks.GetByInitiativeIdAsync(initiativeRuntimeId, cancellationToken);
        if (parentInitiative is null)
            return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, new() { ["parentInitiativeId"] = new() { $"Runtime parent initiative for template parent '{template.ParentInitiativeTemplateId}' not found." } });

        var objectiveRuntimeId = objectiveRuntimeMap is not null && objectiveRuntimeMap.TryGetValue(template.ParentObjectiveTemplateId, out var mappedObjective)
            ? mappedObjective
            : parentInitiative.ParentObjectiveId;
        var goalRuntimeId = goalRuntimeMap is not null && goalRuntimeMap.TryGetValue(template.ParentGoalTemplateId, out var mappedGoal)
            ? mappedGoal
            : parentInitiative.ParentGoalId;

        var existingLinks = await _projectLinks.ListAsync(cancellationToken);
        var duplicate = existingLinks.Any(x =>
            string.Equals(x.SourceTemplateId, template.Id, StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(x.ParentInitiativeId, initiativeRuntimeId, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(x.StrategyLinkStatus, "Linked", StringComparison.OrdinalIgnoreCase)));
        var warnings = new List<string>();
        var duplicateWarnings = 0;
        if (duplicate)
        {
            duplicateWarnings++;
            warnings.Add($"Potential duplicate project for template '{template.Id}'.");
            if (!allowDuplicates)
                return Response<StrategyInstantiationResultDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, new() { ["duplicate"] = new() { "Equivalent live project exists. Enable duplicate override to continue." } });
        }

        var runtimeId = $"prj-{Guid.NewGuid():N}".Substring(0, 15);
        var metric = (await _library.ListProjectTemplateMetricsAsync(template.Id, cancellationToken)).FirstOrDefault();
        var link = new ProjectStrategyLinkAggregate
        {
            Id = Guid.NewGuid().ToString("N"),
            ProjectId = runtimeId,
            SourceSystem = "strategy-library",
            SourceRecordId = template.Id,
            ParentInitiativeId = initiativeRuntimeId,
            ParentObjectiveId = objectiveRuntimeId,
            ParentGoalId = goalRuntimeId,
            StrategyLinkStatus = ApplyOverride("Linked", overrides, "status"),
            ContributionNote = ApplyOverride(template.Description, overrides, "notes"),
            MetricBindingsJson = metric is null ? "[]" : $"{{\"successMetric\":\"{Escape(metric.SuccessMetric)}\",\"baseline\":{metric.BaselineValue},\"target\":{metric.TargetValue}}}",
            DecisionReference = template.DecisionReference,
            EvidenceReference = template.EvidenceReference,
            DeliveryCompanyId = ApplyOverride("enterprise", overrides, "deliveryCompanyId"),
            FundingCompanyId = ApplyOverride("", overrides, "fundingCompanyId"),
            Version = 1,
            SyncedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = actor,
            UpdatedBy = actor,
            SourceTemplateType = "Project",
            SourceTemplateId = template.Id,
            SourceTemplateVersion = template.Version,
            SourceBlueprintPackId = blueprintPackId,
            InstantiationBatchId = batch.Id,
            CreatedFromLibrary = true
        };
        await _projectLinks.AddOrUpdateAsync(link, cancellationToken);
        await _projectCache.UpsertManyAsync(new[]
        {
            new PpmProjectReadModelAggregate
            {
                Id = Guid.NewGuid().ToString("N"),
                ProjectId = runtimeId,
                ProjectName = ApplyOverride(template.Name, overrides, "name"),
                Description = template.Description,
                OwnerPm = ApplyOverride(template.OwnerPm, overrides, "ownerPm"),
                Sponsor = ApplyOverride(template.Sponsor, overrides, "sponsor"),
                Status = ApplyOverride(template.Status, overrides, "status"),
                Phase = template.Phase,
                StartDate = template.StartDate,
                EndDate = template.EndDate,
                DeliveryType = template.DeliveryType,
                SuccessMetric = metric?.SuccessMetric ?? "",
                RiskRating = template.RiskRating,
                ReadinessStatus = template.ReadinessStatus,
                BudgetSummary = template.BudgetSummary,
                SourceSystem = "strategy-library",
                SourceUpdatedAt = DateTime.UtcNow,
                CachedAt = DateTime.UtcNow
            }
        }, cancellationToken);

        await _library.AddInstantiationRecordsAsync(new[]
        {
            new InstantiationRecord
            {
                InstantiationBatchId = batch.Id,
                RuntimeObjectType = "Project",
                RuntimeObjectId = runtimeId,
                SourceTemplateType = "Project",
                SourceTemplateId = template.Id,
                SourceTemplateVersion = template.Version,
                SourceBlueprintPackId = blueprintPackId,
                CreatedAt = DateTime.UtcNow
            }
        }, cancellationToken);
        await _library.AddOverrideLogsAsync(BuildOverrideLogs(overrides, batch.Id, "Project", runtimeId, actor, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = template.Name,
            ["ownerPm"] = template.OwnerPm,
            ["sponsor"] = template.Sponsor,
            ["status"] = template.Status
        }), cancellationToken);
        await UpsertUsageAsync("Template", template.Id, template.Name, actor, cancellationToken);

        return Response<StrategyInstantiationResultDto>.Ok(new StrategyInstantiationResultDto
        {
            InstantiationBatchId = batch.Id,
            SourceType = "Template",
            SourceId = template.Id,
            CreatedCount = 1,
            DuplicateWarnings = duplicateWarnings,
            Warnings = warnings,
            CreatedRecords = new[]
            {
                new InstantiatedLiveRecordDto
                {
                    RuntimeObjectType = "Project",
                    RuntimeObjectId = runtimeId,
                    SourceTemplateType = "Project",
                    SourceTemplateId = template.Id,
                    SourceTemplateVersion = template.Version
                }
            }
        });
    }

    private async Task UpsertUsageAsync(string itemType, string itemId, string itemName, string actor, CancellationToken cancellationToken)
    {
        var all = await _library.ListUsageStatsAsync(cancellationToken);
        var row = all.FirstOrDefault(x => string.Equals(x.ItemType, itemType, StringComparison.OrdinalIgnoreCase) &&
                                          string.Equals(x.ItemId, itemId, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            row = new TemplateUsageStat
            {
                Id = Guid.NewGuid().ToString("N"),
                ItemType = itemType,
                ItemId = itemId,
                ItemName = itemName,
                UsageCount = 1,
                LastInstantiatedBy = actor,
                LastInstantiatedAt = DateTime.UtcNow
            };
        }
        else
        {
            row.UsageCount += 1;
            row.ItemName = itemName;
            row.LastInstantiatedBy = actor;
            row.LastInstantiatedAt = DateTime.UtcNow;
        }
        await _library.UpsertUsageStatsAsync(new[] { row }, cancellationToken);
    }

    private static List<TemplateOverrideLog> BuildOverrideLogs(
        Dictionary<string, string> overrides,
        string batchId,
        string runtimeType,
        string runtimeId,
        string actor,
        Dictionary<string, string> sourceValues)
    {
        var rows = new List<TemplateOverrideLog>();
        foreach (var kv in overrides)
        {
            if (!sourceValues.TryGetValue(kv.Key, out var before)) continue;
            var after = kv.Value ?? "";
            if (string.Equals(before ?? "", after, StringComparison.Ordinal)) continue;
            rows.Add(new TemplateOverrideLog
            {
                Id = Guid.NewGuid().ToString("N"),
                InstantiationBatchId = batchId,
                RuntimeObjectType = runtimeType,
                RuntimeObjectId = runtimeId,
                FieldName = kv.Key,
                BeforeValue = before ?? "",
                AfterValue = after,
                Actor = actor,
                At = DateTime.UtcNow
            });
        }
        return rows;
    }

    private static string ApplyOverride(string current, Dictionary<string, string> overrides, string key)
        => overrides.TryGetValue(key, out var next) && !string.IsNullOrWhiteSpace(next) ? next.Trim() : current;

    private static string InferTemplateType(string templateId)
    {
        var v = templateId.ToLowerInvariant();
        if (v.StartsWith("sg-") || v.StartsWith("goal-")) return "Goal";
        if (v.StartsWith("obj-")) return "Objective";
        if (v.StartsWith("in-") || v.StartsWith("init-")) return "Initiative";
        if (v.StartsWith("pr-") || v.StartsWith("prj-")) return "Project";
        return string.Empty;
    }

    private static string Escape(string text) => (text ?? string.Empty).Replace("\"", "\\\"");

    private static string FlattenErrors(Dictionary<string, List<string>>? errors)
    {
        if (errors is null || errors.Count == 0) return "Unknown error";
        return string.Join("; ", errors.SelectMany(x => x.Value));
    }

    private static TemplateImportIssue NewIssue(string batchId, string severity, string sheet, int row, string code, string message)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            BatchId = batchId,
            Severity = severity,
            SheetName = sheet,
            RowNumber = row,
            Code = code,
            Message = message
        };

    private static StrategyLibraryImportBatchDto ToImportBatchDto(TemplateImportBatch batch, IReadOnlyList<TemplateImportIssue> issues) =>
        new()
        {
            BatchId = batch.Id,
            BatchName = batch.Name,
            Status = batch.Status,
            ImportedAt = batch.ImportedAt,
            ImportedBy = batch.ImportedBy,
            TotalRowsRead = batch.TotalRowsRead,
            UniqueTemplatesCreated = batch.UniqueTemplatesCreated,
            DuplicateRowsCollapsed = batch.DuplicateRowsCollapsed,
            InvalidParentReferences = batch.InvalidParentReferences,
            MissingIds = batch.MissingIds,
            RepeatedMetricsDetected = batch.RepeatedMetricsDetected,
            OrphanRows = batch.OrphanRows,
            VersionConflicts = batch.VersionConflicts,
            Issues = issues.Select(x => new StrategyLibraryImportIssueDto
            {
                Severity = x.Severity,
                SheetName = x.SheetName,
                RowNumber = x.RowNumber,
                Code = x.Code,
                Message = x.Message
            }).ToList()
        };

    private sealed class GoalNormalizeResult
    {
        public List<GoalTemplate> Goals { get; } = new();
        public Dictionary<string, List<GoalTemplateMetric>> Metrics { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int DuplicatesCollapsed { get; set; }
        public int RepeatedMetricRows { get; set; }
    }

    private sealed class ObjectiveNormalizeResult
    {
        public List<ObjectiveTemplate> Objectives { get; } = new();
        public Dictionary<string, List<ObjectiveTemplateMetric>> Metrics { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int DuplicatesCollapsed { get; set; }
        public int RepeatedMetricRows { get; set; }
    }

    private sealed class InitiativeNormalizeResult
    {
        public List<InitiativeTemplate> Initiatives { get; } = new();
        public Dictionary<string, List<InitiativeTemplateMetric>> Metrics { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int DuplicatesCollapsed { get; set; }
        public int RepeatedMetricRows { get; set; }
    }

    private sealed class ProjectNormalizeResult
    {
        public List<ProjectTemplate> Projects { get; } = new();
        public Dictionary<string, List<ProjectTemplateMetric>> Metrics { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int DuplicatesCollapsed { get; set; }
        public int RepeatedMetricRows { get; set; }
    }

    private sealed class BlueprintNormalizeResult
    {
        public List<StrategyBlueprintPack> Packs { get; } = new();
        public Dictionary<string, List<StrategyBlueprintPackItem>> Items { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int DuplicatesCollapsed { get; set; }
    }

    private static GoalNormalizeResult NormalizeGoalTemplates(List<Dictionary<string, string>> rows, List<TemplateImportIssue> issues)
    {
        var result = new GoalNormalizeResult();
        var grouped = new Dictionary<string, List<(Dictionary<string, string> Row, int Index)>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var id = Get(row, "Goal ID");
            if (string.IsNullOrWhiteSpace(id))
            {
                issues.Add(NewIssue("", "Warning", "Goals_List", i + 2, "MISSING_ID", "Missing Goal ID; row ignored."));
                continue;
            }
            if (!grouped.TryGetValue(id, out var list))
            {
                list = new List<(Dictionary<string, string>, int)>();
                grouped[id] = list;
            }
            list.Add((row, i));
        }

        foreach (var kv in grouped)
        {
            var first = kv.Value[0].Row;
            result.DuplicatesCollapsed += Math.Max(0, kv.Value.Count - 1);
            var template = new GoalTemplate
            {
                Id = kv.Key,
                Name = Get(first, "Goal"),
                Category = GoalTemplateTypeCatalog.NormalizeOrDefault(EmptyAs(Get(first, "Goal Type"), Get(first, "Goal Category"))),
                Statement = Get(first, "Goal Statement"),
                Owner = Get(first, "Goal Owner"),
                Status = EmptyAs(Get(first, "Goal Status"), "Draft"),
                PlanningHorizonStart = ParseDateOrYear(Get(first, "Planning Horizon Start")),
                PlanningHorizonEnd = ParseDateOrYear(Get(first, "Planning Horizon End")),
                Priority = Get(first, "Priority"),
                EntityScope = Get(first, "Related Entity Scope"),
                DecisionReference = Get(first, "Decision Reference"),
                EvidenceReference = Get(first, "Evidence Link"),
                ChangeLogRef = Get(first, "Change Log Ref"),
                Version = ParseInt(Get(first, "Version"), 1),
                LifecycleStatus = "Draft",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            result.Goals.Add(template);

            var metricRows = new List<GoalTemplateMetric>();
            var metricSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (r, idx) in kv.Value)
            {
                var metricName = Get(r, "Goal Metric");
                var metricType = Get(r, "Goal Metric Type");
                var key = $"{metricName}|{metricType}|{Get(r, "Baseline Value")}|{Get(r, "Target Value")}|{Get(r, "Unit of Measure")}|{Get(r, "Aggregation Method")}";
                if (string.IsNullOrWhiteSpace(metricName) && string.IsNullOrWhiteSpace(metricType)) continue;
                if (!metricSeen.Add(key))
                {
                    result.RepeatedMetricRows++;
                    continue;
                }
                metricRows.Add(new GoalTemplateMetric
                {
                    Id = Guid.NewGuid().ToString("N"),
                    GoalTemplateId = kv.Key,
                    MetricName = metricName,
                    MetricType = metricType,
                    BaselineValue = ParseDecimal(Get(r, "Baseline Value")),
                    TargetValue = ParseDecimal(Get(r, "Target Value")),
                    UnitOfMeasure = Get(r, "Unit of Measure"),
                    AggregationMethod = Get(r, "Aggregation Method")
                });
                if (string.IsNullOrWhiteSpace(metricName))
                    issues.Add(NewIssue("", "Info", "Goals_List", idx + 2, "REPEATED_METRIC_ATTR", "Metric row without Goal Metric name was normalized as an unnamed metric."));
            }
            result.Metrics[kv.Key] = metricRows;
        }

        return result;
    }

    private static ObjectiveNormalizeResult NormalizeObjectiveTemplates(List<Dictionary<string, string>> rows, List<TemplateImportIssue> issues)
    {
        var result = new ObjectiveNormalizeResult();
        var grouped = new Dictionary<string, List<(Dictionary<string, string> Row, int Index)>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var id = Get(row, "Objective ID");
            if (string.IsNullOrWhiteSpace(id))
            {
                issues.Add(NewIssue("", "Warning", "Objectives_List", i + 2, "MISSING_ID", "Missing Objective ID; row ignored."));
                continue;
            }
            if (!grouped.TryGetValue(id, out var list))
            {
                list = new List<(Dictionary<string, string>, int)>();
                grouped[id] = list;
            }
            list.Add((row, i));
        }

        foreach (var kv in grouped)
        {
            var first = kv.Value[0].Row;
            result.DuplicatesCollapsed += Math.Max(0, kv.Value.Count - 1);
            var template = new ObjectiveTemplate
            {
                Id = kv.Key,
                ParentGoalTemplateId = Get(first, "Parent Goal ID"),
                Name = Get(first, "Objective"),
                Statement = Get(first, "Objective Statement"),
                Owner = Get(first, "Objective Owner"),
                Status = EmptyAs(Get(first, "Objective Status"), "Draft"),
                Type = Get(first, "Objective Type"),
                TimeHorizonStart = ParseDateOrYear(Get(first, "Time Horizon Start")),
                TimeHorizonEnd = ParseDateOrYear(Get(first, "Time Horizon End")),
                Priority = Get(first, "Priority"),
                ContributionType = Get(first, "Contribution Type"),
                ContributionWeight = ParseDecimal(Get(first, "Contribution Weight %")),
                EntityScope = Get(first, "Entity Scope"),
                DependencyNotes = Get(first, "Dependency Notes"),
                DecisionReference = Get(first, "Decision Ref"),
                EvidenceReference = Get(first, "Evidence Ref"),
                Version = ParseInt(Get(first, "Version"), 1),
                LifecycleStatus = "Draft",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            if (string.IsNullOrWhiteSpace(template.ParentGoalTemplateId))
                issues.Add(NewIssue("", "Warning", "Objectives_List", kv.Value[0].Index + 2, "INVALID_PARENT", $"Objective '{template.Id}' has missing Parent Goal ID."));
            result.Objectives.Add(template);

            var metricRows = new List<ObjectiveTemplateMetric>();
            var metricSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (r, idx) in kv.Value)
            {
                var metricName = EmptyAs(Get(r, "Objective Metric"), EmptyAs(Get(r, "Metric"), "Objective Target"));
                var baseline = ParseDecimal(Get(r, "Baseline Value"));
                var target = ParseDecimal(Get(r, "Target Value"));
                var agg = Get(r, "Aggregation Method");
                var uom = Get(r, "Unit of Measure");
                var key = $"{metricName}|{baseline}|{target}|{agg}|{uom}";
                if (!metricSeen.Add(key))
                {
                    result.RepeatedMetricRows++;
                    continue;
                }
                metricRows.Add(new ObjectiveTemplateMetric
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ObjectiveTemplateId = kv.Key,
                    MetricName = metricName,
                    BaselineValue = baseline,
                    TargetValue = target,
                    AggregationMethod = agg,
                    UnitOfMeasure = uom
                });
                if (string.IsNullOrWhiteSpace(Get(r, "Objective Metric")) && string.IsNullOrWhiteSpace(Get(r, "Metric")))
                    issues.Add(NewIssue("", "Info", "Objectives_List", idx + 2, "REPEATED_METRIC_ATTR", "Objective metric row was normalized from repeated metric-bearing attributes."));
            }
            result.Metrics[kv.Key] = metricRows;
        }

        return result;
    }

    private static InitiativeNormalizeResult NormalizeInitiativeTemplates(List<Dictionary<string, string>> rows, List<TemplateImportIssue> issues)
    {
        var result = new InitiativeNormalizeResult();
        var grouped = new Dictionary<string, List<(Dictionary<string, string> Row, int Index)>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var id = Get(row, "Initiative ID");
            if (string.IsNullOrWhiteSpace(id))
            {
                issues.Add(NewIssue("", "Warning", "Initiatives_List", i + 2, "MISSING_ID", "Missing Initiative ID; row ignored."));
                continue;
            }
            if (!grouped.TryGetValue(id, out var list))
            {
                list = new List<(Dictionary<string, string>, int)>();
                grouped[id] = list;
            }
            list.Add((row, i));
        }

        foreach (var kv in grouped)
        {
            var first = kv.Value[0].Row;
            result.DuplicatesCollapsed += Math.Max(0, kv.Value.Count - 1);
            var template = new InitiativeTemplate
            {
                Id = kv.Key,
                ParentObjectiveTemplateId = Get(first, "Parent Objective ID"),
                ParentGoalTemplateId = Get(first, "Parent Goal ID"),
                Name = Get(first, "Initiative"),
                Description = Get(first, "Initiative Description"),
                Owner = Get(first, "Initiative Owner"),
                Status = EmptyAs(Get(first, "Initiative Status"), "Draft"),
                Type = Get(first, "Initiative Type"),
                StartDate = ParseDateOrYear(Get(first, "Start Date")),
                EndDate = ParseDateOrYear(Get(first, "End Date")),
                WaveOrPhase = Get(first, "Planning Wave / Phase"),
                Priority = Get(first, "Priority"),
                Complexity = Get(first, "Complexity"),
                DependencyIds = Get(first, "Dependency IDs"),
                EntityScope = Get(first, "Entity Scope"),
                BudgetEnvelope = Get(first, "Budget Envelope"),
                MaturityReadiness = Get(first, "Maturity / Readiness"),
                DecisionReference = Get(first, "Decision Ref"),
                EvidenceReference = Get(first, "Evidence Ref"),
                InitiativeClass = Get(first, "Initiative Class"),
                Version = ParseInt(Get(first, "Version"), 1),
                LifecycleStatus = "Draft",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            if (string.IsNullOrWhiteSpace(template.ParentGoalTemplateId) || string.IsNullOrWhiteSpace(template.ParentObjectiveTemplateId))
                issues.Add(NewIssue("", "Warning", "Initiatives_List", kv.Value[0].Index + 2, "INVALID_PARENT", $"Initiative '{template.Id}' has missing parent reference(s)."));
            result.Initiatives.Add(template);

            var metricRows = new List<InitiativeTemplateMetric>();
            var metricSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (r, _) in kv.Value)
            {
                var successMeasure = Get(r, "Primary KPI / Success Measure");
                var baseline = ParseDecimal(Get(r, "Baseline"));
                var target = ParseDecimal(Get(r, "Target"));
                var key = $"{successMeasure}|{baseline}|{target}";
                if (string.IsNullOrWhiteSpace(successMeasure) && baseline == 0m && target == 0m) continue;
                if (!metricSeen.Add(key))
                {
                    result.RepeatedMetricRows++;
                    continue;
                }
                metricRows.Add(new InitiativeTemplateMetric
                {
                    Id = Guid.NewGuid().ToString("N"),
                    InitiativeTemplateId = kv.Key,
                    SuccessMeasure = successMeasure,
                    BaselineValue = baseline,
                    TargetValue = target
                });
            }
            result.Metrics[kv.Key] = metricRows;
        }

        return result;
    }

    private static ProjectNormalizeResult NormalizeProjectTemplates(List<Dictionary<string, string>> rows, List<TemplateImportIssue> issues)
    {
        var result = new ProjectNormalizeResult();
        var grouped = new Dictionary<string, List<(Dictionary<string, string> Row, int Index)>>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var id = Get(row, "Project ID");
            if (string.IsNullOrWhiteSpace(id))
            {
                issues.Add(NewIssue("", "Warning", "Projects_List", i + 2, "MISSING_ID", "Missing Project ID; row ignored."));
                continue;
            }
            if (!grouped.TryGetValue(id, out var list))
            {
                list = new List<(Dictionary<string, string>, int)>();
                grouped[id] = list;
            }
            list.Add((row, i));
        }

        foreach (var kv in grouped)
        {
            var first = kv.Value[0].Row;
            result.DuplicatesCollapsed += Math.Max(0, kv.Value.Count - 1);
            var template = new ProjectTemplate
            {
                Id = kv.Key,
                ParentInitiativeTemplateId = Get(first, "Parent Initiative ID"),
                ParentObjectiveTemplateId = Get(first, "Parent Objective ID"),
                ParentGoalTemplateId = Get(first, "Parent Goal ID"),
                Name = Get(first, "Project"),
                Description = Get(first, "Project Description"),
                OwnerPm = Get(first, "Project Owner / PM"),
                Sponsor = Get(first, "Project Sponsor"),
                Status = EmptyAs(Get(first, "Project Status"), "Draft"),
                Phase = Get(first, "Stage / Phase"),
                StartDate = ParseDateOrYear(Get(first, "Start Date")),
                EndDate = ParseDateOrYear(Get(first, "End Date")),
                MilestoneFlag = Get(first, "Milestone Flag / Key Deliverable"),
                DependencyIds = Get(first, "Dependency IDs"),
                DeliveryType = Get(first, "Delivery Type"),
                EntityScope = Get(first, "Entity Scope"),
                BudgetSummary = Get(first, "Budget / CapEx / OpEx"),
                RiskRating = Get(first, "Risk Rating"),
                ReadinessStatus = Get(first, "Readiness Status"),
                DecisionReference = Get(first, "Decision Ref"),
                EvidenceReference = Get(first, "Evidence Ref"),
                Version = ParseVersion(Get(first, "Version"), 1),
                LifecycleStatus = "Draft",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            if (string.IsNullOrWhiteSpace(template.ParentGoalTemplateId) || string.IsNullOrWhiteSpace(template.ParentObjectiveTemplateId) || string.IsNullOrWhiteSpace(template.ParentInitiativeTemplateId))
                issues.Add(NewIssue("", "Warning", "Projects_List", kv.Value[0].Index + 2, "INVALID_PARENT", $"Project '{template.Id}' has missing parent reference(s)."));
            result.Projects.Add(template);

            var metricRows = new List<ProjectTemplateMetric>();
            var metricSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (r, _) in kv.Value)
            {
                var successMetric = Get(r, "Project Success Metric");
                var baseline = ParseDecimal(Get(r, "Metric Baseline"));
                var target = ParseDecimal(Get(r, "Metric Target"));
                var metricType = Get(r, "Metric Type");
                var uom = Get(r, "Unit of Measure");
                var aggregation = Get(r, "Aggregation Method");
                var key = $"{successMetric}|{metricType}|{uom}|{aggregation}|{baseline}|{target}";
                if (string.IsNullOrWhiteSpace(successMetric) && baseline == 0m && target == 0m) continue;
                if (!metricSeen.Add(key))
                {
                    result.RepeatedMetricRows++;
                    continue;
                }
                var orderRaw = Get(r, "Display Order");
                var displayOrder = int.TryParse(orderRaw, out var od) ? od : metricRows.Count;
                metricRows.Add(new ProjectTemplateMetric
                {
                    Id = Guid.NewGuid().ToString("N"),
                    ProjectTemplateId = kv.Key,
                    SuccessMetric = successMetric,
                    MetricType = metricType,
                    BaselineValue = baseline,
                    TargetValue = target,
                    UnitOfMeasure = uom,
                    AggregationMethod = aggregation,
                    DisplayOrder = displayOrder
                });
            }
            result.Metrics[kv.Key] = metricRows;
        }

        return result;
    }

    private static BlueprintNormalizeResult NormalizeBlueprintPacks(List<Dictionary<string, string>> rows, string batchId, List<TemplateImportIssue> issues)
    {
        var result = new BlueprintNormalizeResult();
        var packId = $"pack-{batchId[..8]}";
        var pack = new StrategyBlueprintPack
        {
            Id = packId,
            Name = "Imported Blueprint Pack",
            Description = "Generated from Connection_Map.",
            Owner = "Strategy Library Import",
            Status = "Draft",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        result.Packs.Add(pack);

        var items = new List<StrategyBlueprintPackItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var goalId = EmptyAs(Get(row, "Goal ID"), Get(row, "Goal Template ID"));
            var objectiveId = EmptyAs(Get(row, "Objective ID"), Get(row, "Objective Template ID"));
            var initiativeId = EmptyAs(Get(row, "Initiative ID"), Get(row, "Initiative Template ID"));
            var projectId = EmptyAs(Get(row, "Project ID"), Get(row, "Project Template ID"));
            if (string.IsNullOrWhiteSpace(goalId) && string.IsNullOrWhiteSpace(objectiveId) && string.IsNullOrWhiteSpace(initiativeId) && string.IsNullOrWhiteSpace(projectId))
            {
                issues.Add(NewIssue("", "Info", "Connection_Map", i + 2, "ORPHAN_ROW", "Connection_Map row has no hierarchy IDs and was skipped."));
                continue;
            }
            var key = $"{goalId}|{objectiveId}|{initiativeId}|{projectId}";
            if (!seen.Add(key))
            {
                result.DuplicatesCollapsed++;
                continue;
            }
            items.Add(new StrategyBlueprintPackItem
            {
                Id = Guid.NewGuid().ToString("N"),
                BlueprintPackId = packId,
                GoalTemplateId = goalId,
                ObjectiveTemplateId = objectiveId,
                InitiativeTemplateId = initiativeId,
                ProjectTemplateId = projectId,
                AggregationMethod = Get(row, "Aggregation Method"),
                PlanningYearStart = ParseNullableInt(Get(row, "Baseline Year")),
                PlanningYearEnd = ParseNullableInt(Get(row, "Target Year"))
            });
        }
        result.Items[packId] = items;
        return result;
    }

    private static string Get(Dictionary<string, string> row, string key)
    {
        foreach (var kv in row)
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                return (kv.Value ?? "").Trim();
        return string.Empty;
    }

    private static string EmptyAs(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static int ParseInt(string value, int fallback)
    {
        if (int.TryParse(value, out var parsed)) return parsed;
        return fallback;
    }

    private static int? ParseNullableInt(string value)
    {
        if (int.TryParse(value, out var parsed)) return parsed;
        return null;
    }

    private static decimal ParseDecimal(string value)
    {
        if (decimal.TryParse(value, out var parsed)) return parsed;
        return 0m;
    }

    private static DateTime? ParseDateOrYear(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (int.TryParse(value, out var year) && year > 1900 && year < 3000)
            return new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        if (DateTime.TryParse(value, out var dt)) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return null;
    }
}
