using System.Text.RegularExpressions;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WC-1 — the lifecycle event log: that every act is recorded, that the record says enough to be worth having,
/// and that the projection publishes it.
///
/// <para><b>What this round exists to fix.</b> The work-item contract has declared two activity kinds since it was
/// written, and MOD-0024 emitted one. The DTO said why in as many words: there was no lifecycle event log to draw
/// from, and a timeline derived from the four timestamps a task carries would silently omit accept, plan, claim,
/// release and inquire — a partial history read as a complete one. Nothing here derives anything. The acts are
/// recorded as they happen and read back as they were written.</para>
/// </summary>
public sealed class TaskTransitionLogTests
{
    private static readonly Guid PositionId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid UnitId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    // ── THE COVERAGE GUARD ──────────────────────────────────────────────────────────────────────────────────
    //
    // The most important test of the round, and the reason the others can be ordinary. It is DERIVED from the
    // vocabulary rather than written alongside it: a transition kind added tomorrow has no scenario here, and this
    // fails until somebody proves it is recorded.

    /// <summary>
    /// Every kind in the vocabulary is driven by a real handler here, and every one of those handlers records the
    /// kind it claims to.
    ///
    /// <para>Adding a value to <see cref="TaskTransitionKind"/> turns this red immediately — not with a vague
    /// complaint, but naming the kind nobody exercised.</para>
    /// </summary>
    [Fact]
    public async Task EVERY_transition_kind_is_produced_by_a_real_handler()
    {
        var unexercised = new List<TaskTransitionKind>();
        var misreported = new List<string>();

        foreach (var kind in Enum.GetValues<TaskTransitionKind>())
        {
            if (kind == TaskTransitionKind.Unknown)
            {
                // Unknown is what a FORGOTTEN declaration produces; no handler may ever drive it deliberately.
                // The two tests below are the ones that hold that line.
                continue;
            }

            var scenario = Scenarios.GetValueOrDefault(kind);
            if (scenario is null)
            {
                unexercised.Add(kind);
                continue;
            }

            var recorded = await scenario();
            if (recorded is null)
            {
                misreported.Add($"{kind}: the handler wrote no entry at all");
            }
            else if (recorded.Kind != kind)
            {
                misreported.Add($"{kind}: the handler recorded {recorded.Kind} instead");
            }
        }

        Assert.True(
            unexercised.Count == 0,
            "These transition kinds have no scenario, so nothing proves a handler records them: "
            + string.Join(", ", unexercised)
            + ". Add one to Scenarios — a transition that is not recorded is the partial history WC-1 exists to stop.");

        Assert.True(misreported.Count == 0, string.Join(" | ", misreported));
    }

    /// <summary>
    /// No handler in the module produces <see cref="TaskTransitionKind.Unknown"/>.
    ///
    /// <para>Unknown is the fail-SAFE, not the fail-silent: a writer that moves a task without declaring what it is
    /// doing still gets its act recorded, because losing the entry would restore the exact hole this log closes.
    /// This is what makes that safety net loud. Every scenario above runs again and the whole log is swept.</para>
    /// </summary>
    [Fact]
    public async Task NO_handler_moves_a_task_without_saying_what_it_is_doing()
    {
        foreach (var (kind, scenario) in Scenarios)
        {
            var recorded = await scenario();
            Assert.False(
                recorded?.Kind == TaskTransitionKind.Unknown,
                $"The {kind} path moved a task and declared nothing, so its history reads 'something changed'.");
        }
    }

    /// <summary>
    /// The STRUCTURAL half: no handler may move a task's lifecycle without declaring the act in the same method.
    ///
    /// <para>The scenario table above proves the transitions we know about. This proves the ones we do not: it
    /// reads the handler SOURCE, finds every method that assigns <c>Lifecycle</c>, and requires a
    /// <c>Declare(...)</c> beside it. A new transition written next month is caught the moment it is typed, with
    /// no test of its own and nobody remembering this file exists.</para>
    ///
    /// <para>It is a source scan because the alternative — a private setter — would have rewritten a hundred and
    /// forty-seven test arrangements to buy the same sentence.</para>
    /// </summary>
    [Fact]
    public void EVERY_handler_that_moves_a_lifecycle_declares_what_it_is_doing()
    {
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(HandlerDirectory, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);

            // Split on member declarations, so each chunk is roughly one method body. Rough is enough: a
            // declaration in a DIFFERENT method than the assignment is exactly what this is meant to catch.
            foreach (var member in Regex.Split(source, @"\n    (?:public|private|internal|protected)\s"))
            {
                var moves = Regex.IsMatch(member, @"^\s*\w+\.Lifecycle\s*=", RegexOptions.Multiline);
                if (moves && !member.Contains(".Declare("))
                {
                    var name = Regex.Match(member, @"^[^\(\r\n]*").Value.Trim();
                    offenders.Add($"{Path.GetFileName(file)} → {name}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These handlers move a task's lifecycle without declaring the transition, so the event log records "
            + "them as Unknown: " + string.Join(", ", offenders));
    }

    // ── WHAT A RECORD HAS TO SAY ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_record_says_who_when_and_between_which_two_states()
    {
        var task = AssignedTask(TaskLifecycle.InProgress);
        var repository = new FakeTaskItemRepository(task);
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        await Transition(repository, task, TaskLifecycle.Done);

        var recorded = Assert.Single(repository.Transitions.Events);
        Assert.Equal(TaskTransitionKind.Completed, recorded.Kind);
        Assert.Equal(TaskTestData.Me, recorded.ActorUserId);
        Assert.Equal(TaskLifecycle.InProgress, recorded.FromLifecycle);
        Assert.Equal(TaskLifecycle.Done, recorded.ToLifecycle);
        Assert.InRange(recorded.CreatedAt, before, DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task A_record_carries_the_reason_in_the_actors_own_words()
    {
        var task = AssignedTask(TaskLifecycle.InProgress);
        var repository = new FakeTaskItemRepository(task);

        await new InquireTaskItemHandler(
                repository, new TaskLifecycleService(), new FakeCurrentUserContext(TaskTestData.Me))
            .Handle(
                new InquireTaskItemCommand(
                    task.Id,
                    new InquireTaskItemRequest(task.Version, "Waiting on the lab certificate"),
                    "corr"),
                CancellationToken.None);

        var recorded = Assert.Single(repository.Transitions.Events);
        Assert.Equal(TaskTransitionKind.Waiting, recorded.Kind);
        // WaitingReason is CLEARED when the task resumes, so without this copy nobody could ever answer "what was
        // this blocked on in March".
        Assert.Equal("Waiting on the lab certificate", recorded.Reason);
    }

    [Fact]
    public async Task A_return_and_a_reassignment_to_the_requester_are_told_apart()
    {
        /*
         * The whole reason the kind is DECLARED rather than inferred from the document. Both acts leave an
         * identical task behind: same new holder, same reopened gate, same rewound lifecycle. A diff cannot tell
         * them apart, and a history that called a refusal a reassignment would misattribute the decision.
         */
        var returned = AssignedTask(TaskLifecycle.Open);
        var returnRepository = new FakeTaskItemRepository(returned);
        await Return(returnRepository, returned, "Wrong team");

        var reassigned = AssignedTask(TaskLifecycle.Open);
        var reassignRepository = new FakeTaskItemRepository(reassigned);
        await Reassign(reassignRepository, reassigned, TaskTestData.Rival, "Handing this to the requester");

        Assert.Equal(TaskTransitionKind.Returned, Assert.Single(returnRepository.Transitions.Events).Kind);
        Assert.Equal(TaskTransitionKind.Reassigned, Assert.Single(reassignRepository.Transitions.Events).Kind);

        // …and the two really did leave the same document behind, or the paragraph above is not describing this test.
        Assert.Equal(
            returnRepository.Items.Single().AssigneeUserId,
            reassignRepository.Items.Single().AssigneeUserId);
    }

    [Fact]
    public async Task Resuming_is_recorded_as_resuming_rather_than_as_starting()
    {
        // The target alone does not name the act: reaching InProgress from Waiting is picking work back up, and
        // reaching it from Planned is beginning it. Two different sentences for whoever reads the history.
        var waiting = AssignedTask(TaskLifecycle.Waiting);
        var waitingRepository = new FakeTaskItemRepository(waiting);
        await Transition(waitingRepository, waiting, TaskLifecycle.InProgress);

        var planned = AssignedTask(TaskLifecycle.Planned);
        var plannedRepository = new FakeTaskItemRepository(planned);
        await Transition(plannedRepository, planned, TaskLifecycle.InProgress);

        Assert.Equal(TaskTransitionKind.Resumed, Assert.Single(waitingRepository.Transitions.Events).Kind);
        Assert.Equal(TaskTransitionKind.Started, Assert.Single(plannedRepository.Transitions.Events).Kind);
    }

    [Fact]
    public async Task Accepting_a_PLANNED_task_is_recorded_even_though_nothing_visible_moves()
    {
        /*
         * BL-042's shape, in the log. Accepting a planned task changes neither the lifecycle nor the holder — only
         * the acceptance mark — so a diff watching the two obvious fields would drop accept from the history of
         * exactly the tasks whose acceptance was worth recording.
         */
        var task = AssignedTask(TaskLifecycle.Planned);
        var repository = new FakeTaskItemRepository(task);

        await Accept(repository, task);

        var recorded = Assert.Single(repository.Transitions.Events);
        Assert.Equal(TaskTransitionKind.Accepted, recorded.Kind);
        Assert.Equal(TaskLifecycle.Planned, recorded.FromLifecycle);
        Assert.Equal(TaskLifecycle.Planned, recorded.ToLifecycle);
    }

    [Fact]
    public async Task A_write_that_loses_its_race_records_nothing()
    {
        /*
         * The history follows the COMMIT, never the intent. Two people press "Üzerime al" at the same instant;
         * exactly one write lands, and the loser must leave no trace — a log showing two people claiming one task
         * would be worse than no log.
         */
        var task = PooledTask();
        var repository = new FakeTaskItemRepository(task) { ForcedUpdateConflicts = 1 };

        var response = await Claim(repository, task);

        Assert.Equal(409, response.StatusCode);
        Assert.Empty(repository.Transitions.Events);
    }

    [Fact]
    public async Task An_ordinary_edit_records_nothing()
    {
        /*
         * This is a LIFECYCLE log, not a field-level audit trail. Retitling a task, moving its due date or ticking
         * a checklist item moves none of the three watched fields and writes no entry — otherwise the six entries
         * that tell the task's story would be buried under sixty that do not. (Field-level auditing is a separate
         * concern with a separate owner; see the deferred item raised alongside this round.)
         */
        var task = AssignedTask(TaskLifecycle.InProgress);
        var repository = new FakeTaskItemRepository(task);

        var stored = await repository.GetByIdAsync(task.Id);
        stored!.Title = "A better title";
        stored.DueAt = DateTimeOffset.UtcNow.AddDays(3);
        await repository.UpdateAsync(stored, stored.Version);

        Assert.Empty(repository.Transitions.Events);
    }

    // ── THE WIRE ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EVERY_transition_kind_has_a_wire_code()
    {
        // Derived from the enum, like the coverage guard: a kind added without a code would otherwise throw at
        // projection time, on a live screen, for one task.
        var missing = Enum.GetValues<TaskTransitionKind>()
            .Where(kind => !TaskTransitionCodes.All.ContainsKey(kind))
            .ToList();

        Assert.True(missing.Count == 0, "No wire code for: " + string.Join(", ", missing));

        // lowerCamel, like every other vocabulary on this contract. PascalCase here would be the enum leaking onto
        // the wire — the defect this module has shipped twice.
        Assert.All(TaskTransitionCodes.All.Values, code => Assert.Matches("^[a-z][A-Za-z]*$", code));
    }

    [Fact]
    public async Task The_projection_publishes_events_and_comments_in_ONE_stream_newest_first()
    {
        var task = AssignedTask(TaskLifecycle.InProgress);
        var repository = new FakeTaskItemRepository(task);
        var comments = new FakeTaskCommentRepository();

        await Transition(repository, task, TaskLifecycle.Done);
        await comments.CreateAsync(new TaskComment
        {
            TenantId = TaskTestData.Tenant,
            TaskItemId = task.Id,
            Text = "Signed off with the sponsor",
            AuthorUserId = TaskTestData.Me,
            AuthorDisplayName = "Diten Admin"
        });

        var item = await Project(repository, comments);
        var activity = item.Activity!;

        Assert.Equal(2, activity.Count);
        // One feed, two kinds — what happened and what someone said about it, read together.
        Assert.Contains(activity, entry => entry.Kind == "comment");
        Assert.Contains(activity, entry => entry.Kind == "event");

        // Newest first, the order the composer at the top of the feed assumes.
        Assert.True(
            activity[0].At >= activity[1].At,
            "The feed is not newest-first, so the composer sits above older entries than the ones below it.");
    }

    [Fact]
    public async Task An_event_carries_codes_rather_than_a_sentence()
    {
        var task = AssignedTask(TaskLifecycle.Open);
        var repository = new FakeTaskItemRepository(task);

        await Plan(repository, task);

        var item = await Project(repository, new FakeTaskCommentRepository());
        var entry = Assert.Single(item.Activity!, e => e.Kind == "event");

        // No text: the sentence is built client-side in the reader's language. Shipping one here would ship one
        // language to seven.
        Assert.Null(entry.Text);
        Assert.NotNull(entry.Event);
        Assert.Equal("planned", entry.Event!.Code);
        Assert.Equal(TaskLifecycle.Open.ToString(), entry.Event.From);
        Assert.Equal(TaskLifecycle.Planned.ToString(), entry.Event.To);
        // …and the actor is NAMED, resolved live from the page's batched directory read.
        Assert.Equal(TaskTestData.MeDisplayName, entry.Actor);
    }

    [Fact]
    public async Task A_comment_still_carries_its_text_and_its_snapshotted_author()
    {
        // Non-vacuity for the change above: publishing events must not have quietly reshaped the half that worked.
        var task = AssignedTask(TaskLifecycle.InProgress);
        var comments = new FakeTaskCommentRepository();
        await comments.CreateAsync(new TaskComment
        {
            TenantId = TaskTestData.Tenant,
            TaskItemId = task.Id,
            Text = "Chased the supplier again",
            AuthorUserId = TaskTestData.Me,
            AuthorDisplayName = "Ayşe Yılmaz"
        });

        var item = await Project(new FakeTaskItemRepository(task), comments);
        var entry = Assert.Single(item.Activity!);

        Assert.Equal("comment", entry.Kind);
        Assert.Equal("Chased the supplier again", entry.Text);
        // The name AS RECORDED, not re-resolved — a comment is a quotation.
        Assert.Equal("Ayşe Yılmaz", entry.Actor);
        Assert.Null(entry.Event);
    }

    [Fact]
    public async Task A_task_written_before_the_log_existed_publishes_no_history_and_invents_none()
    {
        /*
         * THE BACKFILL DECISION, asserted rather than described in a document.
         *
         * Tasks that existed before WC-1 have no recorded past, and there is nothing honest to reconstruct it
         * from — deriving one from created/started/completed is the precise move the DTO refused. So the feed for
         * such a task carries its comments and NOTHING else, and in particular no `created` event.
         *
         * That absence is the signal the screen reads: every task written from WC-1 onwards opens its log with
         * `created`, so a feed without one is a task older than the log, and the client says so in one quiet line
         * instead of presenting a hole as a complete story.
         */
        var legacy = AssignedTask(TaskLifecycle.InProgress);
        legacy.StartAt = DateTimeOffset.UtcNow.AddDays(-9);
        legacy.CompletedAt = DateTimeOffset.UtcNow.AddDays(-2);

        var item = await Project(new FakeTaskItemRepository(legacy), new FakeTaskCommentRepository());

        Assert.Empty(item.Activity!);
        Assert.DoesNotContain(item.Activity!, entry => entry.Event?.Code == "created");
    }

    [Fact]
    public async Task A_task_created_from_now_on_opens_its_history_with_created()
    {
        // The other half of the sentence above: the marker has to actually be there, or "no created event" would
        // mean nothing and every task would read as legacy.
        var repository = new FakeTaskItemRepository();

        var response = await Create(repository);

        Assert.Equal(201, response.StatusCode);
        var recorded = Assert.Single(repository.Transitions.Events);
        Assert.Equal(TaskTransitionKind.Created, recorded.Kind);
        Assert.Equal(TaskTestData.Me, recorded.ActorUserId);
    }

    // ── SCENARIOS ───────────────────────────────────────────────────────────────────────────────────────────
    //
    // One real handler per kind. Each returns the entry the handler wrote, or null if it wrote none.

    private static readonly IReadOnlyDictionary<TaskTransitionKind, Func<Task<TaskTransition?>>> Scenarios =
        new Dictionary<TaskTransitionKind, Func<Task<TaskTransition?>>>
        {
            [TaskTransitionKind.Created] = async () =>
            {
                var repository = new FakeTaskItemRepository();
                await Create(repository);
                return Last(repository);
            },
            [TaskTransitionKind.Accepted] = async () =>
            {
                var task = AssignedTask(TaskLifecycle.Open);
                var repository = new FakeTaskItemRepository(task);
                await Accept(repository, task);
                return Last(repository);
            },
            [TaskTransitionKind.Planned] = async () =>
            {
                var task = AssignedTask(TaskLifecycle.Open);
                var repository = new FakeTaskItemRepository(task);
                await Plan(repository, task);
                return Last(repository);
            },
            [TaskTransitionKind.Started] = async () =>
            {
                var task = AssignedTask(TaskLifecycle.Planned);
                var repository = new FakeTaskItemRepository(task);
                await Transition(repository, task, TaskLifecycle.InProgress);
                return Last(repository);
            },
            [TaskTransitionKind.Resumed] = async () =>
            {
                var task = AssignedTask(TaskLifecycle.Waiting);
                var repository = new FakeTaskItemRepository(task);
                await Transition(repository, task, TaskLifecycle.InProgress);
                return Last(repository);
            },
            [TaskTransitionKind.Waiting] = async () =>
            {
                var task = AssignedTask(TaskLifecycle.InProgress);
                var repository = new FakeTaskItemRepository(task);
                await new InquireTaskItemHandler(
                        repository, new TaskLifecycleService(), new FakeCurrentUserContext(TaskTestData.Me))
                    .Handle(
                        new InquireTaskItemCommand(
                            task.Id, new InquireTaskItemRequest(task.Version, "Blocked on procurement"), "corr"),
                        CancellationToken.None);
                return Last(repository);
            },
            [TaskTransitionKind.SubmittedForReview] = async () =>
            {
                var task = AssignedTask(TaskLifecycle.InProgress);
                task.ReviewRequired = true;
                var repository = new FakeTaskItemRepository(task);
                await new SubmitTaskForReviewHandler(
                        repository,
                        new TaskLifecycleService(),
                        new FakeCurrentUserContext(TaskTestData.Me),
                        new FakeTaskReviewService(),
                        new FakeTaskApprovalService(),
                        NullLogger<SubmitTaskForReviewHandler>.Instance)
                    .Handle(
                        new SubmitTaskForReviewCommand(
                            task.Id, new TaskTransitionRequest(task.Version, null, null), "corr"),
                        CancellationToken.None);
                return Last(repository);
            },
            [TaskTransitionKind.ReviewCancelled] = async () =>
            {
                var task = AssignedTask(TaskLifecycle.PendingReview);
                task.ReviewRequired = true;
                task.ReviewWorkflowInstanceId = Guid.NewGuid();
                var repository = new FakeTaskItemRepository(task);
                await UpdateHandler(repository).Handle(
                    new UpdateTaskItemCommand(task.Id, EditTurningReviewOff(task), "corr"),
                    CancellationToken.None);
                return Last(repository);
            },
            [TaskTransitionKind.Completed] = async () =>
            {
                var task = AssignedTask(TaskLifecycle.InProgress);
                var repository = new FakeTaskItemRepository(task);
                await Transition(repository, task, TaskLifecycle.Done);
                return Last(repository);
            },
            [TaskTransitionKind.Cancelled] = async () =>
            {
                var task = AssignedTask(TaskLifecycle.InProgress);
                task.CreatedByUserId = TaskTestData.Me;   // only the requester may cancel
                var repository = new FakeTaskItemRepository(task);
                await Transition(repository, task, TaskLifecycle.Cancelled);
                return Last(repository);
            },
            [TaskTransitionKind.Claimed] = async () =>
            {
                var task = PooledTask();
                var repository = new FakeTaskItemRepository(task);
                await Claim(repository, task);
                return Last(repository);
            },
            [TaskTransitionKind.Released] = async () =>
            {
                var task = PooledTask();
                task.AssigneeUserId = TaskTestData.Me;
                var repository = new FakeTaskItemRepository(task);
                await new ReleaseTaskItemHandler(
                        repository,
                        new FakeTaskAssignmentRepository(),
                        new FakeCurrentUserContext(TaskTestData.Me),
                        new FakeTenantContext(TaskTestData.Tenant))
                    .Handle(
                        new ReleaseTaskItemCommand(
                            task.Id, new TaskTransitionRequest(task.Version, null, null), "corr"),
                        CancellationToken.None);
                return Last(repository);
            },
            [TaskTransitionKind.Reassigned] = async () =>
            {
                var task = AssignedTask(TaskLifecycle.Open);
                var repository = new FakeTaskItemRepository(task);
                await Reassign(repository, task, TaskTestData.Other, "Better fit");
                return Last(repository);
            },
            [TaskTransitionKind.Returned] = async () =>
            {
                var task = AssignedTask(TaskLifecycle.Open);
                var repository = new FakeTaskItemRepository(task);
                await Return(repository, task, "Not my remit");
                return Last(repository);
            }
        };

    private static TaskTransition? Last(FakeTaskItemRepository repository)
        => repository.Transitions.Events.LastOrDefault();

    // ── DRIVERS ─────────────────────────────────────────────────────────────────────────────────────────────

    private static Task<Response<NoContent>> Accept(FakeTaskItemRepository tasks, TaskItem task)
        => new AcceptTaskItemHandler(
                tasks,
                new FakeTaskAssignmentRepository(),
                new FakeCurrentUserContext(TaskTestData.Me),
                new FakeTenantContext(TaskTestData.Tenant))
            .Handle(
                new AcceptTaskItemCommand(task.Id, new TaskTransitionRequest(task.Version, null, null), "corr"),
                CancellationToken.None);

    private static Task<Response<NoContent>> Plan(FakeTaskItemRepository tasks, TaskItem task)
        => new PlanTaskItemHandler(tasks, new TaskLifecycleService(), new FakeCurrentUserContext(TaskTestData.Me))
            .Handle(
                new PlanTaskItemCommand(
                    task.Id,
                    new PlanTaskItemRequest(task.Version, DateTimeOffset.UtcNow.AddDays(2)),
                    "corr"),
                CancellationToken.None);

    private static Task<Response<NoContent>> Transition(
        FakeTaskItemRepository tasks,
        TaskItem task,
        TaskLifecycle target)
        => new TransitionTaskItemHandler(
                tasks,
                new TaskLifecycleService(),
                new FakeCurrentUserContext(TaskTestData.Me),
                new FakeChecklistRunRepository(),
                new TaskChecklistService(),
                new FakeWorkflowTransitionGate(),
                new FakeTaskDependencyRepository(),
                new FakeTaskNotificationService(),
                NullLogger<TransitionTaskItemHandler>.Instance)
            .Handle(
                new TransitionTaskItemCommand(
                    task.Id, target, new TaskTransitionRequest(task.Version, null, null), "corr"),
                CancellationToken.None);

    private static Task<Response<NoContent>> Claim(FakeTaskItemRepository tasks, TaskItem task)
        => new ClaimTaskItemHandler(
                tasks,
                new FakeTaskAssignmentRepository(),
                new FakePositionAssignmentRepository(Holder(TaskTestData.Me)),
                new FakeCurrentUserContext(TaskTestData.Me),
                new FakeTenantContext(TaskTestData.Tenant),
                new FakeTaskNotificationService(),
                NullLogger<ClaimTaskItemHandler>.Instance)
            .Handle(
                new ClaimTaskItemCommand(task.Id, new ClaimTaskItemRequest(task.Version), "corr"),
                CancellationToken.None);

    private static Task<Response<NoContent>> Return(FakeTaskItemRepository tasks, TaskItem task, string reason)
        => new ReturnTaskItemHandler(
                tasks,
                new FakeTaskAssignmentRepository(),
                new FakeCurrentUserContext(TaskTestData.Me),
                new FakeTenantContext(TaskTestData.Tenant))
            .Handle(
                new ReturnTaskItemCommand(task.Id, new ReturnTaskItemRequest(task.Version, reason), "corr"),
                CancellationToken.None);

    private static Task<Response<NoContent>> Reassign(
        FakeTaskItemRepository tasks,
        TaskItem task,
        Guid newAssignee,
        string reason)
        => new ReassignTaskItemHandler(
                tasks,
                new FakeTaskAssignmentRepository(),
                new FakePositionAssignmentRepository(Holder(TaskTestData.Me), Holder(newAssignee)),
                new FakePositionRepository(ActivePosition()),
                new FakeOrganizationUnitRepository(LiveUnit()),
                new FakeCurrentUserContext(TaskTestData.Me),
                new FakeTenantContext(TaskTestData.Tenant))
            .Handle(
                new ReassignTaskItemCommand(
                    task.Id, new ReassignTaskItemRequest(task.Version, newAssignee, reason), "corr"),
                CancellationToken.None);

    private static Task<Response<Guid>> Create(FakeTaskItemRepository tasks)
        => new CreateTaskItemHandler(
                tasks,
                new FakeTaskAssignmentRepository(),
                new FakeTaskWatcherRepository(),
                new FakePositionRepository(ActivePosition()),
                new FakeOrganizationUnitRepository(LiveUnit()),
                new FakePositionAssignmentRepository(Holder(TaskTestData.Me)),
                new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
                new TaskLifecycleService(),
                new FakeTaskApprovalService(),
                new FakeChecklistTemplateRepository(),
                new FakeChecklistRunRepository(),
                new TaskChecklistService(),
                new FakeTaskNotificationService(),
                new FakeCurrentUserContext(TaskTestData.Me),
                new FakeTenantContext(TaskTestData.Tenant),
                NullLogger<CreateTaskItemHandler>.Instance)
            .Handle(new CreateTaskItemCommand(NewTaskRequest(), "corr"), CancellationToken.None);

    private static UpdateTaskItemHandler UpdateHandler(FakeTaskItemRepository tasks)
        => new(
            tasks,
            new FakeOrganizationUnitRepository(LiveUnit()),
            new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
            new FakeCurrentUserContext(TaskTestData.Me),
            new FakeTaskApprovalService(),
            new FakeTaskReviewService(),
            NullLogger<UpdateTaskItemHandler>.Instance);

    /// <summary>An edit that switches the review requirement OFF — the one path that moves a task out of
    /// PendingReview without its holder doing anything.</summary>
    private static UpdateTaskItemRequest EditTurningReviewOff(TaskItem task) => new(
        Title: task.Title,
        Description: null,
        Priority: TaskPriority.Medium,
        OrganizationUnitId: UnitId,
        DueAt: null,
        StartAt: null,
        PlannedDate: null,
        EstimateHours: null,
        Tags: null,
        ReviewRequired: false,
        EmailNotificationsEnabled: false,
        DelegationAllowed: false,
        FieldValues: null,
        ExpectedVersion: task.Version);

    private static async Task<WorkItemProjectionDto> Project(
        FakeTaskItemRepository tasks,
        FakeTaskCommentRepository comments)
    {
        var provider = new TaskWorkItemProvider(
            tasks,
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            // Seeded with the actor's name so the assertion below can tell "resolved" from "the resolver knew
            // nobody" — an event whose actor never joined the batched directory read would come back null here,
            // which is exactly the regression that would go unnoticed.
            new FakeUserDisplayNameResolver((TaskTestData.Me, TaskTestData.MeDisplayName)),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            new FakeTaskDependencyRepository(),
            comments,
            // The store's OWN log — the entries the handlers under test actually wrote.
            tasks.Transitions, new FakeTaskPersonalOverlayRepository(), new FakeTaskWatcherRepository(), TaskActors.PermitAll(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(),
            SlaForTests.Real(),
            new FakeTaskFieldDefinitionRepository());

        var actor = new WorkItemActor(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>());
        return Assert.Single(await provider.GetWorkItemsAsync(actor, CancellationToken.None));
    }

    // ── FIXTURES ────────────────────────────────────────────────────────────────────────────────────────────

    private static TaskItem AssignedTask(TaskLifecycle lifecycle) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Prepare the board pack",
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Rival,
        OrganizationUnitId = UnitId,
        Lifecycle = lifecycle,
        Version = 1
    };

    private static TaskItem PooledTask() => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Review the incoming batch",
        AssignmentTarget = TaskAssignmentTarget.PositionPool,
        PoolPositionId = PositionId,
        CreatedByUserId = TaskTestData.Rival,
        OrganizationUnitId = UnitId,
        Lifecycle = TaskLifecycle.Open,
        Version = 1
    };

    private static CreateTaskItemRequest NewTaskRequest() => new(
        Title: "Draft the quarterly filing",
        Description: null,
        Priority: TaskPriority.Medium,
        AssignmentTarget: TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId: null,
        PoolPositionId: null,
        OrganizationUnitId: UnitId,
        ParentTaskItemId: null,
        DueAt: null,
        StartAt: null,
        PlannedDate: null,
        EstimateHours: null,
        Tags: null,
        ReviewRequired: false,
        ReviewerCandidateUserId: null,
        ApprovalRequired: false,
        ApprovalManagerUserId: null,
        ChecklistTemplateId: null,
        EmailNotificationsEnabled: false,
        DelegationAllowed: false,
        FieldValues: null,
        Watchers: null);

    private static PositionAssignment Holder(Guid userId) => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = PositionId,
        UserId = userId,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
    };

    private static Position ActivePosition() => new()
    {
        Id = PositionId,
        TenantId = TaskTestData.Tenant,
        Code = "QA-1",
        Name = "QA Specialist",
        OrganizationUnitId = UnitId,
        Status = PositionStatus.Active
    };

    private static OrganizationUnit LiveUnit() => new()
    {
        Id = UnitId,
        TenantId = TaskTestData.Tenant,
        Code = "OPS",
        Name = "Operations",
        LegalEntityId = Guid.NewGuid(),
        Status = OrgUnitStatus.Active
    };

    /// <summary>Where the source scan looks. Resolved from this assembly rather than hard-coded from a
    /// working directory, so the test runs the same from an IDE, from the CLI and from CI.</summary>
    private static string HandlerDirectory
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                directory = directory.Parent;
            }

            Assert.NotNull(directory);
            return Path.Combine(
                directory!.FullName,
                "src", "Diten.Platform.Application", "Features", "Tasks", "Handlers", "CommandHandlers");
        }
    }
}
