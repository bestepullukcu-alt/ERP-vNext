using System.Net;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T1a — the approval, end to end on the wire (pack §17 T-10/T-12/T-13/T-14/T-24/T-26): the approver is
/// resolved at submit and handed to a REAL MOD-0023 instance; the decision is made on MOD-0023's own route; this module
/// only reads it back (the pull finalizer) — and recomputes task totals.
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimesheetApprovalHttpMongoTests : TimeEntryScenario
{
    public TimesheetApprovalHttpMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    // ── T-13 — submit starts a MOD-0023 instance with the stored candidates ─────────────────────────────────────

    [Fact]
    public async Task Submit_starts_a_MOD_0023_instance_keyed_by_week_revision_and_submission_routed_to_the_line_manager()
    {
        var weekId = await SubmittedWeekAsync();

        var week = await StoredWeekAsync(weekId);
        Assert.Equal(TimesheetWeekStatus.Submitted, week.Status);
        Assert.Equal([Manager], week.ApproverCandidateUserIds);
        Assert.Equal(Manager, week.AssignedApproverUserId);
        Assert.Equal(TimesheetApproverResolution.LineManager, week.ApproverResolution);
        Assert.Equal(LegalEntity, week.LegalEntityId);
        Assert.Equal(week.SubmittedAtUtc!.Value.UtcTicks, week.SubmittedAtUtcTicks);
        Assert.Equal(1, week.SubmissionCount);

        var instance = await StoredInstanceAsync(week.WorkflowInstanceId!.Value);
        Assert.Equal(TimeEntryModule.ApprovalObjectType, instance.ObjectType);
        Assert.Equal(weekId.ToString(), instance.ObjectId);
        Assert.Equal($"timesheet-week:{Tenant}:{weekId}:1:1", instance.IdempotencyKey);
        var task = await Collection<ApprovalTask>(PlatformCollections.ApprovalTasks)
            .Find(t => t.WorkflowInstanceId == instance.Id).SingleAsync();
        Assert.Equal(Manager.ToString(), task.AssigneeRef);
    }

    [Fact]
    public async Task Starting_the_same_submission_again_returns_the_same_MOD_0023_instance()
    {
        var weekId = await SubmittedWeekAsync();
        var week = await StoredWeekAsync(weekId);

        await using var scope = Host.Services.CreateAsyncScope();
        using (TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), Tenant))
        {
            var again = await scope.ServiceProvider.GetRequiredService<ITimesheetApprovalService>()
                .StartAsync(week, [Manager], week.SubmissionCount);

            Assert.Equal(week.WorkflowInstanceId, again.WorkflowInstanceId);
        }

        Assert.Equal(1, await Collection<WorkflowInstance>(PlatformCollections.WorkflowInstances)
            .CountDocumentsAsync(i => i.TenantId == Tenant));
    }

    // ── T-12 — the resolver matrix ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_vacant_manager_seat_walks_up_to_the_next_level()
    {
        await UnseatAllAsync(ManagerSeat);

        var week = await StoredWeekAsync(await SubmittedWeekAsync());

        Assert.Equal([GrandManager], week.ApproverCandidateUserIds);
        Assert.Equal(TimesheetApproverResolution.ManagerChain, week.ApproverResolution);
    }

    [Fact]
    public async Task The_person_is_never_their_own_approver_a_seat_they_also_hold_is_walked_past()
    {
        // The person ALSO holds the manager seat, as a secondary assignment, and is its only holder: their primary
        // seat reports to a seat that only they sit in — the line manager is themselves.
        await UnseatAllAsync(ManagerSeat);
        await SeatAsync(Person, ManagerSeat, type: Domain.Entities.Organization.AssignmentType.Secondary);

        var week = await StoredWeekAsync(await SubmittedWeekAsync());

        Assert.DoesNotContain(Person, week.ApproverCandidateUserIds);
        Assert.Equal([GrandManager], week.ApproverCandidateUserIds);
        Assert.Equal(TimesheetApproverResolution.ManagerChain, week.ApproverResolution);
        var task = await Collection<ApprovalTask>(PlatformCollections.ApprovalTasks)
            .Find(t => t.WorkflowInstanceId == week.WorkflowInstanceId!.Value).SingleAsync();
        Assert.NotEqual(Person.ToString(), task.AssigneeRef);

        // …and the person cannot reach their own week as an approver either.
        Assert.Equal(HttpStatusCode.NotFound, (await Host.GetAsync($"/api/v1/time-entry/approvals/{week.Id}", ApproverToken(Person))).Status);
    }

    [Fact]
    public async Task No_chain_falls_back_to_the_time_admin_pool_and_an_empty_pool_refuses_the_submit()
    {
        await SetReportsToAsync(PersonSeat, null);

        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA))).Status);
        var noPool = await SubmitAsync();
        Assert.Equal(HttpStatusCode.Conflict, noPool.Status);
        Assert.Equal(TimeEntryReasonCodes.NoApprover, noPool.ReasonCode);
        var draft = Assert.Single(await StoredWeeksAsync());
        Assert.Equal(TimesheetWeekStatus.Draft, draft.Status);
        Assert.Null(draft.WorkflowInstanceId);
        Assert.Equal(0, await Collection<WorkflowInstance>(PlatformCollections.WorkflowInstances).CountDocumentsAsync(i => i.TenantId == Tenant));

        await SetPoolAsync(PoolSeat);
        var pooled = await SubmitAsync();

        Assert.Equal(HttpStatusCode.OK, pooled.Status);
        var week = await StoredWeekAsync(pooled.Data.GetProperty("weekId").GetGuid());
        Assert.Equal([PoolAdmin], week.ApproverCandidateUserIds);
        Assert.Equal(TimesheetApproverResolution.TimeAdminPool, week.ApproverResolution);
    }

    // ── D11 — the approver sees submitted weeks only ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_draft_never_reaches_the_approver_not_even_a_withdrawn_one_that_still_names_them()
    {
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA))).Status);
        var listDraft = await Host.GetAsync("/api/v1/time-entry/approvals", ApproverToken(Manager));
        Assert.Equal(0, listDraft.Data.GetProperty("total").GetInt32());

        var submitted = await SubmitAsync();
        var weekId = submitted.Data.GetProperty("weekId").GetGuid();
        Assert.Equal(1, (await Host.GetAsync("/api/v1/time-entry/approvals", ApproverToken(Manager))).Data.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await Host.GetAsync($"/api/v1/time-entry/approvals/{weekId}", ApproverToken(Manager))).Status);

        Assert.Equal(HttpStatusCode.OK, (await WithdrawAsync()).Status);

        // Withdrawn = Draft again, with the old candidate list still on it. Still invisible.
        Assert.Contains(Manager, (await StoredWeekAsync(weekId)).ApproverCandidateUserIds);
        Assert.Equal(0, (await Host.GetAsync("/api/v1/time-entry/approvals", ApproverToken(Manager))).Data.GetProperty("total").GetInt32());
        var detail = await Host.GetAsync($"/api/v1/time-entry/approvals/{weekId}", ApproverToken(Manager));
        Assert.Equal(HttpStatusCode.NotFound, detail.Status);
        Assert.Equal(TimeEntryReasonCodes.ApprovalWeekNotFound, detail.ReasonCode);
    }

    [Fact]
    public async Task A_week_routed_to_someone_else_is_404_for_another_approver()
    {
        var weekId = await SubmittedWeekAsync();

        var stranger = await Host.GetAsync($"/api/v1/time-entry/approvals/{weekId}", ApproverToken(GrandManager));

        Assert.Equal(HttpStatusCode.NotFound, stranger.Status);
        Assert.Equal(0, (await Host.GetAsync("/api/v1/time-entry/approvals", ApproverToken(GrandManager))).Data.GetProperty("total").GetInt32());
    }

    // ── T-24 — withdraw ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Withdraw_before_the_decision_cancels_the_MOD_0023_instance_and_returns_the_week_to_Draft()
    {
        var weekId = await SubmittedWeekAsync();
        var instanceId = (await StoredWeekAsync(weekId)).WorkflowInstanceId!.Value;

        var withdrawn = await WithdrawAsync();

        Assert.Equal(HttpStatusCode.OK, withdrawn.Status);
        var week = await StoredWeekAsync(weekId);
        Assert.Equal(TimesheetWeekStatus.Draft, week.Status);
        Assert.Null(week.WorkflowInstanceId);
        Assert.NotNull(week.WithdrawnAtUtc);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await StoredInstanceAsync(instanceId)).Status);
    }

    [Fact]
    public async Task Withdraw_after_the_approver_decided_is_409_and_the_decision_is_taken_on_board()
    {
        var weekId = await SubmittedWeekAsync();
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, weekId, approve: true)).Status);

        // Decided in MOD-0023, NOT yet finalized here. The version comes from the store, not from a read: a read would
        // run the finalizer first and the withdraw would then never meet the "decided but not finalized" state.
        var stillSubmitted = await StoredWeekAsync(weekId);
        Assert.Equal(TimesheetWeekStatus.Submitted, stillSubmitted.Status);
        var withdrawn = await Host.PostAsync($"/api/v1/time-entry/weeks/{CurrentWeek}/withdraw", PersonToken(),
            new { expectedVersion = stillSubmitted.Version });

        Assert.Equal(HttpStatusCode.Conflict, withdrawn.Status);
        Assert.Equal(TimeEntryReasonCodes.WithdrawTooLate, withdrawn.ReasonCode);
        var week = await StoredWeekAsync(weekId);
        Assert.Equal(TimesheetWeekStatus.Approved, week.Status);
        Assert.True(week.InForce);
    }

    [Fact]
    public async Task A_resubmitted_revision_opens_a_new_instance_not_the_withdrawn_one()
    {
        var weekId = await SubmittedWeekAsync();
        var first = (await StoredWeekAsync(weekId)).WorkflowInstanceId!.Value;
        Assert.Equal(HttpStatusCode.OK, (await WithdrawAsync()).Status);

        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync()).Status);

        var week = await StoredWeekAsync(weekId);
        Assert.NotEqual(first, week.WorkflowInstanceId);
        Assert.Equal(2, week.SubmissionCount);
        Assert.Equal($"timesheet-week:{Tenant}:{weekId}:1:2", (await StoredInstanceAsync(week.WorkflowInstanceId!.Value)).IdempotencyKey);
    }

    // ── T-26 / R5 — a timesheet rejection must carry a comment; other definitions are unchanged ─────────────────

    [Fact]
    public async Task Rejecting_a_timesheet_without_a_comment_is_refused_by_MOD_0023_and_with_one_returns_the_week()
    {
        var weekId = await SubmittedWeekAsync();

        var bare = await DecideAsync(Manager, weekId, approve: false, comment: "   ");
        Assert.Equal(HttpStatusCode.BadRequest, bare.Status);
        Assert.Equal(WorkflowReasonCodes.WorkflowRejectCommentRequired, bare.ReasonCode);
        Assert.Equal(WorkflowInstanceStatus.Active, (await StoredInstanceAsync((await StoredWeekAsync(weekId)).WorkflowInstanceId!.Value)).Status);

        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, weekId, approve: false, comment: "Wednesday is missing")).Status);

        var read = await GetWeekAsync();
        Assert.Equal("Draft", read.Data.GetProperty("status").GetString());
        Assert.Equal("Wednesday is missing", read.Data.GetProperty("lastRejectionReason").GetString());
        Assert.True(read.Data.GetProperty("editable").GetBoolean());
        var week = await StoredWeekAsync(weekId);
        Assert.Equal(Manager, week.LastRejectedByUserId);
        Assert.Null(week.WorkflowInstanceId);
    }

    [Fact]
    public async Task A_definition_without_the_option_still_rejects_without_a_comment()
    {
        var designer = Host.Token(Manager, Tenant,
            WorkflowPermissions.DefinitionsManage, WorkflowPermissions.DefinitionsPublish, WorkflowPermissions.InstancesStart,
            WorkflowPermissions.TasksReject);

        var created = await Host.PostAsync("/api/v1/workflow/definitions", designer,
            new { templateCode = "plain-approval", name = "Plain approval", description = (string?)null });
        Assert.True(created.Status is HttpStatusCode.OK or HttpStatusCode.Created, created.ToString());
        var definitionId = created.Data.GetProperty("id").GetGuid();
        var published = await Host.PostAsync($"/api/v1/workflow/definitions/{definitionId}/publish", designer, new
        {
            definitionJson = """{ "name": "Plain", "stages": [ { "code": "stage-1", "steps": [ { "code": "approve", "type": "approval" } ] } ] }""",
            schemaVersion = "1.0", expressionVersion = "1.0", publishReason = "test"
        });
        Assert.True(published.Status == HttpStatusCode.OK, published.ToString());
        var started = await Host.PostAsync("/api/v1/workflow/instances", designer, new
        {
            templateId = definitionId, objectType = "task", objectId = Guid.NewGuid().ToString(),
            candidatePrincipalIds = new[] { GrandManager.ToString() }, idempotencyKey = Guid.NewGuid().ToString("N"),
            commentRequired = false, evidenceRequired = false
        });
        Assert.True(started.Status is HttpStatusCode.OK or HttpStatusCode.Created, started.ToString());
        var taskId = started.Data.GetProperty("approvalTaskId").GetGuid();

        var rejected = await Host.PostAsync($"/api/v1/workflow/tasks/{taskId}/reject", ApproverToken(GrandManager),
            new { actorId = GrandManager.ToString(), reasonCode = "NO", idempotencyKey = Guid.NewGuid().ToString("N"), comment = (string?)null });

        Assert.Equal(HttpStatusCode.OK, rejected.Status);
    }

    // ── T-14 / D7 — approve locks, totals are RECOMPUTED ────────────────────────────────────────────────────────

    [Fact]
    public async Task Approval_locks_the_week_and_the_next_read_sets_the_task_total_to_the_approved_sum()
    {
        var weekId = await SubmittedWeekAsync(Row(Monday, 120, TaskA), Row(Monday.AddDays(1), 45, TaskA), Row(Monday, 30, TaskB));
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, weekId, approve: true)).Status);
        Assert.Null(await ApprovedMinutesAsync(TaskA)); // MOD-0023 decided; nothing here has read it yet

        var read = await GetWeekAsync();

        Assert.Equal("Approved", read.Data.GetProperty("status").GetString());
        Assert.False(read.Data.GetProperty("editable").GetBoolean());
        Assert.Equal(165, await ApprovedMinutesAsync(TaskA));
        Assert.Equal(30, await ApprovedMinutesAsync(TaskB));
        var week = await StoredWeekAsync(weekId);
        Assert.Equal(Manager, week.ApprovedByUserId);
        Assert.True(week.InForce);
        Assert.False(week.IsOpen);

        var edit = await SaveAsync(week.Version, CurrentWeek, null, Row(Monday, 60, TaskA));
        Assert.Equal(HttpStatusCode.Conflict, edit.Status);
        Assert.Equal(TimeEntryReasonCodes.WeekNotOpen, edit.ReasonCode);
    }

    [Fact]
    public async Task The_approvals_page_also_takes_a_decision_on_board()
    {
        var weekId = await SubmittedWeekAsync();
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, weekId, approve: true)).Status);

        var list = await Host.GetAsync("/api/v1/time-entry/approvals", ApproverToken(Manager));

        Assert.Equal(0, list.Data.GetProperty("total").GetInt32());
        Assert.Equal(TimesheetWeekStatus.Approved, (await StoredWeekAsync(weekId)).Status);
        Assert.Equal(120, await ApprovedMinutesAsync(TaskA));
    }

    [Fact]
    public async Task A_replayed_finalization_writes_the_same_total_not_a_doubled_one()
    {
        var weekId = await SubmittedWeekAsync();
        var instanceId = (await StoredWeekAsync(weekId)).WorkflowInstanceId!.Value;
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, weekId, approve: true)).Status);
        await GetWeekAsync();
        Assert.Equal(120, await ApprovedMinutesAsync(TaskA));

        // The replay the consumed-event store exists for: the outcome is recorded as applied, but the week's own write
        // is (as if) lost. The finalizer runs again for the same (instance, outcome).
        await Collection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks).UpdateOneAsync(
            w => w.Id == weekId,
            Builders<TimesheetWeek>.Update
                .Set(w => w.Status, TimesheetWeekStatus.Submitted)
                .Set(w => w.InForce, false)
                .Set(w => w.IsOpen, true)
                .Set(w => w.WorkflowInstanceId, instanceId));

        var result = await FinalizeDirectlyAsync(weekId);

        Assert.Equal(TimesheetFinalizationResult.Approved, result);
        Assert.Equal(120, await ApprovedMinutesAsync(TaskA));
        Assert.Equal(TimesheetWeekStatus.Approved, (await StoredWeekAsync(weekId)).Status);
        Assert.Equal(TimesheetFinalizationResult.NotApplicable, await FinalizeDirectlyAsync(weekId));
        Assert.Equal(120, await ApprovedMinutesAsync(TaskA));
    }

    // ── T-10 / D5 — corrections ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_correction_needs_a_reason_keeps_the_original_in_force_until_approved_then_supersedes_it()
    {
        var originalId = await SubmittedWeekAsync(Row(Monday, 120, TaskA));
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, originalId, approve: true)).Status);
        await GetWeekAsync();
        Assert.Equal(120, await ApprovedMinutesAsync(TaskA));

        var noReason = await Host.PostAsync($"/api/v1/time-entry/weeks/{CurrentWeek}/corrections", PersonToken(), new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.Status);
        Assert.Equal(TimeEntryReasonCodes.CorrectionReasonRequired, noReason.ReasonCode);

        var opened = await Host.PostAsync($"/api/v1/time-entry/weeks/{CurrentWeek}/corrections", PersonToken(),
            new { reason = "Monday was three hours, not two" });
        Assert.Equal(HttpStatusCode.Created, opened.Status);
        var correctionId = opened.Data.GetProperty("weekId").GetGuid();
        Assert.Equal(2, opened.Data.GetProperty("revisionNumber").GetInt32());

        var second = await Host.PostAsync($"/api/v1/time-entry/weeks/{CurrentWeek}/corrections", PersonToken(), new { reason = "again" });
        Assert.Equal(HttpStatusCode.Conflict, second.Status);
        Assert.Equal(TimeEntryReasonCodes.CorrectionAlreadyOpen, second.ReasonCode);

        // The correction starts from the approved figures and is edited on its own rows.
        Assert.Equal(120, Assert.Single(await StoredEntriesAsync(correctionId)).DurationMinutes);
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 180, TaskA))).Status);
        Assert.Equal(120, Assert.Single(await StoredEntriesAsync(originalId)).DurationMinutes);

        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync()).Status);
        var original = await StoredWeekAsync(originalId);
        Assert.Equal(TimesheetWeekStatus.Approved, original.Status);
        Assert.True(original.InForce);
        var read = await GetWeekAsync();
        Assert.Equal(originalId, read.Data.GetProperty("inForce").GetProperty("weekId").GetGuid());
        Assert.Equal(120, await ApprovedMinutesAsync(TaskA));

        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, correctionId, approve: true)).Status);
        await GetWeekAsync();

        original = await StoredWeekAsync(originalId);
        var correction = await StoredWeekAsync(correctionId);
        Assert.Equal(TimesheetWeekStatus.Superseded, original.Status);
        Assert.False(original.InForce);
        Assert.Equal(TimesheetWeekStatus.Approved, correction.Status);
        Assert.True(correction.InForce);
        Assert.Equal(1, correction.CorrectionOfRevision);
        Assert.Equal(180, await ApprovedMinutesAsync(TaskA));
    }

    [Fact]
    public async Task A_rejected_correction_goes_back_to_Draft_and_the_original_stays_approved_and_in_force()
    {
        var originalId = await SubmittedWeekAsync(Row(Monday, 120, TaskA));
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, originalId, approve: true)).Status);
        await GetWeekAsync();
        var opened = await Host.PostAsync($"/api/v1/time-entry/weeks/{CurrentWeek}/corrections", PersonToken(), new { reason = "fix" });
        var correctionId = opened.Data.GetProperty("weekId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 240, TaskA))).Status);
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync()).Status);

        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, correctionId, approve: false, comment: "No")).Status);
        await GetWeekAsync();

        Assert.Equal(TimesheetWeekStatus.Draft, (await StoredWeekAsync(correctionId)).Status);
        var original = await StoredWeekAsync(originalId);
        Assert.Equal(TimesheetWeekStatus.Approved, original.Status);
        Assert.True(original.InForce);
        Assert.Equal(120, await ApprovedMinutesAsync(TaskA));

        // …and a discarded correction draft leaves the original exactly as it was.
        Assert.Equal(HttpStatusCode.NoContent, (await Host.DeleteAsync($"/api/v1/time-entry/weeks/{CurrentWeek}/corrections/draft", PersonToken())).Status);
        Assert.True((await StoredWeekAsync(correctionId)).IsDeleted);
        Assert.True((await StoredWeekAsync(originalId)).InForce);
    }

    // ── F2 / §13 — a decision by the person on their OWN week is refused, and the week comes back to them ───────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_decision_MOD_0023_records_as_the_persons_own_is_refused_and_the_week_returns_to_Draft(bool approve)
    {
        var weekId = await SubmittedWeekAsync();
        var instanceId = (await StoredWeekAsync(weekId)).WorkflowInstanceId!.Value;
        // A delegation that routed the approval back to the person, as MOD-0023 would record it — on an instance that
        // predates StartedByUserId (the legacy shape, where MOD-0023's own starter check cannot see them).
        await RawUnsetAsync(PlatformCollections.WorkflowInstances, instanceId, "StartedByUserId");
        await Collection<RuntimeAssignmentSnapshot>(PlatformCollections.WorkflowRuntimeAssignmentSnapshots).UpdateManyAsync(
            s => s.WorkflowInstanceId == instanceId,
            Builders<RuntimeAssignmentSnapshot>.Update.Set(s => s.ResolvedPrincipalId, Person.ToString()));
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Person, weekId, approve, comment: "mine")).Status);

        var read = await GetWeekAsync();

        Assert.Equal("Draft", read.Data.GetProperty("status").GetString());
        Assert.Equal(TimeEntryReasonCodes.SelfDecisionRefused, read.Data.GetProperty("finalizationBlockedReason").GetString());
        Assert.True(read.Data.GetProperty("editable").GetBoolean());
        var week = await StoredWeekAsync(weekId);
        Assert.False(week.InForce);
        Assert.Null(week.WorkflowInstanceId);
        Assert.Null(week.LastRejectionReason);
        Assert.Null(await ApprovedMinutesAsync(TaskA));
        Assert.Single(Host.Audit.Requests, r => r.RequestType == "FinalizeTimesheetDecisionCommand");
    }

    /// <summary>B2 on the timesheet (needs WP-WORKFLOW-APPROVAL-STATUS-01): the person STARTED the instance by submitting,
    /// so MOD-0023 itself refuses their approval even when an approval task is routed to them.</summary>
    [Fact]
    public async Task MOD_0023_refuses_the_submitters_own_approval_of_their_timesheet()
    {
        var weekId = await SubmittedWeekAsync();
        var instanceId = (await StoredWeekAsync(weekId)).WorkflowInstanceId!.Value;
        Assert.Equal(Person, (await StoredInstanceAsync(instanceId)).StartedByUserId);
        await Collection<RuntimeAssignmentSnapshot>(PlatformCollections.WorkflowRuntimeAssignmentSnapshots).UpdateManyAsync(
            s => s.WorkflowInstanceId == instanceId,
            Builders<RuntimeAssignmentSnapshot>.Update.Set(s => s.ResolvedPrincipalId, Person.ToString()));

        var approve = await DecideAsync(Person, weekId, approve: true);

        Assert.Equal(HttpStatusCode.Conflict, approve.Status);
        Assert.Equal(WorkflowReasonCodes.SodViolation, approve.ReasonCode);
        Assert.Equal("Submitted", (await GetWeekAsync()).Data.GetProperty("status").GetString());
    }

    // ── F1 — the submit order never lets an old decision onto new content ──────────────────────────────────────

    [Fact]
    public async Task A_submit_that_dies_after_starting_leaves_no_way_for_its_instance_to_approve_the_next_submission()
    {
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA))).Status);
        Host.Probes.AfterApprovalStarted = _ => throw new InvalidOperationException("crash between start and week write");
        await SubmitAsync();
        Host.Probes.AfterApprovalStarted = null;

        var draft = Assert.Single(await StoredWeeksAsync());
        Assert.Equal(TimesheetWeekStatus.Draft, draft.Status);
        Assert.Null(draft.WorkflowInstanceId);
        Assert.Equal(1, draft.SubmissionCount);
        var stray = await Collection<WorkflowInstance>(PlatformCollections.WorkflowInstances)
            .Find(i => i.TenantId == Tenant).SingleAsync();

        // The manager approves the stray instance in MOD-0023 — its content was 60 minutes.
        Assert.Equal(HttpStatusCode.OK, (await DecideInstanceAsync(Manager, stray.Id, approve: true)).Status);

        // The person changes the week and submits again: a NEW instance, and the old approval decides nothing.
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 480, TaskA))).Status);
        var resubmitted = await SubmitAsync();
        Assert.Equal(HttpStatusCode.OK, resubmitted.Status);

        var week = await StoredWeekAsync(draft.Id);
        Assert.NotEqual(stray.Id, week.WorkflowInstanceId);
        Assert.Equal($"timesheet-week:{Tenant}:{draft.Id}:1:2", (await StoredInstanceAsync(week.WorkflowInstanceId!.Value)).IdempotencyKey);
        Assert.Equal("Submitted", (await GetWeekAsync()).Data.GetProperty("status").GetString());
        Assert.Null(await ApprovedMinutesAsync(TaskA));
    }

    [Fact]
    public async Task A_stray_instance_nobody_decided_is_cancelled_by_the_next_submit()
    {
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA))).Status);
        Host.Probes.AfterApprovalStarted = _ => throw new InvalidOperationException("crash");
        await SubmitAsync();
        Host.Probes.AfterApprovalStarted = null;
        var stray = await Collection<WorkflowInstance>(PlatformCollections.WorkflowInstances).Find(i => i.TenantId == Tenant).SingleAsync();

        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync()).Status);

        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await StoredInstanceAsync(stray.Id)).Status);
    }

    [Fact]
    public async Task An_instance_handed_back_closed_for_a_reused_key_is_never_adopted()
    {
        // Stage the collision: a week whose submission counter is behind an instance MOD-0023 already closed with an
        // approval (data older than the F1 order, or a restored backup). The next start reuses that key.
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA))).Status);
        Host.Probes.AfterApprovalStarted = _ => throw new InvalidOperationException("crash");
        await SubmitAsync();
        Host.Probes.AfterApprovalStarted = null;
        var draft = Assert.Single(await StoredWeeksAsync());
        var stray = await Collection<WorkflowInstance>(PlatformCollections.WorkflowInstances).Find(i => i.TenantId == Tenant).SingleAsync();
        Assert.Equal(HttpStatusCode.OK, (await DecideInstanceAsync(Manager, stray.Id, approve: true)).Status);
        await RawSetAsync(PlatformCollections.TimeEntryTimesheetWeeks, draft.Id, ("SubmissionCount", 0));

        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 480, TaskA))).Status);
        var refused = await SubmitAsync();

        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal(TimeEntryReasonCodes.ApprovalInstanceClosed, refused.ReasonCode);
        Assert.Equal("Draft", (await GetWeekAsync()).Data.GetProperty("status").GetString());
        Assert.Null(await ApprovedMinutesAsync(TaskA));
    }

    // ── F3 — the ONE approver MOD-0023 assigned is the only one who sees the week ─────────────────────────────

    [Fact]
    public async Task With_two_holders_of_the_manager_seat_only_the_one_MOD_0023_assigned_sees_the_week()
    {
        var secondManager = Guid.NewGuid();
        await SeatAsync(secondManager, ManagerSeat);

        var weekId = await SubmittedWeekAsync();

        var week = await StoredWeekAsync(weekId);
        Assert.Equal(new[] { Manager, secondManager }.OrderBy(g => g), week.ApproverCandidateUserIds.OrderBy(g => g));
        var task = await Collection<ApprovalTask>(PlatformCollections.ApprovalTasks)
            .Find(t => t.WorkflowInstanceId == week.WorkflowInstanceId!.Value).SingleAsync();
        var assigned = Guid.Parse(task.AssigneeRef);
        var other = assigned == Manager ? secondManager : Manager;
        Assert.Equal(assigned, week.AssignedApproverUserId);

        Assert.Equal(1, (await Host.GetAsync("/api/v1/time-entry/approvals", ApproverToken(assigned))).Data.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await Host.GetAsync($"/api/v1/time-entry/approvals/{weekId}", ApproverToken(assigned))).Status);
        Assert.Equal(0, (await Host.GetAsync("/api/v1/time-entry/approvals", ApproverToken(other))).Data.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await Host.GetAsync($"/api/v1/time-entry/approvals/{weekId}", ApproverToken(other))).Status);
    }

    // ── F8 — withdraw cancels whatever MOD-0023 has left open, escalated included ─────────────────────────────

    [Fact]
    public async Task Withdraw_cancels_an_ESCALATED_approval_too()
    {
        var weekId = await SubmittedWeekAsync();
        var instanceId = (await StoredWeekAsync(weekId)).WorkflowInstanceId!.Value;
        var task = await Collection<ApprovalTask>(PlatformCollections.ApprovalTasks).Find(t => t.WorkflowInstanceId == instanceId).SingleAsync();
        // As MOD-0023's escalation sweep leaves it.
        await RawSetAsync(PlatformCollections.ApprovalTasks, task.Id, ("Status", (int)ApprovalTaskStatus.Escalated));
        await RawSetAsync(PlatformCollections.WorkflowInstances, instanceId, ("Status", (int)WorkflowInstanceStatus.Escalated));

        var withdrawn = await WithdrawAsync();

        Assert.Equal(HttpStatusCode.OK, withdrawn.Status);
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await StoredInstanceAsync(instanceId)).Status);
        Assert.Equal(TimesheetWeekStatus.Draft, (await StoredWeekAsync(weekId)).Status);
    }

    [Fact]
    public async Task Withdraw_of_a_TIMED_OUT_approval_returns_the_week_without_anything_left_to_cancel()
    {
        var weekId = await SubmittedWeekAsync();
        var instanceId = (await StoredWeekAsync(weekId)).WorkflowInstanceId!.Value;
        await RawSetAsync(PlatformCollections.WorkflowInstances, instanceId, ("Status", (int)WorkflowInstanceStatus.TimedOut));

        var withdrawn = await WithdrawAsync();

        Assert.Equal(HttpStatusCode.OK, withdrawn.Status);
        Assert.Equal(TimesheetWeekStatus.Draft, (await StoredWeekAsync(weekId)).Status);
    }

    // ── F9 — the finalizer is safe to rerun, and two finalizers never overwrite each other ─────────────────────

    [Fact]
    public async Task A_rerun_after_a_crash_behind_the_supersede_still_recomputes_the_tasks_only_the_old_revision_had()
    {
        var originalId = await SubmittedWeekAsync(Row(Monday, 120, TaskA), Row(Monday, 30, TaskB));
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, originalId, approve: true)).Status);
        await GetWeekAsync();
        Assert.Equal(30, await ApprovedMinutesAsync(TaskB));

        var opened = await Host.PostAsync($"/api/v1/time-entry/weeks/{CurrentWeek}/corrections", PersonToken(), new { reason = "no TaskB" });
        var correctionId = opened.Data.GetProperty("weekId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 180, TaskA))).Status);
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync()).Status);
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, correctionId, approve: true)).Status);

        // The first run got as far as superseding the original, then died.
        await RawSetAsync(PlatformCollections.TimeEntryTimesheetWeeks, originalId,
            ("Status", (int)TimesheetWeekStatus.Superseded), ("InForce", false));

        await GetWeekAsync();

        Assert.Equal(TimesheetWeekStatus.Approved, (await StoredWeekAsync(correctionId)).Status);
        Assert.Equal(180, await ApprovedMinutesAsync(TaskA));
        Assert.Equal(0, await ApprovedMinutesAsync(TaskB));
    }

    [Fact]
    public async Task Two_finalizers_of_the_same_task_racing_end_on_the_full_total_not_the_stale_one()
    {
        // Two people, one shared task (created by one, held by the other — both may read it), one manager.
        var shared = Guid.NewGuid();
        await SeedTaskAsync(Tenant, shared, assignee: SecondPerson, creator: Person);
        var secondSeat = Guid.NewGuid();
        await SeedPositionAsync(secondSeat, reportsTo: ManagerSeat);
        await SeatAsync(SecondPerson, secondSeat);

        var mine = await SubmittedWeekAsync(Row(Monday, 120, shared));
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(0, CurrentWeek, PersonToken(person: SecondPerson), Row(Monday, 60, shared))).Status);
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(CurrentWeek, PersonToken(person: SecondPerson))).Status);
        var theirs = (await StoredWeeksAsync()).Single(w => w.UserId == SecondPerson).Id;
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, mine, approve: true)).Status);
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, theirs, approve: true)).Status);

        // While the first finalizer holds its computed total, the second one finalizes and writes the full total.
        var interleaved = false;
        Host.Probes.BeforeTaskTotalWrite = async _ =>
        {
            if (interleaved)
            {
                return;
            }

            interleaved = true;
            Assert.Equal(TimesheetFinalizationResult.Approved, await FinalizeDirectlyAsync(theirs));
        };

        Assert.Equal(TimesheetFinalizationResult.Approved, await FinalizeDirectlyAsync(mine));
        Host.Probes.BeforeTaskTotalWrite = null;

        Assert.True(interleaved);
        Assert.Equal(180, await ApprovedMinutesAsync(shared));
    }

    // ── F10 — oldest submission first, by a sortable field ─────────────────────────────────────────────────────

    [Fact]
    public async Task The_sweep_list_is_oldest_submission_first()
    {
        var weeks = Collection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks);
        var basis = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        // Inserted newest first; one with a +03:00 offset, so an offset-sensitive order would also be wrong.
        var times = new[] { basis.AddHours(5), basis.ToOffset(TimeSpan.FromHours(3)).AddHours(1), basis };
        foreach (var at in times)
        {
            await weeks.InsertOneAsync(new TimesheetWeek
            {
                TenantId = Tenant, UserId = Guid.NewGuid(), WeekKey = CurrentWeek, WeekStartDate = Monday, TimeZoneId = Zone,
                RevisionNumber = 1, Status = TimesheetWeekStatus.Submitted, WorkflowInstanceId = Guid.NewGuid(),
                SubmittedAtUtc = at, SubmittedAtUtcTicks = at.UtcTicks
            });
        }

        await using var scope = Host.Services.CreateAsyncScope();
        using (TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), Tenant))
        {
            var oldestTwo = await scope.ServiceProvider.GetRequiredService<Domain.Repositories.ITimesheetWeekRepository>().ListSubmittedAsync(2);

            Assert.Equal(new[] { basis.UtcTicks, basis.AddHours(1).UtcTicks }, oldestTwo.Select(w => w.SubmittedAtUtcTicks!.Value));
        }
    }

    // ── F1 — the Task Center card is only for the approval the week is waiting on ─────────────────────────────

    [Fact]
    public async Task A_withdrawn_weeks_instance_gets_no_card_context()
    {
        var weekId = await SubmittedWeekAsync();
        var instance = await StoredInstanceAsync((await StoredWeekAsync(weekId)).WorkflowInstanceId!.Value);
        Assert.Equal(HttpStatusCode.OK, (await WithdrawAsync()).Status);

        await using var scope = Host.Services.CreateAsyncScope();
        using (TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), Tenant))
        {
            var resolver = scope.ServiceProvider.GetServices<Application.Features.WorkAggregation.Services.IApprovalSourceResolver>()
                .Single(r => r.Handles(TimeEntryModule.ApprovalObjectType));
            var context = await resolver.ResolveAsync([instance],
                new Application.Features.WorkAggregation.WorkItemActor(Manager, false, new HashSet<string>()));

            Assert.Empty(context);
        }
    }

    // ── BL-437 — what the Task Center approval card says ───────────────────────────────────────────────────────

    [Fact]
    public async Task The_approval_card_names_the_week_and_the_person_marks_flagged_days_and_links_the_read_only_week()
    {
        var weekId = await SubmittedWeekAsync(Row(Monday, 480, TaskA), Row(Monday, 240, TaskB));
        var instance = await StoredInstanceAsync((await StoredWeekAsync(weekId)).WorkflowInstanceId!.Value);

        await using var scope = Host.Services.CreateAsyncScope();
        using (TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), Tenant))
        {
            var resolver = scope.ServiceProvider.GetServices<Application.Features.WorkAggregation.Services.IApprovalSourceResolver>()
                .Single(r => r.Handles(TimeEntryModule.ApprovalObjectType));
            var context = (await resolver.ResolveAsync([instance],
                new Application.Features.WorkAggregation.WorkItemActor(Manager, false, new HashSet<string>())))[instance.Id];

            Assert.Equal("Timesheet · 2026-W41 · Ayşe Yılmaz · ⚑", context.Title);
            Assert.Equal(Person.ToString(), context.Requester!.Id);
            Assert.Equal($"/TimeEntry/Approvals/{weekId}", context.DeepLink);
        }
    }

    // ── Audit ───────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Every_state_change_is_audited_through_the_pipeline_and_a_quiet_read_audits_nothing()
    {
        var weekId = await SubmittedWeekAsync();
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, weekId, approve: true)).Status);
        var before = Host.Audit.Requests.Count(r => r.SourceModule == TimeEntryModule.AuditSourceModule);

        await GetWeekAsync(); // finalizes → one FinalizeTimesheetDecision entry
        await GetWeekAsync(); // nothing waiting → nothing audited

        var ours = Host.Audit.Requests.Where(r => r.SourceModule == TimeEntryModule.AuditSourceModule).ToList();
        Assert.Contains(ours, r => r.RequestType == "SaveTimeEntriesCommand");
        Assert.Contains(ours, r => r.RequestType == "SubmitTimesheetWeekCommand");
        Assert.Single(ours, r => r.RequestType == "FinalizeTimesheetDecisionCommand");
        Assert.Equal(before + 1, ours.Count);
        Assert.All(ours, r => Assert.Equal(TimeEntryModule.AuditCategoryValue, r.Category));
    }

    private async Task<TimesheetFinalizationResult> FinalizeDirectlyAsync(Guid weekId)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        using (TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), Tenant))
        {
            return await scope.ServiceProvider.GetRequiredService<ITimesheetFinalizer>().FinalizeAsync(weekId);
        }
    }
}
