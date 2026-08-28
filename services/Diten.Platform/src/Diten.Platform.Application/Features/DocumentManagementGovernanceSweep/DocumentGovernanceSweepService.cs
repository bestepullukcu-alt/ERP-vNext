using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementDowntime.Services;
using Diten.Platform.Application.Features.DocumentManagementPeriodicReview.Services;
using Diten.Platform.Application.Features.DocumentManagementSuspension.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementGovernanceSweep;

/// <summary>
/// MOD-0029-FU32 — the background governance sweep orchestrator (GMG-QMS-SOP-0001).
///
/// WHAT A SWEEP IS: a periodic OBSERVER over the FU12–FU23 governance surface. It finds the due, overdue, expired
/// and eligible conditions that would otherwise stay invisible until somebody opened the right screen, and it turns
/// them into escalations (where a pre-existing idempotent evaluator already raises them) or into report lines.
///
/// WHAT A SWEEP IS NOT — every one of these is a hard boundary, covered by tests:
/// • it never deletes or purges anything (no repository used here even exposes a delete);
/// • it never closes, approves, makes effective, disposes of, signs or retires a subject;
/// • it never rewrites a lifecycle state machine or an existing evaluator's business behaviour;
/// • it never calls an external system (no QMS API, no e-signature provider, no source-authority fetch).
///
/// The only writes a sweep can cause are the ones the ALREADY-IDEMPOTENT FU12 / FU13 / FU20 evaluators perform when
/// invoked explicitly: flagging an overdue condition and raising a duplicate-suppressed escalation or suspension
/// CASE. Groups without such an evaluator (CAPA, signature requests, retention, legal hold) are strictly
/// report-only — they read and summarise, and write nothing but the run-history row.
///
/// TENANCY: every entry point requires a resolved tenant context. TenantId is never read from a client payload;
/// there is no cross-tenant enumeration anywhere in this service.
///
/// IDEMPOTENCY: re-running a sweep produces no duplicate escalation — the underlying evaluators suppress an
/// already-open escalation of the same type, and this service counts the difference before/after so the run row
/// distinguishes "created" from "skipped existing". Each run writes a NEW append-only history row; a dry run writes
/// none at all.
///
/// FAILURE MODEL: group-level isolation. A group that throws records a warning and the run continues, finishing as
/// <see cref="DocumentGovernanceSweepStatus.CompletedWithWarnings"/>. Only a failure to establish the run itself is
/// <see cref="DocumentGovernanceSweepStatus.Failed"/>, and even then the history row is written best-effort.
/// </summary>
public sealed class DocumentGovernanceSweepService(
    IDocumentGovernanceSweepRunRepository runs,
    ITenantContext tenantContext,
    ICurrentUserContext currentUser,
    IDocumentMasterRegisterRepository register,
    IDocumentPeriodicReviewEscalationRepository reviewEscalations,
    DocumentPeriodicReviewService periodicReviews,
    IExternalDocumentRegisterRepository externalDocuments,
    IExternalDocumentImpactAssessmentRepository externalImpacts,
    ITemporaryInstructionControlRepository temporaryInstructions,
    IDocumentSuspensionCaseRepository suspensionCases,
    TemporaryInstructionService temporaryInstructionService,
    IDocumentTemporaryControlledIssueRepository temporaryIssues,
    IDocumentDowntimeEscalationRepository downtimeEscalations,
    DocumentTemporaryIssueService temporaryIssueService,
    IDocumentCAPAActionRepository capaActions,
    IDocumentSignatureRequestRepository signatureRequests,
    IDocumentRetentionSubjectRepository retentionSubjects,
    IDocumentLegalHoldRepository legalHolds)
{
    // ── public entry points ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Runs every group (or the subset named by <c>SweepKeys</c>) in one append-only run.</summary>
    public Task<Response<GovernanceSweepRunModel>> RunAllAsync(
        GovernanceSweepRunInput input, string correlationId, CancellationToken ct = default)
    {
        var requested = input.SweepKeys is { Count: > 0 }
            ? input.SweepKeys.Where(DocumentGovernanceSweepCatalog.IsKnown).Distinct().ToList()
            : [.. DocumentGovernanceSweepCatalog.RunAllGroups];

        var unknown = input.SweepKeys?.Where(k => !DocumentGovernanceSweepCatalog.IsKnown(k)).ToList() ?? [];
        return ExecuteAsync(DocumentGovernanceSweepKeys.RunAll, requested, unknown, input, correlationId, ct);
    }

    public Task<Response<GovernanceSweepRunModel>> RunPeriodicReviewsAsync(
        GovernanceSweepRunInput input, string correlationId, CancellationToken ct = default) =>
        RunSingleAsync(DocumentGovernanceSweepKeys.PeriodicReviews, input, correlationId, ct);

    public Task<Response<GovernanceSweepRunModel>> RunExternalDocumentsAsync(
        GovernanceSweepRunInput input, string correlationId, CancellationToken ct = default) =>
        RunSingleAsync(DocumentGovernanceSweepKeys.ExternalDocuments, input, correlationId, ct);

    public Task<Response<GovernanceSweepRunModel>> RunTemporaryInstructionsAsync(
        GovernanceSweepRunInput input, string correlationId, CancellationToken ct = default) =>
        RunSingleAsync(DocumentGovernanceSweepKeys.TemporaryInstructions, input, correlationId, ct);

    public Task<Response<GovernanceSweepRunModel>> RunDowntimeTemporaryIssuesAsync(
        GovernanceSweepRunInput input, string correlationId, CancellationToken ct = default) =>
        RunSingleAsync(DocumentGovernanceSweepKeys.DowntimeTemporaryIssues, input, correlationId, ct);

    public Task<Response<GovernanceSweepRunModel>> RunCapaAsync(
        GovernanceSweepRunInput input, string correlationId, CancellationToken ct = default) =>
        RunSingleAsync(DocumentGovernanceSweepKeys.QualityCapa, input, correlationId, ct);

    public Task<Response<GovernanceSweepRunModel>> RunSignatureRequestsAsync(
        GovernanceSweepRunInput input, string correlationId, CancellationToken ct = default) =>
        RunSingleAsync(DocumentGovernanceSweepKeys.SignatureRequests, input, correlationId, ct);

    public Task<Response<GovernanceSweepRunModel>> RunRetentionEligibilityAsync(
        GovernanceSweepRunInput input, string correlationId, CancellationToken ct = default) =>
        RunSingleAsync(DocumentGovernanceSweepKeys.RetentionEligibility, input, correlationId, ct);

    public Task<Response<GovernanceSweepRunModel>> RunLegalHoldScopeAsync(
        GovernanceSweepRunInput input, string correlationId, CancellationToken ct = default) =>
        RunSingleAsync(DocumentGovernanceSweepKeys.LegalHoldScope, input, correlationId, ct);

    /// <summary>
    /// A read-only projection of what a run-all would report. Implemented as a forced dry run, so it is provably
    /// incapable of writing: no escalation, no finding, no subject mutation, no history row.
    /// </summary>
    public Task<Response<GovernanceSweepRunModel>> PreviewAllAsync(
        GovernanceSweepRunInput input, string correlationId, CancellationToken ct = default) =>
        RunAllAsync(input with { DryRun = true }, correlationId, ct);

    // ── history reads ────────────────────────────────────────────────────────────────────────────────────

    public async Task<Response<IReadOnlyList<GovernanceSweepRunSummaryModel>>> ListRunsAsync(
        string correlationId, CancellationToken ct = default)
    {
        if (!tenantContext.IsResolved)
        {
            return Response<IReadOnlyList<GovernanceSweepRunSummaryModel>>.Fail(
                "Tenant context is required.", 400, GovernanceSweepReasonCodes.TenantRequired, correlationId);
        }

        var rows = await runs.GetAllForTenantAsync(ct);
        IReadOnlyList<GovernanceSweepRunSummaryModel> models = rows
            .Select(r => new GovernanceSweepRunSummaryModel(
                r.Id, r.SweepKey, r.SweepName, r.SweepVersion, r.TriggerType, r.Status,
                r.StartedAt, r.CompletedAt, r.ItemsScanned, r.ItemsAffected,
                r.EscalationsCreated, r.FindingsCreated, r.CreatedBy))
            .ToList();

        return Response<IReadOnlyList<GovernanceSweepRunSummaryModel>>.Success(models, 200, correlationId);
    }

    /// <summary>Tenant-scoped read — a cross-tenant id resolves to not-found (no existence leakage).</summary>
    public async Task<Response<GovernanceSweepRunDetailModel>> GetRunAsync(
        Guid id, string correlationId, CancellationToken ct = default)
    {
        if (!tenantContext.IsResolved)
        {
            return Response<GovernanceSweepRunDetailModel>.Fail(
                "Tenant context is required.", 400, GovernanceSweepReasonCodes.TenantRequired, correlationId);
        }

        var r = await runs.GetByIdAsync(id, ct);
        if (r is null)
        {
            return Response<GovernanceSweepRunDetailModel>.Fail(
                "Governance sweep run not found.", 404, GovernanceSweepReasonCodes.RunNotFound, correlationId);
        }

        var model = new GovernanceSweepRunDetailModel(
            r.Id, r.SweepKey, r.SweepName, r.SweepVersion, r.TriggerType, r.Status,
            r.StartedAt, r.CompletedAt, r.AsOfDate, r.TriggeredByUserId, r.CreatedBy, r.CorrelationId,
            r.ItemsScanned, r.ItemsAffected, r.FindingsCreated, r.EscalationsCreated,
            r.ExistingFindingsSkipped, r.ExistingEscalationsSkipped,
            r.SweepKeysExecuted, r.Warnings, r.ErrorMessage, r.DryRun,
            [.. r.ResultItems.Select(ToItemModel)]);

        return Response<GovernanceSweepRunDetailModel>.Success(model, 200, correlationId);
    }

    // ── orchestration ────────────────────────────────────────────────────────────────────────────────────

    private Task<Response<GovernanceSweepRunModel>> RunSingleAsync(
        string sweepKey, GovernanceSweepRunInput input, string correlationId, CancellationToken ct) =>
        ExecuteAsync(sweepKey, [sweepKey], [], input, correlationId, ct);

    private async Task<Response<GovernanceSweepRunModel>> ExecuteAsync(
        string sweepKey,
        IReadOnlyList<string> groupKeys,
        IReadOnlyList<string> unknownKeys,
        GovernanceSweepRunInput input,
        string correlationId,
        CancellationToken ct)
    {
        if (!tenantContext.IsResolved)
        {
            return Response<GovernanceSweepRunModel>.Fail(
                "Tenant context is required.", 400, GovernanceSweepReasonCodes.TenantRequired, correlationId);
        }

        var tenantId = TenantGuard.RequireTenant(tenantContext);
        var startedAt = DateTimeOffset.UtcNow;
        var asOf = input.AsOfDate ?? startedAt;

        var groups = new List<SweepGroup>();
        var warnings = new List<string>();

        foreach (var unknown in unknownKeys)
        {
            warnings.Add($"Unknown sweep key '{unknown}' was ignored ({GovernanceSweepReasonCodes.Unsupported}).");
        }

        if (input.MaxItems is <= 0)
        {
            warnings.Add("maxItems must be greater than zero; the cap was ignored.");
        }

        if (input.DryRun)
        {
            warnings.Add($"Dry run ({GovernanceSweepReasonCodes.DryRun}): conditions were evaluated but nothing was written — no escalation, no finding, no subject change and no run-history row.");
        }

        foreach (var key in groupKeys)
        {
            ct.ThrowIfCancellationRequested();
            var group = new SweepGroup(key);
            try
            {
                await RunGroupAsync(key, group, input, asOf, correlationId, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Group-level isolation: one failing group must not hide the findings of the others.
                group.Warnings.Add($"{GovernanceSweepReasonCodes.PartialFailure}: sweep group '{key}' failed: {ex.Message}");
            }

            groups.Add(group);
        }

        warnings.AddRange(groups.SelectMany(g => g.Warnings));
        var status = warnings.Count > 0
            ? DocumentGovernanceSweepStatus.CompletedWithWarnings
            : DocumentGovernanceSweepStatus.Completed;

        var completedAt = DateTimeOffset.UtcNow;

        // A dry run leaves no trace at all — not even a history row. The caller gets the full report in the response.
        if (input.DryRun)
        {
            return Response<GovernanceSweepRunModel>.Success(
                ToRunModel(Guid.Empty, sweepKey, status, dryRun: true, startedAt, completedAt, asOf, groupKeys, warnings, null, groups),
                200, correlationId);
        }

        var run = new DocumentGovernanceSweepRun
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SweepKey = sweepKey,
            SweepName = DocumentGovernanceSweepCatalog.NameOf(sweepKey),
            SweepVersion = DocumentGovernanceSweepCatalog.SweepVersion,
            TriggerType = DocumentGovernanceSweepTriggerType.Manual,
            Status = status,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            AsOfDate = asOf,
            TriggeredByUserId = currentUser.UserId == Guid.Empty ? null : currentUser.UserId,
            CorrelationId = correlationId,
            ItemsScanned = groups.Sum(g => g.ItemsScanned),
            ItemsAffected = groups.Sum(g => g.ItemsAffected),
            FindingsCreated = groups.Sum(g => g.FindingsCreated),
            EscalationsCreated = groups.Sum(g => g.EscalationsCreated),
            ExistingFindingsSkipped = groups.Sum(g => g.ExistingFindingsSkipped),
            ExistingEscalationsSkipped = groups.Sum(g => g.ExistingEscalationsSkipped),
            Warnings = [.. warnings],
            SweepKeysExecuted = [.. groupKeys],
            ResultItems = [.. groups.SelectMany(g => g.Items)],
            DryRun = false,
            CreatedBy = currentUser.ActorName
        };

        DocumentGovernanceSweepRun saved;
        try
        {
            saved = await runs.CreateAsync(run, ct);
        }
        catch (Exception ex)
        {
            // The sweep itself already completed; only the evidence write failed. Report it rather than pretending.
            return Response<GovernanceSweepRunModel>.Fail(
                $"Governance sweep completed but its run history could not be recorded: {ex.Message}",
                500, GovernanceSweepReasonCodes.Failed, correlationId);
        }

        return Response<GovernanceSweepRunModel>.Success(
            ToRunModel(saved.Id, sweepKey, status, dryRun: false, startedAt, completedAt, asOf, groupKeys, warnings, null, groups),
            200, correlationId);
    }

    private Task RunGroupAsync(
        string key, SweepGroup group, GovernanceSweepRunInput input, DateTimeOffset asOf,
        string correlationId, CancellationToken ct) => key switch
    {
        DocumentGovernanceSweepKeys.PeriodicReviews => SweepPeriodicReviewsAsync(group, input, asOf, correlationId, ct),
        DocumentGovernanceSweepKeys.ExternalDocuments => SweepExternalDocumentsAsync(group, input, asOf, ct),
        DocumentGovernanceSweepKeys.TemporaryInstructions => SweepTemporaryInstructionsAsync(group, input, asOf, correlationId, ct),
        DocumentGovernanceSweepKeys.DowntimeTemporaryIssues => SweepDowntimeTemporaryIssuesAsync(group, input, asOf, correlationId, ct),
        DocumentGovernanceSweepKeys.QualityCapa => SweepCapaAsync(group, input, asOf, ct),
        DocumentGovernanceSweepKeys.SignatureRequests => SweepSignatureRequestsAsync(group, input, asOf, ct),
        DocumentGovernanceSweepKeys.RetentionEligibility => SweepRetentionEligibilityAsync(group, input, asOf, ct),
        DocumentGovernanceSweepKeys.LegalHoldScope => SweepLegalHoldScopeAsync(group, input, asOf, ct),
        _ => UnsupportedAsync(key, group)
    };

    private static Task UnsupportedAsync(string key, SweepGroup group)
    {
        group.Warnings.Add($"{GovernanceSweepReasonCodes.Unsupported}: sweep group '{key}' is not implemented.");
        return Task.CompletedTask;
    }

    // ── group 1: periodic review overdue (FU12) ──────────────────────────────────────────────────────────
    //
    // Delegates to the pre-existing DocumentPeriodicReviewService.EvaluateOverdueAsync, which is already idempotent
    // (it suppresses an open escalation of the same type). It never suspends the document and never transitions its
    // lifecycle — the escalation is the whole output. Auto-initiating a due review is deliberately NOT done here:
    // initiation is a human act with an owner.
    private async Task SweepPeriodicReviewsAsync(
        SweepGroup group, GovernanceSweepRunInput input, DateTimeOffset asOf, string correlationId, CancellationToken ct)
    {
        WarnIfAsOfIgnored(group, input, "FU12 periodic review");

        var entries = Cap(
            (await register.GetAllForTenantAsync(ct))
                .Where(DocumentReviewCycleCalculator.IsScheduledForReview)
                .Where(e => DocumentReviewCycleCalculator.CurrentDueDate(e) is { } due && asOf > due),
            input, group);

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            group.ItemsScanned++;

            if (input.DryRun)
            {
                group.Report(nameof(DocumentMasterRegisterEntry), entry.Id, "EvaluatePeriodicReviewOverdue",
                    DocumentGovernanceSweepItemOutcome.DryRun,
                    "Periodic review is past its due date; a dry run raised no escalation.");
                continue;
            }

            var before = (await reviewEscalations.GetByRegisterEntryAsync(entry.Id, ct)).Count;
            var response = await periodicReviews.EvaluateOverdueAsync(entry.Id, correlationId, ct);
            if (!response.IsSuccessful)
            {
                group.Warn(nameof(DocumentMasterRegisterEntry), entry.Id, "EvaluatePeriodicReviewOverdue",
                    $"Overdue evaluation failed: {response.ReasonCode}.");
                continue;
            }

            var after = (await reviewEscalations.GetByRegisterEntryAsync(entry.Id, ct)).Count;
            group.RecordEscalationDelta(nameof(DocumentMasterRegisterEntry), entry.Id, "EvaluatePeriodicReviewOverdue",
                after - before, "Periodic review is overdue.");
        }
    }

    // ── group 2: external document monitoring + impact (FU14) ────────────────────────────────────────────
    //
    // REPORT-ONLY. No monitoring check is completed on the owner's behalf, no source status is touched, no internal
    // document lifecycle moves, and no external authority is contacted — the sweep only reads the register.
    private async Task SweepExternalDocumentsAsync(
        SweepGroup group, GovernanceSweepRunInput input, DateTimeOffset asOf, CancellationToken ct)
    {
        var entries = Cap((await externalDocuments.GetAllForTenantAsync(ct))
            .Where(e => e.ExternalDocumentStatus == ExternalDocumentStatus.Active), input, group);

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            group.ItemsScanned++;

            if (entry.NextCheckDueDate is not { } due)
            {
                continue; // OnTrigger cadence or an archived source: never "overdue" on a schedule.
            }

            if (asOf > due)
            {
                group.Report(nameof(ExternalDocumentRegisterEntry), entry.Id, "ReportMonitoringOverdue",
                    DocumentGovernanceSweepItemOutcome.Reported,
                    $"Monitoring check is overdue by {(int)Math.Floor((asOf - due).TotalDays)} day(s) (due {due:yyyy-MM-dd}).");
            }
            else if (asOf >= due.AddDays(-MonitoringDueSoonDays))
            {
                group.Report(nameof(ExternalDocumentRegisterEntry), entry.Id, "ReportMonitoringDue",
                    DocumentGovernanceSweepItemOutcome.Reported,
                    $"Monitoring check is due on {due:yyyy-MM-dd}.");
            }
        }

        var impacts = (await externalImpacts.GetAllForTenantAsync(ct))
            .Where(a => a.AssessmentStatus is ExternalImpactAssessmentStatus.Pending
                        or ExternalImpactAssessmentStatus.InProgress
                        or ExternalImpactAssessmentStatus.Overdue)
            .Where(a => asOf > a.DueDate)
            .ToList();

        foreach (var assessment in impacts)
        {
            group.ItemsScanned++;
            group.Report(nameof(ExternalDocumentImpactAssessment), assessment.Id, "ReportImpactAssessmentOverdue",
                DocumentGovernanceSweepItemOutcome.Reported,
                $"Impact assessment is overdue (due {assessment.DueDate:yyyy-MM-dd}, status {assessment.AssessmentStatus}).");
        }
    }

    // ── group 3: temporary instruction expiry (FU13) ─────────────────────────────────────────────────────
    //
    // Delegates to the pre-existing TemporaryInstructionService.EvaluateExpiryAsync. That evaluator marks an expired
    // instruction Expired and — only when no expiry action was decided — OPENS a suspension case through the
    // idempotent OpenInternalAsync (which returns the already-open case rather than creating a second one). Opening
    // a case is not executing a suspension: the document's lifecycle is untouched and approval remains a human act.
    private async Task SweepTemporaryInstructionsAsync(
        SweepGroup group, GovernanceSweepRunInput input, DateTimeOffset asOf, string correlationId, CancellationToken ct)
    {
        WarnIfAsOfIgnored(group, input, "FU13 temporary instruction expiry");

        var controls = Cap((await temporaryInstructions.GetAllForTenantAsync(ct))
            .Where(c => c.TemporaryInstructionStatus is TemporaryInstructionStatus.Active
                        or TemporaryInstructionStatus.DueToExpire)
            .Where(c => asOf > c.ValidUntil), input, group);

        foreach (var control in controls)
        {
            ct.ThrowIfCancellationRequested();
            group.ItemsScanned++;

            if (input.DryRun)
            {
                group.Report(nameof(TemporaryInstructionControl), control.Id, "EvaluateTemporaryInstructionExpiry",
                    DocumentGovernanceSweepItemOutcome.DryRun,
                    $"Temporary instruction expired on {control.ValidUntil:yyyy-MM-dd}; a dry run opened no suspension case.");
                continue;
            }

            var existingCase = await suspensionCases.GetOpenAsync(control.RegisterEntryId, ct);
            var response = await temporaryInstructionService.EvaluateExpiryAsync(control.RegisterEntryId, correlationId, ct);
            if (!response.IsSuccessful)
            {
                group.Warn(nameof(TemporaryInstructionControl), control.Id, "EvaluateTemporaryInstructionExpiry",
                    $"Expiry evaluation failed: {response.ReasonCode}.");
                continue;
            }

            var openCase = await suspensionCases.GetOpenAsync(control.RegisterEntryId, ct);
            if (existingCase is null && openCase is not null)
            {
                group.ItemsAffected++;
                group.FindingsCreated++;
                group.Items.Add(Item(nameof(TemporaryInstructionControl), control.Id, "EvaluateTemporaryInstructionExpiry",
                    DocumentGovernanceSweepItemOutcome.EscalationCreated,
                    "Temporary instruction expired with no expiry action; a suspension case was opened for human decision.",
                    findingId: openCase.Id));
            }
            else if (existingCase is not null)
            {
                group.ItemsAffected++;
                group.ExistingFindingsSkipped++;
                group.Items.Add(Item(nameof(TemporaryInstructionControl), control.Id, "EvaluateTemporaryInstructionExpiry",
                    DocumentGovernanceSweepItemOutcome.SkippedExisting,
                    "A suspension case is already open for this register entry; nothing was duplicated.",
                    findingId: existingCase.Id));
            }
            else
            {
                group.Report(nameof(TemporaryInstructionControl), control.Id, "EvaluateTemporaryInstructionExpiry",
                    DocumentGovernanceSweepItemOutcome.Reported,
                    "Temporary instruction expired with an expiry action already decided; no case was required.");
            }
        }
    }

    // ── group 4: downtime temporary controlled issue reconciliation (FU20) ───────────────────────────────
    //
    // Delegates to the pre-existing DocumentTemporaryIssueService.EvaluateOverdueAsync, whose escalations are
    // duplicate-suppressed by EnsureEscalationAsync. It never closes the issue, never withdraws a controlled copy
    // and never settles the downtime event.
    private async Task SweepDowntimeTemporaryIssuesAsync(
        SweepGroup group, GovernanceSweepRunInput input, DateTimeOffset asOf, string correlationId, CancellationToken ct)
    {
        WarnIfAsOfIgnored(group, input, "FU20 downtime reconciliation");

        var issues = Cap((await temporaryIssues.GetOutstandingAsync(ct))
            .Where(i => i.IssueStatus is TemporaryIssueStatus.Issued or TemporaryIssueStatus.ReconciliationDue)
            .Where(i => i.ReconciliationDueDate is { } due && asOf > due), input, group);

        foreach (var issue in issues)
        {
            ct.ThrowIfCancellationRequested();
            group.ItemsScanned++;

            if (input.DryRun)
            {
                group.Report(nameof(DocumentTemporaryControlledIssue), issue.Id, "EvaluateReconciliationOverdue",
                    DocumentGovernanceSweepItemOutcome.DryRun,
                    $"Reconciliation is overdue (due {issue.ReconciliationDueDate:yyyy-MM-dd}); a dry run raised no escalation.");
                continue;
            }

            var before = (await downtimeEscalations.GetByDowntimeEventAsync(issue.DowntimeEventId, ct)).Count;
            var response = await temporaryIssueService.EvaluateOverdueAsync(
                issue.DowntimeEventId, issue.Id, correlationId, ct);
            if (!response.IsSuccessful)
            {
                group.Warn(nameof(DocumentTemporaryControlledIssue), issue.Id, "EvaluateReconciliationOverdue",
                    $"Overdue evaluation failed: {response.ReasonCode}.");
                continue;
            }

            var after = (await downtimeEscalations.GetByDowntimeEventAsync(issue.DowntimeEventId, ct)).Count;
            group.RecordEscalationDelta(nameof(DocumentTemporaryControlledIssue), issue.Id, "EvaluateReconciliationOverdue",
                after - before, $"Temporary controlled issue {issue.IssueNumber} is unreconciled past its due date.");
        }
    }

    // ── group 5: quality event / CAPA overdue (FU22) ─────────────────────────────────────────────────────
    //
    // REPORT-ONLY: FU22 has no overdue evaluator, and inventing one here would be rewriting its state machine. No
    // CAPA is closed, cancelled or marked effective; no deviation or quality event is closed; the FU22 bridge is
    // not auto-triggered.
    private async Task SweepCapaAsync(
        SweepGroup group, GovernanceSweepRunInput input, DateTimeOffset asOf, CancellationToken ct)
    {
        var actions = Cap((await capaActions.GetAllForTenantAsync(ct)).Where(a => !a.IsTerminal()), input, group);

        foreach (var action in actions)
        {
            ct.ThrowIfCancellationRequested();
            group.ItemsScanned++;

            if (action.ActionStatus is CapaActionStatus.Draft or CapaActionStatus.Open or CapaActionStatus.InProgress
                && action.DueDate is { } due && asOf > due)
            {
                group.Report(nameof(DocumentCAPAAction), action.Id, "ReportCapaOverdue",
                    DocumentGovernanceSweepItemOutcome.Reported,
                    $"CAPA {action.CAPANumber} is overdue (due {due:yyyy-MM-dd}, status {action.ActionStatus}).");
            }

            if (action.EffectivenessCheckRequired
                && action.EffectivenessResult == CapaEffectivenessResult.Pending
                && action.EffectivenessDueDate is { } effectivenessDue && asOf > effectivenessDue)
            {
                group.Report(nameof(DocumentCAPAAction), action.Id, "ReportCapaEffectivenessOverdue",
                    DocumentGovernanceSweepItemOutcome.Reported,
                    $"CAPA {action.CAPANumber} effectiveness verification is overdue (due {effectivenessDue:yyyy-MM-dd}).");
            }
        }
    }

    // ── group 6: signature request expiry (FU23) ─────────────────────────────────────────────────────────
    //
    // REPORT-ONLY. FU23 exposes no expiry transition command, so the sweep does not invent one: it never signs,
    // never verifies and never invalidates a signature or a request. Marking a request Expired stays a deferred,
    // explicitly-designed FU23 concern.
    private async Task SweepSignatureRequestsAsync(
        SweepGroup group, GovernanceSweepRunInput input, DateTimeOffset asOf, CancellationToken ct)
    {
        var requests = Cap((await signatureRequests.GetAllForTenantAsync(ct))
            .Where(r => r.RequestStatus is SignatureRequestStatus.Draft or SignatureRequestStatus.Pending)
            .Where(r => r.DueDate is { } due && asOf > due), input, group);

        foreach (var request in requests)
        {
            ct.ThrowIfCancellationRequested();
            group.ItemsScanned++;
            group.Report(nameof(DocumentSignatureRequest), request.Id, "ReportSignatureRequestExpired",
                DocumentGovernanceSweepItemOutcome.Reported,
                $"Signature request {request.SignatureRequestNumber} passed its due date ({request.DueDate:yyyy-MM-dd}) while still {request.RequestStatus}. Report only: FU23 defines no expiry transition, so nothing was signed, invalidated or expired.");
        }
    }

    // ── group 7: retention eligibility (FU15) ────────────────────────────────────────────────────────────
    //
    // REPORT-ONLY, and emphatically so: nothing is deleted, purged or disposed of, and no disposition request is
    // raised. The sweep reads the retention subjects the FU15 evaluator has already produced and summarises them.
    // Subjects that were never evaluated are reported as a coverage gap rather than silently evaluated here.
    private async Task SweepRetentionEligibilityAsync(
        SweepGroup group, GovernanceSweepRunInput input, DateTimeOffset asOf, CancellationToken ct)
    {
        var activeHoldIds = (await legalHolds.GetActiveAsync(ct)).Select(h => h.Id).ToHashSet();
        var subjects = Cap(await retentionSubjects.GetAllForTenantAsync(ct), input, group);

        foreach (var subject in subjects)
        {
            ct.ThrowIfCancellationRequested();
            group.ItemsScanned++;

            var blockedNow = subject.IsBlockedByLegalHold || subject.ActiveLegalHoldIds.Any(activeHoldIds.Contains);

            var (outcome, message) = (subject.EvaluationStatus, blockedNow) switch
            {
                (_, true) => (DocumentGovernanceSweepItemOutcome.Reported,
                    "Blocked by an active legal hold — retention elapse is irrelevant while the hold stands."),
                (RetentionEvaluationStatus.Eligible, _) => (DocumentGovernanceSweepItemOutcome.Reported,
                    $"Retention has elapsed (due {subject.RetentionDueDate:yyyy-MM-dd}); eligible for a disposition REQUEST only. Nothing was deleted."),
                (RetentionEvaluationStatus.MissingPolicy, _) => (DocumentGovernanceSweepItemOutcome.Warning,
                    "No active retention policy matches this subject; fail-closed as not eligible."),
                (RetentionEvaluationStatus.NotEvaluated, _) => (DocumentGovernanceSweepItemOutcome.Warning,
                    "Subject has never been evaluated; the sweep reports the coverage gap rather than evaluating it."),
                _ when subject.IsPermanentRetention => (DocumentGovernanceSweepItemOutcome.NoActionRequired,
                    "Permanently retained; never eligible for disposition."),
                _ when subject.RetentionDueDate is { } due && asOf > due => (DocumentGovernanceSweepItemOutcome.Reported,
                    $"Retention due date has passed ({due:yyyy-MM-dd}) but the stored evaluation still reads {subject.EvaluationStatus}; a re-evaluation is recommended."),
                _ => (DocumentGovernanceSweepItemOutcome.NoActionRequired, "Within its retention period.")
            };

            if (outcome == DocumentGovernanceSweepItemOutcome.NoActionRequired)
            {
                continue;
            }

            group.Report(nameof(DocumentRetentionSubject), subject.Id, "ReportRetentionEligibility", outcome, message);
        }
    }

    // ── group 8: legal hold scope freshness (FU15) ───────────────────────────────────────────────────────
    //
    // REPORT-ONLY. A hold is never released, never cancelled and never re-scoped by a sweep — release requires
    // legal approval plus GQD concurrence, which no background job can supply.
    private async Task SweepLegalHoldScopeAsync(
        SweepGroup group, GovernanceSweepRunInput input, DateTimeOffset asOf, CancellationToken ct)
    {
        var holds = Cap((await legalHolds.GetActiveAsync(ct)), input, group);

        foreach (var hold in holds)
        {
            ct.ThrowIfCancellationRequested();
            group.ItemsScanned++;

            var scopeSize = hold.RegisterEntryIds.Count + hold.ControlledDocumentIds.Count + hold.ExternalDocumentIds.Count;

            if (hold.EffectiveUntil is { } until && asOf > until)
            {
                group.Report(nameof(DocumentLegalHold), hold.Id, "ReportLegalHoldPastEffectiveUntil",
                    DocumentGovernanceSweepItemOutcome.Reported,
                    $"Hold '{hold.HoldKey}' is still Active past its EffectiveUntil ({until:yyyy-MM-dd}). Report only: release requires legal approval and GQD concurrence.");
            }

            if (scopeSize == 0 && hold.SubjectTypes.Count == 0)
            {
                group.Report(nameof(DocumentLegalHold), hold.Id, "ReportLegalHoldEmptyScope",
                    DocumentGovernanceSweepItemOutcome.Warning,
                    $"Hold '{hold.HoldKey}' is Active with an empty scope; the scope should be confirmed.");
            }
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>How far ahead of its due date a monitoring check is reported as "due soon".</summary>
    private const int MonitoringDueSoonDays = 14;

    /// <summary>
    /// A group that delegates to a pre-existing evaluator cannot honour <c>asOfDate</c> exactly: those evaluators
    /// time-stamp with UtcNow by design. <c>asOfDate</c> still governs which subjects are selected, so a past date
    /// narrows the candidate set — the caller is told the difference rather than left to guess.
    /// </summary>
    private static void WarnIfAsOfIgnored(SweepGroup group, GovernanceSweepRunInput input, string evaluatorName)
    {
        if (input.AsOfDate is not null && !input.DryRun)
        {
            group.Warnings.Add($"asOfDate governed candidate selection only: the {evaluatorName} evaluator time-stamps its own writes with the current UTC time.");
        }
    }

    private static List<T> Cap<T>(IEnumerable<T> source, GovernanceSweepRunInput input, SweepGroup group)
    {
        if (input.MaxItems is not { } max || max <= 0)
        {
            return [.. source];
        }

        var all = source.ToList();
        if (all.Count <= max)
        {
            return all;
        }

        group.Warnings.Add($"Scan was capped at maxItems={max}; {all.Count - max} further candidate(s) were not examined.");
        return [.. all.Take(max)];
    }

    private static DocumentGovernanceSweepResultItem Item(
        string subjectType, Guid subjectId, string action, DocumentGovernanceSweepItemOutcome outcome,
        string? message, Guid? findingId = null, Guid? escalationId = null) => new()
    {
        SubjectType = subjectType,
        SubjectId = subjectId,
        Action = action,
        Outcome = outcome,
        Message = message,
        RelatedFindingId = findingId,
        RelatedEscalationId = escalationId
    };

    private static GovernanceSweepResultItemModel ToItemModel(DocumentGovernanceSweepResultItem i) =>
        new(i.SubjectType, i.SubjectId, i.Action, i.Outcome, i.Message, i.RelatedFindingId, i.RelatedEscalationId);

    private static GovernanceSweepRunModel ToRunModel(
        Guid runId, string sweepKey, DocumentGovernanceSweepStatus status, bool dryRun,
        DateTimeOffset startedAt, DateTimeOffset completedAt, DateTimeOffset asOf,
        IReadOnlyList<string> groupKeys, IReadOnlyList<string> warnings, string? errorMessage,
        IReadOnlyList<SweepGroup> groups) =>
        new(runId, sweepKey, DocumentGovernanceSweepCatalog.NameOf(sweepKey), DocumentGovernanceSweepCatalog.SweepVersion,
            DocumentGovernanceSweepTriggerType.Manual, status, dryRun, startedAt, completedAt, asOf,
            groups.Sum(g => g.ItemsScanned), groups.Sum(g => g.ItemsAffected),
            groups.Sum(g => g.FindingsCreated), groups.Sum(g => g.EscalationsCreated),
            groups.Sum(g => g.ExistingFindingsSkipped), groups.Sum(g => g.ExistingEscalationsSkipped),
            groupKeys, warnings, errorMessage,
            [.. groups.Select(g => new GovernanceSweepGroupSummaryModel(
                g.SweepKey, DocumentGovernanceSweepCatalog.NameOf(g.SweepKey),
                g.ItemsScanned, g.ItemsAffected, g.EscalationsCreated, g.ExistingEscalationsSkipped,
                g.FindingsCreated, g.ExistingFindingsSkipped, g.Warnings,
                [.. g.Items.Select(ToItemModel)]))]);

    /// <summary>Per-group accumulator. Counters only; it owns no persistence and no subject.</summary>
    private sealed class SweepGroup(string sweepKey)
    {
        public string SweepKey { get; } = sweepKey;
        public int ItemsScanned { get; set; }
        public int ItemsAffected { get; set; }
        public int FindingsCreated { get; set; }
        public int EscalationsCreated { get; set; }
        public int ExistingFindingsSkipped { get; set; }
        public int ExistingEscalationsSkipped { get; set; }
        public List<string> Warnings { get; } = [];
        public List<DocumentGovernanceSweepResultItem> Items { get; } = [];

        public void Report(string subjectType, Guid subjectId, string action,
            DocumentGovernanceSweepItemOutcome outcome, string message)
        {
            ItemsAffected++;
            Items.Add(Item(subjectType, subjectId, action, outcome, message));
        }

        public void Warn(string subjectType, Guid subjectId, string action, string message)
        {
            Warnings.Add($"{subjectType} {subjectId}: {message}");
            Report(subjectType, subjectId, action, DocumentGovernanceSweepItemOutcome.Warning, message);
        }

        /// <summary>
        /// Turns the escalation-count delta around an idempotent evaluator call into created-vs-skipped evidence.
        /// A delta of zero means the evaluator suppressed a duplicate, which is the idempotency guarantee in action.
        /// </summary>
        public void RecordEscalationDelta(string subjectType, Guid subjectId, string action, int delta, string message)
        {
            ItemsAffected++;
            if (delta > 0)
            {
                EscalationsCreated += delta;
                Items.Add(Item(subjectType, subjectId, action,
                    DocumentGovernanceSweepItemOutcome.EscalationCreated, message));
            }
            else
            {
                ExistingEscalationsSkipped++;
                Items.Add(Item(subjectType, subjectId, action,
                    DocumentGovernanceSweepItemOutcome.SkippedExisting,
                    $"{message} An equivalent escalation is already open; nothing was duplicated."));
            }
        }
    }
}
