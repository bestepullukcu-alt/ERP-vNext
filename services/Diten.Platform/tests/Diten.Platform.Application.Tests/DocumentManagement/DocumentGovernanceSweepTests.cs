using System.Reflection;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementDowntime;
using Diten.Platform.Application.Features.DocumentManagementDowntime.Services;
using Diten.Platform.Application.Features.DocumentManagementElectronicSignature;
using Diten.Platform.Application.Features.DocumentManagementExternalDocuments;
using Diten.Platform.Application.Features.DocumentManagementGovernanceSweep;
using Diten.Platform.Application.Features.DocumentManagementLifecycle;
using Diten.Platform.Application.Features.DocumentManagementLifecycle.Services;
using Diten.Platform.Application.Features.DocumentManagementPeriodicReview;
using Diten.Platform.Application.Features.DocumentManagementPeriodicReview.Services;
using Diten.Platform.Application.Features.DocumentManagementQualityEvent;
using Diten.Platform.Application.Features.DocumentManagementRetention;
using Diten.Platform.Application.Features.DocumentManagementSuspension;
using Diten.Platform.Application.Features.DocumentManagementSuspension.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU32 — background governance sweep tests (GMG-QMS-SOP-0001).
///
/// The suite is organised around one claim: a sweep OBSERVES. The REAL FU08/FU12/FU13/FU20 services are wired in
/// (never mocked away), so the destructiveness assertions are made against actual behaviour rather than against a
/// stub that could not have deleted anything in the first place. Every fake repository counts its delete and update
/// calls, and the boundary tests assert those counters directly.
/// </summary>
public sealed class DocumentGovernanceSweepTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555532");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555532");
    private const string Corr = "fu32-corr-1";

    // ── 1 ── run-all writes exactly one append-only history row
    [Fact]
    public async Task Run_all_creates_sweep_run_history()
    {
        var f = Fixture();
        SeedOverduePeriodicReview(f);

        var r = await f.Service.RunAllAsync(new GovernanceSweepRunInput(), Corr);

        Assert.True(r.IsSuccessful);
        var row = Assert.Single(f.Runs.Rows);
        Assert.Equal(row.Id, r.Data!.RunId);
        Assert.Equal(TenantId, row.TenantId);
        Assert.Equal(DocumentGovernanceSweepKeys.RunAll, row.SweepKey);
        Assert.Equal(DocumentGovernanceSweepCatalog.SweepVersion, row.SweepVersion);
        Assert.Equal(DocumentGovernanceSweepTriggerType.Manual, row.TriggerType);
        Assert.False(row.DryRun);
        Assert.NotNull(row.CompletedAt);
        Assert.Equal(DocumentGovernanceSweepCatalog.RunAllGroups.Count, row.SweepKeysExecuted.Count);
    }

    // ── 2 ── the run row and every candidate read are tenant-scoped
    [Fact]
    public async Task Run_all_is_tenant_scoped()
    {
        var f = Fixture();
        var mine = SeedOverduePeriodicReview(f);
        var foreign = SeedOverduePeriodicReview(f, OtherTenantId);

        await f.Service.RunAllAsync(new GovernanceSweepRunInput(), Corr);

        var row = Assert.Single(f.Runs.Rows);
        Assert.Equal(TenantId, row.TenantId);
        Assert.Contains(row.ResultItems, i => i.SubjectId == mine.Id);
        Assert.DoesNotContain(row.ResultItems, i => i.SubjectId == foreign.Id);
    }

    // ── 3 ── an unresolved tenant is rejected before any group runs
    [Fact]
    public async Task Run_all_requires_tenant_context()
    {
        var f = Fixture(resolveTenant: false);

        var r = await f.Service.RunAllAsync(new GovernanceSweepRunInput(), Corr);

        Assert.False(r.IsSuccessful);
        Assert.Equal(400, r.StatusCode);
        Assert.Equal(GovernanceSweepReasonCodes.TenantRequired, r.ReasonCode);
        Assert.Empty(f.Runs.Rows);
    }

    // ── 4 ── no hard delete and no purge, anywhere
    [Fact]
    public async Task Run_all_does_not_hard_delete_or_purge()
    {
        var f = Fixture();
        SeedEverything(f);

        await f.Service.RunAllAsync(new GovernanceSweepRunInput(), Corr);

        Assert.Equal(0, f.TotalDeleteCalls());
        Assert.NotEmpty(f.Register.Items);
        Assert.NotEmpty(f.Capa.Items);
        Assert.NotEmpty(f.RetentionSubjects.Items);
        Assert.NotEmpty(f.SignatureRequests.Items);
        Assert.NotEmpty(f.LegalHolds.Items);
        Assert.NotEmpty(f.ExternalDocs.Items);
    }

    // ── 5 ── nothing is auto-closed, auto-approved, auto-effective, auto-disposed, auto-signed or auto-retired
    [Fact]
    public async Task Run_all_does_not_auto_close_or_auto_approve_subjects()
    {
        var f = Fixture();
        var seeded = SeedEverything(f);

        await f.Service.RunAllAsync(new GovernanceSweepRunInput(), Corr);

        // CAPA: still open, never Closed / Effective / Cancelled.
        Assert.Equal(CapaActionStatus.InProgress, f.Capa.Items.Single(x => x.Id == seeded.OverdueCapa.Id).ActionStatus);
        Assert.Equal(CapaEffectivenessResult.Pending,
            f.Capa.Items.Single(x => x.Id == seeded.EffectivenessOverdueCapa.Id).EffectivenessResult);

        // Signature: still Pending, never Signed / Expired / Cancelled — and no signature record was produced.
        Assert.Equal(SignatureRequestStatus.Pending,
            f.SignatureRequests.Items.Single(x => x.Id == seeded.ExpiredSignatureRequest.Id).RequestStatus);

        // Retention: no disposition, no eligibility flip written by the sweep.
        Assert.Equal(0, f.RetentionSubjects.UpdateCalls);

        // Legal hold: still Active, never Released.
        Assert.Equal(LegalHoldStatus.Active, f.LegalHolds.Items.Single(x => x.Id == seeded.ActiveHold.Id).HoldStatus);

        // External document: source status and lifecycle untouched.
        Assert.Equal(0, f.ExternalDocs.UpdateCalls);

        // The document itself is never retired or superseded by a sweep.
        Assert.DoesNotContain(f.Register.Items, e => e.LifecycleStatus is ControlledDocumentLifecycleStatus.Retired
            or ControlledDocumentLifecycleStatus.Superseded);
    }

    // ── 6 ── periodic review: escalation created once, then skipped as a duplicate
    [Fact]
    public async Task Periodic_review_sweep_creates_or_skips_duplicate_overdue_escalations()
    {
        var f = Fixture();
        SeedOverduePeriodicReview(f);

        var first = await f.Service.RunPeriodicReviewsAsync(new GovernanceSweepRunInput(), Corr);
        var escalationsAfterFirst = f.ReviewEscalations.Items.Count;
        var second = await f.Service.RunPeriodicReviewsAsync(new GovernanceSweepRunInput(), Corr);

        Assert.Equal(1, first.Data!.EscalationsCreated);
        Assert.Equal(1, escalationsAfterFirst);

        // Idempotency: the second run raises nothing new and says so.
        Assert.Equal(0, second.Data!.EscalationsCreated);
        Assert.Equal(1, second.Data.ExistingEscalationsSkipped);
        Assert.Single(f.ReviewEscalations.Items);
    }

    // ── 7 ── the periodic review sweep never suspends the document
    [Fact]
    public async Task Periodic_review_sweep_does_not_auto_suspend_document()
    {
        var f = Fixture();
        var entry = SeedOverduePeriodicReview(f);

        await f.Service.RunPeriodicReviewsAsync(new GovernanceSweepRunInput(), Corr);

        Assert.Equal(ControlledDocumentLifecycleStatus.Effective,
            f.Register.Items.Single(x => x.Id == entry.Id).LifecycleStatus);
        Assert.Empty(f.SuspensionCases.Items);
        Assert.Equal(0, f.Register.UpdateCalls);
    }

    // ── 8 ── external monitoring is reported, never performed
    [Fact]
    public async Task External_document_sweep_reports_due_and_overdue_monitoring_without_external_api_call()
    {
        var f = Fixture();
        var overdue = SeedExternalDocument(f, nextCheckDue: DateTimeOffset.UtcNow.AddDays(-10));
        var dueSoon = SeedExternalDocument(f, nextCheckDue: DateTimeOffset.UtcNow.AddDays(3));
        SeedExternalDocument(f, nextCheckDue: DateTimeOffset.UtcNow.AddYears(1));

        var r = await f.Service.RunExternalDocumentsAsync(new GovernanceSweepRunInput(), Corr);

        var items = r.Data!.Groups.Single().Items;
        Assert.Contains(items, i => i.SubjectId == overdue.Id && i.Action == "ReportMonitoringOverdue");
        Assert.Contains(items, i => i.SubjectId == dueSoon.Id && i.Action == "ReportMonitoringDue");
        Assert.Equal(3, r.Data.ItemsScanned);

        // No monitoring check was recorded on the owner's behalf, and no register row was touched.
        Assert.Equal(0, f.ExternalDocs.UpdateCalls);
        Assert.Equal(0, f.ExternalImpacts.UpdateCalls);
    }

    // ── 9 ── impact assessment overdue is reported without any lifecycle mutation
    [Fact]
    public async Task External_impact_sweep_reports_overdue_without_lifecycle_mutation()
    {
        var f = Fixture();
        var entry = SeedExternalDocument(f, nextCheckDue: DateTimeOffset.UtcNow.AddYears(1));
        var assessment = new ExternalDocumentImpactAssessment
        {
            Id = Guid.NewGuid(), TenantId = TenantId, ExternalDocumentRegisterEntryId = entry.Id,
            AssessmentStatus = ExternalImpactAssessmentStatus.Pending, DueDate = DateTimeOffset.UtcNow.AddDays(-5)
        };
        f.ExternalImpacts.Items.Add(assessment);

        var r = await f.Service.RunExternalDocumentsAsync(new GovernanceSweepRunInput(), Corr);

        Assert.Contains(r.Data!.Groups.Single().Items,
            i => i.SubjectId == assessment.Id && i.Action == "ReportImpactAssessmentOverdue");

        // The assessment status is NOT flipped to Overdue by the sweep — that stays an FU14 command.
        Assert.Equal(ExternalImpactAssessmentStatus.Pending, assessment.AssessmentStatus);
        Assert.Equal(0, f.ExternalImpacts.UpdateCalls);
        Assert.Equal(0, f.Register.UpdateCalls);
    }

    // ── 10 ── expired temporary instruction opens exactly one suspension case, idempotently
    [Fact]
    public async Task Temporary_instruction_sweep_creates_idempotent_suspension_case_for_expired_without_action()
    {
        var f = Fixture();
        SeedExpiredTemporaryInstruction(f);

        var first = await f.Service.RunTemporaryInstructionsAsync(new GovernanceSweepRunInput(), Corr);
        var second = await f.Service.RunTemporaryInstructionsAsync(new GovernanceSweepRunInput(), Corr);

        Assert.Equal(1, first.Data!.FindingsCreated);
        Assert.Single(f.SuspensionCases.Items);

        // The second run finds the control already Expired, so it is no longer a candidate — nothing duplicated.
        Assert.Equal(0, second.Data!.FindingsCreated);
        Assert.Single(f.SuspensionCases.Items);
    }

    // ── 11 ── opening a case is not executing a suspension
    [Fact]
    public async Task Temporary_instruction_sweep_does_not_execute_suspension()
    {
        var f = Fixture();
        var entry = SeedExpiredTemporaryInstruction(f);

        await f.Service.RunTemporaryInstructionsAsync(new GovernanceSweepRunInput(), Corr);

        var suspensionCase = Assert.Single(f.SuspensionCases.Items);
        Assert.Equal(SuspensionCaseStatus.Opened, suspensionCase.CaseStatus);
        Assert.Null(suspensionCase.ApprovedAt);
        Assert.Null(suspensionCase.ExecutedAt);

        // The document stays operationally effective: a sweep never drives the lifecycle engine.
        Assert.Equal(ControlledDocumentLifecycleStatus.Effective,
            f.Register.Items.Single(x => x.Id == entry.Id).LifecycleStatus);
        Assert.Empty(f.Transitions.Items);
    }

    // ── 12 ── downtime reconciliation overdue escalates once, then skips
    [Fact]
    public async Task Downtime_temp_issue_sweep_reports_reconciliation_overdue_and_skips_duplicates()
    {
        var f = Fixture();
        SeedOverdueTemporaryIssue(f);

        var first = await f.Service.RunDowntimeTemporaryIssuesAsync(new GovernanceSweepRunInput(), Corr);
        var escalationsAfterFirst = f.DowntimeEscalations.Items.Count;
        var second = await f.Service.RunDowntimeTemporaryIssuesAsync(new GovernanceSweepRunInput(), Corr);

        Assert.Equal(2, first.Data!.EscalationsCreated); // ReconciliationOverdue + MissingReconciliation
        Assert.Equal(2, escalationsAfterFirst);

        // The issue is now Overdue, so it drops out of the candidate set and no escalation is duplicated.
        Assert.Equal(0, second.Data!.EscalationsCreated);
        Assert.Equal(2, f.DowntimeEscalations.Items.Count);
    }

    // ── 13 ── the downtime sweep never withdraws a controlled copy or closes the issue
    [Fact]
    public async Task Downtime_temp_issue_sweep_does_not_withdraw_controlled_copy()
    {
        var f = Fixture();
        var issue = SeedOverdueTemporaryIssue(f);

        await f.Service.RunDowntimeTemporaryIssuesAsync(new GovernanceSweepRunInput(), Corr);

        Assert.Equal(0, f.Copies.UpdateCalls);
        Assert.Equal(0, f.Copies.DeleteCalls);
        Assert.NotEqual(TemporaryIssueStatus.Reconciled, f.TempIssues.Items.Single(x => x.Id == issue.Id).IssueStatus);
        Assert.NotEqual(TemporaryIssueStatus.Cancelled, f.TempIssues.Items.Single(x => x.Id == issue.Id).IssueStatus);
        Assert.Null(f.TempIssues.Items.Single(x => x.Id == issue.Id).ReconciledAt);
    }

    // ── 14 ── CAPA due and effectiveness-due are reported without closing anything
    [Fact]
    public async Task CAPA_sweep_reports_due_and_effectiveness_overdue_without_closing_CAPA()
    {
        var f = Fixture();
        var seeded = SeedEverything(f);

        var r = await f.Service.RunCapaAsync(new GovernanceSweepRunInput(), Corr);

        var items = r.Data!.Groups.Single().Items;
        Assert.Contains(items, i => i.SubjectId == seeded.OverdueCapa.Id && i.Action == "ReportCapaOverdue");
        Assert.Contains(items, i => i.SubjectId == seeded.EffectivenessOverdueCapa.Id
                                    && i.Action == "ReportCapaEffectivenessOverdue");
        Assert.Equal(0, f.Capa.UpdateCalls);
        Assert.Equal(0, r.Data.EscalationsCreated);
        Assert.DoesNotContain(f.Capa.Items, a => a.ActionStatus is CapaActionStatus.Closed or CapaActionStatus.Cancelled);
    }

    // ── 15 ── signature request expiry is report-only
    [Fact]
    public async Task Signature_request_sweep_reports_expired_without_signing_or_invalidating()
    {
        var f = Fixture();
        var seeded = SeedEverything(f);

        var r = await f.Service.RunSignatureRequestsAsync(new GovernanceSweepRunInput(), Corr);

        var item = Assert.Single(r.Data!.Groups.Single().Items);
        Assert.Equal(seeded.ExpiredSignatureRequest.Id, item.SubjectId);
        Assert.Equal(DocumentGovernanceSweepItemOutcome.Reported, item.Outcome);

        Assert.Equal(0, f.SignatureRequests.UpdateCalls);
        Assert.Equal(SignatureRequestStatus.Pending, seeded.ExpiredSignatureRequest.RequestStatus);
        Assert.Null(seeded.ExpiredSignatureRequest.SignatureRecordId);
        Assert.Null(seeded.ExpiredSignatureRequest.SignedAt);
    }

    // ── 16 ── retention eligibility reports every outcome and deletes nothing
    [Fact]
    public async Task Retention_eligibility_sweep_reports_eligible_blocked_missing_policy_permanent_without_deleting()
    {
        var f = Fixture();
        var seeded = SeedEverything(f);

        var r = await f.Service.RunRetentionEligibilityAsync(new GovernanceSweepRunInput(), Corr);

        var items = r.Data!.Groups.Single().Items;
        Assert.Contains(items, i => i.SubjectId == seeded.EligibleSubject.Id
                                    && i.Outcome == DocumentGovernanceSweepItemOutcome.Reported);
        Assert.Contains(items, i => i.SubjectId == seeded.HeldSubject.Id && i.Message!.Contains("legal hold"));
        Assert.Contains(items, i => i.SubjectId == seeded.MissingPolicySubject.Id
                                    && i.Outcome == DocumentGovernanceSweepItemOutcome.Warning);

        // A permanently retained subject is never eligible, so it produces no line at all.
        Assert.DoesNotContain(items, i => i.SubjectId == seeded.PermanentSubject.Id);

        Assert.Equal(0, f.RetentionSubjects.UpdateCalls);
        Assert.Equal(0, f.RetentionSubjects.DeleteCalls);
        Assert.Equal(4, f.RetentionSubjects.Items.Count);
    }

    // ── 17 ── the legal hold sweep never releases a hold
    [Fact]
    public async Task Legal_hold_sweep_does_not_release_hold()
    {
        var f = Fixture();
        var seeded = SeedEverything(f);

        await f.Service.RunLegalHoldScopeAsync(new GovernanceSweepRunInput(), Corr);

        var hold = f.LegalHolds.Items.Single(x => x.Id == seeded.ActiveHold.Id);
        Assert.Equal(LegalHoldStatus.Active, hold.HoldStatus);
        Assert.Null(hold.ReleasedAt);
        Assert.Equal(0, f.LegalHolds.UpdateCalls);
    }

    // ── 18 ── a dry run writes no finding, no escalation and no history row
    [Fact]
    public async Task Sweep_dry_run_writes_no_findings_or_escalations()
    {
        var f = Fixture();
        SeedEverything(f);

        var r = await f.Service.RunAllAsync(new GovernanceSweepRunInput(DryRun: true), Corr);

        Assert.True(r.IsSuccessful);
        Assert.Equal(Guid.Empty, r.Data!.RunId);
        Assert.True(r.Data.DryRun);
        Assert.Empty(f.Runs.Rows);
        Assert.Empty(f.ReviewEscalations.Items);
        Assert.Empty(f.DowntimeEscalations.Items);
        Assert.Empty(f.SuspensionCases.Items);
        Assert.Equal(0, r.Data.EscalationsCreated);
        Assert.Equal(0, r.Data.FindingsCreated);
    }

    // ── 19 ── a dry run changes no subject state at all
    [Fact]
    public async Task Sweep_dry_run_writes_no_subject_state_changes()
    {
        var f = Fixture();
        var entry = SeedOverduePeriodicReview(f);
        SeedExpiredTemporaryInstruction(f);
        var issue = SeedOverdueTemporaryIssue(f);

        await f.Service.RunAllAsync(new GovernanceSweepRunInput(DryRun: true), Corr);

        Assert.Equal(0, f.TotalUpdateCalls());
        Assert.Equal(0, f.TotalDeleteCalls());
        Assert.Equal(PeriodicReviewStatus.InProgress, f.Reviews.Items.Single(x => x.RegisterEntryId == entry.Id).ReviewStatus);
        Assert.Equal(TemporaryIssueStatus.Issued, f.TempIssues.Items.Single(x => x.Id == issue.Id).IssueStatus);
        Assert.Equal(TemporaryInstructionStatus.Active, f.TempInstructions.Items.Single().TemporaryInstructionStatus);
    }

    // ── 20 ── run detail is tenant-scoped (cross-tenant resolves to not-found, no existence leakage)
    [Fact]
    public async Task Sweep_run_detail_cross_tenant_blocked()
    {
        var f = Fixture();
        SeedOverduePeriodicReview(f);
        var run = (await f.Service.RunAllAsync(new GovernanceSweepRunInput(), Corr)).Data!;

        var other = Fixture(tenantId: OtherTenantId, sharedRuns: f.Runs.Rows);
        var r = await other.Service.GetRunAsync(run.RunId, Corr);

        Assert.False(r.IsSuccessful);
        Assert.Equal(404, r.StatusCode);
        Assert.Equal(GovernanceSweepReasonCodes.RunNotFound, r.ReasonCode);
    }

    // ── 21 ── unknown run id → 404 with the reason code
    [Fact]
    public async Task Unknown_sweep_run_returns_not_found()
    {
        var f = Fixture();

        var r = await f.Service.GetRunAsync(Guid.NewGuid(), Corr);

        Assert.False(r.IsSuccessful);
        Assert.Equal(404, r.StatusCode);
        Assert.Equal(GovernanceSweepReasonCodes.RunNotFound, r.ReasonCode);
    }

    // ── extra ── run history list is tenant-scoped and requires a tenant
    [Fact]
    public async Task Sweep_run_list_is_tenant_scoped_and_requires_tenant()
    {
        var f = Fixture();
        SeedOverduePeriodicReview(f);
        await f.Service.RunAllAsync(new GovernanceSweepRunInput(), Corr);

        var mine = await f.Service.ListRunsAsync(Corr);
        var other = await Fixture(tenantId: OtherTenantId, sharedRuns: f.Runs.Rows).Service.ListRunsAsync(Corr);
        var unresolved = await Fixture(resolveTenant: false, sharedRuns: f.Runs.Rows).Service.ListRunsAsync(Corr);

        Assert.Single(mine.Data!);
        Assert.Empty(other.Data!);
        Assert.False(unresolved.IsSuccessful);
        Assert.Equal(GovernanceSweepReasonCodes.TenantRequired, unresolved.ReasonCode);
    }

    // ── extra ── a failing group is isolated: the run still completes, with a warning
    [Fact]
    public async Task Sweep_group_failure_is_isolated_and_reported_as_completed_with_warnings()
    {
        var f = Fixture();
        SeedEverything(f);
        f.Capa.ThrowOnRead = true;

        var r = await f.Service.RunAllAsync(new GovernanceSweepRunInput(), Corr);

        Assert.True(r.IsSuccessful);
        Assert.Equal(DocumentGovernanceSweepStatus.CompletedWithWarnings, r.Data!.Status);
        Assert.Contains(r.Data.Warnings, w => w.Contains(GovernanceSweepReasonCodes.PartialFailure));
        // Every other group still produced its findings.
        Assert.Contains(r.Data.Groups, g => g.SweepKey == DocumentGovernanceSweepKeys.SignatureRequests && g.ItemsAffected > 0);
        Assert.Single(f.Runs.Rows);
    }

    // ── extra ── an unknown sweep key is ignored with a warning rather than failing the run
    [Fact]
    public async Task Unknown_sweep_key_is_reported_as_unsupported()
    {
        var f = Fixture();

        var r = await f.Service.RunAllAsync(new GovernanceSweepRunInput(SweepKeys: ["nope"]), Corr);

        Assert.True(r.IsSuccessful);
        Assert.Contains(r.Data!.Warnings, w => w.Contains(GovernanceSweepReasonCodes.Unsupported));
    }

    // ── extra ── maxItems caps the scan and says so
    [Fact]
    public async Task Sweep_max_items_caps_the_scan_and_warns()
    {
        var f = Fixture();
        SeedExternalDocument(f, nextCheckDue: DateTimeOffset.UtcNow.AddDays(-10));
        SeedExternalDocument(f, nextCheckDue: DateTimeOffset.UtcNow.AddDays(-9));

        var r = await f.Service.RunExternalDocumentsAsync(new GovernanceSweepRunInput(MaxItems: 1), Corr);

        Assert.Equal(1, r.Data!.ItemsScanned);
        Assert.Contains(r.Data.Warnings, w => w.Contains("maxItems=1"));
    }

    // ── extra ── a past asOfDate narrows the candidate set (server-validated, never client tenant data)
    [Fact]
    public async Task Sweep_as_of_date_narrows_candidate_selection()
    {
        var f = Fixture();
        SeedExternalDocument(f, nextCheckDue: DateTimeOffset.UtcNow.AddDays(-10));

        var now = await f.Service.RunExternalDocumentsAsync(new GovernanceSweepRunInput(), Corr);
        var past = await f.Service.RunExternalDocumentsAsync(
            new GovernanceSweepRunInput(AsOfDate: DateTimeOffset.UtcNow.AddDays(-30)), Corr);

        Assert.Equal(1, now.Data!.ItemsAffected);
        Assert.Equal(0, past.Data!.ItemsAffected);
    }

    // ── extra ── preview is a forced dry run
    [Fact]
    public async Task Preview_all_writes_nothing()
    {
        var f = Fixture();
        SeedEverything(f);

        var r = await f.Service.PreviewAllAsync(new GovernanceSweepRunInput(), Corr);

        Assert.True(r.Data!.DryRun);
        Assert.Empty(f.Runs.Rows);
        Assert.Equal(0, f.TotalUpdateCalls());
    }

    // ── 22/23/24 ── controller permission attribution (FU29A rules; nearest seeded keys, none invented)
    [Fact]
    public void Controller_run_all_uses_seeded_manage_permission() =>
        Assert.Equal(DocumentRetentionPermissions.RetentionManage, KeyByRoute("run-all"));

    [Theory]
    [InlineData("runs")]
    [InlineData("runs/{id:guid}")]
    [InlineData("preview")]
    public void Controller_run_history_uses_seeded_view_permission(string route) =>
        Assert.Equal(DocumentRetentionPermissions.RetentionView, KeyByRoute(route));

    [Theory]
    [InlineData("periodic-reviews/run", "platform.document-management.master-register.periodic-review.manage")]
    [InlineData("external-documents/run", "platform.document-management.external-documents.manage")]
    [InlineData("temporary-instructions/run", "platform.document-management.master-register.suspension.manage")]
    [InlineData("downtime-temporary-issues/run", "platform.document-management.downtime.manage")]
    [InlineData("quality-capa/run", "platform.document-management.capa.view")]
    [InlineData("signature-requests/run", "platform.document-management.signatures.view")]
    [InlineData("retention-eligibility/run", "platform.document-management.retention.view")]
    [InlineData("legal-hold-scope/run", "platform.document-management.legal-hold.view")]
    public void Controller_group_endpoints_use_the_nearest_seeded_domain_key(string route, string expected) =>
        Assert.Equal(expected, KeyByRoute(route));

    /// <summary>
    /// Every key the controller uses must already exist as a constant in its own feature's permission class — the
    /// FU29A rule that forbids inventing an unseeded key. A literal string here would be exactly the kind of drift
    /// this test exists to catch.
    /// </summary>
    [Fact]
    public void Controller_does_not_use_unseeded_permission_keys()
    {
        var seeded = new HashSet<string>(StringComparer.Ordinal)
        {
            DocumentRetentionPermissions.RetentionView, DocumentRetentionPermissions.RetentionManage,
            DocumentRetentionPermissions.LegalHoldView,
            DocumentPeriodicReviewPermissions.Manage,
            ExternalDocumentPermissions.Manage,
            DocumentSuspensionPermissions.Manage,
            DowntimePermissions.Manage,
            QualityEventPermissions.CapaView,
            ElectronicSignaturePermissions.SignaturesView
        };

        var used = typeof(DocumentManagementGovernanceSweepController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.GetCustomAttribute<HasPermissionAttribute>())
            .Where(a => a is not null)
            .Select(a => a!.Permission)
            .ToList();

        Assert.NotEmpty(used);
        Assert.All(used, key => Assert.Contains(key, seeded));
        // No governance-sweep-specific key was invented ahead of an FU29 seed.
        Assert.DoesNotContain(used, k => k.Contains("governance-sweep", StringComparison.Ordinal));
    }

    /// <summary>A sweep controller must expose no destructive verb — there is no DELETE and no PUT.</summary>
    [Fact]
    public void Controller_exposes_no_destructive_verb()
    {
        var verbs = typeof(DocumentManagementGovernanceSweepController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>())
            .SelectMany(a => a.HttpMethods)
            .Distinct()
            .ToList();

        Assert.DoesNotContain("DELETE", verbs);
        Assert.DoesNotContain("PUT", verbs);
        Assert.DoesNotContain("PATCH", verbs);
    }

    private static string KeyByRoute(string route)
    {
        var controller = typeof(DocumentManagementGovernanceSweepController);
        foreach (var m in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var perm = m.GetCustomAttribute<HasPermissionAttribute>();
            if (perm is null) continue;
            var template = m.GetCustomAttributes().OfType<HttpMethodAttribute>().Select(a => a.Template).FirstOrDefault();
            if (string.Equals(template, route, StringComparison.Ordinal)) return perm.Permission;
        }

        throw new InvalidOperationException($"No action with route '{route}' and a [HasPermission] attribute.");
    }

    // ── seeding ──────────────────────────────────────────────────────────────────────────────────────────

    private static DocumentMasterRegisterEntry SeedOverduePeriodicReview(Harness f, Guid? tenantId = null)
    {
        var tid = tenantId ?? TenantId;
        var entry = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(), TenantId = tid, DocumentTitle = "SOP under review",
            LifecycleStatus = ControlledDocumentLifecycleStatus.Effective,
            Criticality = DocumentCriticality.Major,
            EffectiveDate = DateTimeOffset.UtcNow.AddYears(-5),
            NextReviewDueDate = DateTimeOffset.UtcNow.AddDays(-30)
        };
        f.Register.Items.Add(entry);
        f.Reviews.Items.Add(new DocumentPeriodicReview
        {
            Id = Guid.NewGuid(), TenantId = tid, RegisterEntryId = entry.Id,
            ReviewStatus = PeriodicReviewStatus.InProgress,
            ReviewDueDate = DateTimeOffset.UtcNow.AddDays(-30),
            InitiationWindowStartDate = DateTimeOffset.UtcNow.AddDays(-90)
        });
        return entry;
    }

    private static DocumentMasterRegisterEntry SeedExpiredTemporaryInstruction(Harness f)
    {
        var entry = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DocumentTitle = "Temporary instruction",
            LifecycleStatus = ControlledDocumentLifecycleStatus.Effective,
            Criticality = DocumentCriticality.UrgentTemporary
        };
        f.Register.Items.Add(entry);
        f.TempInstructions.Items.Add(new TemporaryInstructionControl
        {
            Id = Guid.NewGuid(), TenantId = TenantId, RegisterEntryId = entry.Id,
            TemporaryInstructionStatus = TemporaryInstructionStatus.Active,
            ValidFrom = DateTimeOffset.UtcNow.AddDays(-40),
            ValidUntil = DateTimeOffset.UtcNow.AddDays(-10)
        });
        return entry;
    }

    private static DocumentTemporaryControlledIssue SeedOverdueTemporaryIssue(Harness f)
    {
        var downtime = new DocumentRepositoryDowntimeEvent
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DowntimeNumber = "DTE-0001",
            DetectionEvidenceReference = "TICKET-1",
            DowntimeStatus = DowntimeStatus.Restored,
            StartedAt = DateTimeOffset.UtcNow.AddDays(-20),
            RestoredAt = DateTimeOffset.UtcNow.AddDays(-15)
        };
        f.DowntimeEvents.Items.Add(downtime);

        var entry = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DocumentTitle = "Downtime-issued SOP",
            LifecycleStatus = ControlledDocumentLifecycleStatus.Effective
        };
        f.Register.Items.Add(entry);

        var issue = new DocumentTemporaryControlledIssue
        {
            Id = Guid.NewGuid(), TenantId = TenantId, DowntimeEventId = downtime.Id, RegisterEntryId = entry.Id,
            IssueNumber = "TCI-0001", IssueStatus = TemporaryIssueStatus.Issued,
            IssuedAt = DateTimeOffset.UtcNow.AddDays(-14),
            ReconciliationDueDate = DateTimeOffset.UtcNow.AddDays(-7)
        };
        f.TempIssues.Items.Add(issue);
        return issue;
    }

    private static ExternalDocumentRegisterEntry SeedExternalDocument(Harness f, DateTimeOffset? nextCheckDue)
    {
        var entry = new ExternalDocumentRegisterEntry
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            ExternalDocumentTitle = "EU GMP Annex 11", ExternalAuthorityName = "EMA", SourceReference = "ANNEX-11",
            ExternalDocumentStatus = ExternalDocumentStatus.Active,
            MonitoringFrequency = ExternalMonitoringFrequency.Annual,
            NextCheckDueDate = nextCheckDue
        };
        f.ExternalDocs.Items.Add(entry);
        return entry;
    }

    private sealed record SeededWorld(
        DocumentCAPAAction OverdueCapa,
        DocumentCAPAAction EffectivenessOverdueCapa,
        DocumentSignatureRequest ExpiredSignatureRequest,
        DocumentRetentionSubject EligibleSubject,
        DocumentRetentionSubject HeldSubject,
        DocumentRetentionSubject MissingPolicySubject,
        DocumentRetentionSubject PermanentSubject,
        DocumentLegalHold ActiveHold);

    /// <summary>One overdue/expired/eligible subject in every group, so a run-all exercises all eight.</summary>
    private static SeededWorld SeedEverything(Harness f)
    {
        SeedOverduePeriodicReview(f);
        SeedExpiredTemporaryInstruction(f);
        SeedOverdueTemporaryIssue(f);
        SeedExternalDocument(f, nextCheckDue: DateTimeOffset.UtcNow.AddDays(-10));

        var overdueCapa = new DocumentCAPAAction
        {
            Id = Guid.NewGuid(), TenantId = TenantId, CAPANumber = "CAPA-0001",
            ActionTitle = "Fix", ActionDescription = "Fix the thing",
            ActionStatus = CapaActionStatus.InProgress,
            DueDate = DateTimeOffset.UtcNow.AddDays(-3)
        };
        var effectivenessCapa = new DocumentCAPAAction
        {
            Id = Guid.NewGuid(), TenantId = TenantId, CAPANumber = "CAPA-0002",
            ActionTitle = "Verify", ActionDescription = "Verify the fix",
            ActionStatus = CapaActionStatus.EffectivenessPending,
            EffectivenessCheckRequired = true,
            EffectivenessResult = CapaEffectivenessResult.Pending,
            EffectivenessDueDate = DateTimeOffset.UtcNow.AddDays(-2)
        };
        f.Capa.Items.AddRange([overdueCapa, effectivenessCapa]);

        var expiredRequest = new DocumentSignatureRequest
        {
            Id = Guid.NewGuid(), TenantId = TenantId, SignatureRequestNumber = "SIG-0001",
            SubjectId = Guid.NewGuid(), RequestStatus = SignatureRequestStatus.Pending,
            DueDate = DateTimeOffset.UtcNow.AddDays(-1)
        };
        f.SignatureRequests.Items.Add(expiredRequest);

        var hold = new DocumentLegalHold
        {
            Id = Guid.NewGuid(), TenantId = TenantId, HoldKey = "HOLD-1", HoldTitle = "Litigation",
            HoldStatus = LegalHoldStatus.Active,
            RegisterEntryIds = [Guid.NewGuid()],
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-10)
        };
        f.LegalHolds.Items.Add(hold);

        var eligible = RetentionSubject(RetentionEvaluationStatus.Eligible);
        var held = RetentionSubject(RetentionEvaluationStatus.BlockedByHold, blocked: true, holdId: hold.Id);
        var missing = RetentionSubject(RetentionEvaluationStatus.MissingPolicy);
        var permanent = RetentionSubject(RetentionEvaluationStatus.Current, permanent: true);
        f.RetentionSubjects.Items.AddRange([eligible, held, missing, permanent]);

        return new SeededWorld(overdueCapa, effectivenessCapa, expiredRequest, eligible, held, missing, permanent, hold);
    }

    private static DocumentRetentionSubject RetentionSubject(
        RetentionEvaluationStatus status, bool blocked = false, bool permanent = false, Guid? holdId = null) => new()
    {
        Id = Guid.NewGuid(), TenantId = TenantId, SubjectId = Guid.NewGuid(),
        SubjectType = RetentionSubjectType.ApprovalEvidence,
        EvaluationStatus = status,
        IsBlockedByLegalHold = blocked,
        ActiveLegalHoldIds = holdId is { } h ? [h] : [],
        IsPermanentRetention = permanent,
        RetentionDueDate = DateTimeOffset.UtcNow.AddDays(-5),
        LastEvaluatedAt = DateTimeOffset.UtcNow.AddDays(-1)
    };

    // ── fixture ──────────────────────────────────────────────────────────────────────────────────────────

    private static Harness Fixture(
        bool resolveTenant = true, Guid? tenantId = null, List<DocumentGovernanceSweepRun>? sharedRuns = null)
    {
        var tenant = new TenantContext();
        if (resolveTenant)
        {
            tenant.SetTenant(tenantId ?? TenantId);
        }

        var user = new FakeUser();

        var runs = new FakeSweepRunRepo(tenant, sharedRuns);
        var register = new FakeRegisterRepo(tenant);
        var reviews = new FakeReviewRepo(tenant);
        var extensions = new FakeReviewExtensionRepo(tenant);
        var reviewEscalations = new FakeReviewEscalationRepo(tenant);
        var externalDocs = new FakeExternalDocRepo(tenant);
        var externalImpacts = new FakeExternalImpactRepo(tenant);
        var tempInstructions = new FakeTempInstructionRepo(tenant);
        var suspensionCases = new FakeSuspensionCaseRepo(tenant);
        var transitions = new FakeTransitionRepo(tenant);
        var downtimeEvents = new FakeDowntimeEventRepo(tenant);
        var tempIssues = new FakeTempIssueRepo(tenant);
        var downtimeEscalations = new FakeDowntimeEscalationRepo(tenant);
        var assessments = new FakeAssessmentRepo(tenant);
        var copies = new FakeCopyRepo(tenant);
        var capa = new FakeCapaRepo(tenant);
        var signatureRequests = new FakeSignatureRequestRepo(tenant);
        var retentionSubjects = new FakeRetentionSubjectRepo(tenant);
        var legalHolds = new FakeLegalHoldRepo(tenant);

        // The REAL FU08/FU12/FU13/FU20 services — a sweep must never re-implement their behaviour, so the tests
        // exercise the genuine ones.
        var periodicReviews = new DocumentPeriodicReviewService(register, reviews, extensions, reviewEscalations,
            new DocumentPeriodicReviewStatusEvaluator(), tenant, user, Options.Create(new DocumentPeriodicReviewOptions()));

        var lifecycle = new DocumentLifecycleService(register, transitions, tenant, user,
            Options.Create(new DocumentLifecycleOptions()));
        var suspension = new DocumentSuspensionService(register, suspensionCases, reviewEscalations, lifecycle, tenant, user);
        var temporaryInstructionService = new TemporaryInstructionService(register, tempInstructions, suspension,
            tenant, user, Options.Create(new DocumentWithdrawalOptions()));

        var downtimeService = new DocumentRepositoryDowntimeService(
            downtimeEvents, tempIssues, downtimeEscalations, assessments, tenant, user);
        var temporaryIssueService = new DocumentTemporaryIssueService(
            downtimeEvents, tempIssues, register, copies, assessments, downtimeService, tenant, user);

        var service = new DocumentGovernanceSweepService(
            runs, tenant, user, register, reviewEscalations, periodicReviews,
            externalDocs, externalImpacts, tempInstructions, suspensionCases, temporaryInstructionService,
            tempIssues, downtimeEscalations, temporaryIssueService,
            capa, signatureRequests, retentionSubjects, legalHolds);

        return new Harness(service, runs, register, reviews, reviewEscalations, externalDocs, externalImpacts,
            tempInstructions, suspensionCases, transitions, downtimeEvents, tempIssues, downtimeEscalations,
            copies, capa, signatureRequests, retentionSubjects, legalHolds);
    }

    private sealed record Harness(
        DocumentGovernanceSweepService Service,
        FakeSweepRunRepo Runs,
        FakeRegisterRepo Register,
        FakeReviewRepo Reviews,
        FakeReviewEscalationRepo ReviewEscalations,
        FakeExternalDocRepo ExternalDocs,
        FakeExternalImpactRepo ExternalImpacts,
        FakeTempInstructionRepo TempInstructions,
        FakeSuspensionCaseRepo SuspensionCases,
        FakeTransitionRepo Transitions,
        FakeDowntimeEventRepo DowntimeEvents,
        FakeTempIssueRepo TempIssues,
        FakeDowntimeEscalationRepo DowntimeEscalations,
        FakeCopyRepo Copies,
        FakeCapaRepo Capa,
        FakeSignatureRequestRepo SignatureRequests,
        FakeRetentionSubjectRepo RetentionSubjects,
        FakeLegalHoldRepo LegalHolds)
    {
        /// <summary>No fake here implements a delete; the counters exist to prove the claim, not to permit it.</summary>
        public int TotalDeleteCalls() =>
            Register.DeleteCalls + ExternalDocs.DeleteCalls + TempInstructions.DeleteCalls + TempIssues.DeleteCalls
            + Copies.DeleteCalls + Capa.DeleteCalls + SignatureRequests.DeleteCalls + RetentionSubjects.DeleteCalls
            + LegalHolds.DeleteCalls;

        public int TotalUpdateCalls() =>
            Register.UpdateCalls + ExternalDocs.UpdateCalls + ExternalImpacts.UpdateCalls
            + TempInstructions.UpdateCalls + TempIssues.UpdateCalls + Copies.UpdateCalls + Capa.UpdateCalls
            + SignatureRequests.UpdateCalls + RetentionSubjects.UpdateCalls + LegalHolds.UpdateCalls
            + Reviews.UpdateCalls;
    }

    // ── fakes ────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-1111-2222-3333-444444444432");
        public string? Email => "fu32@example.test";
        public string? DisplayName => "FU32 Tester";
        public string ActorName => "fu32@example.test";
        public bool IsAuthenticated => true;
    }

    /// <summary>
    /// Two fixtures can share one physical row list while each keeps its own tenant context — exactly the shape
    /// the cross-tenant leakage tests need.
    /// </summary>
    private sealed class FakeSweepRunRepo(ITenantContext tenant, List<DocumentGovernanceSweepRun>? shared = null)
        : IDocumentGovernanceSweepRunRepository
    {
        public List<DocumentGovernanceSweepRun> Rows { get; } = shared ?? [];
        public int UpdateCalls;

        public Task<DocumentGovernanceSweepRun> CreateAsync(DocumentGovernanceSweepRun r, CancellationToken ct = default)
        { Rows.Add(r); return Task.FromResult(r); }

        // Tenant-scoped like the real TenantRepository ExecutionFilter — a cross-tenant id resolves to null.
        public Task<DocumentGovernanceSweepRun?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(r => r.Id == id && r.TenantId == tenant.TenantId));
        public Task<IReadOnlyList<DocumentGovernanceSweepRun>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentGovernanceSweepRun>>(
                Rows.Where(r => r.TenantId == tenant.TenantId).OrderByDescending(r => r.StartedAt).ToList());
        public Task<DocumentGovernanceSweepRun?> GetLatestBySweepKeyAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(Rows.Where(r => r.TenantId == tenant.TenantId && r.SweepKey == key)
                .OrderByDescending(r => r.StartedAt).FirstOrDefault());
        public Task<bool> UpdateAsync(DocumentGovernanceSweepRun r, CancellationToken ct = default)
        { UpdateCalls++; var i = Rows.FindIndex(x => x.Id == r.Id); if (i >= 0) Rows[i] = r; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeRegisterRepo(ITenantContext tenant) : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Items { get; } = [];
        public int UpdateCalls;
        public int DeleteCalls;
        private IEnumerable<DocumentMasterRegisterEntry> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry e, CancellationToken ct = default)
        { Items.Add(e); return Task.FromResult(e); }
        public Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string uid, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.PermanentUid == uid));
        public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.DocumentCode == code));
        public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.ControlledDocumentId == id));
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry e, CancellationToken ct = default)
        { UpdateCalls++; var i = Items.FindIndex(x => x.Id == e.Id); if (i >= 0) Items[i] = e; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeReviewRepo(ITenantContext tenant) : IDocumentPeriodicReviewRepository
    {
        public List<DocumentPeriodicReview> Items { get; } = [];
        public int UpdateCalls;
        private IEnumerable<DocumentPeriodicReview> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentPeriodicReview> CreateAsync(DocumentPeriodicReview r, CancellationToken ct = default)
        { Items.Add(r); return Task.FromResult(r); }
        public Task<DocumentPeriodicReview?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentPeriodicReview>> GetByRegisterEntryAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentPeriodicReview>>(Scoped.Where(x => x.RegisterEntryId == entryId).ToList());
        public Task<DocumentPeriodicReview?> GetOpenAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.RegisterEntryId == entryId
                && x.ReviewStatus is not (PeriodicReviewStatus.Completed or PeriodicReviewStatus.Cancelled)));
        public Task<bool> UpdateAsync(DocumentPeriodicReview r, CancellationToken ct = default)
        { UpdateCalls++; var i = Items.FindIndex(x => x.Id == r.Id); if (i >= 0) Items[i] = r; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeReviewExtensionRepo(ITenantContext tenant) : IDocumentPeriodicReviewExtensionRepository
    {
        public List<DocumentPeriodicReviewExtension> Items { get; } = [];
        private IEnumerable<DocumentPeriodicReviewExtension> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentPeriodicReviewExtension> CreateAsync(DocumentPeriodicReviewExtension e, CancellationToken ct = default)
        { Items.Add(e); return Task.FromResult(e); }
        public Task<DocumentPeriodicReviewExtension?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentPeriodicReviewExtension>> GetByReviewAsync(Guid reviewId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentPeriodicReviewExtension>>(Scoped.Where(x => x.PeriodicReviewId == reviewId).ToList());
        public Task<bool> UpdateAsync(DocumentPeriodicReviewExtension e, CancellationToken ct = default)
        { var i = Items.FindIndex(x => x.Id == e.Id); if (i >= 0) Items[i] = e; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeReviewEscalationRepo(ITenantContext tenant) : IDocumentPeriodicReviewEscalationRepository
    {
        public List<DocumentPeriodicReviewEscalation> Items { get; } = [];
        private IEnumerable<DocumentPeriodicReviewEscalation> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentPeriodicReviewEscalation> CreateAsync(DocumentPeriodicReviewEscalation e, CancellationToken ct = default)
        { Items.Add(e); return Task.FromResult(e); }
        public Task<IReadOnlyList<DocumentPeriodicReviewEscalation>> GetByReviewAsync(Guid reviewId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentPeriodicReviewEscalation>>(Scoped.Where(x => x.PeriodicReviewId == reviewId).ToList());
        public Task<IReadOnlyList<DocumentPeriodicReviewEscalation>> GetByRegisterEntryAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentPeriodicReviewEscalation>>(Scoped.Where(x => x.RegisterEntryId == entryId).ToList());
    }

    private sealed class FakeExternalDocRepo(ITenantContext tenant) : IExternalDocumentRegisterRepository
    {
        public List<ExternalDocumentRegisterEntry> Items { get; } = [];
        public int UpdateCalls;
        public int DeleteCalls;
        private IEnumerable<ExternalDocumentRegisterEntry> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<ExternalDocumentRegisterEntry> CreateAsync(ExternalDocumentRegisterEntry e, CancellationToken ct = default)
        { Items.Add(e); return Task.FromResult(e); }
        public Task<ExternalDocumentRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<ExternalDocumentRegisterEntry>> ListAsync(ExternalDocumentListFilter filter, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExternalDocumentRegisterEntry>>(Scoped.ToList());
        public Task<IReadOnlyList<ExternalDocumentRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExternalDocumentRegisterEntry>>(Scoped.ToList());
        public Task<bool> UpdateAsync(ExternalDocumentRegisterEntry e, CancellationToken ct = default)
        { UpdateCalls++; var i = Items.FindIndex(x => x.Id == e.Id); if (i >= 0) Items[i] = e; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeExternalImpactRepo(ITenantContext tenant) : IExternalDocumentImpactAssessmentRepository
    {
        public List<ExternalDocumentImpactAssessment> Items { get; } = [];
        public int UpdateCalls;
        private IEnumerable<ExternalDocumentImpactAssessment> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<ExternalDocumentImpactAssessment> CreateAsync(ExternalDocumentImpactAssessment a, CancellationToken ct = default)
        { Items.Add(a); return Task.FromResult(a); }
        public Task<ExternalDocumentImpactAssessment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<ExternalDocumentImpactAssessment>> GetByExternalDocumentAsync(Guid docId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExternalDocumentImpactAssessment>>(
                Scoped.Where(x => x.ExternalDocumentRegisterEntryId == docId).ToList());
        public Task<IReadOnlyList<ExternalDocumentImpactAssessment>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ExternalDocumentImpactAssessment>>(Scoped.ToList());
        public Task<bool> UpdateAsync(ExternalDocumentImpactAssessment a, CancellationToken ct = default)
        { UpdateCalls++; var i = Items.FindIndex(x => x.Id == a.Id); if (i >= 0) Items[i] = a; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeTempInstructionRepo(ITenantContext tenant) : ITemporaryInstructionControlRepository
    {
        public List<TemporaryInstructionControl> Items { get; } = [];
        public int UpdateCalls;
        public int DeleteCalls;
        private IEnumerable<TemporaryInstructionControl> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<TemporaryInstructionControl> CreateAsync(TemporaryInstructionControl c, CancellationToken ct = default)
        { Items.Add(c); return Task.FromResult(c); }
        public Task<TemporaryInstructionControl?> GetByRegisterEntryAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.RegisterEntryId == entryId));
        public Task<IReadOnlyList<TemporaryInstructionControl>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TemporaryInstructionControl>>(Scoped.OrderBy(x => x.ValidUntil).ToList());
        public Task<bool> UpdateAsync(TemporaryInstructionControl c, CancellationToken ct = default)
        { UpdateCalls++; var i = Items.FindIndex(x => x.Id == c.Id); if (i >= 0) Items[i] = c; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeSuspensionCaseRepo(ITenantContext tenant) : IDocumentSuspensionCaseRepository
    {
        public List<DocumentSuspensionCase> Items { get; } = [];
        public int UpdateCalls;
        private IEnumerable<DocumentSuspensionCase> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentSuspensionCase> CreateAsync(DocumentSuspensionCase c, CancellationToken ct = default)
        { Items.Add(c); return Task.FromResult(c); }
        public Task<DocumentSuspensionCase?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentSuspensionCase>> GetByRegisterEntryAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSuspensionCase>>(Scoped.Where(x => x.RegisterEntryId == entryId).ToList());
        public Task<DocumentSuspensionCase?> GetOpenAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.RegisterEntryId == entryId
                && x.CaseStatus is not (SuspensionCaseStatus.Closed or SuspensionCaseStatus.Cancelled or SuspensionCaseStatus.Rejected)));
        public Task<bool> UpdateAsync(DocumentSuspensionCase c, CancellationToken ct = default)
        { UpdateCalls++; var i = Items.FindIndex(x => x.Id == c.Id); if (i >= 0) Items[i] = c; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeTransitionRepo(ITenantContext tenant) : IDocumentLifecycleTransitionRecordRepository
    {
        public List<DocumentLifecycleTransitionRecord> Items { get; } = [];

        public Task<DocumentLifecycleTransitionRecord> CreateAsync(DocumentLifecycleTransitionRecord r, CancellationToken ct = default)
        { Items.Add(r); return Task.FromResult(r); }
        public Task<IReadOnlyList<DocumentLifecycleTransitionRecord>> GetByRegisterEntryAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLifecycleTransitionRecord>>(
                Items.Where(x => x.TenantId == tenant.TenantId && x.RegisterEntryId == entryId).ToList());
        public Task<IReadOnlyList<DocumentLifecycleTransitionRecord>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLifecycleTransitionRecord>>(
                Items.Where(x => x.TenantId == tenant.TenantId).ToList());
    }

    private sealed class FakeDowntimeEventRepo(ITenantContext tenant) : IDocumentRepositoryDowntimeEventRepository
    {
        public List<DocumentRepositoryDowntimeEvent> Items { get; } = [];
        public int UpdateCalls;
        private IEnumerable<DocumentRepositoryDowntimeEvent> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentRepositoryDowntimeEvent> CreateAsync(DocumentRepositoryDowntimeEvent e, CancellationToken ct = default)
        { Items.Add(e); return Task.FromResult(e); }
        public Task<DocumentRepositoryDowntimeEvent?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentRepositoryDowntimeEvent>> GetByStatusAsync(DowntimeStatus status, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRepositoryDowntimeEvent>>(Scoped.Where(x => x.DowntimeStatus == status).ToList());
        public Task<IReadOnlyList<DocumentRepositoryDowntimeEvent>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRepositoryDowntimeEvent>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentRepositoryDowntimeEvent e, CancellationToken ct = default)
        { UpdateCalls++; var i = Items.FindIndex(x => x.Id == e.Id); if (i >= 0) Items[i] = e; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeTempIssueRepo(ITenantContext tenant) : IDocumentTemporaryControlledIssueRepository
    {
        public List<DocumentTemporaryControlledIssue> Items { get; } = [];
        public int UpdateCalls;
        public int DeleteCalls;
        private IEnumerable<DocumentTemporaryControlledIssue> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentTemporaryControlledIssue> CreateAsync(DocumentTemporaryControlledIssue i, CancellationToken ct = default)
        { Items.Add(i); return Task.FromResult(i); }
        public Task<DocumentTemporaryControlledIssue?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetByDowntimeEventAsync(Guid eventId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTemporaryControlledIssue>>(Scoped.Where(x => x.DowntimeEventId == eventId).ToList());
        public Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetByRegisterEntryAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTemporaryControlledIssue>>(Scoped.Where(x => x.RegisterEntryId == entryId).ToList());
        public Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetOutstandingAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTemporaryControlledIssue>>(Scoped.Where(x => !x.IsSettled()).ToList());
        public Task<bool> UpdateAsync(DocumentTemporaryControlledIssue i, CancellationToken ct = default)
        { UpdateCalls++; var idx = Items.FindIndex(x => x.Id == i.Id); if (idx >= 0) Items[idx] = i; return Task.FromResult(idx >= 0); }
    }

    private sealed class FakeDowntimeEscalationRepo(ITenantContext tenant) : IDocumentDowntimeEscalationRepository
    {
        public List<DocumentDowntimeEscalation> Items { get; } = [];
        private IEnumerable<DocumentDowntimeEscalation> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentDowntimeEscalation> CreateAsync(DocumentDowntimeEscalation e, CancellationToken ct = default)
        { Items.Add(e); return Task.FromResult(e); }
        public Task<IReadOnlyList<DocumentDowntimeEscalation>> GetByDowntimeEventAsync(Guid eventId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentDowntimeEscalation>>(Scoped.Where(x => x.DowntimeEventId == eventId).ToList());
        public Task<bool> UpdateAsync(DocumentDowntimeEscalation e, CancellationToken ct = default)
        { var i = Items.FindIndex(x => x.Id == e.Id); if (i >= 0) Items[i] = e; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeAssessmentRepo(ITenantContext tenant) : IDocumentRepositoryAssessmentRepository
    {
        public List<DocumentRepositoryAssessment> Items { get; } = [];
        private IEnumerable<DocumentRepositoryAssessment> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentRepositoryAssessment> CreateAsync(DocumentRepositoryAssessment a, CancellationToken ct = default)
        { Items.Add(a); return Task.FromResult(a); }
        public Task<DocumentRepositoryAssessment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentRepositoryAssessment>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRepositoryAssessment>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentRepositoryAssessment a, CancellationToken ct = default)
        { var i = Items.FindIndex(x => x.Id == a.Id); if (i >= 0) Items[i] = a; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeCopyRepo(ITenantContext tenant) : IDocumentControlledCopyRepository
    {
        public List<DocumentControlledCopy> Items { get; } = [];
        public int UpdateCalls;
        public int DeleteCalls;
        private IEnumerable<DocumentControlledCopy> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentControlledCopy> CreateAsync(DocumentControlledCopy c, CancellationToken ct = default)
        { Items.Add(c); return Task.FromResult(c); }
        public Task<DocumentControlledCopy?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentControlledCopy>> GetByRegisterEntryAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentControlledCopy>>(Scoped.Where(x => x.RegisterEntryId == entryId).ToList());
        public Task<DocumentControlledCopy?> GetByCopyNumberAsync(Guid entryId, int number, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.RegisterEntryId == entryId && x.CopyNumber == number));
        public Task<bool> UpdateAsync(DocumentControlledCopy c, CancellationToken ct = default)
        { UpdateCalls++; var i = Items.FindIndex(x => x.Id == c.Id); if (i >= 0) Items[i] = c; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeCapaRepo(ITenantContext tenant) : IDocumentCAPAActionRepository
    {
        public List<DocumentCAPAAction> Items { get; } = [];
        public int UpdateCalls;
        public int DeleteCalls;

        /// <summary>Forces the group-isolation path so a failing group can be proven not to abort the run.</summary>
        public bool ThrowOnRead { get; set; }

        private IEnumerable<DocumentCAPAAction> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentCAPAAction> CreateAsync(DocumentCAPAAction a, CancellationToken ct = default)
        { Items.Add(a); return Task.FromResult(a); }
        public Task<DocumentCAPAAction?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentCAPAAction>> GetByQualityEventAsync(Guid eventId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentCAPAAction>>(Scoped.Where(x => x.QualityEventId == eventId).ToList());
        public Task<IReadOnlyList<DocumentCAPAAction>> GetByDeviationAsync(Guid deviationId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentCAPAAction>>(Scoped.Where(x => x.DeviationId == deviationId).ToList());
        public Task<IReadOnlyList<DocumentCAPAAction>> GetAllForTenantAsync(CancellationToken ct = default) =>
            ThrowOnRead
                ? throw new InvalidOperationException("simulated CAPA store outage")
                : Task.FromResult<IReadOnlyList<DocumentCAPAAction>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentCAPAAction a, CancellationToken ct = default)
        { UpdateCalls++; var i = Items.FindIndex(x => x.Id == a.Id); if (i >= 0) Items[i] = a; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeSignatureRequestRepo(ITenantContext tenant) : IDocumentSignatureRequestRepository
    {
        public List<DocumentSignatureRequest> Items { get; } = [];
        public int UpdateCalls;
        public int DeleteCalls;
        private IEnumerable<DocumentSignatureRequest> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentSignatureRequest> CreateAsync(DocumentSignatureRequest r, CancellationToken ct = default)
        { Items.Add(r); return Task.FromResult(r); }
        public Task<DocumentSignatureRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentSignatureRequest>> GetBySubjectAsync(
            SignableSubjectType type, Guid subjectId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSignatureRequest>>(
                Scoped.Where(x => x.SubjectType == type && x.SubjectId == subjectId).ToList());
        public Task<IReadOnlyList<DocumentSignatureRequest>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSignatureRequest>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentSignatureRequest r, CancellationToken ct = default)
        { UpdateCalls++; var i = Items.FindIndex(x => x.Id == r.Id); if (i >= 0) Items[i] = r; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeRetentionSubjectRepo(ITenantContext tenant) : IDocumentRetentionSubjectRepository
    {
        public List<DocumentRetentionSubject> Items { get; } = [];
        public int UpdateCalls;
        public int DeleteCalls;
        private IEnumerable<DocumentRetentionSubject> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentRetentionSubject> CreateAsync(DocumentRetentionSubject s, CancellationToken ct = default)
        { Items.Add(s); return Task.FromResult(s); }
        public Task<DocumentRetentionSubject?> GetBySubjectAsync(RetentionSubjectType type, Guid subjectId, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.SubjectType == type && x.SubjectId == subjectId));
        public Task<IReadOnlyList<DocumentRetentionSubject>> GetByRegisterEntryAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRetentionSubject>>(Scoped.Where(x => x.RegisterEntryId == entryId).ToList());
        public Task<IReadOnlyList<DocumentRetentionSubject>> GetEligibleAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRetentionSubject>>(Scoped.Where(x => x.IsDispositionEligible).ToList());
        public Task<IReadOnlyList<DocumentRetentionSubject>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRetentionSubject>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentRetentionSubject s, CancellationToken ct = default)
        { UpdateCalls++; var i = Items.FindIndex(x => x.Id == s.Id); if (i >= 0) Items[i] = s; return Task.FromResult(i >= 0); }
    }

    private sealed class FakeLegalHoldRepo(ITenantContext tenant) : IDocumentLegalHoldRepository
    {
        public List<DocumentLegalHold> Items { get; } = [];
        public int UpdateCalls;
        public int DeleteCalls;
        private IEnumerable<DocumentLegalHold> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);

        public Task<DocumentLegalHold> CreateAsync(DocumentLegalHold h, CancellationToken ct = default)
        { Items.Add(h); return Task.FromResult(h); }
        public Task<DocumentLegalHold?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentLegalHold>> GetActiveAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLegalHold>>(Scoped.Where(x => x.HoldStatus == LegalHoldStatus.Active).ToList());
        public Task<IReadOnlyList<DocumentLegalHold>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentLegalHold>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentLegalHold h, CancellationToken ct = default)
        { UpdateCalls++; var i = Items.FindIndex(x => x.Id == h.Id); if (i >= 0) Items[i] = h; return Task.FromResult(i >= 0); }
    }
}
