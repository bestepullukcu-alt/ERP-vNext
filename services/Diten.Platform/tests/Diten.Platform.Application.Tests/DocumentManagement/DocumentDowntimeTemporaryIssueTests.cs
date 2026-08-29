using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.DocumentManagementDowntime;
using Diten.Platform.Application.Features.DocumentManagementDowntime.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.DocumentManagement;

/// <summary>
/// MOD-0029-FU20 — repository downtime / temporary controlled issue tests (GMG-QMS-SOP-0001 §11.3). Tenant-aware
/// in-memory fakes exercise the downtime log, the outside-normal-environment approval, FU17 copy creation, the
/// 3-working-day reconciliation window, the 2-working-day escalation threshold and the closure guards.
///
/// The guard assertions matter most: a temporary controlled issue must never be able to slip into being an
/// uncontrolled copy, which is why closure is blocked while any issue is unsettled and why an unapproved
/// repository cannot be a source at all.
/// </summary>
public sealed class DocumentDowntimeTemporaryIssueTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private const string Corr = "fu20-corr-1";

    // Fixed anchors so working-day arithmetic is deterministic.
    private static readonly DateTimeOffset Monday = new(2026, 7, 6, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Wednesday = new(2026, 7, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Thursday = new(2026, 7, 9, 9, 0, 0, TimeSpan.Zero);

    // ── downtime event ────────────────────────────────────────────────────────

    [Fact]
    public async Task Open_downtime_requires_detection_evidence()
    {
        var f = Fixture();

        var r = await f.Downtime.OpenAsync(OpenInput() with { DetectionEvidenceReference = "  " }, Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(DowntimeReasonCodes.DetectionEvidenceRequired, r.ReasonCode);
        Assert.Empty(f.Events.Items);
    }

    [Fact]
    public async Task Open_downtime_rejects_a_future_start_time()
    {
        var f = Fixture();

        var r = await f.Downtime.OpenAsync(OpenInput() with { StartedAt = DateTimeOffset.UtcNow.AddDays(1) }, Corr, CancellationToken.None);

        Assert.Equal(DowntimeReasonCodes.StartedAtInFuture, r.ReasonCode);
    }

    [Fact]
    public async Task Open_downtime_with_repository_assessment_link()
    {
        var f = Fixture();
        var assessment = SeedAssessment(f, RepositoryType.ApprovedInterimRepository);

        var r = await f.Downtime.OpenAsync(OpenInput() with { RepositoryAssessmentId = assessment.Id }, Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(201, r.StatusCode);
        Assert.Equal("Open", r.Data!.DowntimeStatus);
        Assert.Equal(assessment.Id, r.Data.RepositoryAssessmentId);
        Assert.Equal(assessment.RepositoryName, r.Data.RepositoryName);
        Assert.StartsWith("DTE-", r.Data.DowntimeNumber);
    }

    [Fact]
    public async Task Open_downtime_with_a_foreign_repository_assessment_is_blocked()
    {
        var f = Fixture();
        var foreign = new DocumentRepositoryAssessment
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, RepositoryKey = "FOREIGN", RepositoryName = "Foreign DMS"
        };
        f.Assessments.Items.Add(foreign);

        var r = await f.Downtime.OpenAsync(OpenInput() with { RepositoryAssessmentId = foreign.Id }, Corr, CancellationToken.None);

        Assert.Equal(404, r.StatusCode);
        Assert.Empty(f.Events.Items);
    }

    [Fact]
    public async Task Mark_restored_requires_restore_evidence()
    {
        var f = Fixture();
        var e = await OpenEventAsync(f);

        var noEvidence = await f.Downtime.MarkRestoredAsync(e, new MarkRepositoryRestoredInput("", null), Corr, CancellationToken.None);
        Assert.Equal(DowntimeReasonCodes.RestoreEvidenceRequired, noEvidence.ReasonCode);

        var ok = await f.Downtime.MarkRestoredAsync(e, new MarkRepositoryRestoredInput("RESTORE-1", Wednesday), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.Equal("Restored", ok.Data!.DowntimeStatus);
        Assert.Equal(Wednesday, ok.Data.RestoredAt);
    }

    [Fact]
    public async Task Mark_restored_twice_is_refused()
    {
        var f = Fixture();
        var e = await OpenEventAsync(f);
        await f.Downtime.MarkRestoredAsync(e, new MarkRepositoryRestoredInput("RESTORE-1", Wednesday), Corr, CancellationToken.None);

        var again = await f.Downtime.MarkRestoredAsync(e, new MarkRepositoryRestoredInput("RESTORE-2", Thursday), Corr, CancellationToken.None);

        Assert.Equal(DowntimeReasonCodes.DowntimeAlreadyRestored, again.ReasonCode);
    }

    // ── 2 working day escalation ──────────────────────────────────────────────

    [Fact]
    public async Task Downtime_over_two_working_days_creates_GQD_and_ITCSV_escalations()
    {
        var f = Fixture();
        var e = await OpenEventAsync(f);
        // Mon → Thu = 3 working days, past the 2-working-day threshold.
        await f.Downtime.MarkRestoredAsync(e, new MarkRepositoryRestoredInput("RESTORE-1", Thursday), Corr, CancellationToken.None);

        var r = await f.Downtime.EvaluateEscalationAsync(e, Corr, CancellationToken.None);

        Assert.Equal(3, r.Data!.DurationWorkingDays);
        Assert.True(r.Data.ExceedsTwoWorkingDays);
        Assert.Contains(r.Data.Escalations, x => x.EscalationType == nameof(DowntimeEscalationType.DowntimeExceedsTwoWorkingDays)
                                                 && x.RequiredRole == nameof(DowntimeEscalationRole.GQD));
        Assert.Contains(r.Data.Escalations, x => x.EscalationType == nameof(DowntimeEscalationType.BcpAssessmentRequired)
                                                 && x.RequiredRole == nameof(DowntimeEscalationRole.ITCSVOwner));
        Assert.True(f.Events.Items.Single().RequiresGqdItCsvEscalation);
    }

    [Fact]
    public async Task Downtime_within_two_working_days_creates_no_escalation()
    {
        var f = Fixture();
        var e = await OpenEventAsync(f);
        // Mon → Wed = 2 working days, exactly at the threshold and therefore NOT exceeding it.
        await f.Downtime.MarkRestoredAsync(e, new MarkRepositoryRestoredInput("RESTORE-1", Wednesday), Corr, CancellationToken.None);

        var r = await f.Downtime.EvaluateEscalationAsync(e, Corr, CancellationToken.None);

        Assert.Equal(2, r.Data!.DurationWorkingDays);
        Assert.False(r.Data.ExceedsTwoWorkingDays);
        Assert.Empty(r.Data.Escalations);
    }

    [Fact]
    public async Task Escalation_evaluation_is_idempotent()
    {
        var f = Fixture();
        var e = await OpenEventAsync(f);
        await f.Downtime.MarkRestoredAsync(e, new MarkRepositoryRestoredInput("RESTORE-1", Thursday), Corr, CancellationToken.None);

        await f.Downtime.EvaluateEscalationAsync(e, Corr, CancellationToken.None);
        var second = await f.Downtime.EvaluateEscalationAsync(e, Corr, CancellationToken.None);

        Assert.Equal(2, second.Data!.Escalations.Count);
        Assert.Equal(2, f.Escalations.Items.Count);
    }

    [Fact]
    public async Task Downtime_close_requires_BCP_assessment_when_threshold_exceeded()
    {
        var f = Fixture();
        var e = await OpenEventAsync(f);
        await f.Downtime.MarkRestoredAsync(e, new MarkRepositoryRestoredInput("RESTORE-1", Thursday), Corr, CancellationToken.None);

        var blocked = await f.Downtime.CloseAsync(e, new CloseDowntimeEventInput(null, null), Corr, CancellationToken.None);
        Assert.Equal(DowntimeReasonCodes.BcpAssessmentRequired, blocked.ReasonCode);

        var ok = await f.Downtime.CloseAsync(e, new CloseDowntimeEventInput("BCP-2026-01", "Restored and assessed"), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.Equal("Closed", ok.Data!.DowntimeStatus);
        Assert.Equal("BCP-2026-01", ok.Data.BcpAssessmentReference);
    }

    [Fact]
    public async Task Short_downtime_closes_without_a_BCP_assessment()
    {
        var f = Fixture();
        var e = await OpenEventAsync(f);
        await f.Downtime.MarkRestoredAsync(e, new MarkRepositoryRestoredInput("RESTORE-1", Wednesday), Corr, CancellationToken.None);

        var r = await f.Downtime.CloseAsync(e, new CloseDowntimeEventInput(null, null), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Closed", r.Data!.DowntimeStatus);
    }

    [Fact]
    public async Task Downtime_close_blocks_when_a_temporary_issue_is_not_reconciled()
    {
        var f = Fixture();
        var (e, issue) = await IssuedCopiesAsync(f);

        var r = await f.Downtime.CloseAsync(e, new CloseDowntimeEventInput(null, null), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(DowntimeReasonCodes.UnsettledIssuesBlockClose, r.ReasonCode);
        Assert.NotEqual(TemporaryIssueStatus.Reconciled, f.Issues.Items.Single(x => x.Id == issue).IssueStatus);
    }

    /// <summary>The happy path: a same-day outage, copies issued, reconciled on time, event closed.</summary>
    [Fact]
    public async Task Close_downtime_after_all_issues_reconciled_succeeds()
    {
        var f = Fixture();
        // A short, current outage — so no 2-working-day escalation and no BCP assessment is due.
        var open = await f.Downtime.OpenAsync(
            OpenInput() with { StartedAt = DateTimeOffset.UtcNow.AddHours(-2) }, Corr, CancellationToken.None);
        var e = open.Data!.Id;

        var issue = await ApprovedIssueOnEventAsync(f, e);
        await f.Issue.IssueCopiesAsync(e, issue, IssueCopies(), Corr, CancellationToken.None);
        await f.Downtime.MarkRestoredAsync(e, new MarkRepositoryRestoredInput("RESTORE-1", null), Corr, CancellationToken.None);
        await f.Issue.ReconcileAsync(e, issue, Reconcile(), Corr, CancellationToken.None);

        var r = await f.Downtime.CloseAsync(e, new CloseDowntimeEventInput(null, "All copies reconciled"), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal("Closed", r.Data!.DowntimeStatus);
        Assert.Equal(0, r.Data.DurationWorkingDays);
    }

    // ── temporary issue request / approval ────────────────────────────────────

    [Fact]
    public async Task Request_temporary_issue_requires_an_operational_document()
    {
        var f = Fixture();
        var e = await OpenEventAsync(f);
        var entry = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective);
        var draft = SeedEntry(f, ControlledDocumentLifecycleStatus.Draft);

        var ok = await f.Issue.RequestAsync(e, Request(entry.Id), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.Equal("Requested", ok.Data!.IssueStatus);

        var rejected = await f.Issue.RequestAsync(e, Request(draft.Id), Corr, CancellationToken.None);
        Assert.Equal(DowntimeReasonCodes.DocumentNotOperational, rejected.ReasonCode);
    }

    [Fact]
    public async Task Under_revision_document_can_still_be_temporarily_issued()
    {
        var f = Fixture();
        var e = await OpenEventAsync(f);
        var entry = SeedEntry(f, ControlledDocumentLifecycleStatus.UnderRevision);

        var r = await f.Issue.RequestAsync(e, Request(entry.Id), Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
    }

    [Theory]
    [InlineData(ControlledDocumentLifecycleStatus.Suspended)]
    [InlineData(ControlledDocumentLifecycleStatus.Retired)]
    [InlineData(ControlledDocumentLifecycleStatus.Superseded)]
    public async Task Temporary_issue_is_blocked_for_non_operational_documents(ControlledDocumentLifecycleStatus status)
    {
        var f = Fixture();
        var e = await OpenEventAsync(f);
        var entry = SeedEntry(f, status);

        var r = await f.Issue.RequestAsync(e, Request(entry.Id), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(DowntimeReasonCodes.DocumentNotOperational, r.ReasonCode);
        Assert.Empty(f.Issues.Items);
    }

    [Fact]
    public async Task Temporary_issue_cannot_be_raised_on_a_closed_downtime_event()
    {
        var f = Fixture();
        var e = await OpenEventAsync(f);
        var entry = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective);
        await f.Downtime.MarkRestoredAsync(e, new MarkRepositoryRestoredInput("RESTORE-1", Wednesday), Corr, CancellationToken.None);
        await f.Downtime.CloseAsync(e, new CloseDowntimeEventInput(null, null), Corr, CancellationToken.None);

        var r = await f.Issue.RequestAsync(e, Request(entry.Id), Corr, CancellationToken.None);

        Assert.Equal(DowntimeReasonCodes.DowntimeNotAcceptingIssues, r.ReasonCode);
    }

    [Fact]
    public async Task Temporary_issue_approval_requires_mechanism_and_evidence()
    {
        var f = Fixture();
        var (e, issue) = await RequestedIssueAsync(f);

        var noMechanism = await f.Issue.ApproveAsync(e, issue, Approve() with { ApprovalMechanism = "Telepathy" }, Corr, CancellationToken.None);
        Assert.Equal(DowntimeReasonCodes.ApprovalMechanismRequired, noMechanism.ReasonCode);

        var noEvidence = await f.Issue.ApproveAsync(e, issue, Approve() with { ApprovalEvidenceReference = " " }, Corr, CancellationToken.None);
        Assert.Equal(DowntimeReasonCodes.ApprovalEvidenceRequired, noEvidence.ReasonCode);

        var ok = await f.Issue.ApproveAsync(e, issue, Approve(), Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.Equal("Approved", ok.Data!.IssueStatus);
        Assert.Equal("WetSignature", ok.Data.ApprovalMechanism);
    }

    [Fact]
    public async Task Temporary_issue_approval_rejects_a_role_outside_the_permitted_set()
    {
        var f = Fixture();
        var (e, issue) = await RequestedIssueAsync(f);

        var r = await f.Issue.ApproveAsync(e, issue, Approve() with { ApprovedByRole = "DocumentOwner" }, Corr, CancellationToken.None);

        Assert.Equal(DowntimeReasonCodes.ApproverRoleInvalid, r.ReasonCode);
    }

    // ── issuing copies ────────────────────────────────────────────────────────

    [Fact]
    public async Task Issue_copy_requires_approval()
    {
        var f = Fixture();
        var (e, issue) = await RequestedIssueAsync(f);

        var r = await f.Issue.IssueCopiesAsync(e, issue, IssueCopies(), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(DowntimeReasonCodes.IssueNotApproved, r.ReasonCode);
        Assert.Empty(f.Copies.Items);
    }

    [Fact]
    public async Task Issue_copy_validates_count_and_location()
    {
        var f = Fixture();
        var (e, issue) = await ApprovedIssueAsync(f);

        var zeroCount = await f.Issue.IssueCopiesAsync(e, issue, IssueCopies() with { IssuedCopyCount = 0 }, Corr, CancellationToken.None);
        Assert.Equal(DowntimeReasonCodes.CopyCountInvalid, zeroCount.ReasonCode);

        var noLocation = await f.Issue.IssueCopiesAsync(e, issue, IssueCopies() with { TemporaryLocationDescription = " " }, Corr, CancellationToken.None);
        Assert.Equal(DowntimeReasonCodes.TemporaryLocationRequired, noLocation.ReasonCode);
    }

    [Fact]
    public async Task Issue_copy_creates_temporary_controlled_copies_in_the_FU17_register()
    {
        var f = Fixture();
        var (e, issue) = await ApprovedIssueAsync(f);

        var r = await f.Issue.IssueCopiesAsync(e, issue, IssueCopies() with { IssuedCopyCount = 3 }, Corr, CancellationToken.None);

        Assert.True(r.IsSuccessful);
        Assert.Equal(3, r.Data!.IssuedCopyCount);
        Assert.Equal(3, r.Data.RelatedControlledCopyIds.Count);

        // Every copy is a real FU17 controlled copy of the temporary type — never an untracked handout.
        Assert.Equal(3, f.Copies.Items.Count);
        Assert.All(f.Copies.Items, c =>
        {
            Assert.Equal(ControlledCopyType.TemporaryControlledIssue, c.CopyType);
            Assert.Equal(ControlledCopyStatus.Active, c.CopyStatus);
            Assert.Equal("Shift office, Building B", c.LocationDescription);
        });
        // Copy numbers continue the register entry's own sequence.
        Assert.Equal(new[] { 1, 2, 3 }, f.Copies.Items.Select(c => c.CopyNumber).OrderBy(n => n).ToArray());
    }

    [Fact]
    public async Task Issue_copy_numbering_continues_an_existing_copy_sequence()
    {
        var f = Fixture();
        var (e, issue) = await ApprovedIssueAsync(f);
        var entryId = f.Issues.Items.Single().RegisterEntryId;
        f.Copies.Items.Add(new DocumentControlledCopy
        {
            Id = Guid.NewGuid(), TenantId = TenantId, RegisterEntryId = entryId, CopyNumber = 7,
            CopyType = ControlledCopyType.PrintedControlledCopy
        });

        await f.Issue.IssueCopiesAsync(e, issue, IssueCopies() with { IssuedCopyCount = 2 }, Corr, CancellationToken.None);

        var temporary = f.Copies.Items.Where(c => c.CopyType == ControlledCopyType.TemporaryControlledIssue).ToList();
        Assert.Equal(new[] { 8, 9 }, temporary.Select(c => c.CopyNumber).OrderBy(n => n).ToArray());
    }

    // ── 3 working day reconciliation ──────────────────────────────────────────

    [Fact]
    public async Task Reconciliation_due_date_runs_from_issued_at_when_already_restored()
    {
        var f = Fixture();
        var e = await OpenEventAsync(f);
        // Restore happens BEFORE the issue, so the clock starts at issue time.
        await f.Downtime.MarkRestoredAsync(e, new MarkRepositoryRestoredInput("RESTORE-1", Monday), Corr, CancellationToken.None);
        var issue = await ApprovedIssueOnEventAsync(f, e);

        await f.Issue.IssueCopiesAsync(e, issue, IssueCopies(), Corr, CancellationToken.None);

        var stored = f.Issues.Items.Single(x => x.Id == issue);
        Assert.Equal(DowntimeScheduleCalculator.AddWorkingDays(stored.IssuedAt!.Value, 3), stored.ReconciliationDueDate);
        Assert.Equal(TemporaryIssueStatus.ReconciliationDue, stored.IssueStatus);
    }

    [Fact]
    public async Task Reconciliation_due_date_rebases_to_restored_at_when_restored_after_issue()
    {
        var f = Fixture();
        var (e, issue) = await IssuedCopiesAsync(f);
        var beforeRestore = f.Issues.Items.Single(x => x.Id == issue).ReconciliationDueDate;

        // The repository comes back later; reconciliation into the normal system only becomes possible now.
        var restoredAt = DateTimeOffset.UtcNow.AddDays(2);
        await f.Downtime.MarkRestoredAsync(e, new MarkRepositoryRestoredInput("RESTORE-1", restoredAt), Corr, CancellationToken.None);

        var stored = f.Issues.Items.Single(x => x.Id == issue);
        Assert.Equal(DowntimeScheduleCalculator.AddWorkingDays(restoredAt, 3), stored.ReconciliationDueDate);
        Assert.NotEqual(beforeRestore, stored.ReconciliationDueDate);
        Assert.Equal(TemporaryIssueStatus.ReconciliationDue, stored.IssueStatus);
    }

    [Fact]
    public void Working_day_arithmetic_skips_weekends()
    {
        // Friday + 3 working days = Wednesday.
        var friday = new DateTimeOffset(2026, 7, 10, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero),
            DowntimeScheduleCalculator.AddWorkingDays(friday, 3));

        // A weekend contributes no working days.
        var saturday = new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero);
        var sunday = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(0, DowntimeScheduleCalculator.CountWorkingDays(saturday, sunday));
    }

    [Fact]
    public async Task Reconcile_requires_evidence()
    {
        var f = Fixture();
        var (e, issue) = await IssuedCopiesAsync(f);

        var r = await f.Issue.ReconcileAsync(e, issue, Reconcile() with { ReconciliationEvidenceReference = "" }, Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(DowntimeReasonCodes.ReconciliationEvidenceRequired, r.ReasonCode);
    }

    [Fact]
    public async Task Late_reconciliation_requires_a_deviation_reference()
    {
        var f = Fixture();
        var (e, issue) = await IssuedCopiesAsync(f);
        // Wind the due date into the past.
        f.Issues.Items.Single(x => x.Id == issue).ReconciliationDueDate = DateTimeOffset.UtcNow.AddDays(-1);

        var blocked = await f.Issue.ReconcileAsync(e, issue, Reconcile(), Corr, CancellationToken.None);
        Assert.Equal(DowntimeReasonCodes.DeviationReferenceRequired, blocked.ReasonCode);

        var ok = await f.Issue.ReconcileAsync(e, issue,
            Reconcile() with { DeviationReference = "DEV-2026-014" }, Corr, CancellationToken.None);
        Assert.True(ok.IsSuccessful);
        Assert.Equal("Reconciled", ok.Data!.IssueStatus);
        Assert.Equal("DEV-2026-014", ok.Data.DeviationReference);
    }

    [Fact]
    public async Task Reconcile_marks_related_temporary_copies_reconciled()
    {
        var f = Fixture();
        var (e, issue) = await IssuedCopiesAsync(f, copyCount: 2);

        await f.Issue.ReconcileAsync(e, issue, Reconcile(), Corr, CancellationToken.None);

        Assert.All(f.Copies.Items, c =>
        {
            Assert.Equal(ControlledCopyStatus.Reconciled, c.CopyStatus);
            Assert.Equal("RECON-1", c.ReconciliationEvidenceReference);
            Assert.NotNull(c.ReconciledAt);
        });
    }

    [Fact]
    public async Task Reconcile_can_withdraw_the_copies_instead()
    {
        var f = Fixture();
        var (e, issue) = await IssuedCopiesAsync(f, copyCount: 2);

        await f.Issue.ReconcileAsync(e, issue, Reconcile() with { WithdrawCopiesInsteadOfReconcile = true }, Corr, CancellationToken.None);

        Assert.All(f.Copies.Items, c =>
        {
            Assert.Equal(ControlledCopyStatus.Withdrawn, c.CopyStatus);
            Assert.Equal("RECON-1", c.WithdrawalEvidenceReference);
            Assert.NotNull(c.WithdrawnAt);
        });
    }

    [Fact]
    public async Task Reconciling_the_last_issue_moves_the_event_to_reconciled()
    {
        var f = Fixture();
        var (e, issue) = await IssuedCopiesAsync(f);
        await f.Downtime.MarkRestoredAsync(e, new MarkRepositoryRestoredInput("RESTORE-1", null), Corr, CancellationToken.None);

        await f.Issue.ReconcileAsync(e, issue, Reconcile(), Corr, CancellationToken.None);

        Assert.Equal(DowntimeStatus.Reconciled, f.Events.Items.Single().DowntimeStatus);
    }

    // ── overdue evaluation ────────────────────────────────────────────────────

    [Fact]
    public async Task Evaluate_temporary_issue_overdue_sets_status_and_creates_escalations()
    {
        var f = Fixture();
        var (e, issue) = await IssuedCopiesAsync(f);
        f.Issues.Items.Single(x => x.Id == issue).ReconciliationDueDate = DateTimeOffset.UtcNow.AddDays(-1);

        var r = await f.Issue.EvaluateOverdueAsync(e, issue, Corr, CancellationToken.None);

        Assert.Equal("Overdue", r.Data!.IssueStatus);
        Assert.True(r.Data.IsOverdue);
        Assert.Contains(f.Escalations.Items, x => x.EscalationType == DowntimeEscalationType.ReconciliationOverdue
                                                  && x.RequiredRole == DowntimeEscalationRole.QADocumentation);
        // A missed reconciliation is a deviation-grade finding for GQD.
        Assert.Contains(f.Escalations.Items, x => x.EscalationType == DowntimeEscalationType.MissingReconciliation
                                                  && x.RequiredRole == DowntimeEscalationRole.GQD
                                                  && x.Severity == DowntimeEscalationSeverity.Critical);
    }

    [Fact]
    public async Task Evaluate_overdue_leaves_an_issue_within_its_window_untouched()
    {
        var f = Fixture();
        var (e, issue) = await IssuedCopiesAsync(f);

        var r = await f.Issue.EvaluateOverdueAsync(e, issue, Corr, CancellationToken.None);

        Assert.NotEqual("Overdue", r.Data!.IssueStatus);
        Assert.Empty(f.Escalations.Items);
    }

    // ── cancellation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_requires_a_reason_and_is_refused_once_copies_are_out()
    {
        var f = Fixture();
        var (e, issue) = await ApprovedIssueAsync(f);

        var noReason = await f.Issue.CancelAsync(e, issue, new CancelTemporaryIssueInput(" "), Corr, CancellationToken.None);
        Assert.Equal(DowntimeReasonCodes.ReasonRequired, noReason.ReasonCode);

        var ok = await f.Issue.CancelAsync(e, issue, new CancelTemporaryIssueInput("No longer needed"), Corr, CancellationToken.None);
        Assert.Equal("Cancelled", ok.Data!.IssueStatus);
    }

    [Fact]
    public async Task Issued_copies_cannot_be_waved_away_by_cancelling()
    {
        var f = Fixture();
        var (e, issue) = await IssuedCopiesAsync(f);

        var r = await f.Issue.CancelAsync(e, issue, new CancelTemporaryIssueInput("Changed our mind"), Corr, CancellationToken.None);

        Assert.False(r.IsSuccessful);
        Assert.Equal(DowntimeReasonCodes.IssueInvalidState, r.ReasonCode);
        Assert.All(f.Copies.Items, c => Assert.Equal(ControlledCopyStatus.Active, c.CopyStatus));
    }

    // ── FU16 repository boundary ──────────────────────────────────────────────

    [Fact]
    public async Task ValidatedDms_boundary_statement_makes_no_e_signature_claim()
    {
        var f = Fixture();
        var assessment = SeedAssessment(f, RepositoryType.ValidatedDms);
        var r = await f.Downtime.OpenAsync(OpenInput() with { RepositoryAssessmentId = assessment.Id }, Corr, CancellationToken.None);

        Assert.Contains("no e-signature claim", r.Data!.RepositoryBoundaryStatement);
        Assert.Contains("not a platform-verified signature", r.Data.RepositoryBoundaryStatement);
        Assert.Contains("no e-signature", r.Data.BoundaryStatement);
    }

    [Fact]
    public async Task Interim_repository_boundary_does_not_claim_a_validated_DMS()
    {
        var f = Fixture();
        var assessment = SeedAssessment(f, RepositoryType.ApprovedInterimRepository);

        var r = await f.Downtime.OpenAsync(OpenInput() with { RepositoryAssessmentId = assessment.Id }, Corr, CancellationToken.None);

        Assert.Contains("cannot be presented as a validated DMS", r.Data!.RepositoryBoundaryStatement);
    }

    /// <summary>
    /// PRODUCT DECISION: an unapproved repository BLOCKS the issue. Issuing a controlled copy from it is exactly
    /// how an uncontrolled copy is created, and no emergency override exists in this FU by design.
    /// </summary>
    [Fact]
    public async Task Unapproved_repository_blocks_the_temporary_issue()
    {
        var f = Fixture();
        var assessment = SeedAssessment(f, RepositoryType.UnapprovedRepository);
        var open = await f.Downtime.OpenAsync(OpenInput() with { RepositoryAssessmentId = assessment.Id }, Corr, CancellationToken.None);
        var entry = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective);

        // Logging the downtime itself is still permitted...
        Assert.True(open.IsSuccessful);

        // ...but the controlled issue is refused.
        var r = await f.Issue.RequestAsync(open.Data!.Id, Request(entry.Id), Corr, CancellationToken.None);
        Assert.False(r.IsSuccessful);
        Assert.Equal(DowntimeReasonCodes.UnapprovedRepositoryBlocked, r.ReasonCode);
        Assert.Empty(f.Issues.Items);
    }

    // ── FU15 retention integration ────────────────────────────────────────────

    [Fact]
    public void Retention_subject_types_appended_without_shifting_existing_ordinals()
    {
        Assert.Equal(27, (int)RetentionSubjectType.Other);
        Assert.Equal(30, (int)RetentionSubjectType.TemplateVariantParentChangeAssessment);
        Assert.Equal(31, (int)RetentionSubjectType.RepositoryDowntimeEvent);
        Assert.Equal(32, (int)RetentionSubjectType.TemporaryControlledIssue);
        Assert.Equal(33, (int)RetentionSubjectType.DowntimeEscalation);
    }

    // ── isolation / durability ────────────────────────────────────────────────

    [Fact]
    public async Task Cross_tenant_downtime_event_is_blocked()
    {
        var f = Fixture();
        var foreign = new DocumentRepositoryDowntimeEvent
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, DowntimeNumber = "DTE-FOREIGN",
            DetectionEvidenceReference = "INC-FOREIGN"
        };
        f.Events.Items.Add(foreign);

        var read = await f.Downtime.GetAsync(foreign.Id, Corr, CancellationToken.None);
        var close = await f.Downtime.CloseAsync(foreign.Id, new CloseDowntimeEventInput(null, null), Corr, CancellationToken.None);

        Assert.Equal(404, read.StatusCode);
        Assert.Equal(404, close.StatusCode);
        Assert.Equal(DowntimeStatus.Open, f.Events.Items.Single(x => x.Id == foreign.Id).DowntimeStatus);
    }

    [Fact]
    public async Task Cross_tenant_temporary_issue_is_blocked()
    {
        var f = Fixture();
        var e = await OpenEventAsync(f);
        var foreignIssue = new DocumentTemporaryControlledIssue
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, DowntimeEventId = e,
            RegisterEntryId = Guid.NewGuid(), IssueNumber = "TCI-FOREIGN"
        };
        f.Issues.Items.Add(foreignIssue);

        var approve = await f.Issue.ApproveAsync(e, foreignIssue.Id, Approve(), Corr, CancellationToken.None);
        var reconcile = await f.Issue.ReconcileAsync(e, foreignIssue.Id, Reconcile(), Corr, CancellationToken.None);

        Assert.Equal(404, approve.StatusCode);
        Assert.Equal(404, reconcile.StatusCode);
        Assert.Equal(TemporaryIssueStatus.Requested, f.Issues.Items.Single(x => x.Id == foreignIssue.Id).IssueStatus);
    }

    [Fact]
    public async Task Cross_tenant_register_entry_cannot_be_temporarily_issued()
    {
        var f = Fixture();
        var e = await OpenEventAsync(f);
        var foreignEntry = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(), TenantId = OtherTenantId, DocumentTitle = "Foreign SOP",
            LifecycleStatus = ControlledDocumentLifecycleStatus.Effective
        };
        f.Register.Items.Add(foreignEntry);

        var r = await f.Issue.RequestAsync(e, Request(foreignEntry.Id), Corr, CancellationToken.None);

        Assert.Equal(404, r.StatusCode);
        Assert.Equal(DowntimeReasonCodes.RegisterEntryNotFound, r.ReasonCode);
    }

    /// <summary>A full downtime cycle removes nothing from any store.</summary>
    [Fact]
    public async Task A_full_downtime_cycle_deletes_nothing()
    {
        var f = Fixture();
        var (e, issue) = await IssuedCopiesAsync(f);
        await f.Downtime.MarkRestoredAsync(e, new MarkRepositoryRestoredInput("RESTORE-1", null), Corr, CancellationToken.None);
        await f.Downtime.EvaluateEscalationAsync(e, Corr, CancellationToken.None);
        await f.Issue.ReconcileAsync(e, issue, Reconcile(), Corr, CancellationToken.None);
        await f.Downtime.CloseAsync(e, new CloseDowntimeEventInput("BCP-1", "done"), Corr, CancellationToken.None);

        Assert.NotEmpty(f.Events.Items);
        Assert.NotEmpty(f.Issues.Items);
        Assert.NotEmpty(f.Copies.Items);
        Assert.DoesNotContain(f.Events.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.Issues.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.Copies.Items, x => x.IsDeleted);
        Assert.DoesNotContain(f.Escalations.Items, x => x.IsDeleted);
    }

    [Fact]
    public void No_downtime_repository_contract_exposes_a_delete_operation()
    {
        var contracts = new[]
        {
            typeof(IDocumentRepositoryDowntimeEventRepository),
            typeof(IDocumentTemporaryControlledIssueRepository),
            typeof(IDocumentDowntimeEscalationRepository)
        };

        foreach (var contract in contracts)
        {
            Assert.DoesNotContain(contract.GetMethods(), m =>
                m.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                || m.Name.Contains("Remove", StringComparison.OrdinalIgnoreCase)
                || m.Name.Contains("Purge", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>No FU20 aggregate can carry document content — incidents and approvals are references.</summary>
    [Fact]
    public void No_downtime_aggregate_exposes_a_binary_content_property()
    {
        var types = new[]
        {
            typeof(DocumentRepositoryDowntimeEvent), typeof(DocumentTemporaryControlledIssue),
            typeof(DocumentDowntimeEscalation)
        };

        foreach (var type in types)
        {
            Assert.DoesNotContain(type.GetProperties(), p =>
                p.PropertyType == typeof(byte[]) || p.PropertyType == typeof(Stream) || p.PropertyType == typeof(Memory<byte>));
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> OpenEventAsync(Harness f)
    {
        var r = await f.Downtime.OpenAsync(OpenInput(), Corr, CancellationToken.None);
        return r.Data!.Id;
    }

    private async Task<(Guid EventId, Guid IssueId)> RequestedIssueAsync(Harness f)
    {
        var e = await OpenEventAsync(f);
        var entry = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective);
        var issue = await f.Issue.RequestAsync(e, Request(entry.Id), Corr, CancellationToken.None);
        return (e, issue.Data!.Id);
    }

    private async Task<(Guid EventId, Guid IssueId)> ApprovedIssueAsync(Harness f)
    {
        var (e, issue) = await RequestedIssueAsync(f);
        await f.Issue.ApproveAsync(e, issue, Approve(), Corr, CancellationToken.None);
        return (e, issue);
    }

    private async Task<Guid> ApprovedIssueOnEventAsync(Harness f, Guid eventId)
    {
        var entry = SeedEntry(f, ControlledDocumentLifecycleStatus.Effective);
        var issue = await f.Issue.RequestAsync(eventId, Request(entry.Id), Corr, CancellationToken.None);
        await f.Issue.ApproveAsync(eventId, issue.Data!.Id, Approve(), Corr, CancellationToken.None);
        return issue.Data.Id;
    }

    private async Task<(Guid EventId, Guid IssueId)> IssuedCopiesAsync(Harness f, int copyCount = 1)
    {
        var (e, issue) = await ApprovedIssueAsync(f);
        await f.Issue.IssueCopiesAsync(e, issue, IssueCopies() with { IssuedCopyCount = copyCount }, Corr, CancellationToken.None);
        return (e, issue);
    }

    private static OpenDowntimeEventInput OpenInput() => new(
        DetectionEvidenceReference: "INC-2026-0042",
        DowntimeType: nameof(DowntimeType.UnplannedOutage),
        RepositoryAssessmentId: null,
        RepositoryName: "Primary QMS DMS",
        StartedAt: Monday,
        DetectedByUserId: null,
        ImpactSummary: "Effective copies unavailable to production floor");

    private static RequestTemporaryIssueInput Request(Guid registerEntryId) => new(
        RegisterEntryId: registerEntryId,
        ControlledDocumentId: null,
        ControlledDocumentVersionId: null,
        IssueReason: "Batch in progress requires the effective SOP",
        RecipientRole: "Production Supervisor",
        RecipientDepartment: "Manufacturing",
        RecipientUserIds: null);

    private static ApproveTemporaryIssueInput Approve() => new(
        ApprovedByRole: nameof(ApprovalRequiredRole.GQD),
        ApprovalMechanism: nameof(OutsideNormalEnvironmentApprovalMechanism.WetSignature),
        ApprovalEvidenceReference: "APPR-2026-0042",
        ApprovedByUserId: null);

    private static IssueTemporaryControlledCopyInput IssueCopies() => new(
        IssuedCopyCount: 1,
        TemporaryLocationDescription: "Shift office, Building B",
        LocationType: nameof(ControlledCopyLocationType.PointOfUse));

    private static ReconcileTemporaryIssueInput Reconcile() => new(
        ReconciliationEvidenceReference: "RECON-1",
        DeviationReference: null,
        CorrectiveActionReference: null,
        MissingReconciliationReason: null);

    private static DocumentRepositoryAssessment SeedAssessment(Harness f, RepositoryType type)
    {
        var assessment = new DocumentRepositoryAssessment
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            RepositoryKey = "PRIMARY-DMS",
            RepositoryName = "Primary QMS DMS",
            RepositoryType = type,
            AssessmentStatus = RepositoryAssessmentStatus.Approved
        };
        f.Assessments.Items.Add(assessment);
        return assessment;
    }

    private static DocumentMasterRegisterEntry SeedEntry(Harness f, ControlledDocumentLifecycleStatus status)
    {
        var entry = new DocumentMasterRegisterEntry
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            DocumentTitle = "Document Control",
            DocumentClass = ControlledDocumentClass.Sop,
            DocumentType = DocumentType.Sop,
            Criticality = DocumentCriticality.Critical,
            LifecycleStatus = status,
            RegisterStatus = DocumentRegisterStatus.Active,
            PermanentUid = $"UID-{f.Register.Items.Count + 1:D7}",
            DocumentCode = $"GMG-QMS-SOP-{f.Register.Items.Count + 1:D4}"
        };
        f.Register.Items.Add(entry);
        return entry;
    }

    private static Harness Fixture()
    {
        var tenant = new TenantContext();
        tenant.SetTenant(TenantId);
        var user = new FakeUser();

        var events = new FakeEventRepo(tenant);
        var issues = new FakeIssueRepo(tenant);
        var escalations = new FakeEscalationRepo(tenant);
        var assessments = new FakeAssessmentRepo(tenant);
        var register = new FakeRegisterRepo(tenant);
        var copies = new FakeCopyRepo(tenant);

        var downtime = new DocumentRepositoryDowntimeService(events, issues, escalations, assessments, tenant, user);
        var issue = new DocumentTemporaryIssueService(events, issues, register, copies, assessments, downtime, tenant, user);

        return new Harness(downtime, issue, events, issues, escalations, assessments, register, copies);
    }

    private sealed record Harness(
        DocumentRepositoryDowntimeService Downtime,
        DocumentTemporaryIssueService Issue,
        FakeEventRepo Events,
        FakeIssueRepo Issues,
        FakeEscalationRepo Escalations,
        FakeAssessmentRepo Assessments,
        FakeRegisterRepo Register,
        FakeCopyRepo Copies);

    private sealed class FakeUser : ICurrentUserContext
    {
        public Guid UserId => Guid.Parse("cccccccc-1111-2222-3333-444444444420");
        public string? Email => "fu20@example.test";
        public string? DisplayName => "FU20 Tester";
        public string ActorName => "fu20@example.test";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeEventRepo(ITenantContext tenant) : IDocumentRepositoryDowntimeEventRepository
    {
        public List<DocumentRepositoryDowntimeEvent> Items { get; } = [];
        private IEnumerable<DocumentRepositoryDowntimeEvent> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentRepositoryDowntimeEvent> CreateAsync(DocumentRepositoryDowntimeEvent e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<DocumentRepositoryDowntimeEvent?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentRepositoryDowntimeEvent>> GetByStatusAsync(DowntimeStatus status, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRepositoryDowntimeEvent>>(Scoped.Where(x => x.DowntimeStatus == status).ToList());
        public Task<IReadOnlyList<DocumentRepositoryDowntimeEvent>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRepositoryDowntimeEvent>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentRepositoryDowntimeEvent e, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == e.Id);
            if (i >= 0) Items[i] = e;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeIssueRepo(ITenantContext tenant) : IDocumentTemporaryControlledIssueRepository
    {
        public List<DocumentTemporaryControlledIssue> Items { get; } = [];
        private IEnumerable<DocumentTemporaryControlledIssue> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentTemporaryControlledIssue> CreateAsync(DocumentTemporaryControlledIssue i, CancellationToken ct = default) { Items.Add(i); return Task.FromResult(i); }
        public Task<DocumentTemporaryControlledIssue?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetByDowntimeEventAsync(Guid eventId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTemporaryControlledIssue>>(Scoped.Where(x => x.DowntimeEventId == eventId).ToList());
        public Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetByRegisterEntryAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTemporaryControlledIssue>>(Scoped.Where(x => x.RegisterEntryId == entryId).ToList());
        public Task<IReadOnlyList<DocumentTemporaryControlledIssue>> GetOutstandingAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTemporaryControlledIssue>>(Scoped
                .Where(x => x.IssueStatus is TemporaryIssueStatus.Issued or TemporaryIssueStatus.ReconciliationDue or TemporaryIssueStatus.Overdue)
                .ToList());
        public Task<bool> UpdateAsync(DocumentTemporaryControlledIssue i, CancellationToken ct = default)
        {
            var idx = Items.FindIndex(x => x.Id == i.Id);
            if (idx >= 0) Items[idx] = i;
            return Task.FromResult(idx >= 0);
        }
    }

    private sealed class FakeEscalationRepo(ITenantContext tenant) : IDocumentDowntimeEscalationRepository
    {
        public List<DocumentDowntimeEscalation> Items { get; } = [];
        private IEnumerable<DocumentDowntimeEscalation> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentDowntimeEscalation> CreateAsync(DocumentDowntimeEscalation e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<IReadOnlyList<DocumentDowntimeEscalation>> GetByDowntimeEventAsync(Guid eventId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentDowntimeEscalation>>(Scoped.Where(x => x.DowntimeEventId == eventId).ToList());
        public Task<bool> UpdateAsync(DocumentDowntimeEscalation e, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == e.Id);
            if (i >= 0) Items[i] = e;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeAssessmentRepo(ITenantContext tenant) : IDocumentRepositoryAssessmentRepository
    {
        public List<DocumentRepositoryAssessment> Items { get; } = [];
        private IEnumerable<DocumentRepositoryAssessment> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentRepositoryAssessment> CreateAsync(DocumentRepositoryAssessment a, CancellationToken ct = default) { Items.Add(a); return Task.FromResult(a); }
        public Task<DocumentRepositoryAssessment?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentRepositoryAssessment>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentRepositoryAssessment>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentRepositoryAssessment a, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == a.Id);
            if (i >= 0) Items[i] = a;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeCopyRepo(ITenantContext tenant) : IDocumentControlledCopyRepository
    {
        public List<DocumentControlledCopy> Items { get; } = [];
        private IEnumerable<DocumentControlledCopy> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentControlledCopy> CreateAsync(DocumentControlledCopy c, CancellationToken ct = default) { Items.Add(c); return Task.FromResult(c); }
        public Task<DocumentControlledCopy?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<DocumentControlledCopy>> GetByRegisterEntryAsync(Guid entryId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentControlledCopy>>(Scoped.Where(x => x.RegisterEntryId == entryId).ToList());
        public Task<DocumentControlledCopy?> GetByCopyNumberAsync(Guid entryId, int copyNumber, CancellationToken ct = default) =>
            Task.FromResult(Scoped.FirstOrDefault(x => x.RegisterEntryId == entryId && x.CopyNumber == copyNumber));
        public Task<bool> UpdateAsync(DocumentControlledCopy c, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == c.Id);
            if (i >= 0) Items[i] = c;
            return Task.FromResult(i >= 0);
        }
    }

    private sealed class FakeRegisterRepo(ITenantContext tenant) : IDocumentMasterRegisterRepository
    {
        public List<DocumentMasterRegisterEntry> Items { get; } = [];
        private IEnumerable<DocumentMasterRegisterEntry> Scoped => Items.Where(x => x.TenantId == tenant.TenantId && !x.IsDeleted);
        public Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry e, CancellationToken ct = default) { Items.Add(e); return Task.FromResult(e); }
        public Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.Id == id));
        public Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string uid, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.PermanentUid == uid));
        public Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.DocumentCode == code));
        public Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Scoped.FirstOrDefault(x => x.ControlledDocumentId == id));
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentMasterRegisterEntry>>(Scoped.ToList());
        public Task<bool> UpdateAsync(DocumentMasterRegisterEntry e, CancellationToken ct = default)
        {
            var i = Items.FindIndex(x => x.Id == e.Id);
            if (i >= 0) Items[i] = e;
            return Task.FromResult(i >= 0);
        }
    }
}
