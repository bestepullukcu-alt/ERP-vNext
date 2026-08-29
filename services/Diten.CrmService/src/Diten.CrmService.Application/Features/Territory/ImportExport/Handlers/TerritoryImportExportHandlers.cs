using System.Globalization;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.ImportExport.Xlsx;
using Diten.CrmService.Application.Features.Territory.PlanVsCurrent;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.ImportExport.Handlers;

/// <summary>
/// MOD-0151 FU08 export/template writer. Both produce the SAME workbook shape (the export just carries rows), so an
/// export round-trips into the import reader column-for-column.
///
/// <para>The export is strictly read-only: it reads the model, its nodes, rules, account assignments (current AND
/// history), resource assignments (current AND history), the CoverageSummary projection and the FU04B Plan vs Current
/// diff, and writes them out. No <c>TenantId</c> column is emitted — tenancy is a claim, never file data.</para>
/// </summary>
public sealed class TerritoryWorkbookExportHandler
    : IRequestHandler<ExportTerritoryModelWorkbookQuery, Response<ExportFileDto>>,
        IRequestHandler<BuildTerritoryImportTemplateQuery, Response<ExportFileDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryAssignmentRuleRepository _rules;
    private readonly IAccountTerritoryAssignmentRepository _assignments;
    private readonly ITerritoryResourceAssignmentRepository _resources;
    private readonly ITerritoryResourceAssignmentPlanSnapshotRepository _planSnapshots;
    private readonly IReferenceDataCatalogReader _catalog;

    public TerritoryWorkbookExportHandler(
        ITenantContext tenant,
        ITerritoryModelRepository models,
        ITerritoryNodeRepository nodes,
        ITerritoryAssignmentRuleRepository rules,
        IAccountTerritoryAssignmentRepository assignments,
        ITerritoryResourceAssignmentRepository resources,
        ITerritoryResourceAssignmentPlanSnapshotRepository planSnapshots,
        IReferenceDataCatalogReader catalog)
    {
        _tenant = tenant;
        _models = models;
        _nodes = nodes;
        _rules = rules;
        _assignments = assignments;
        _resources = resources;
        _planSnapshots = planSnapshots;
        _catalog = catalog;
    }

    private static readonly string[] WorkbookSets =
    [
        TerritoryReferenceSets.TerritoryLevel,
        TerritoryReferenceSets.TerritoryModelStatus,
        TerritoryReferenceSets.TerritoryNodeStatus,
        TerritoryReferenceSets.TerritoryRuleType,
        TerritoryReferenceSets.TerritoryConflictPolicy,
        TerritoryReferenceSets.TerritoryAssignmentStatus,
        TerritoryReferenceSets.TerritoryAssignmentSource,
        TerritoryReferenceSets.TerritoryCoverageScope,
        TerritoryReferenceSets.BusinessUnitValueSet
    ];

    public Task<Response<ExportFileDto>> Handle(BuildTerritoryImportTemplateQuery request, CancellationToken cancellationToken)
        => BuildAsync(request.ModelId, isTemplate: true, cancellationToken);

    public Task<Response<ExportFileDto>> Handle(ExportTerritoryModelWorkbookQuery request, CancellationToken cancellationToken)
        => BuildAsync(request.ModelId, isTemplate: false, cancellationToken);

    private async Task<Response<ExportFileDto>> BuildAsync(Guid modelId, bool isTemplate, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ExportFileDto>.Fail("Tenant context is required.", 400);
        }

        var model = await _models.GetByIdAsync(tenantId, modelId, cancellationToken);
        if (model is null)
        {
            return Response<ExportFileDto>.Fail("Territory model not found.", 404);
        }

        var referenceSets = new List<ReferenceSetSnapshot>();
        foreach (var setCode in WorkbookSets)
        {
            referenceSets.Add(await _catalog.GetPublishedValuesAsync(setCode, cancellationToken));
        }

        var sheets = new Dictionary<string, IReadOnlyList<IReadOnlyList<string?>>>(StringComparer.OrdinalIgnoreCase);
        var correlationId = Guid.NewGuid().ToString("N");

        if (!isTemplate)
        {
            var nodes = await _nodes.ListByModelAsync(tenantId, model.Id, cancellationToken);
            var nodeById = nodes.ToDictionary(n => n.Id);
            var rules = await _rules.ListByModelAsync(tenantId, model.Id, cancellationToken);
            var assignments = await _assignments.ListByModelAsync(tenantId, model.Id, cancellationToken);
            var resources = await _resources.ListByModelAsync(tenantId, model.Id, cancellationToken);
            var snapshot = await _planSnapshots.GetLatestAsync(tenantId, model.Id, cancellationToken);

            sheets[TerritoryWorkbookSchema.ModelSheet] = [ModelRow(model)];
            sheets[TerritoryWorkbookSchema.NodesSheet] = nodes.Select(n => NodeRow(n, nodeById)).ToList();
            sheets[TerritoryWorkbookSchema.AssignmentRulesSheet] = rules.Select(r => RuleRow(r, nodeById)).ToList();
            sheets[TerritoryWorkbookSchema.AccountAssignmentsSheet] = assignments.Select(AssignmentRow).ToList();
            sheets[TerritoryWorkbookSchema.ResourceAssignmentsSheet] = resources.Select(r => ResourceRow(r, nodeById)).ToList();
            sheets[TerritoryWorkbookSchema.CoverageSummarySheet] = CoverageRows(model, assignments);
            sheets[TerritoryWorkbookSchema.PlanVsCurrentSheet] = PlanVsCurrentRows(model, snapshot, resources, nodeById);
        }

        var request = new TerritoryWorkbookRequest(
            isTemplate, model.ModelCode, model.Name, model.Status, sheets, referenceSets,
            DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), correlationId);

        var name = Sanitize(model.ModelCode);
        var fileName = isTemplate
            ? $"territory-import-template-{name}.xlsx"
            : $"territory-export-{name}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.xlsx";

        return Response<ExportFileDto>.Success(TerritoryWorkbookBuilder.File(TerritoryWorkbookBuilder.Build(request), fileName));
    }

    // ---- row shapers (schema column order) -----------------------------------------------------------------------

    private static IReadOnlyList<string?> ModelRow(TerritoryModel model) =>
    [
        null, model.Id.ToString(), model.ModelCode, model.Name, model.CountryScope, Scopes(model.BusinessScopes),
        TerritoryImportValues.Iso(model.EffectiveFrom), TerritoryImportValues.IsoOrNull(model.EffectiveTo),
        model.Status, model.ChangeReason
    ];

    private static IReadOnlyList<string?> NodeRow(TerritoryNode node, IReadOnlyDictionary<Guid, TerritoryNode> nodeById) =>
    [
        null, node.Id.ToString(), node.TerritoryCode, node.Name, node.TerritoryLevel,
        node.ParentTerritoryId is { } parentId && nodeById.TryGetValue(parentId, out var parent) ? parent.TerritoryCode : null,
        node.CountryCode, node.DivisionCode, node.RegionCode, node.AreaCode, node.ZoneCode, node.MicroZoneCode,
        TerritoryImportValues.Iso(node.EffectiveFrom), TerritoryImportValues.IsoOrNull(node.EffectiveTo),
        node.SortOrder.ToString(CultureInfo.InvariantCulture), node.Status
    ];

    private static IReadOnlyList<string?> RuleRow(TerritoryAssignmentRule rule, IReadOnlyDictionary<Guid, TerritoryNode> nodeById) =>
    [
        null, rule.Id.ToString(), rule.RuleCode, rule.Name, rule.RuleType,
        nodeById.TryGetValue(rule.TerritoryId, out var node) ? node.TerritoryCode : null,
        rule.ConflictPolicy, rule.Priority.ToString(CultureInfo.InvariantCulture), rule.IsEnabled ? "TRUE" : "FALSE",
        TerritoryImportValues.Iso(rule.EffectiveFrom), TerritoryImportValues.IsoOrNull(rule.EffectiveTo),
        Join(rule.Criteria.CountryRefs), Join(rule.Criteria.CityRefs), Join(rule.Criteria.DistrictRefs),
        Join(rule.Criteria.AccountTypes), Join(rule.Criteria.AccountCategories), Join(rule.Criteria.AccountStatuses)
    ];

    private static IReadOnlyList<string?> AssignmentRow(AccountTerritoryAssignment a) =>
    [
        null, a.Id.ToString(), a.AccountId.ToString(), a.AccountCode, a.AccountDisplayName, a.TerritoryNodeCode,
        Scopes(a.BusinessScopes), TerritoryImportValues.Iso(a.EffectiveFrom), TerritoryImportValues.IsoOrNull(a.EffectiveTo),
        a.ConflictPolicy, null, a.OverrideReason, a.AssignmentStatus, a.AssignmentSource, a.AppliedRuleCode,
        TerritoryImportValues.IsoOrNull(a.EndedAt)
    ];

    private static IReadOnlyList<string?> ResourceRow(
        TerritoryResourceAssignment a, IReadOnlyDictionary<Guid, TerritoryNode> nodeById) =>
    [
        null, a.Id.ToString(),
        a.TerritoryId is { } id && nodeById.TryGetValue(id, out var node) ? node.TerritoryCode : null,
        a.EffectivePositionCode, a.EffectivePositionTitle, a.Resource.ResourceId, a.Resource.DisplayName,
        Scopes(a.BusinessScopes), a.CoverageScope, a.IsPrimary ? "TRUE" : "FALSE",
        TerritoryImportValues.Iso(a.ValidFrom), TerritoryImportValues.IsoOrNull(a.ValidTo), a.Status, a.ChangeReason
    ];

    /// <summary>CoverageSummary export — the SAME current-coverage rule the FU05A guard applies (active model +
    /// open assignment at the instant asked). Export only: this sheet is never read back.</summary>
    private static List<IReadOnlyList<string?>> CoverageRows(
        TerritoryModel model, IReadOnlyList<AccountTerritoryAssignment> assignments)
    {
        var at = DateTimeOffset.UtcNow;
        var modelCurrent = !model.IsDeleted
                           && string.Equals(model.Status, "active", StringComparison.OrdinalIgnoreCase)
                           && model.EffectiveFrom <= at && (model.EffectiveTo is null || model.EffectiveTo >= at);

        return assignments
            .GroupBy(a => a.AccountId)
            .Select(group =>
            {
                var current = modelCurrent
                    ? group.FirstOrDefault(a =>
                        string.Equals(a.AssignmentStatus, "active", StringComparison.OrdinalIgnoreCase)
                        && a.EndedAt is null && a.EffectiveFrom <= at && (a.EffectiveTo is null || a.EffectiveTo >= at))
                    : null;

                var any = group.First();
                return (IReadOnlyList<string?>)
                [
                    any.AccountId.ToString(), any.AccountCode, any.AccountDisplayName,
                    TerritoryImportValues.Iso(at), current is not null ? "TRUE" : "FALSE",
                    current?.TerritoryNodeCode, current?.TerritoryNodeName, Scopes(current?.BusinessScopes),
                    current is null ? null : TerritoryImportValues.Iso(current.EffectiveFrom),
                    current is null ? null : TerritoryImportValues.IsoOrNull(current.EffectiveTo)
                ];
            })
            .ToList();
    }

    /// <summary>Plan vs Current export — reuses the FU04B diff engine itself, so the sheet cannot drift from the tab.</summary>
    private static List<IReadOnlyList<string?>> PlanVsCurrentRows(
        TerritoryModel model,
        TerritoryResourceAssignmentPlanSnapshot? snapshot,
        IReadOnlyList<TerritoryResourceAssignment> current,
        IReadOnlyDictionary<Guid, TerritoryNode> nodeById)
    {
        if (snapshot is null) return [];

        var rows = TerritoryPlanVsCurrentEngine.Compute(new TerritoryPlanVsCurrentEngine.Input(
            model.Id, model.ModelCode, snapshot, current, nodeById, DateTimeOffset.UtcNow));

        return rows
            .Select(row => (IReadOnlyList<string?>)
            [
                row.TerritoryNodeCode, row.PositionCode, row.DiffType,
                row.PlannedResourceId, row.PlannedResourceDisplayName,
                row.CurrentResourceId, row.CurrentResourceDisplayName,
                TerritoryImportValues.IsoOrNull(row.PlannedEffectiveFrom),
                TerritoryImportValues.IsoOrNull(row.CurrentEffectiveFrom),
                row.ReplacementReason ?? row.TransferReason ?? row.ChangeReason
            ])
            .ToList();
    }

    private static string Join(IEnumerable<string>? values)
        => values is null ? string.Empty : string.Join("; ", values);

    private static string Scopes(IEnumerable<TerritoryBusinessScope>? scopes)
        => scopes is null
            ? string.Empty
            : string.Join("; ", scopes
                .Where(s => string.Equals(s.ScopeType, TerritoryReferenceSets.BusinessUnitScopeType, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.ScopeCode));

    private static string Sanitize(string value)
    {
        var safe = new string(value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray());
        return safe.Length == 0 ? "model" : safe;
    }
}

/// <summary>MOD-0151 FU08 — dry-run / apply. The engine holds the whole contract; this handler only forwards.</summary>
public sealed class TerritoryImportFileHandler : IRequestHandler<TerritoryImportFileCommand, Response<TerritoryImportPreviewDto>>
{
    private readonly TerritoryImportEngine _engine;

    public TerritoryImportFileHandler(TerritoryImportEngine engine) => _engine = engine;

    public Task<Response<TerritoryImportPreviewDto>> Handle(TerritoryImportFileCommand request, CancellationToken cancellationToken)
        => _engine.RunAsync(
            request.ModelId, request.File, request.FileName, request.DryRun, request.StrictMode,
            request.CorrelationId, request.Actor, cancellationToken);
}

/// <summary>MOD-0151 FU08 — append-only import run history (read).</summary>
public sealed class GetTerritoryImportRunsHandler : IRequestHandler<GetTerritoryImportRunsQuery, Response<TerritoryImportRunListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryImportRunRepository _runs;

    public GetTerritoryImportRunsHandler(
        ITenantContext tenant, ITerritoryModelRepository models, ITerritoryImportRunRepository runs)
    {
        _tenant = tenant;
        _models = models;
        _runs = runs;
    }

    public async Task<Response<TerritoryImportRunListDto>> Handle(
        GetTerritoryImportRunsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<TerritoryImportRunListDto>.Fail("Tenant context is required.", 400);
        }

        if (await _models.GetByIdAsync(tenantId, request.ModelId, cancellationToken) is null)
        {
            return Response<TerritoryImportRunListDto>.Fail("Territory model not found.", 404);
        }

        var runs = await _runs.ListByModelAsync(tenantId, request.ModelId, cancellationToken);
        var items = runs.Select(r => new TerritoryImportRunDto(
            r.Id, r.TerritoryModelId, r.ModelCode, r.FileName, r.FileHash, r.UploadedBy, r.UploadedAt, r.Status,
            r.AppliedAt, r.AppliedBy, r.CorrelationId,
            r.DryRunResult.TotalRows, r.DryRunResult.Creates, r.DryRunResult.Updates, r.DryRunResult.Ends,
            r.DryRunResult.Skips, r.ErrorCount, r.WarningCount, r.DryRunResult.StrictMode,
            r.DryRunResult.SheetOutcomes,
            r.SheetCounts.Select(s => new TerritoryImportRunSheetCountDto(
                s.Sheet, s.Total, s.Created, s.Updated, s.Ended, s.Skipped)).ToList())).ToList();

        return Response<TerritoryImportRunListDto>.Success(new TerritoryImportRunListDto(items.Count, items));
    }
}
