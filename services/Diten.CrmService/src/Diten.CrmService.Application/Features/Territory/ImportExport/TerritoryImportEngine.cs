using System.Security.Cryptography;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Territory.ImportExport;

/// <summary>
/// MOD-0151 FU08 import engine (pack §22.5): parse → validate → (optionally) apply → record.
///
/// <para><b>Dry-run writes nothing.</b> Not a node, not a rule, not an assignment, and not an import-run row. The
/// apply path runs the very same validation pass first, so the plan the operator approved is the plan that executes.
/// </para>
///
/// <para><b>Apply granularity</b> is per pack §22.5: <c>Model</c>/<c>Nodes</c>/<c>AssignmentRules</c> are sheet-level
/// all-or-nothing (a partially written hierarchy would leave orphan branches), <c>AccountAssignments</c> is
/// batch-level all-or-nothing (the same contract FU05's on-screen apply uses), and <c>strictMode</c> lifts both to
/// file-level. Whatever is skipped is reported explicitly — a silent partial apply is forbidden.</para>
/// </summary>
public sealed class TerritoryImportEngine
{
    /// <summary>Above this share of blocking rows the file is treated as "wrong file / wrong template" and apply is
    /// refused outright, instead of writing the handful of rows that happened to parse.</summary>
    public const double BlockingRatioLimit = 0.20;

    /// <summary>Cap on the accounts pulled into the validation context (same seam and spirit as the FU03 preview cap).</summary>
    public const int MaxAccountsInScope = 10000;

    private readonly ITenantContext _tenant;
    private readonly ITerritoryModelRepository _models;
    private readonly ITerritoryNodeRepository _nodes;
    private readonly ITerritoryAssignmentRuleRepository _rules;
    private readonly IAccountTerritoryAssignmentRepository _assignments;
    private readonly ITerritoryAccountReader _accounts;
    private readonly ITerritoryImportRunRepository _runs;
    private readonly IReferenceDataCatalogReader _catalog;
    private readonly ITerritoryReferenceValidator _references;

    public TerritoryImportEngine(
        ITenantContext tenant,
        ITerritoryModelRepository models,
        ITerritoryNodeRepository nodes,
        ITerritoryAssignmentRuleRepository rules,
        IAccountTerritoryAssignmentRepository assignments,
        ITerritoryAccountReader accounts,
        ITerritoryImportRunRepository runs,
        IReferenceDataCatalogReader catalog,
        ITerritoryReferenceValidator references)
    {
        _tenant = tenant;
        _models = models;
        _nodes = nodes;
        _rules = rules;
        _assignments = assignments;
        _accounts = accounts;
        _runs = runs;
        _catalog = catalog;
        _references = references;
    }

    public async Task<Response<TerritoryImportPreviewDto>> RunAsync(
        Guid modelId, byte[] file, string fileName, bool dryRun, bool strictMode, string? correlationId,
        string actor, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<TerritoryImportPreviewDto>.Fail("Tenant context is required.", 400);
        }

        var model = await _models.GetByIdAsync(tenantId, modelId, cancellationToken);
        if (model is null)
        {
            return Response<TerritoryImportPreviewDto>.Fail("Territory model not found.", 404);
        }

        var correlation = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId.Trim();
        var hash = Convert.ToHexString(SHA256.HashData(file)).ToLowerInvariant();

        using var stream = new MemoryStream(file, writable: false);
        var parsed = TerritoryWorkbookReader.Read(stream);

        var previousApplies = (await _runs.ListByFileHashAsync(tenantId, modelId, hash, cancellationToken)).Count;

        if (!parsed.IsReadable)
        {
            return Response<TerritoryImportPreviewDto>.Success(Preview(
                correlation, model, dryRun, applied: false, canApply: false,
                blockedReason: "The file could not be read.", strictMode, hash, previousApplies, null, null,
                parsed.FileErrors, parsed.FileWarnings, [], []));
        }

        var context = await BuildContextAsync(tenantId, model, cancellationToken);
        var plan = new TerritoryImportValidator(context, tenantId).Validate(parsed);

        var sheetOutcomes = BuildSheetOutcomes(plan);
        var blockingRows = plan.Count(r => r.Blocking);
        var actionableRows = plan.Count(r => r.Action is not null);
        var totalConsidered = plan.Count(r => r.Status != TerritoryImportRowStatuses.Skip);

        var blockedReason = ResolveBlockedReason(context, actionableRows, blockingRows, totalConsidered, strictMode);
        var canApply = blockedReason is null;

        if (dryRun)
        {
            return Response<TerritoryImportPreviewDto>.Success(Preview(
                correlation, model, dryRun: true, applied: false, canApply, blockedReason, strictMode, hash,
                previousApplies, null, null, parsed.FileErrors, parsed.FileWarnings, sheetOutcomes, plan));
        }

        if (!canApply)
        {
            // Apply refused: this IS an apply attempt, so it is recorded — but nothing was written.
            var blockedRun = await RecordRunAsync(
                tenantId, model, fileName, hash, actor, TerritoryImportRunStatuses.Blocked, correlation, strictMode,
                plan, sheetOutcomes, cancellationToken);

            return Response<TerritoryImportPreviewDto>.Success(Preview(
                correlation, model, dryRun: false, applied: false, canApply: false, blockedReason, strictMode, hash,
                previousApplies, blockedRun.Id, blockedRun.Status, parsed.FileErrors, parsed.FileWarnings,
                sheetOutcomes, plan));
        }

        var applyOutcomes = await ApplyAsync(tenantId, plan, sheetOutcomes, correlation, cancellationToken);
        var status = applyOutcomes.All(s => s.Applied || s.TotalRows == 0)
            ? TerritoryImportRunStatuses.Applied
            : applyOutcomes.Any(s => s.Applied)
                ? TerritoryImportRunStatuses.PartiallyApplied
                : TerritoryImportRunStatuses.Failed;

        var run = await RecordRunAsync(
            tenantId, model, fileName, hash, actor, status, correlation, strictMode, plan, applyOutcomes, cancellationToken);

        return Response<TerritoryImportPreviewDto>.Success(Preview(
            correlation, model, dryRun: false, applied: status != TerritoryImportRunStatuses.Failed, canApply: true,
            blockedReason: null, strictMode, hash, previousApplies, run.Id, run.Status,
            parsed.FileErrors, parsed.FileWarnings, applyOutcomes, plan));
    }

    // ---- apply ---------------------------------------------------------------------------------------------------

    private async Task<List<TerritoryImportSheetOutcomeDto>> ApplyAsync(
        Guid tenantId,
        List<TerritoryImportPlanRow> plan,
        List<TerritoryImportSheetOutcomeDto> outcomes,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var results = new List<TerritoryImportSheetOutcomeDto>();

        foreach (var outcome in outcomes)
        {
            var rows = plan.Where(r => r.Sheet == outcome.Sheet).ToList();
            if (rows.Count == 0) { results.Add(outcome); continue; }

            if (outcome.BlockingRows > 0)
            {
                // Sheet-level / batch-level all-or-nothing: one blocking row keeps the WHOLE sheet unwritten, and the
                // rows that would have been written are re-labelled so the report never implies they landed.
                foreach (var row in rows.Where(r => r.Action is not null))
                {
                    row.Status = TerritoryImportRowStatuses.NotApplied;
                    row.Severity = TerritoryImportSeverities.Warning;
                    row.ErrorCode = TerritoryImportErrorCodes.SheetBlocked;
                    row.Message = "Not applied: another row on this sheet has a blocking error, and the sheet is all-or-nothing.";
                }

                results.Add(outcome with
                {
                    Applied = false,
                    NotAppliedReason = $"{outcome.BlockingRows} blocking row(s) on this sheet; the sheet is all-or-nothing."
                });
                continue;
            }

            var buffer = new TerritoryImportApplyBuffer();
            foreach (var row in rows.Where(r => r.Action is not null))
            {
                row.Action!(buffer);
            }

            try
            {
                await WriteAsync(tenantId, buffer, correlationId, cancellationToken);
            }
            catch (Exception)
            {
                foreach (var row in rows.Where(r => r.Action is not null))
                {
                    row.Status = TerritoryImportRowStatuses.NotApplied;
                    row.Severity = TerritoryImportSeverities.Error;
                    row.ErrorCode = TerritoryImportErrorCodes.SheetBlocked;
                    row.Message = "Not applied: writing this sheet failed. No other sheet was affected.";
                }

                results.Add(outcome with { Applied = false, NotAppliedReason = "The write failed for this sheet." });
                continue;
            }

            foreach (var row in rows.Where(r => r.Action is not null))
            {
                row.Status = row.Status switch
                {
                    TerritoryImportRowStatuses.Create => TerritoryImportRowStatuses.Create,
                    TerritoryImportRowStatuses.Update => TerritoryImportRowStatuses.Update,
                    TerritoryImportRowStatuses.End => TerritoryImportRowStatuses.End,
                    _ => TerritoryImportRowStatuses.Applied
                };
            }

            results.Add(outcome with { Applied = true, NotAppliedReason = null });
        }

        return results;
    }

    private async Task WriteAsync(
        Guid tenantId, TerritoryImportApplyBuffer buffer, string correlationId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        if (buffer.ModelUpdate is { } model)
        {
            model.UpdatedAt = now;
            model.CorrelationId = correlationId;
            await _models.UpdateAsync(model, cancellationToken);
        }

        foreach (var node in buffer.NodeInserts)
        {
            node.CorrelationId = correlationId;
            await _nodes.InsertAsync(node, cancellationToken);
        }

        foreach (var node in buffer.NodeUpdates)
        {
            node.UpdatedAt = now;
            node.CorrelationId = correlationId;
            await _nodes.UpdateAsync(node, cancellationToken);
        }

        foreach (var rule in buffer.RuleInserts)
        {
            rule.CorrelationId = correlationId;
            await _rules.InsertAsync(rule, cancellationToken);
        }

        foreach (var rule in buffer.RuleUpdates)
        {
            rule.UpdatedAt = now;
            rule.CorrelationId = correlationId;
            await _rules.UpdateAsync(rule, cancellationToken);
        }

        if (buffer.AssignmentEnds.Count > 0 || buffer.AssignmentInserts.Count > 0)
        {
            foreach (var assignment in buffer.AssignmentEnds.Concat(buffer.AssignmentInserts))
            {
                assignment.CorrelationId = correlationId;
            }

            // Same commit path (and standalone-Mongo compensation fallback) the FU05 on-screen apply uses.
            await _assignments.CommitApplyAsync(buffer.AssignmentEnds, buffer.AssignmentInserts, cancellationToken);
        }

        _ = tenantId;
    }

    // ---- context / reporting -------------------------------------------------------------------------------------

    private async Task<TerritoryImportContext> BuildContextAsync(
        Guid tenantId, TerritoryModel model, CancellationToken cancellationToken)
    {
        var published = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var levelRanks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var setCode in ContextSets)
        {
            var snapshot = await _catalog.GetPublishedValuesAsync(setCode, cancellationToken);
            published[setCode] = snapshot.IsPublished
                ? snapshot.Values.Where(v => !v.IsDeprecated).Select(v => v.ValueCode).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var level in published[TerritoryReferenceSets.TerritoryLevel])
        {
            var rank = await _references.ResolveLevelRankAsync(level, cancellationToken);
            if (rank.Ok) levelRanks[level] = rank.Rank;
        }

        return new TerritoryImportContext
        {
            Model = model,
            Nodes = await _nodes.ListByModelAsync(tenantId, model.Id, cancellationToken),
            Rules = await _rules.ListByModelAsync(tenantId, model.Id, cancellationToken),
            Assignments = await _assignments.ListByModelAsync(tenantId, model.Id, cancellationToken),
            Accounts = await _accounts.ListForPreviewAsync(tenantId, MaxAccountsInScope, cancellationToken),
            OtherActiveModels = await _models.ListActiveAsync(tenantId, model.Id, cancellationToken),
            PublishedValues = published,
            LevelRanks = levelRanks
        };
    }

    private static readonly string[] ContextSets =
    [
        TerritoryReferenceSets.TerritoryLevel,
        TerritoryReferenceSets.TerritoryRuleType,
        TerritoryReferenceSets.TerritoryConflictPolicy,
        TerritoryReferenceSets.TerritoryAssignmentStatus,
        TerritoryReferenceSets.TerritoryAssignmentSource,
        TerritoryReferenceSets.TerritoryCoverageScope,
        TerritoryReferenceSets.BusinessUnitValueSet
    ];

    private static List<TerritoryImportSheetOutcomeDto> BuildSheetOutcomes(List<TerritoryImportPlanRow> plan)
        => TerritoryWorkbookSchema.ImportableSheets
            .Select(sheet =>
            {
                var rows = plan.Where(r => r.Sheet == sheet).ToList();
                return new TerritoryImportSheetOutcomeDto(
                    sheet,
                    rows.Count,
                    rows.Count(r => r.Blocking),
                    Applied: false,
                    NotAppliedReason: null,
                    Created: rows.Count(r => r.Status == TerritoryImportRowStatuses.Create),
                    Updated: rows.Count(r => r.Status == TerritoryImportRowStatuses.Update),
                    Ended: rows.Count(r => r.Status == TerritoryImportRowStatuses.End),
                    Skipped: rows.Count(r => r.Status is TerritoryImportRowStatuses.Skip or TerritoryImportRowStatuses.NoChange));
            })
            .Where(s => s.TotalRows > 0)
            .ToList();

    private static string? ResolveBlockedReason(
        TerritoryImportContext context, int actionableRows, int blockingRows, int totalConsidered, bool strictMode)
    {
        if (strictMode && blockingRows > 0)
        {
            return $"Strict mode: {blockingRows} blocking row(s) — nothing will be applied.";
        }

        if (actionableRows == 0)
        {
            return "There is nothing to apply: every row was skipped or blocked.";
        }

        if (totalConsidered > 0 && (double)blockingRows / totalConsidered > BlockingRatioLimit)
        {
            return $"{blockingRows} of {totalConsidered} rows are blocked — this looks like the wrong file or an outdated template.";
        }

        // Reference dependencies are fail-closed at file level too, not just per row.
        if (!context.SetPublished(TerritoryReferenceSets.TerritoryLevel))
        {
            return $"Reference set '{TerritoryReferenceSets.TerritoryLevel}' is not published for this tenant.";
        }

        return null;
    }

    private async Task<TerritoryImportRun> RecordRunAsync(
        Guid tenantId, TerritoryModel model, string fileName, string hash, string actor, string status,
        string correlationId, bool strictMode, List<TerritoryImportPlanRow> plan,
        List<TerritoryImportSheetOutcomeDto> outcomes, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var summary = Summarize(plan);

        var run = new TerritoryImportRun
        {
            TenantId = tenantId,
            TerritoryModelId = model.Id,
            ModelCode = model.ModelCode,
            // The file NAME is stored (it is operator-chosen metadata); the file CONTENT never is — only its hash.
            FileName = fileName,
            FileHash = hash,
            UploadedBy = actor,
            UploadedAt = now,
            Status = status,
            AppliedAt = status is TerritoryImportRunStatuses.Applied or TerritoryImportRunStatuses.PartiallyApplied ? now : null,
            AppliedBy = status is TerritoryImportRunStatuses.Applied or TerritoryImportRunStatuses.PartiallyApplied ? actor : null,
            CorrelationId = correlationId,
            ErrorCount = summary.Errors,
            WarningCount = summary.Warnings,
            DryRunResult = new TerritoryImportRunResult
            {
                TotalRows = summary.TotalRows,
                Creates = summary.Creates,
                Updates = summary.Updates,
                Ends = summary.Ends,
                Skips = summary.Skips,
                Errors = summary.Errors,
                Conflicts = summary.Conflicts,
                Warnings = summary.Warnings,
                StrictMode = strictMode,
                SheetOutcomes = outcomes
                    .Select(o => o.Applied
                        ? $"{o.Sheet}: applied (+{o.Created} created, {o.Updated} updated, {o.Ended} ended, {o.Skipped} skipped)"
                        : $"{o.Sheet}: NOT applied — {o.NotAppliedReason ?? "blocked"}")
                    .ToList()
            },
            SheetCounts = outcomes
                .Select(o => new TerritoryImportRunSheetCount
                {
                    Sheet = o.Sheet, Total = o.TotalRows,
                    Created = o.Applied ? o.Created : 0,
                    Updated = o.Applied ? o.Updated : 0,
                    Ended = o.Applied ? o.Ended : 0,
                    Skipped = o.Skipped
                })
                .ToList()
        };

        await _runs.InsertAsync(run, cancellationToken);
        return run;
    }

    private static TerritoryImportSummaryDto Summarize(List<TerritoryImportPlanRow> plan) => new(
        plan.Count,
        plan.Count(r => r.Status == TerritoryImportRowStatuses.Create),
        plan.Count(r => r.Status == TerritoryImportRowStatuses.Update),
        plan.Count(r => r.Status == TerritoryImportRowStatuses.End),
        plan.Count(r => r.Status is TerritoryImportRowStatuses.Skip or TerritoryImportRowStatuses.NoChange),
        plan.Count(r => r.Status is TerritoryImportRowStatuses.Error or TerritoryImportRowStatuses.NotApplied),
        plan.Count(r => r.Status == TerritoryImportRowStatuses.Conflict),
        plan.Count(r => r.Severity == TerritoryImportSeverities.Warning));

    private static TerritoryImportPreviewDto Preview(
        string correlationId, TerritoryModel model, bool dryRun, bool applied, bool canApply, string? blockedReason,
        bool strictMode, string hash, int previousApplies, Guid? runId, string? runStatus,
        IReadOnlyList<string> fileErrors, IReadOnlyList<string> fileWarnings,
        IReadOnlyList<TerritoryImportSheetOutcomeDto> sheets, List<TerritoryImportPlanRow> plan)
        => new(
            correlationId, model.Id, model.ModelCode, model.Status, dryRun, applied, canApply, blockedReason,
            "validate-all-then-apply; Model/Nodes/AssignmentRules sheet-level all-or-nothing; AccountAssignments batch-level all-or-nothing",
            strictMode, hash, previousApplies, runId, runStatus,
            Summarize(plan), fileErrors, fileWarnings, sheets, plan.Select(r => r.ToDto()).ToList());
}
