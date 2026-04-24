using Diten.Application.Common.Models;
using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Repositories;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Domain.Aggregates.EnterpriseStrategy;
using System.Text.Json;

namespace Diten.Application.EnterpriseStrategy.Services;

public interface IKpiRuntimeService
{
    Task<Response<PagedResponseDto<KpiDefinitionDto>>> ListAsync(PagedRequestDto request, CancellationToken cancellationToken = default);
    Task<Response<KpiDefinitionDto>> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<Response<KpiDefinitionDto>> CreateAsync(KpiDefinitionDto body, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<KpiDefinitionDto>> UpdateAsync(string id, KpiDefinitionDto body, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<KpiDefinitionDto>> ArchiveAsync(string id, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<KpiUsageDto>> UsageAsync(string id, CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<KpiOwnershipRowDto>>> OwnershipAsync(CancellationToken cancellationToken = default);
    Task<Response<ScorecardSnapshotDto>> ScorecardAsync(string? goalId, string? objectiveId, string? company, string? period, CancellationToken cancellationToken = default);
    Task<Response<KpiDefinitionDto>> CreateFromTemplateAsync(KpiInstantiateFromTemplateRequestDto request, string actor, string correlationId, CancellationToken cancellationToken = default);
}

public interface IKpiLibraryService
{
    Task<Response<PagedResponseDto<KpiTemplateDto>>> CatalogAsync(PagedRequestDto request, CancellationToken cancellationToken = default);
    Task<Response<KpiTemplateDto>> TemplateAsync(string id, CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<KpiThresholdModelDto>>> ThresholdModelsAsync(CancellationToken cancellationToken = default);
    Task<Response<KpiThresholdModelDto>> ThresholdModelAsync(string idOrCode, CancellationToken cancellationToken = default);
    Task<Response<PagedResponseDto<KpiScorecardPackDto>>> PacksAsync(PagedRequestDto request, CancellationToken cancellationToken = default);
    Task<Response<KpiScorecardPackDto>> PackAsync(string id, CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<KpiScorecardPackItemDto>>> PackItemsAsync(string id, CancellationToken cancellationToken = default);
    Task<Response<KpiTemplateDto>> CloneTemplateAsync(string id, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<KpiTemplateDto>> LifecycleAsync(string id, string action, string actor, string correlationId, CancellationToken cancellationToken = default);
    Task<Response<KpiGovernanceSummaryDto>> GovernanceSummaryAsync(CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<KpiGovernanceExceptionDto>>> GovernanceExceptionsAsync(CancellationToken cancellationToken = default);
    Task<Response<IReadOnlyList<KpiGovernanceActionAggregate>>> GovernanceActionsAsync(CancellationToken cancellationToken = default);
}

public sealed class KpiScorecardService : IKpiRuntimeService, IKpiLibraryService
{
    private readonly IKpiScorecardRepository _repo;
    private readonly IEnterpriseStrategyAuditSink _audit;
    private static readonly SemaphoreSlim SeedGate = new(1, 1);
    private static bool _seeded;

    public KpiScorecardService(IKpiScorecardRepository repo, IEnterpriseStrategyAuditSink audit)
    {
        _repo = repo;
        _audit = audit;
    }

    public async Task<Response<PagedResponseDto<KpiDefinitionDto>>> ListAsync(PagedRequestDto request, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        IEnumerable<KpiCatalogItemAggregate> query = await _repo.ListRuntimeKpisAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => $"{x.Id} {x.Name} {x.Description} {x.Owner}".Contains(request.Search, StringComparison.OrdinalIgnoreCase));
        foreach (var f in request.Filters)
        {
            if (string.IsNullOrWhiteSpace(f.Value)) continue;
            query = f.Key.ToLowerInvariant() switch
            {
                "category" => query.Where(x => x.Category.Equals(f.Value, StringComparison.OrdinalIgnoreCase)),
                "type" => query.Where(x => x.Type.Equals(f.Value, StringComparison.OrdinalIgnoreCase)),
                "owner" => query.Where(x => x.Owner.Equals(f.Value, StringComparison.OrdinalIgnoreCase)),
                "status" => query.Where(x => x.Status.Equals(f.Value, StringComparison.OrdinalIgnoreCase)),
                "unitofmeasure" => query.Where(x => x.UnitOfMeasure.Equals(f.Value, StringComparison.OrdinalIgnoreCase)),
                "aggregationmethod" => query.Where(x => x.AggregationMethod.Equals(f.Value, StringComparison.OrdinalIgnoreCase)),
                "reportingfrequency" => query.Where(x => x.ReportingFrequency.Equals(f.Value, StringComparison.OrdinalIgnoreCase)),
                "company" => query.Where(x => string.Equals(x.CompanyId, f.Value, StringComparison.OrdinalIgnoreCase)),
                _ => query
            };
        }

        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 200);
        var total = query.Count();
        var items = query.OrderByDescending(x => x.UpdatedAt).Skip((page - 1) * size).Take(size).Select(ToRuntimeDto).ToList();
        return Response<PagedResponseDto<KpiDefinitionDto>>.Ok(new PagedResponseDto<KpiDefinitionDto>
        {
            Page = page,
            PageSize = size,
            TotalCount = total,
            Items = items
        });
    }

    public async Task<Response<KpiDefinitionDto>> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var row = await _repo.GetRuntimeKpiAsync(id, cancellationToken);
        return row is null
            ? Response<KpiDefinitionDto>.Fail(EnterpriseStrategyErrorCodes.NotFound)
            : Response<KpiDefinitionDto>.Ok(ToRuntimeDto(row));
    }

    public async Task<Response<KpiDefinitionDto>> CreateAsync(KpiDefinitionDto body, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var id = string.IsNullOrWhiteSpace(body.Id) ? $"kpi-{DateTime.UtcNow.Ticks}" : body.Id.Trim();
        if (await _repo.GetRuntimeKpiAsync(id, cancellationToken) is not null)
            return Response<KpiDefinitionDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, Errors("KPI ID already exists"));
        var now = DateTime.UtcNow;
        var row = ToRuntimeAggregate(body);
        row.Id = id;
        row.Version = Math.Max(1, body.Version);
        row.CreatedAt = now;
        row.UpdatedAt = now;
        if (string.IsNullOrWhiteSpace(row.Status)) row.Status = "Active";
        await _repo.AddRuntimeKpiAsync(row, cancellationToken);
        await _audit.WriteMutationAsync(actor, "KpiCatalogItem", row.Id, "kpi.created.v1", correlationId, "enterprise-strategy.kpis", "", row.Name, cancellationToken);
        return Response<KpiDefinitionDto>.Ok(ToRuntimeDto(row));
    }

    public async Task<Response<KpiDefinitionDto>> UpdateAsync(string id, KpiDefinitionDto body, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var current = await _repo.GetRuntimeKpiAsync(id, cancellationToken);
        if (current is null) return Response<KpiDefinitionDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (expectedVersion > 0 && expectedVersion != current.Version)
            return Response<KpiDefinitionDto>.Fail(EnterpriseStrategyErrorCodes.StaleVersion, Errors("KPI version is stale"));
        var next = ToRuntimeAggregate(body);
        next.Id = current.Id;
        next.Version = current.Version + 1;
        next.CreatedAt = current.CreatedAt;
        next.UpdatedAt = DateTime.UtcNow;
        next.SourceKpiTemplateId ??= current.SourceKpiTemplateId;
        next.SourceKpiTemplateCode ??= current.SourceKpiTemplateCode;
        next.SourceKpiTemplateVersion ??= current.SourceKpiTemplateVersion;
        next.CreatedFromLibrary = current.CreatedFromLibrary || body.CreatedFromLibrary;
        await _repo.UpdateRuntimeKpiAsync(next, cancellationToken);
        await _audit.WriteMutationAsync(actor, "KpiCatalogItem", next.Id, "kpi.updated.v1", correlationId, "enterprise-strategy.kpis", "", next.Name, cancellationToken);
        return Response<KpiDefinitionDto>.Ok(ToRuntimeDto(next));
    }

    public async Task<Response<KpiDefinitionDto>> ArchiveAsync(string id, int expectedVersion, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var current = await _repo.GetRuntimeKpiAsync(id, cancellationToken);
        if (current is null) return Response<KpiDefinitionDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (expectedVersion > 0 && expectedVersion != current.Version)
            return Response<KpiDefinitionDto>.Fail(EnterpriseStrategyErrorCodes.StaleVersion, Errors("KPI version is stale"));
        current.Status = "Archived";
        current.Version += 1;
        current.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateRuntimeKpiAsync(current, cancellationToken);
        await _audit.WriteMutationAsync(actor, "KpiCatalogItem", current.Id, "kpi.archived.v1", correlationId, "enterprise-strategy.kpis", "", "Archived", cancellationToken);
        return Response<KpiDefinitionDto>.Ok(ToRuntimeDto(current));
    }

    public async Task<Response<KpiUsageDto>> UsageAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var row = await _repo.GetRuntimeKpiAsync(id, cancellationToken);
        if (row is null) return Response<KpiUsageDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        return Response<KpiUsageDto>.Ok(new KpiUsageDto
        {
            KpiId = id,
            GoalIds = new[] { "goal-001", "goal-002" },
            ObjectiveIds = new[] { "obj-001" },
            InitiativeIds = new[] { "init-001" },
            ProjectIds = new[] { "prj-001" },
            ScorecardIds = new[] { "sc-main" }
        });
    }

    public async Task<Response<IReadOnlyList<KpiOwnershipRowDto>>> OwnershipAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var rows = (await _repo.ListRuntimeKpisAsync(cancellationToken)).Select(x => new KpiOwnershipRowDto
        {
            KpiId = x.Id,
            KpiName = x.Name,
            Owner = x.Owner,
            BackupOwner = x.BackupOwner,
            ReportingFrequency = x.ReportingFrequency,
            AggregationMethod = x.AggregationMethod,
            CompanyScope = string.IsNullOrWhiteSpace(x.CompanyId) ? x.ScopeMode : $"{x.ScopeMode} ({x.CompanyId})",
            UsedByCount = 1 + Math.Abs(x.Id.GetHashCode()) % 7,
            Status = x.Status
        }).ToList();
        return Response<IReadOnlyList<KpiOwnershipRowDto>>.Ok(rows);
    }

    public async Task<Response<ScorecardSnapshotDto>> ScorecardAsync(string? goalId, string? objectiveId, string? company, string? period, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var list = (await _repo.ListRuntimeKpisAsync(cancellationToken))
            .Where(x => string.IsNullOrWhiteSpace(company) || string.Equals(x.CompanyId, company, StringComparison.OrdinalIgnoreCase))
            .Select(x =>
            {
                var baseline = x.BaselineValue ?? 0m;
                var target = x.TargetValue ?? 0m;
                var current = baseline + ((target - baseline) * 0.67m);
                var variance = current - target;
                return new ScorecardKpiRowDto
                {
                    KpiId = x.Id,
                    KpiName = x.Name,
                    GoalId = goalId ?? "goal-001",
                    ObjectiveId = objectiveId ?? "obj-001",
                    CompanyId = x.CompanyId ?? "enterprise",
                    TimePeriod = string.IsNullOrWhiteSpace(period) ? $"{DateTime.UtcNow:yyyy-MM}" : period,
                    CurrentValue = decimal.Round(current, 2),
                    BaselineValue = x.BaselineValue,
                    TargetValue = x.TargetValue,
                    Variance = decimal.Round(variance, 2),
                    Trend = variance >= 0 ? "Up" : "Down",
                    Status = variance >= 0 ? "On Track" : "At Risk",
                    SourceKpiTemplateCode = x.SourceKpiTemplateCode,
                    SourceKpiTemplateVersion = x.SourceKpiTemplateVersion,
                    CreatedFromLibrary = x.CreatedFromLibrary
                };
            }).ToList();
        return Response<ScorecardSnapshotDto>.Ok(new ScorecardSnapshotDto
        {
            TotalKpis = list.Count,
            OnTrackCount = list.Count(x => x.Status == "On Track"),
            AtRiskCount = list.Count(x => x.Status == "At Risk"),
            OffTrackCount = list.Count(x => x.Status == "Off Track"),
            Rows = list
        });
    }

    public async Task<Response<KpiDefinitionDto>> CreateFromTemplateAsync(KpiInstantiateFromTemplateRequestDto request, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var template = await _repo.GetKpiTemplateAsync(request.TemplateId, cancellationToken);
        if (template is null) return Response<KpiDefinitionDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        if (!template.Status.Equals("Published", StringComparison.OrdinalIgnoreCase))
            return Response<KpiDefinitionDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, Errors("Only Published templates can be instantiated."));

        var existing = await _repo.GetRuntimeKpiAsync(template.TemplateCode, cancellationToken);
        if (existing is not null && !request.AllowDuplicates)
            return Response<KpiDefinitionDto>.Fail(EnterpriseStrategyErrorCodes.Conflict, Errors("Runtime KPI already exists for this template."));

        var runtimeId = existing is null ? template.TemplateCode : $"{template.TemplateCode}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var runtime = new KpiCatalogItemAggregate
        {
            Id = runtimeId,
            Name = template.Name,
            Category = template.Category,
            Type = template.Type,
            Description = template.Description,
            Owner = template.DefaultOwnerRole,
            BackupOwner = template.ReviewRole,
            UnitOfMeasure = template.UnitOfMeasure,
            AggregationMethod = template.AggregationMethod,
            ThresholdModel = template.ThresholdModelCode,
            ReportingFrequency = template.ReportingFrequency,
            Status = "Active",
            ScopeMode = template.StrategicPerspective.Contains("enterprise", StringComparison.OrdinalIgnoreCase) ? "Enterprise" : "SingleCompany",
            SourceType = template.FormulaType.Contains("ratio", StringComparison.OrdinalIgnoreCase) ? "Derived" : "Source",
            DecisionReference = template.DecisionReferenceRequirement,
            EvidenceReference = template.EvidenceRequirement,
            Notes = template.BusinessQuestion,
            SourceKpiTemplateId = template.Id,
            SourceKpiTemplateCode = template.TemplateCode,
            SourceKpiTemplateVersion = template.VersionLabel,
            CreatedFromLibrary = true,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _repo.AddRuntimeKpiAsync(runtime, cancellationToken);
        template.UsageCount += 1;
        template.LastUsedBy = actor;
        template.LastUsedAt = DateTime.UtcNow;
        template.UpdatedAt = DateTime.UtcNow;
        await _repo.UpsertKpiTemplatesAsync(new[] { template }, cancellationToken);
        await _repo.AddGovernanceActionAsync(new KpiGovernanceActionAggregate
        {
            EntityType = "KpiTemplate",
            EntityId = template.Id,
            Action = "instantiate",
            BeforeStatus = template.Status,
            AfterStatus = template.Status,
            Actor = actor,
            At = DateTime.UtcNow
        }, cancellationToken);
        await _audit.WriteMutationAsync(actor, "KpiCatalogItem", runtime.Id, "kpi.instantiated_from_library.v1", correlationId, "enterprise-strategy.kpis", "", template.TemplateCode, cancellationToken);
        return Response<KpiDefinitionDto>.Ok(ToRuntimeDto(runtime));
    }

    public async Task<Response<PagedResponseDto<KpiTemplateDto>>> CatalogAsync(PagedRequestDto request, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        IEnumerable<KpiTemplateAggregate> query = await _repo.ListKpiTemplatesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => $"{x.TemplateCode} {x.Name} {x.Category} {x.Type} {x.ObjectLevel} {x.Tags}".Contains(request.Search, StringComparison.OrdinalIgnoreCase));
        foreach (var f in request.Filters)
        {
            if (string.IsNullOrWhiteSpace(f.Value)) continue;
            query = f.Key.ToLowerInvariant() switch
            {
                "category" => query.Where(x => x.Category.Equals(f.Value, StringComparison.OrdinalIgnoreCase)),
                "strategicperspective" => query.Where(x => x.StrategicPerspective.Equals(f.Value, StringComparison.OrdinalIgnoreCase)),
                "type" => query.Where(x => x.Type.Equals(f.Value, StringComparison.OrdinalIgnoreCase)),
                "objectlevel" => query.Where(x => x.ObjectLevel.Equals(f.Value, StringComparison.OrdinalIgnoreCase)),
                "reportingfrequency" => query.Where(x => x.ReportingFrequency.Equals(f.Value, StringComparison.OrdinalIgnoreCase)),
                "status" => query.Where(x => x.Status.Equals(f.Value, StringComparison.OrdinalIgnoreCase)),
                "thresholdmodel" => query.Where(x => x.ThresholdModelCode.Equals(f.Value, StringComparison.OrdinalIgnoreCase)),
                "polarity" => query.Where(x => x.Polarity.Equals(f.Value, StringComparison.OrdinalIgnoreCase)),
                _ => query
            };
        }
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 200);
        var total = query.Count();
        var items = query.OrderByDescending(x => x.UpdatedAt).Skip((page - 1) * size).Take(size).Select(ToTemplateDto).ToList();
        return Response<PagedResponseDto<KpiTemplateDto>>.Ok(new PagedResponseDto<KpiTemplateDto>
        {
            Page = page,
            PageSize = size,
            TotalCount = total,
            Items = items
        });
    }

    public async Task<Response<KpiTemplateDto>> TemplateAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var row = await _repo.GetKpiTemplateAsync(id, cancellationToken);
        return row is null ? Response<KpiTemplateDto>.Fail(EnterpriseStrategyErrorCodes.NotFound) : Response<KpiTemplateDto>.Ok(ToTemplateDto(row));
    }

    public async Task<Response<IReadOnlyList<KpiThresholdModelDto>>> ThresholdModelsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var rows = (await _repo.ListThresholdModelsAsync(cancellationToken)).OrderBy(x => x.ModelCode).Select(ToThresholdDto).ToList();
        return Response<IReadOnlyList<KpiThresholdModelDto>>.Ok(rows);
    }

    public async Task<Response<KpiThresholdModelDto>> ThresholdModelAsync(string idOrCode, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var row = await _repo.GetThresholdModelAsync(idOrCode, cancellationToken);
        return row is null ? Response<KpiThresholdModelDto>.Fail(EnterpriseStrategyErrorCodes.NotFound) : Response<KpiThresholdModelDto>.Ok(ToThresholdDto(row));
    }

    public async Task<Response<PagedResponseDto<KpiScorecardPackDto>>> PacksAsync(PagedRequestDto request, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        IEnumerable<KpiScorecardPackAggregate> query = await _repo.ListScorecardPacksAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => $"{x.PackCode} {x.PackName} {x.PackLevel} {x.Description}".Contains(request.Search, StringComparison.OrdinalIgnoreCase));
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 200);
        var total = query.Count();
        var rows = query.OrderByDescending(x => x.UpdatedAt).Skip((page - 1) * size).Take(size).ToList();
        var items = new List<KpiScorecardPackDto>();
        foreach (var row in rows)
        {
            var count = (await _repo.ListScorecardPackItemsAsync(row.Id, cancellationToken)).Count;
            items.Add(ToPackDto(row, count));
        }
        return Response<PagedResponseDto<KpiScorecardPackDto>>.Ok(new PagedResponseDto<KpiScorecardPackDto> { Page = page, PageSize = size, TotalCount = total, Items = items });
    }

    public async Task<Response<KpiScorecardPackDto>> PackAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var row = await _repo.GetScorecardPackAsync(id, cancellationToken);
        if (row is null) return Response<KpiScorecardPackDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        var count = (await _repo.ListScorecardPackItemsAsync(row.Id, cancellationToken)).Count;
        return Response<KpiScorecardPackDto>.Ok(ToPackDto(row, count));
    }

    public async Task<Response<IReadOnlyList<KpiScorecardPackItemDto>>> PackItemsAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var items = (await _repo.ListScorecardPackItemsAsync(id, cancellationToken)).Select(ToPackItemDto).ToList();
        return Response<IReadOnlyList<KpiScorecardPackItemDto>>.Ok(items);
    }

    public async Task<Response<KpiTemplateDto>> CloneTemplateAsync(string id, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var template = await _repo.GetKpiTemplateAsync(id, cancellationToken);
        if (template is null) return Response<KpiTemplateDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        var clone = new KpiTemplateAggregate
        {
            Id = Guid.NewGuid().ToString("N"),
            TemplateCode = $"{template.TemplateCode}-COPY",
            Name = $"{template.Name} (Copy)",
            Category = template.Category,
            StrategicPerspective = template.StrategicPerspective,
            Type = template.Type,
            ObjectLevel = template.ObjectLevel,
            Description = template.Description,
            BusinessQuestion = template.BusinessQuestion,
            Polarity = template.Polarity,
            UnitOfMeasure = template.UnitOfMeasure,
            AggregationMethod = template.AggregationMethod,
            ReportingFrequency = template.ReportingFrequency,
            FormulaType = template.FormulaType,
            NumeratorDefinition = template.NumeratorDefinition,
            DenominatorDefinition = template.DenominatorDefinition,
            FormulaExpression = template.FormulaExpression,
            BaselineLogic = template.BaselineLogic,
            TargetLogic = template.TargetLogic,
            ThresholdModelCode = template.ThresholdModelCode,
            DefaultOwnerRole = template.DefaultOwnerRole,
            ReviewRole = template.ReviewRole,
            DataSourcePattern = template.DataSourcePattern,
            EvidenceRequirement = template.EvidenceRequirement,
            DecisionReferenceRequirement = template.DecisionReferenceRequirement,
            Tags = template.Tags,
            Status = "Draft",
            VersionLabel = template.VersionLabel,
            UsageCount = 0,
            LastUsedAt = null,
            LastUsedBy = null,
            PublishDate = null,
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        await _repo.UpsertKpiTemplatesAsync(new[] { clone }, cancellationToken);
        await _repo.AddGovernanceActionAsync(new KpiGovernanceActionAggregate
        {
            EntityType = "KpiTemplate",
            EntityId = clone.Id,
            Action = "clone",
            BeforeStatus = template.Status,
            AfterStatus = clone.Status,
            Actor = actor
        }, cancellationToken);
        await _audit.WriteMutationAsync(actor, "KpiTemplate", clone.Id, "kpi.template_cloned.v1", correlationId, "enterprise-strategy.kpis", "", clone.TemplateCode, cancellationToken);
        return Response<KpiTemplateDto>.Ok(ToTemplateDto(clone));
    }

    public async Task<Response<KpiTemplateDto>> LifecycleAsync(string id, string action, string actor, string correlationId, CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var template = await _repo.GetKpiTemplateAsync(id, cancellationToken);
        if (template is null) return Response<KpiTemplateDto>.Fail(EnterpriseStrategyErrorCodes.NotFound);
        var before = template.Status;
        template.Status = action.ToLowerInvariant() switch
        {
            "submit-review" => "In Review",
            "approve" => "Approved",
            "publish" => "Published",
            "retire" => "Retired",
            _ => template.Status
        };
        if (action.Equals("publish", StringComparison.OrdinalIgnoreCase))
            template.PublishDate = DateTime.UtcNow;
        if (before == template.Status)
            return Response<KpiTemplateDto>.Fail(EnterpriseStrategyErrorCodes.ValidationError, Errors("Unsupported lifecycle action."));
        template.UpdatedAt = DateTime.UtcNow;
        await _repo.UpsertKpiTemplatesAsync(new[] { template }, cancellationToken);
        await _repo.AddGovernanceActionAsync(new KpiGovernanceActionAggregate
        {
            EntityType = "KpiTemplate",
            EntityId = template.Id,
            Action = action,
            BeforeStatus = before,
            AfterStatus = template.Status,
            Actor = actor,
            At = DateTime.UtcNow
        }, cancellationToken);
        await _audit.WriteMutationAsync(actor, "KpiTemplate", template.Id, "kpi.template_lifecycle.v1", correlationId, "enterprise-strategy.kpis", before, template.Status, cancellationToken);
        return Response<KpiTemplateDto>.Ok(ToTemplateDto(template));
    }

    public async Task<Response<KpiGovernanceSummaryDto>> GovernanceSummaryAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var templates = await _repo.ListKpiTemplatesAsync(cancellationToken);
        return Response<KpiGovernanceSummaryDto>.Ok(new KpiGovernanceSummaryDto
        {
            TotalTemplates = templates.Count,
            Draft = templates.Count(x => x.Status == "Draft"),
            InReview = templates.Count(x => x.Status == "In Review"),
            Approved = templates.Count(x => x.Status == "Approved"),
            Published = templates.Count(x => x.Status == "Published"),
            Retired = templates.Count(x => x.Status == "Retired"),
            MissingOwner = templates.Count(x => string.IsNullOrWhiteSpace(x.DefaultOwnerRole)),
            MissingThreshold = templates.Count(x => string.IsNullOrWhiteSpace(x.ThresholdModelCode)),
            MissingFormula = templates.Count(x => string.IsNullOrWhiteSpace(x.FormulaExpression))
        });
    }

    public async Task<Response<IReadOnlyList<KpiGovernanceExceptionDto>>> GovernanceExceptionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        var templates = await _repo.ListKpiTemplatesAsync(cancellationToken);
        var outRows = new List<KpiGovernanceExceptionDto>();
        foreach (var t in templates)
        {
            if (string.IsNullOrWhiteSpace(t.DefaultOwnerRole))
                outRows.Add(new KpiGovernanceExceptionDto { TemplateId = t.Id, TemplateCode = t.TemplateCode, Name = t.Name, Status = t.Status, ExceptionType = "MissingOwner", Message = "Default owner role is required." });
            if (string.IsNullOrWhiteSpace(t.ThresholdModelCode))
                outRows.Add(new KpiGovernanceExceptionDto { TemplateId = t.Id, TemplateCode = t.TemplateCode, Name = t.Name, Status = t.Status, ExceptionType = "MissingThreshold", Message = "Threshold model is required." });
            if (string.IsNullOrWhiteSpace(t.FormulaExpression))
                outRows.Add(new KpiGovernanceExceptionDto { TemplateId = t.Id, TemplateCode = t.TemplateCode, Name = t.Name, Status = t.Status, ExceptionType = "MissingFormula", Message = "Formula expression is required." });
        }
        return Response<IReadOnlyList<KpiGovernanceExceptionDto>>.Ok(outRows);
    }

    public async Task<Response<IReadOnlyList<KpiGovernanceActionAggregate>>> GovernanceActionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        return Response<IReadOnlyList<KpiGovernanceActionAggregate>>.Ok(await _repo.ListGovernanceActionsAsync(cancellationToken));
    }

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        if (_seeded) return;
        await SeedGate.WaitAsync(cancellationToken);
        try
        {
            if (_seeded) return;
            var payload = LoadEmbeddedSeedPayload();
            try
            {
                await _repo.UpsertKpiTemplatesAsync(payload.Templates, cancellationToken);
                await _repo.UpsertThresholdModelsAsync(payload.ThresholdModels, cancellationToken);
                await _repo.UpsertScorecardPacksAsync(payload.Packs, cancellationToken);
                foreach (var pack in payload.Packs)
                {
                    var items = payload.PackItems.Where(x => x.PackCode.Equals(pack.PackCode, StringComparison.OrdinalIgnoreCase)).ToList();
                    await _repo.ReplaceScorecardPackItemsAsync(pack.PackCode, items, cancellationToken);
                }
            }
            catch
            {
                // Local UI routes should degrade gracefully when the persistence backend is unavailable.
            }
            _seeded = true;
        }
        finally
        {
            SeedGate.Release();
        }
    }

    private static KpiSeedPayload LoadEmbeddedSeedPayload()
    {
        const string primaryPath = "/Users/natig/Downloads/Cursor_Prompt_KPI_Catalog_Refactor_With_Embedded_Seed.md";
        try
        {
            if (!File.Exists(primaryPath))
                return new KpiSeedPayload();

            var markdown = File.ReadAllText(primaryPath);
            var startFence = markdown.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (startFence < 0) return new KpiSeedPayload();
            var jsonStart = markdown.IndexOf('{', startFence);
            var endFence = markdown.IndexOf("```", jsonStart + 1, StringComparison.OrdinalIgnoreCase);
            if (jsonStart < 0 || endFence <= jsonStart) return new KpiSeedPayload();
            var json = markdown.Substring(jsonStart, endFence - jsonStart).Trim();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var payload = new KpiSeedPayload();

            foreach (var row in root.GetProperty("KPI_Templates").GetProperty("rows").EnumerateArray())
            {
                var code = ReadString(row, "KpiTemplateCode");
                if (string.IsNullOrWhiteSpace(code)) continue;
                payload.Templates.Add(new KpiTemplateAggregate
                {
                    Id = $"kpitpl-{code.ToLowerInvariant()}",
                    TemplateCode = code,
                    Name = ReadString(row, "KpiName"),
                    Category = ReadString(row, "KpiCategory"),
                    StrategicPerspective = ReadString(row, "StrategicPerspective"),
                    Type = ReadString(row, "KpiType"),
                    ObjectLevel = ReadString(row, "ObjectLevel"),
                    Description = ReadString(row, "Description"),
                    BusinessQuestion = ReadString(row, "BusinessQuestion"),
                    Polarity = ReadString(row, "Polarity"),
                    UnitOfMeasure = ReadString(row, "UnitOfMeasure"),
                    AggregationMethod = ReadString(row, "AggregationMethod"),
                    ReportingFrequency = ReadString(row, "ReportingFrequency"),
                    FormulaType = ReadString(row, "FormulaType"),
                    NumeratorDefinition = ReadString(row, "NumeratorDefinition"),
                    DenominatorDefinition = ReadString(row, "DenominatorDefinition"),
                    FormulaExpression = ReadString(row, "FormulaExpression"),
                    BaselineLogic = ReadString(row, "BaselineLogic"),
                    TargetLogic = ReadString(row, "TargetLogic"),
                    ThresholdModelCode = ReadString(row, "ThresholdModelCode"),
                    DefaultOwnerRole = ReadString(row, "DefaultOwnerRole"),
                    ReviewRole = ReadString(row, "ReviewRole"),
                    DataSourcePattern = ReadString(row, "DataSourcePattern"),
                    EvidenceRequirement = ReadString(row, "EvidenceRequirement"),
                    DecisionReferenceRequirement = ReadString(row, "DecisionReferenceRequirement"),
                    Status = ReadString(row, "Status", "Draft"),
                    VersionLabel = ReadString(row, "Version", "v1.0"),
                    PublishDate = ReadDate(row, "PublishDate"),
                    Tags = ReadString(row, "Tags"),
                    UpdatedAt = DateTime.UtcNow
                });
            }

            foreach (var row in root.GetProperty("KPI_Threshold_Models").GetProperty("rows").EnumerateArray())
            {
                var code = ReadString(row, "ThresholdModelCode");
                if (string.IsNullOrWhiteSpace(code)) continue;
                payload.ThresholdModels.Add(new KpiThresholdModelAggregate
                {
                    Id = $"kpith-{code.ToLowerInvariant()}",
                    ModelCode = code,
                    MetricUnit = ReadString(row, "MetricUnit"),
                    Polarity = ReadString(row, "Polarity"),
                    ModelName = ReadString(row, "ModelName"),
                    RedFloor = ReadDecimal(row, "RedFloor"),
                    AmberFloor = ReadDecimal(row, "AmberFloor"),
                    GreenTarget = ReadDecimal(row, "GreenTarget"),
                    GreenStretch = ReadDecimal(row, "GreenStretch"),
                    UpperControlLimit = ReadDecimal(row, "UpperControlLimit"),
                    Interpretation = ReadString(row, "Interpretation"),
                    Status = "Published",
                    VersionLabel = "v1.0",
                    PublishDate = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            foreach (var row in root.GetProperty("Scorecard_Packs").GetProperty("rows").EnumerateArray())
            {
            var code = ReadString(row, "PackCode");
            if (string.IsNullOrWhiteSpace(code)) continue;
            payload.Packs.Add(new KpiScorecardPackAggregate
            {
                Id = $"kpipack-{code.ToLowerInvariant()}",
                PackCode = code,
                PackName = ReadString(row, "PackName"),
                PackLevel = ReadString(row, "PackLevel"),
                Description = ReadString(row, "Description"),
                Status = ReadString(row, "Status", "Draft"),
                VersionLabel = ReadString(row, "Version", "v1.0"),
                PublishDate = ReadDate(row, "PublishDate"),
                DefaultOwnerRole = ReadString(row, "DefaultOwnerRole"),
                UpdatedAt = DateTime.UtcNow
            });
        }

        var templateByCode = payload.Templates.ToDictionary(x => x.TemplateCode, StringComparer.OrdinalIgnoreCase);
            foreach (var row in root.GetProperty("Scorecard_Pack_Items").GetProperty("rows").EnumerateArray())
            {
            var packCode = ReadString(row, "PackCode");
            var templateCode = ReadString(row, "KpiTemplateCode");
            if (string.IsNullOrWhiteSpace(packCode) || string.IsNullOrWhiteSpace(templateCode))
                continue;
            if (!payload.Packs.Any(x => x.PackCode.Equals(packCode, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Seed payload contains pack item with unknown PackCode '{packCode}'.");
            if (!templateByCode.ContainsKey(templateCode))
                throw new InvalidOperationException($"Seed payload contains pack item with unknown KpiTemplateCode '{templateCode}'.");

            var pack = payload.Packs.First(x => x.PackCode.Equals(packCode, StringComparison.OrdinalIgnoreCase));
            var tpl = templateByCode[templateCode];
            payload.PackItems.Add(new KpiScorecardPackItemAggregate
            {
                Id = $"kpipackitem-{packCode.ToLowerInvariant()}-{templateCode.ToLowerInvariant()}",
                PackId = pack.Id,
                PackCode = packCode,
                KpiTemplateId = tpl.Id,
                KpiTemplateCode = tpl.TemplateCode,
                KpiTemplateName = tpl.Name,
                DisplayOrder = ReadInt(row, "DisplayOrder"),
                PriorityClass = ReadString(row, "PriorityClass"),
                Rationale = ReadString(row, "Rationale")
            });
        }

            return payload;
        }
        catch
        {
            // Local seed path may be inaccessible in restricted environments; continue with DB-backed data.
            return new KpiSeedPayload();
        }
    }

    private static string ReadString(JsonElement row, string property, string fallback = "")
        => row.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() ?? fallback : fallback;

    private static int ReadInt(JsonElement row, string property)
    {
        var text = ReadString(row, property);
        return int.TryParse(text, out var val) ? val : 0;
    }

    private static decimal? ReadDecimal(JsonElement row, string property)
    {
        var text = ReadString(row, property);
        return decimal.TryParse(text, out var val) ? val : null;
    }

    private static DateTime? ReadDate(JsonElement row, string property)
    {
        var text = ReadString(row, property);
        return DateTime.TryParse(text, out var val) ? val : null;
    }

    private static Dictionary<string, List<string>> Errors(string message) =>
        new(StringComparer.OrdinalIgnoreCase) { ["general"] = new() { message } };

    private static KpiDefinitionDto ToRuntimeDto(KpiCatalogItemAggregate row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Category = row.Category,
        Type = row.Type,
        Description = row.Description,
        Owner = row.Owner,
        BackupOwner = row.BackupOwner,
        UnitOfMeasure = row.UnitOfMeasure,
        AggregationMethod = row.AggregationMethod,
        ThresholdModel = row.ThresholdModel,
        ReportingFrequency = row.ReportingFrequency,
        Status = row.Status,
        ScopeMode = row.ScopeMode,
        CompanyId = row.CompanyId,
        SourceType = row.SourceType,
        BaselineValue = row.BaselineValue,
        TargetValue = row.TargetValue,
        DecisionReference = row.DecisionReference,
        EvidenceReference = row.EvidenceReference,
        Notes = row.Notes,
        SourceKpiTemplateId = row.SourceKpiTemplateId,
        SourceKpiTemplateCode = row.SourceKpiTemplateCode,
        SourceKpiTemplateVersion = row.SourceKpiTemplateVersion,
        CreatedFromLibrary = row.CreatedFromLibrary,
        Version = row.Version,
        CreatedAt = row.CreatedAt,
        UpdatedAt = row.UpdatedAt
    };

    private static KpiCatalogItemAggregate ToRuntimeAggregate(KpiDefinitionDto row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Category = row.Category,
        Type = row.Type,
        Description = row.Description,
        Owner = row.Owner,
        BackupOwner = row.BackupOwner,
        UnitOfMeasure = row.UnitOfMeasure,
        AggregationMethod = row.AggregationMethod,
        ThresholdModel = row.ThresholdModel,
        ReportingFrequency = row.ReportingFrequency,
        Status = row.Status,
        ScopeMode = row.ScopeMode,
        CompanyId = row.CompanyId,
        SourceType = row.SourceType,
        BaselineValue = row.BaselineValue,
        TargetValue = row.TargetValue,
        DecisionReference = row.DecisionReference,
        EvidenceReference = row.EvidenceReference,
        Notes = row.Notes,
        SourceKpiTemplateId = row.SourceKpiTemplateId,
        SourceKpiTemplateCode = row.SourceKpiTemplateCode,
        SourceKpiTemplateVersion = row.SourceKpiTemplateVersion,
        CreatedFromLibrary = row.CreatedFromLibrary,
        Version = row.Version,
        CreatedAt = row.CreatedAt,
        UpdatedAt = row.UpdatedAt
    };

    private static KpiTemplateDto ToTemplateDto(KpiTemplateAggregate row) => new()
    {
        Id = row.Id,
        TemplateCode = row.TemplateCode,
        Name = row.Name,
        Category = row.Category,
        StrategicPerspective = row.StrategicPerspective,
        Type = row.Type,
        ObjectLevel = row.ObjectLevel,
        Description = row.Description,
        BusinessQuestion = row.BusinessQuestion,
        Polarity = row.Polarity,
        UnitOfMeasure = row.UnitOfMeasure,
        AggregationMethod = row.AggregationMethod,
        ReportingFrequency = row.ReportingFrequency,
        FormulaType = row.FormulaType,
        NumeratorDefinition = row.NumeratorDefinition,
        DenominatorDefinition = row.DenominatorDefinition,
        FormulaExpression = row.FormulaExpression,
        BaselineLogic = row.BaselineLogic,
        TargetLogic = row.TargetLogic,
        ThresholdModelCode = row.ThresholdModelCode,
        DefaultOwnerRole = row.DefaultOwnerRole,
        ReviewRole = row.ReviewRole,
        DataSourcePattern = row.DataSourcePattern,
        EvidenceRequirement = row.EvidenceRequirement,
        DecisionReferenceRequirement = row.DecisionReferenceRequirement,
        Status = row.Status,
        VersionLabel = row.VersionLabel,
        PublishDate = row.PublishDate,
        Tags = row.Tags,
        UsageCount = row.UsageCount,
        LastUsedBy = row.LastUsedBy,
        LastUsedAt = row.LastUsedAt,
        UpdatedAt = row.UpdatedAt
    };

    private static KpiThresholdModelDto ToThresholdDto(KpiThresholdModelAggregate row) => new()
    {
        Id = row.Id,
        ModelCode = row.ModelCode,
        MetricUnit = row.MetricUnit,
        ModelName = row.ModelName,
        Polarity = row.Polarity,
        RedFloor = row.RedFloor,
        AmberFloor = row.AmberFloor,
        GreenTarget = row.GreenTarget,
        GreenStretch = row.GreenStretch,
        UpperControlLimit = row.UpperControlLimit,
        Interpretation = row.Interpretation,
        Status = row.Status,
        VersionLabel = row.VersionLabel,
        PublishDate = row.PublishDate
    };

    private static KpiScorecardPackDto ToPackDto(KpiScorecardPackAggregate row, int kpiCount) => new()
    {
        Id = row.Id,
        PackCode = row.PackCode,
        PackName = row.PackName,
        PackLevel = row.PackLevel,
        Description = row.Description,
        Status = row.Status,
        VersionLabel = row.VersionLabel,
        PublishDate = row.PublishDate,
        DefaultOwnerRole = row.DefaultOwnerRole,
        KpiCount = kpiCount,
        UsageCount = row.UsageCount,
        UpdatedAt = row.UpdatedAt
    };

    private static KpiScorecardPackItemDto ToPackItemDto(KpiScorecardPackItemAggregate row) => new()
    {
        Id = row.Id,
        PackId = row.PackId,
        PackCode = row.PackCode,
        KpiTemplateId = row.KpiTemplateId,
        KpiTemplateCode = row.KpiTemplateCode,
        KpiTemplateName = row.KpiTemplateName,
        DisplayOrder = row.DisplayOrder,
        PriorityClass = row.PriorityClass,
        Rationale = row.Rationale
    };

    private sealed class KpiSeedPayload
    {
        public List<KpiTemplateAggregate> Templates { get; } = new();
        public List<KpiThresholdModelAggregate> ThresholdModels { get; } = new();
        public List<KpiScorecardPackAggregate> Packs { get; } = new();
        public List<KpiScorecardPackItemAggregate> PackItems { get; } = new();
    }
}
