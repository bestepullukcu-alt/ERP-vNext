using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.DocumentManagementContract;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-439 — the second half of "Bilgi bekle": the person a waiting task is asking gets the question, and answers it.
///
/// <para>Three people, never two: <see cref="Holder"/> parks the task, <see cref="Asked"/> is named in
/// <c>WaitingOnUserId</c>, and <see cref="Bystander"/> is anybody else. With only two, "the one who may answer" and
/// "the one who may not" collapse into holder-versus-not, and a rule that admitted every non-holder would pass.</para>
///
/// <para>Every lever runs through the REAL handler, the REAL read rule and the REAL provider. Only the stores are
/// doubles; the HTTP + Mongo round trip is <see cref="TaskInquiryHttpMongoTests"/>.</para>
/// </summary>
public sealed class TaskInquiryAnswerTests
{
    private static readonly Guid Holder = TaskTestData.Me;
    private static readonly Guid Asked = TaskTestData.Other;
    private static readonly Guid Bystander = TaskTestData.Rival;
    private const string Question = "Lot 42'nin sertifikası hangi tarihte gelir?";
    private const string Answer = "Cuma günü tedarikçiden gelecek.";

    // ── The answer ──────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// MUTATION TARGET (state return · history · notice). The whole flow in one: the answer lands in the history
    /// in the addressee's own words, the waiting story is cleared, the task goes back to what it was, and the
    /// holder — only the holder — is told.
    /// </summary>
    [Fact]
    public async Task The_addressee_answers_and_the_task_goes_back_to_where_it_was_with_the_answer_in_its_history()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);

        var result = await f.AnswerAsync(Asked, Answer);

        Assert.Equal(204, result.StatusCode);
        Assert.Equal(TaskLifecycle.InProgress, f.Task.Lifecycle);
        Assert.Null(f.Task.WaitingReason);
        Assert.Null(f.Task.WaitingOnUserId);

        var recorded = Assert.Single(f.Tasks.Transitions.Events, e => e.Kind == TaskTransitionKind.InquiryAnswered);
        Assert.Equal(Asked, recorded.ActorUserId);
        Assert.Equal(Answer, recorded.Reason);
        Assert.Equal(TaskLifecycle.Waiting, recorded.FromLifecycle);
        Assert.Equal(TaskLifecycle.InProgress, recorded.ToLifecycle);
        Assert.Equal("inquiryAnswered", TaskTransitionCodes.For(recorded.Kind));

        var notice = Assert.Single(f.Notifications.Notifications, n => n.EventCode == TaskNotificationEvents.InquiryAnswered);
        Assert.Equal([Holder], notice.Candidates);
        Assert.Equal(Asked, notice.ActingUserId);
    }

    /// <summary>
    /// "Waiting'e girmeden önceki durumuna döner" — measured for all three entries the lifecycle matrix allows,
    /// read from the Waiting entry's own <c>FromLifecycle</c>. An Open task stays unstarted and a planned one keeps
    /// its plan: answering a question is not starting the work.
    /// </summary>
    [Theory]
    [InlineData(TaskLifecycle.Open)]
    [InlineData(TaskLifecycle.Planned)]
    [InlineData(TaskLifecycle.InProgress)]
    public async Task The_answer_returns_the_task_to_the_lifecycle_it_was_parked_from(TaskLifecycle parkedFrom)
    {
        var f = new Fixture(parkedFrom);
        await f.InquireAsync(Question, Asked);
        Assert.Equal(TaskLifecycle.Waiting, f.Task.Lifecycle);

        await f.AnswerAsync(Asked, Answer);

        Assert.Equal(parkedFrom, f.Task.Lifecycle);
    }

    [Fact]
    public void With_no_record_of_how_the_wait_began_the_answer_returns_to_Open_which_skips_no_gate()
    {
        var lifecycle = new TaskLifecycleService();

        Assert.Equal(TaskLifecycle.Open, lifecycle.ResolveInquiryReturn(null));
        Assert.Equal(TaskLifecycle.Open, lifecycle.ResolveInquiryReturn(TaskLifecycle.Waiting));
        Assert.Equal(TaskLifecycle.Planned, lifecycle.ResolveInquiryReturn(TaskLifecycle.Planned));
    }

    /// <summary>MUTATION TARGET (fail-closed). A third person answering changes NOTHING — no state, no history, no notice.</summary>
    [Fact]
    public async Task Somebody_the_task_is_not_asking_cannot_answer_and_nothing_changes()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);
        var eventsBefore = f.Tasks.Transitions.Events.Count;
        var noticesBefore = f.Notifications.Notifications.Count;

        var result = await f.AnswerAsync(Bystander, Answer);

        Assert.Equal(403, result.StatusCode);
        Assert.Equal(TaskReasonCodes.InquiryNotAddressee, result.ReasonCode);
        Assert.Equal(TaskLifecycle.Waiting, f.Task.Lifecycle);
        Assert.Equal(Asked, f.Task.WaitingOnUserId);
        Assert.Equal(eventsBefore, f.Tasks.Transitions.Events.Count);
        Assert.Equal(noticesBefore, f.Notifications.Notifications.Count);
    }

    [Fact]
    public async Task The_holder_who_asked_cannot_answer_their_own_question_they_resume_instead()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);

        var result = await f.AnswerAsync(Holder, Answer);

        Assert.Equal(403, result.StatusCode);
        Assert.Equal(TaskReasonCodes.InquiryNotAddressee, result.ReasonCode);
        Assert.Equal(TaskLifecycle.Waiting, f.Task.Lifecycle);
    }

    [Fact]
    public async Task Once_answered_the_same_person_cannot_answer_again()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);
        await f.AnswerAsync(Asked, Answer);

        var second = await f.AnswerAsync(Asked, "Bir de şu var.");

        Assert.Equal(403, second.StatusCode);
        Assert.Single(f.Tasks.Transitions.Events, e => e.Kind == TaskTransitionKind.InquiryAnswered);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_answer_is_refused_and_the_task_keeps_waiting(string answer)
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);

        var result = await f.AnswerAsync(Asked, answer);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(TaskReasonCodes.InquiryAnswerRequired, result.ReasonCode);
        Assert.Equal(TaskLifecycle.Waiting, f.Task.Lifecycle);
    }

    [Fact]
    public async Task An_answer_longer_than_a_description_may_be_is_refused()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);

        var result = await f.AnswerAsync(Asked, new string('x', TaskFieldLimits.MaxDescriptionLength + 1));

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(TaskReasonCodes.InquiryAnswerTooLong, result.ReasonCode);
    }

    [Fact]
    public async Task A_stale_version_is_a_controlled_conflict_and_writes_no_history()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);

        var result = await f.AnswerAsync(Asked, Answer, version: f.Task.Version - 1);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ConcurrencyConflict, result.ReasonCode);
        Assert.DoesNotContain(f.Tasks.Transitions.Events, e => e.Kind == TaskTransitionKind.InquiryAnswered);
    }

    [Fact]
    public async Task A_notification_failure_never_fails_the_answer()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);
        f.Notifications.Throws = true;

        var result = await f.AnswerAsync(Asked, Answer);

        Assert.Equal(204, result.StatusCode);
        Assert.Equal(TaskLifecycle.InProgress, f.Task.Lifecycle);
    }

    // ── The question reaches the person it is for ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Asking_somebody_tells_them_and_nobody_else()
    {
        var f = new Fixture(TaskLifecycle.InProgress);

        await f.InquireAsync(Question, Asked);

        var notice = Assert.Single(f.Notifications.Notifications);
        Assert.Equal(TaskNotificationEvents.InquiryAsked, notice.EventCode);
        Assert.Equal([Asked], notice.Candidates);
        Assert.Equal(Holder, notice.ActingUserId);
    }

    [Fact]
    public async Task A_wait_on_nobody_in_particular_tells_nobody()
    {
        var f = new Fixture(TaskLifecycle.InProgress);

        await f.InquireAsync("Tedarikçiden fiyat bekleniyor.");

        Assert.Empty(f.Notifications.Notifications);
    }

    // ── Withdrawing the question (the existing clear path) ─────────────────────────────────────────────────

    /// <summary>
    /// "Soran kişi soruyu geri çekebilir (mevcut bekleme temizleme yolu)": the holder resumes. The item leaves the
    /// addressee's inbox, the read leg closes, and a late answer is refused.
    /// </summary>
    [Fact]
    public async Task The_holder_withdraws_by_resuming_and_the_question_leaves_the_addressees_inbox()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);
        Assert.Single(await f.BoardOfAsync(Asked));

        Assert.Equal(204, (await f.ResumeAsync()).StatusCode);

        Assert.Empty(await f.BoardOfAsync(Asked));
        Assert.False(await f.ReadPolicyFor(Asked).CanReadAsync(f.Task, Asked, CancellationToken.None));
        Assert.Equal(403, (await f.AnswerAsync(Asked, Answer)).StatusCode);
    }

    // ── The addressee gains NOTHING else ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// MUTATION TARGET (no other authority). "başlat/tamamla/devret yok" — each of the task's own acts, asked by
    /// the addressee while the question stands, is refused by its own handler. Reading is not holding.
    /// </summary>
    [Fact]
    public async Task The_addressee_cannot_start_complete_cancel_reassign_or_return_the_task()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);

        var start = await f.TransitionAsAsync(Asked, TaskLifecycle.InProgress, mayCancelAny: false);
        Assert.Equal(403, start.StatusCode);

        var cancel = await f.TransitionAsAsync(Asked, TaskLifecycle.Cancelled, mayCancelAny: false);
        Assert.Equal(403, cancel.StatusCode);
        Assert.Equal(TaskReasonCodes.CancelNotRequester, cancel.ReasonCode);

        var reassign = await f.ReassignAsAsync(Asked, Bystander);
        Assert.Equal(403, reassign.StatusCode);

        var giveBack = await f.ReturnAsAsync(Asked);
        Assert.Equal(403, giveBack.StatusCode);

        // And nothing moved.
        Assert.Equal(TaskLifecycle.Waiting, f.Task.Lifecycle);
        Assert.Equal(Holder, f.Task.AssigneeUserId);
    }

    [Fact]
    public async Task Complete_is_refused_to_the_addressee_even_once_the_task_is_back_in_progress()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);
        await f.AnswerAsync(Asked, Answer);

        var complete = await f.TransitionAsAsync(Asked, TaskLifecycle.Done, mayCancelAny: false);

        Assert.Equal(403, complete.StatusCode);
        Assert.Equal(DocumentManagementReasonCodes.PermissionDenied, complete.ReasonCode);
        Assert.Equal(TaskLifecycle.InProgress, f.Task.Lifecycle);
    }

    // ── The read rule ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// MUTATION TARGET (only while asking). The addressee reads the task while the question stands; the moment it
    /// is answered the leg closes. A bystander never reads it.
    /// </summary>
    [Fact]
    public async Task The_addressee_reads_the_task_only_while_the_question_stands()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        Assert.False(await f.ReadPolicyFor(Asked).CanReadAsync(f.Task, Asked, CancellationToken.None));

        await f.InquireAsync(Question, Asked);
        Assert.True(await f.ReadPolicyFor(Asked).CanReadAsync(f.Task, Asked, CancellationToken.None));
        Assert.False(await f.ReadPolicyFor(Bystander).CanReadAsync(f.Task, Bystander, CancellationToken.None));

        await f.AnswerAsync(Asked, Answer);
        Assert.False(await f.ReadPolicyFor(Asked).CanReadAsync(f.Task, Asked, CancellationToken.None));
    }

    /// <summary>
    /// MUTATION TARGET (only while waiting — the RULE, not the clearing). The two fields should never part company
    /// (ClearWaiting), and if they ever do the STATE wins: the read leg must not open on the id alone.
    /// </summary>
    [Fact]
    public async Task A_stale_WaitingOnUserId_on_a_task_that_is_no_longer_waiting_admits_nobody()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        f.Task.WaitingOnUserId = Asked;

        Assert.False(TaskInquiryRules.IsAskedOf(f.Task, Asked));
        Assert.False(await f.ReadPolicyFor(Asked).CanReadAsync(f.Task, Asked, CancellationToken.None));
        Assert.Empty(await f.BoardOfAsync(Asked));
    }

    // ── The answer never walks round a gate `resume` asks (independent review, 2026-09-24) ──────────────────

    [Fact]
    public async Task An_answer_into_running_work_that_approval_now_blocks_lands_in_Open_not_InProgress()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);
        f.Task.ApprovalRequired = true;   // switched on while the task waited
        f.Gate.Blocked = true;

        var result = await f.AnswerAsync(Asked, Answer);

        Assert.Equal(204, result.StatusCode);
        Assert.Equal(TaskLifecycle.Open, f.Task.Lifecycle);
        Assert.Contains(f.Gate.Calls, call => call.RequestedTransition == "start");
        // The answer itself still landed — what the gate refuses is RUNNING, not the information.
        Assert.Single(f.Tasks.Transitions.Events, e => e.Kind == TaskTransitionKind.InquiryAnswered);
    }

    [Fact]
    public async Task An_answer_into_running_work_with_an_open_predecessor_lands_in_Open()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        var predecessor = new TaskItem
        {
            TenantId = TaskTestData.Tenant, Title = "Önce bu", AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = Bystander, OrganizationUnitId = f.Task.OrganizationUnitId, Lifecycle = TaskLifecycle.InProgress
        };
        await f.Tasks.CreateAsync(predecessor);
        await f.InquireAsync(Question, Asked);
        await f.Dependencies.CreateAsync(new TaskDependency
        {
            TenantId = TaskTestData.Tenant, TaskItemId = f.Task.Id, DependsOnTaskItemId = predecessor.Id,
            DependencyType = TaskDependencyType.FinishToStart
        });

        await f.AnswerAsync(Asked, Answer);

        Assert.Equal(TaskLifecycle.Open, f.Task.Lifecycle);
    }

    [Fact]
    public async Task An_answer_into_running_work_the_gates_allow_does_not_ask_approval_it_does_not_need()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);

        await f.AnswerAsync(Asked, Answer);

        Assert.Equal(TaskLifecycle.InProgress, f.Task.Lifecycle);
        Assert.Empty(f.Gate.Calls);
    }

    /// <summary>Naming yourself would make you the addressee — the one person who may answer — and a way round `resume`.</summary>
    [Fact]
    public async Task The_holder_cannot_park_the_task_waiting_on_themselves()
    {
        var f = new Fixture(TaskLifecycle.InProgress);

        var result = await f.InquireAsync(Question, Holder);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(TaskReasonCodes.AssigneeInvalid, result.ReasonCode);
        Assert.Equal(TaskLifecycle.InProgress, f.Task.Lifecycle);
        Assert.Null(f.Task.WaitingOnUserId);
    }

    [Fact]
    public async Task Returning_or_reassigning_a_waiting_task_drops_the_question_with_the_wait()
    {
        var returned = new Fixture(TaskLifecycle.InProgress, createdBy: Bystander);
        await returned.InquireAsync(Question, Asked);
        Assert.Equal(204, (await returned.ReturnAsAsync(Holder)).StatusCode);
        Assert.Null(returned.Task.WaitingOnUserId);
        Assert.Null(returned.Task.WaitingReason);
        Assert.Empty(await returned.BoardOfAsync(Asked));

        var reassigned = new Fixture(TaskLifecycle.InProgress);
        await reassigned.InquireAsync(Question, Asked);
        Assert.Equal(204, (await reassigned.ReassignAsAsync(Holder, Bystander)).StatusCode);
        Assert.Null(reassigned.Task.WaitingOnUserId);
        Assert.Empty(await reassigned.BoardOfAsync(Asked));
    }

    [Fact]
    public async Task Being_asked_about_a_subtask_does_not_open_its_parent()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        var parent = new TaskItem
        {
            TenantId = TaskTestData.Tenant, Title = "Parent", AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = Holder, CreatedByUserId = Holder, OrganizationUnitId = f.Task.OrganizationUnitId,
            Lifecycle = TaskLifecycle.InProgress
        };
        await f.Tasks.CreateAsync(parent);
        f.Task.ParentTaskItemId = parent.Id;
        await f.InquireAsync(Question, Asked);

        Assert.True(await f.ReadPolicyFor(Asked).CanReadAsync(f.Task, Asked, CancellationToken.None));
        Assert.False(await f.ReadPolicyFor(Asked).CanReadAsync(parent, Asked, CancellationToken.None));
    }

    // ── The projection ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// MUTATION TARGET (in the addressee's inbox · not the asker's). The question is its OWN item in the
    /// addressee's inbox — the question, who asked, and one action — and nothing of the task beyond that.
    /// </summary>
    [Fact]
    public async Task The_question_is_an_inquiry_item_in_the_addressees_inbox_carrying_only_the_question()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);

        var item = Assert.Single(await f.BoardOfAsync(Asked));

        Assert.Equal(WorkItemContract.IntentInquiry, item.WorkIntent);
        Assert.Equal(f.Task.Id.ToString(), item.Id);
        Assert.Equal(WorkItemContract.StatusPending, item.NormalizedStatus);
        Assert.Equal(WorkItemContract.NotApplicable, item.TaskLifecycle);
        Assert.Null(item.WaitingContext);
        Assert.Equal(Question, item.Summary!.Text);
        Assert.Equal("WorkAggregation_Title_Inquiry", item.Title.Key);
        Assert.Equal(f.Task.Title, item.Title.Args!["title"]);
        Assert.Equal(Holder.ToString(), item.Requester!.Id);
        Assert.Equal("Ali Tufanoğlu", item.Requester.DisplayName);
        Assert.True(item.Assignee!.IsCurrentUser);

        var action = Assert.Single(item.Actions);
        Assert.Equal("answer", action.Code);
        Assert.True(action.Enabled);
        Assert.True(action.RequiresReason);
        Assert.Equal("answer", item.PrimaryActionCode);

        // Being asked a question is not being handed the task.
        Assert.Empty(item.WorkItemCapabilities);
        Assert.Null(item.Checklist);
        Assert.Null(item.Activity);
        Assert.Null(item.Gates);
        Assert.Null(item.BusinessContext);
        Assert.Null(item.Attachments);
    }

    [Fact]
    public async Task The_holder_sees_the_task_waiting_and_no_question_item()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);

        var item = Assert.Single(await f.BoardOfAsync(Holder));

        Assert.Equal("task", item.WorkIntent);
        Assert.Equal(WorkItemContract.StatusWaiting, item.NormalizedStatus);
        Assert.DoesNotContain(item.Actions, a => a.Code == "answer");
    }

    [Fact]
    public async Task A_bystander_is_shown_nothing()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);

        Assert.Empty(await f.BoardOfAsync(Bystander));
    }

    /// <summary>
    /// The common collision: the holder most often needs information from whoever asked for the work. Asked
    /// outranks opened, so the requester gets ONE row — the question — and gets their Outbox row back once it is
    /// answered.
    /// </summary>
    [Fact]
    public async Task A_requester_who_is_asked_sees_one_row_the_question_and_gets_the_outbox_row_back_after_answering()
    {
        var f = new Fixture(TaskLifecycle.InProgress, createdBy: Asked);
        await f.InquireAsync(Question, Asked);

        var asked = Assert.Single(await f.BoardOfAsync(Asked));
        Assert.Equal(WorkItemContract.IntentInquiry, asked.WorkIntent);

        await f.AnswerAsync(Asked, Answer);

        var outbox = Assert.Single(await f.BoardOfAsync(Asked));
        Assert.Equal("task", outbox.WorkIntent);
        Assert.Equal(WorkItemContract.ViewerRelationInitiator, outbox.ViewerRelation);
    }

    [Fact]
    public async Task Reading_the_question_by_id_projects_exactly_what_the_list_shows()
    {
        var f = new Fixture(TaskLifecycle.InProgress, createdBy: Asked);
        await f.InquireAsync(Question, Asked);

        var fromList = Assert.Single(await f.BoardOfAsync(Asked));
        var byId = await f.Provider().GetWorkItemAsync(f.Task, f.Actor(Asked), CancellationToken.None);

        Assert.Equal(fromList.WorkIntent, byId.WorkIntent);
        Assert.Equal(fromList.Summary, byId.Summary);
        Assert.Equal(fromList.Actions.Select(a => a.Code), byId.Actions.Select(a => a.Code));
    }

    [Fact]
    public async Task Without_read_the_answer_action_is_shown_disabled_and_says_why()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);

        var item = Assert.Single(await f.BoardOfAsync(Asked, granted: []));

        var action = Assert.Single(item.Actions);
        Assert.False(action.Enabled);
        Assert.Equal(WorkAggregationReasonCodes.PermissionDenied, action.DisabledReasonCode);
    }

    /// <summary>"soranın kartında cevap geldiğinde 'X cevapladı' görünür" — and stops once the task is parked again.</summary>
    [Fact]
    public async Task After_the_answer_the_holders_item_says_who_answered_and_what_until_it_is_parked_again()
    {
        var f = new Fixture(TaskLifecycle.InProgress);
        await f.InquireAsync(Question, Asked);
        await f.AnswerAsync(Asked, Answer);

        var item = Assert.Single(await f.BoardOfAsync(Holder));
        Assert.NotNull(item.InquiryAnswer);
        Assert.Equal(Asked.ToString(), item.InquiryAnswer!.AnsweredBy!.Id);
        Assert.Equal("Ayşe Yılmaz", item.InquiryAnswer.AnsweredBy.DisplayName);
        Assert.Equal(Answer, item.InquiryAnswer.Answer!.Text);

        // The history carries it too, under its own code.
        Assert.Contains(item.Activity!, entry => entry.Event?.Code == "inquiryAnswered");

        await f.InquireAsync("Bir soru daha.", Asked);
        Assert.Null(Assert.Single(await f.BoardOfAsync(Holder)).InquiryAnswer);
    }

    // ── The dispatch seam ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Dispatching_answer_sends_the_answer_command_with_the_answer_text_and_the_version()
    {
        var mediator = new CapturingMediator();
        var dispatcher = new TaskWorkItemActionDispatcher(mediator);
        var itemId = Guid.NewGuid();

        await dispatcher.DispatchAsync(
            new WorkItemActionDispatchRequest(
                itemId, "answer", new WorkItemActionPayloadDto(ExpectedVersion: 7, Answer: Answer),
                new WorkItemActor(Asked, false, new HashSet<string> { TaskPermissions.Read }), "corr"),
            CancellationToken.None);

        var command = Assert.IsType<AnswerInquiryCommand>(mediator.Sent);
        Assert.Equal(itemId, command.Id);
        Assert.Equal(7, command.Request.ExpectedVersion);
        Assert.Equal(Answer, command.Request.Answer);
        Assert.Equal(TaskPermissions.Read, dispatcher.RequiredPermission("answer"));
    }

    [Fact]
    public async Task Dispatching_answer_without_an_answer_is_refused_before_it_reaches_the_task()
    {
        var mediator = new CapturingMediator();
        var dispatcher = new TaskWorkItemActionDispatcher(mediator);

        var result = await dispatcher.DispatchAsync(
            new WorkItemActionDispatchRequest(
                Guid.NewGuid(), "answer", new WorkItemActionPayloadDto(ExpectedVersion: 1, Reason: Answer),
                new WorkItemActor(Asked, false, new HashSet<string> { TaskPermissions.Read }), "corr"),
            CancellationToken.None);

        Assert.Equal(WorkItemActionReasonCodes.PayloadInvalid, result.ReasonCode);
        Assert.Null(mediator.Sent);
    }

    // ── harness ────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class Fixture
    {
        private readonly IReadOnlyList<Position> _positions;
        private readonly IReadOnlyList<OrganizationUnit> _units;
        private readonly IReadOnlyList<PositionAssignment> _seats;

        public Fixture(TaskLifecycle lifecycle, Guid? createdBy = null)
        {
            var unit = new OrganizationUnit
            {
                TenantId = TaskTestData.Tenant, Code = "OU-1", Name = "Kalite",
                LegalEntityId = Guid.NewGuid(), Status = OrgUnitStatus.Active
            };
            var position = new Position
            {
                TenantId = TaskTestData.Tenant, Code = "POS-1", Name = "Uzman",
                OrganizationUnitId = unit.Id, Status = PositionStatus.Active
            };
            _units = [unit];
            _positions = [position];
            // All three hold a seat, so each is assignable — nobody is refused for a reason other than the rule.
            _seats = new[] { Holder, Asked, Bystander }
                .Select(user => new PositionAssignment
                {
                    TenantId = TaskTestData.Tenant, PositionId = position.Id,
                    UserId = user, EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30)
                })
                .ToList();

            Task = new TaskItem
            {
                TenantId = TaskTestData.Tenant,
                Title = "Lot 42 serbest bırakma",
                AssignmentTarget = TaskAssignmentTarget.Person,
                AssigneeUserId = Holder,
                CreatedByUserId = createdBy ?? Holder,
                OrganizationUnitId = unit.Id,
                Lifecycle = lifecycle,
                Version = 1
            };
            Task.CloseAcceptanceGate(Holder);
            Tasks = new FakeTaskItemRepository(Task);
        }

        public TaskItem Task { get; }

        public FakeTaskItemRepository Tasks { get; }

        public FakeTaskNotificationService Notifications { get; } = new();

        /// <summary>The MOD-0023 gate `resume` asks — and, since the review, an answer back into running work too.</summary>
        public FakeWorkflowTransitionGate Gate { get; } = new();

        public FakeTaskDependencyRepository Dependencies { get; } = new();

        public Task<Response<NoContent>> InquireAsync(string reason, Guid? waitingOn = null)
            => new InquireTaskItemHandler(
                    Tasks, new TaskLifecycleService(), new FakeCurrentUserContext(Holder),
                    new FakePositionAssignmentRepository([.. _seats]),
                    new FakePositionRepository([.. _positions]),
                    new FakeOrganizationUnitRepository([.. _units]),
                    Notifications,
                    NullLogger<InquireTaskItemHandler>.Instance)
                .Handle(
                    new InquireTaskItemCommand(Task.Id, new InquireTaskItemRequest(Task.Version, reason, waitingOn), "corr"),
                    CancellationToken.None);

        public Task<Response<NoContent>> AnswerAsync(Guid actor, string answer, int? version = null)
            => new AnswerInquiryHandler(
                    Tasks, Tasks.Transitions, new TaskLifecycleService(), new FakeCurrentUserContext(actor),
                    Gate, Dependencies, Notifications, NullLogger<AnswerInquiryHandler>.Instance)
                .Handle(
                    new AnswerInquiryCommand(Task.Id, new AnswerInquiryRequest(version ?? Task.Version, answer), "corr"),
                    CancellationToken.None);

        public Task<Response<NoContent>> ResumeAsync()
            => TransitionAsAsync(Holder, TaskLifecycle.InProgress, mayCancelAny: false);

        public Task<Response<NoContent>> TransitionAsAsync(Guid actor, TaskLifecycle target, bool mayCancelAny)
            => new TransitionTaskItemHandler(
                    Tasks, new TaskLifecycleService(), new FakeCurrentUserContext(actor),
                    new FakeChecklistRunRepository(), new TaskChecklistService(),
                    new FakeWorkflowTransitionGate(), new FakeTaskDependencyRepository(),
                    new FakeTaskTypeRepository(), new FakeTaskNotificationService(),
                    new TaskFieldDefinitionService(
                        new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
                    new FakeTaskAttachmentRepository(),
                    NullLogger<TransitionTaskItemHandler>.Instance)
                .Handle(
                    new TransitionTaskItemCommand(
                        Task.Id, target, new TaskTransitionRequest(Task.Version, null, null), "corr",
                        ActorMayCancelAnyTask: mayCancelAny),
                    CancellationToken.None);

        public Task<Response<NoContent>> ReassignAsAsync(Guid actor, Guid newAssignee)
            => new ReassignTaskItemHandler(
                    Tasks, new FakeTaskAssignmentRepository(), TaskAssignmentGuards.AdmitAll(),
                    new FakeCurrentUserContext(actor), new FakeTenantContext(TaskTestData.Tenant))
                .Handle(
                    new ReassignTaskItemCommand(
                        Task.Id, new ReassignTaskItemRequest(Task.Version, newAssignee, "Başkası baksın."), "corr"),
                    CancellationToken.None);

        public Task<Response<NoContent>> ReturnAsAsync(Guid actor)
            => new ReturnTaskItemHandler(
                    Tasks, new FakeTaskAssignmentRepository(), new FakeCurrentUserContext(actor),
                    new FakeTenantContext(TaskTestData.Tenant))
                .Handle(
                    new ReturnTaskItemCommand(Task.Id, new ReturnTaskItemRequest(Task.Version, "Benim işim değil."), "corr"),
                    CancellationToken.None);

        public ITaskReadAccessPolicy ReadPolicyFor(Guid caller)
            => new TaskReadAccessPolicy(
                Tasks, new FakeTaskWatcherRepository(), new FakeTaskNotificationService(),
                new FakeOrganizationUnitRepository(), new EmptyScope(), new FakeTaskTeamResolver(),
                TaskActors.None(), new FakeCurrentUserContext(caller));

        public WorkItemActor Actor(Guid user, IReadOnlyCollection<string>? granted = null)
            => new(user, IsPlatformActor: false,
                new HashSet<string>(granted ?? [TaskPermissions.Read, TaskPermissions.Update, TaskPermissions.Complete,
                    TaskPermissions.Cancel, TaskPermissions.Claim, TaskPermissions.Assign]));

        public TaskWorkItemProvider Provider()
            => new(
                Tasks,
                new FakePositionAssignmentRepository([.. _seats]),
                new TaskLifecycleService(),
                new TaskAssignmentResolver(),
                new FakeUserDisplayNameResolver(
                    (Holder, TaskTestData.MeDisplayName), (Asked, "Ayşe Yılmaz"), (Bystander, "Can Demir")),
                new FakeChecklistRunRepository(),
                new FakeTaskApprovalService(),
                new FakeTaskDependencyRepository(),
                new FakeTaskCommentRepository(),
                Tasks.Transitions,
                new FakeTaskPersonalOverlayRepository(),
                new FakeTaskWatcherRepository(),
                TaskActors.PermitAll(),
                new FakePositionRepository([.. _positions]),
                new FakeOrganizationUnitRepository([.. _units]),
                SlaForTests.Real(),
                new FakeTaskFieldDefinitionRepository(), new FakeTaskTypeRepository());

        public async Task<IReadOnlyList<WorkItemProjectionDto>> BoardOfAsync(
            Guid user, IReadOnlyCollection<string>? granted = null)
            => (await Provider().GetWorkItemsAsync(Actor(user, granted), CancellationToken.None))
                .Where(item => item.Id == Task.Id.ToString())
                .ToList();
    }

    private sealed class EmptyScope : ITaskAssignmentScopeResolver
    {
        public Task<TaskAssignmentScope> ResolveAsync(CancellationToken ct) => Task.FromResult(TaskAssignmentScope.Empty);
    }

    /// <summary>Records the one request the dispatcher sends and answers 204.</summary>
    private sealed class CapturingMediator : IMediator
    {
        public object? Sent { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            Sent = request;
            return Task.FromResult((TResponse)(object)Response<NoContent>.Success(204, "corr"));
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => throw new NotSupportedException();
    }
}
