using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Handlers.CommandHandlers;
using Diten.Platform.Application.Tests.Audit;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// WP-WF-ENGINE-CANDIDATES-01 · D1 — WHO MAY DECIDE AN APPROVAL TASK, over the real repositories and a real
/// replica-set transaction (never the shared 27017).
/// <list type="bullet">
/// <item>BL-475: ANY resolved candidate of the step decides, the first decision wins — not only the candidate that
/// sorted first.</item>
/// <item>Two candidates deciding in the same instant: exactly one decision is written, the other is told 409 — for a
/// FINAL step (one completion event) and for an INTERMEDIATE step (one next task; the whole step is one write).</item>
/// <item>BL-477: a user id is the same person in any spelling.</item>
/// <item>BL-478: an ESCALATED task is decided by its escalation candidates.</item>
/// </list>
/// The assignment state is SEEDED exactly as production writes it (start / next-step / escalation snapshots), so
/// each test controls the candidate list; every decision goes through the production handlers.
/// </summary>
public sealed partial class WorkflowCandidateDecisionMongoTests(AuditOutboxTemporalReplicaSetFixture replicaSet) :
    IAsyncLifetime,
    IClassFixture<AuditOutboxTemporalReplicaSetFixture>
{
    private static readonly Guid First = Guid.Parse("0a000000-0000-4000-8000-000000000001");   // sorts first → AssigneeRef
    private static readonly Guid Second = Guid.Parse("0b000000-0000-4000-8000-000000000002");
    private static readonly Guid Outsider = Guid.Parse("0c000000-0000-4000-8000-000000000003");
    private static readonly Guid NextApprover = Guid.Parse("0d000000-0000-4000-8000-000000000004");
    private static readonly Guid Starter = Guid.Parse("0e000000-0000-4000-8000-000000000005");

    private Store _store = null!;

    public async Task InitializeAsync() =>
        _store = await Store.CreateAsync(replicaSet.ConnectionString, "diten_platform_workflow_candidate_decision");

    public Task DisposeAsync() => Task.CompletedTask;

    // ── a · any candidate decides ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_candidate_that_is_NOT_the_resolved_principal_approves_and_is_recorded_as_the_decider()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        Assert.Equal(D(First), seeded.Task.AssigneeRef); // non-vacuity: the decider below is NOT the shown assignee

        var response = await _store.ApproveAsync(seeded.Task.Id, D(Second), "approve-second");

        Assert.True(response.IsSuccessful, response.ReasonCode);
        var task = (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!;
        Assert.Equal(ApprovalTaskStatus.Approved, task.Status);
        Assert.Equal(D(Second), task.ActionedBy);
        // "who to show" is unchanged; "who decided" is the actor — and the trusted evidence rule
        // (task.ActionedBy == log.ActorId == log.ActorRef) still holds for a non-first candidate.
        Assert.Equal(D(First), task.AssigneeRef);
        var log = Assert.Single(await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id),
            x => x.Action == WorkflowTransitionAction.Approve);
        Assert.Equal(task.ActionedBy, log.ActorId);
        Assert.Equal(log.ActorId, log.ActorRef);
        Assert.Equal(WorkflowInstanceStatus.Completed, (await _store.Instances.GetByIdAsync(seeded.Instance.Id))!.Status);
    }

    [Fact]
    public async Task Somebody_who_is_not_a_candidate_is_denied_on_every_decision_gate()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);

        var approve = await _store.ApproveAsync(seeded.Task.Id, D(Outsider), "outsider-approve");
        var reject = await _store.RejectAsync(seeded.Task.Id, D(Outsider), "outsider-reject");
        var info = await _store.RequestInfoAsync(seeded.Task.Id, D(Outsider), "outsider-info");
        var hand = await _store.DelegateAsync(seeded.Task.Id, D(Outsider), D(NextApprover), "outsider-delegate");

        Assert.All(new[] { approve, reject, info, hand }, response =>
        {
            Assert.Equal(403, response.StatusCode);
            Assert.Equal(WorkflowReasonCodes.WorkflowActorDenied, response.ReasonCode);
        });
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.Status);
    }

    [Fact]
    public async Task The_second_candidate_can_reject_ask_for_information_and_delegate()
    {
        var rejected = await _store.SeedAsync([D(First), D(Second)]);
        Assert.True((await _store.RejectAsync(rejected.Task.Id, D(Second), "second-reject")).IsSuccessful);
        Assert.Equal(D(Second), (await _store.Tasks.GetByIdAsync(rejected.Task.Id))!.ActionedBy);

        var asked = await _store.SeedAsync([D(First), D(Second)]);
        Assert.True((await _store.RequestInfoAsync(asked.Task.Id, D(Second), "second-info")).IsSuccessful);
        Assert.Equal(ApprovalTaskStatus.WaitingEvidence, (await _store.Tasks.GetByIdAsync(asked.Task.Id))!.Status);

        var handed = await _store.SeedAsync([D(First), D(Second)]);
        Assert.True((await _store.DelegateAsync(handed.Task.Id, D(Second), D(NextApprover), "second-delegate")).IsSuccessful);
        // After a delegation the delegate is the ONLY candidate: the old candidates are out.
        Assert.Equal(403, (await _store.ApproveAsync(handed.Task.Id, D(First), "after-delegate-first")).StatusCode);
        Assert.True((await _store.ApproveAsync(handed.Task.Id, D(NextApprover), "after-delegate-new")).IsSuccessful);
    }

    // ── b · two candidates at the same instant ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Two_candidates_approving_the_FINAL_step_at_once_write_one_decision_and_one_completion_event()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);
        var barrier = new TwoCallerBarrier(new PlatformTransactionExecutor(_store.Context));

        var responses = await Task.WhenAll(
            _store.ApproveAsync(seeded.Task.Id, D(First), "race-final-first", barrier),
            _store.ApproveAsync(seeded.Task.Id, D(Second), "race-final-second", barrier));

        var winner = Assert.Single(responses, r => r.IsSuccessful);
        var loser = Assert.Single(responses, r => !r.IsSuccessful);
        Assert.Equal(409, loser.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTransitionConflict, loser.ReasonCode);

        var approvals = (await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id))
            .Where(x => x.Action == WorkflowTransitionAction.Approve).ToList();
        var decision = Assert.Single(approvals);
        Assert.Equal(winner.Data!.TransitionLogId, decision.Id);
        Assert.Equal(decision.ActorId, (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.ActionedBy);
        Assert.Equal(1, await _store.CompletionEventsAsync(seeded.Instance.Id));
        Assert.Equal(WorkflowInstanceStatus.Completed, (await _store.Instances.GetByIdAsync(seeded.Instance.Id))!.Status);
    }

    [Fact]
    public async Task Two_candidates_approving_an_INTERMEDIATE_step_at_once_create_exactly_one_next_task()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)], twoSteps: true);
        var barrier = new TwoCallerBarrier(new PlatformTransactionExecutor(_store.Context));

        var responses = await Task.WhenAll(
            _store.ApproveAsync(seeded.Task.Id, D(First), "race-step-first", barrier),
            _store.ApproveAsync(seeded.Task.Id, D(Second), "race-step-second", barrier));

        Assert.Single(responses, r => r.IsSuccessful);
        var loser = Assert.Single(responses, r => !r.IsSuccessful);
        Assert.Equal(409, loser.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTransitionConflict, loser.ReasonCode);

        var tasks = await _store.Tasks.ListByInstanceIdAsync(seeded.Instance.Id);
        Assert.Equal(2, tasks.Count); // the decided one + exactly ONE next task
        var next = Assert.Single(tasks, t => t.Id != seeded.Task.Id);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, next.Status);
        Assert.Equal("step-b", next.StepCode);
        Assert.Equal(D(NextApprover), next.AssigneeRef);

        var snapshots = await _store.Snapshots.ListByInstanceIdAsync(seeded.Instance.Id);
        Assert.Equal(2, snapshots.Count);
        Assert.Equal(next.AssignmentSnapshotId, Assert.Single(snapshots, s => s.ApprovalTaskId == next.Id).Id);

        var logs = await _store.Logs.ListByInstanceIdAsync(seeded.Instance.Id);
        Assert.Single(logs, x => x.Action == WorkflowTransitionAction.Approve);
        Assert.Single(logs, x => x.Action == WorkflowTransitionAction.Start && x.ApprovalTaskId == next.Id);

        var instance = (await _store.Instances.GetByIdAsync(seeded.Instance.Id))!;
        Assert.Equal(WorkflowInstanceStatus.Active, instance.Status);
        Assert.Equal("step-b", instance.CurrentStep);
        Assert.Equal(0, await _store.CompletionEventsAsync(seeded.Instance.Id)); // not terminal: no completion event
    }

    [Fact]
    public async Task An_intermediate_approval_without_transactions_writes_nothing_and_says_so()
    {
        // The step is ONE transaction now. On a deployment that cannot run one, nothing may be half-written.
        var standalone = await Store.CreateAsync(
            replicaSet.StandaloneConnectionString, "diten_platform_workflow_candidate_decision_standalone");
        var seeded = await standalone.SeedAsync([D(First), D(Second)], twoSteps: true);

        var response = await standalone.ApproveAsync(seeded.Task.Id, D(Second), "standalone-step");

        Assert.Equal(503, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTransactionUnavailable, response.ReasonCode);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, (await standalone.Tasks.GetByIdAsync(seeded.Task.Id))!.Status);
        Assert.Single(await standalone.Tasks.ListByInstanceIdAsync(seeded.Instance.Id));
        Assert.DoesNotContain(await standalone.Logs.ListByInstanceIdAsync(seeded.Instance.Id),
            x => x.Action == WorkflowTransitionAction.Approve);
    }

    // ── c · one person, any spelling ───────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("N")] // no dashes
    [InlineData("B")] // braces
    [InlineData("UPPER")]
    public async Task A_candidate_stored_in_another_spelling_of_their_id_can_still_decide(string spelling)
    {
        var stored = spelling == "UPPER" ? Second.ToString("D").ToUpperInvariant() : Second.ToString(spelling);
        Assert.NotEqual(D(Second), stored); // non-vacuity: the snapshot does NOT hold the token's spelling
        var seeded = await _store.SeedAsync([D(First), stored]);

        var response = await _store.ApproveAsync(seeded.Task.Id, D(Second), "spelling-" + spelling);

        Assert.True(response.IsSuccessful, response.ReasonCode);
        Assert.Equal(D(Second), (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.ActionedBy);
    }

    [Fact]
    public async Task The_resolver_writes_a_user_id_in_one_spelling_and_names_one_person_once()
    {
        var resolved = await WorkflowCandidateResolver.ResolveAsync(
            [Second.ToString("B"), "user:" + Second.ToString("N").ToUpperInvariant(), D(First), "role-queue"],
            null, null, default);

        Assert.Equal([D(First), D(Second), "role-queue"], resolved);
    }

    [Fact]
    public void Two_principals_are_compared_as_ids_when_both_are_ids_and_letter_for_letter_otherwise()
    {
        Assert.True(WorkflowTaskTransitionSupport.PrincipalEquals(Second.ToString("B").ToUpperInvariant(), D(Second)));
        Assert.False(WorkflowTaskTransitionSupport.PrincipalEquals(D(First), D(Second)));
        Assert.True(WorkflowTaskTransitionSupport.PrincipalEquals("role-queue", " role-queue "));
        Assert.False(WorkflowTaskTransitionSupport.PrincipalEquals("role-queue", "ROLE-QUEUE")); // opaque: no case folding
        Assert.False(WorkflowTaskTransitionSupport.PrincipalEquals(null, null));
        Assert.False(WorkflowTaskTransitionSupport.PrincipalEquals("", ""));
    }

    // ── d · escalated ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_escalated_task_is_decided_by_its_escalation_candidates_and_not_by_the_old_ones()
    {
        var seeded = await _store.SeedAsync([D(First)], escalatedTo: [D(Second), D(NextApprover)]);
        Assert.Equal(ApprovalTaskStatus.Escalated, seeded.Task.Status);

        var old = await _store.ApproveAsync(seeded.Task.Id, D(First), "escalated-old");
        Assert.Equal(403, old.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowActorDenied, old.ReasonCode);

        // The escalation candidate that did NOT sort first decides.
        var decided = await _store.ApproveAsync(seeded.Task.Id, D(NextApprover), "escalated-new");
        Assert.True(decided.IsSuccessful, decided.ReasonCode);
        Assert.Equal(ApprovalTaskStatus.Approved, (await _store.Tasks.GetByIdAsync(seeded.Task.Id))!.Status);
        Assert.Equal(WorkflowInstanceStatus.Completed, (await _store.Instances.GetByIdAsync(seeded.Instance.Id))!.Status);
        Assert.Equal(1, await _store.CompletionEventsAsync(seeded.Instance.Id));
    }

    [Fact]
    public async Task An_escalated_task_can_be_rejected_delegated_and_asked_about_but_still_not_publicly_cancelled()
    {
        var rejected = await _store.SeedAsync([D(First)], escalatedTo: [D(Second)]);
        Assert.True((await _store.RejectAsync(rejected.Task.Id, D(Second), "escalated-reject")).IsSuccessful);

        var asked = await _store.SeedAsync([D(First)], escalatedTo: [D(Second)]);
        Assert.True((await _store.RequestInfoAsync(asked.Task.Id, D(Second), "escalated-info")).IsSuccessful);

        var handed = await _store.SeedAsync([D(First)], escalatedTo: [D(Second)]);
        Assert.True((await _store.DelegateAsync(handed.Task.Id, D(Second), D(NextApprover), "escalated-delegate")).IsSuccessful);
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, (await _store.Tasks.GetByIdAsync(handed.Task.Id))!.Status);

        // Unchanged (B3): the public cancel still refuses an escalated task.
        var cancel = await _store.SeedAsync([D(First)], escalatedTo: [D(Second)]);
        var cancelled = await new CancelWorkflowTaskHandler(_store.Tasks, _store.Instances, _store.Snapshots, _store.Logs)
            .Handle(new CancelWorkflowTaskCommand(cancel.Task.Id,
                new CancelWorkflowTaskRequest(D(Second), "CANCEL", "escalated-cancel", null), "corr"), default);
        Assert.Equal(409, cancelled.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTaskInvalidState, cancelled.ReasonCode);
    }

    // ── e · the starter ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_starter_cannot_approve_even_as_a_candidate_and_the_other_candidate_still_can()
    {
        var seeded = await _store.SeedAsync([D(First), D(Starter)], startedBy: Starter);

        var own = await _store.ApproveAsync(seeded.Task.Id, D(Starter), "starter-approve");
        Assert.Equal(409, own.StatusCode);
        Assert.Equal(WorkflowReasonCodes.SodViolation, own.ReasonCode);

        Assert.True((await _store.ApproveAsync(seeded.Task.Id, D(First), "starter-other")).IsSuccessful);
    }

    // ── h · seeing a task and being able to decide it are the same set ─────────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)] // the second candidate stored in another spelling of their id (the screen half of test c)
    public async Task Who_SEES_a_task_in_both_lists_is_exactly_who_the_gate_lets_DECIDE_it(bool otherSpelling)
    {
        var storedSecond = otherSpelling ? Second.ToString("B").ToUpperInvariant() : D(Second);
        var seeded = await _store.SeedAsync([D(First), storedSecond]);

        foreach (var (person, expected) in new[] { (First, true), (Second, true), (Outsider, false), (Starter, false) })
        {
            var gate = WorkflowTaskTransitionSupport.IsCandidate(seeded.Snapshot, D(person));
            var inTaskCenter = (await _store.TaskCenterItemsAsync(person)).Any(item => item.Id == seeded.Task.Id.ToString());
            var inMyTasks = (await _store.MyWorkflowTasksAsync(person)).Any(task => task.Id == seeded.Task.Id);

            Assert.Equal(expected, gate);
            Assert.Equal(gate, inTaskCenter);
            Assert.Equal(gate, inMyTasks);
        }

        // …and the gate is not only a predicate: the non-first candidate really decides.
        Assert.True((await _store.ApproveAsync(seeded.Task.Id, D(Second), "sees-and-decides-" + otherSpelling)).IsSuccessful);
    }

    [Fact]
    public async Task The_Task_Center_tells_the_candidate_that_is_not_the_assignee_that_the_task_is_theirs_to_decide()
    {
        var seeded = await _store.SeedAsync([D(First), D(Second)]);

        var item = Assert.Single(await _store.TaskCenterItemsAsync(Second), i => i.Id == seeded.Task.Id.ToString());

        Assert.NotNull(item.Assignee);
        Assert.True(item.Assignee!.IsCurrentUser);
        Assert.All(new[] { "approve", "reject", "requestInfo", "delegate" },
            code => Assert.True(item.Actions.Single(a => a.Code == code).Enabled, code));
    }

    [Fact]
    public void The_projection_offers_nothing_to_somebody_who_may_not_decide()
    {
        var projection = new WorkItemProjectionService(Diten.Platform.Application.Tests.Tasks.SlaForTests.Real());
        var task = new ApprovalTask
        {
            TenantId = Guid.NewGuid(), WorkflowInstanceId = Guid.NewGuid(), Status = ApprovalTaskStatus.WaitingApproval,
            AssigneeRef = D(First)
        };
        var instance = new WorkflowInstance
        {
            Id = task.WorkflowInstanceId, TenantId = task.TenantId, TemplateId = Guid.NewGuid(),
            WorkflowTemplateId = Guid.NewGuid(), ObjectType = "o", ObjectId = "1", ObjectRef = "o|1"
        };

        var outsider = projection.Project(task, instance, ActorWithEveryPermission(Outsider), "workflow", "1.0",
            decidingPrincipalIds: [D(First), D(Second)])!;
        Assert.False(outsider.Assignee!.IsCurrentUser);
        Assert.All(outsider.Actions, action =>
        {
            Assert.False(action.Enabled, action.Code);
            Assert.Equal(WorkAggregationReasonCodes.PermissionDenied, action.DisabledReasonCode);
        });

        // A caller that does not pass the list gets the projection it always got: actions by permission alone,
        // "is it you" from AssigneeRef.
        var legacy = projection.Project(task, instance, ActorWithEveryPermission(Outsider), "workflow", "1.0")!;
        Assert.False(legacy.Assignee!.IsCurrentUser);
        Assert.All(legacy.Actions, action => Assert.True(action.Enabled, action.Code));
        var legacyAssignee = projection.Project(task, instance, ActorWithEveryPermission(First), "workflow", "1.0")!;
        Assert.True(legacyAssignee.Assignee!.IsCurrentUser);
    }

    private static WorkItemActor ActorWithEveryPermission(Guid userId) => new(
        userId,
        IsPlatformActor: false,
        new HashSet<string>(StringComparer.Ordinal)
        {
            WorkflowPermissions.TasksApprove, WorkflowPermissions.TasksReject,
            WorkflowPermissions.TasksRequestInfo, WorkflowPermissions.TasksDelegate
        });

    private static string D(Guid id) => id.ToString("D");

    // ── harness ────────────────────────────────────────────────────────────────────────────────────────────────

    private sealed record Seeded(WorkflowInstance Instance, ApprovalTask Task, RuntimeAssignmentSnapshot Snapshot);

    private sealed partial class Store
    {
        public required IPlatformDbContext Context { get; init; }
        public required TenantContext TenantContext { get; init; }
        public required IWorkflowInstanceRepository Instances { get; init; }
        public required IApprovalTaskRepository Tasks { get; init; }
        public required IWorkflowTransitionLogRepository Logs { get; init; }
        public required IWorkflowTemplateRepository Templates { get; init; }
        public required IWorkflowTemplateVersionRepository Versions { get; init; }
        public required IRuntimeAssignmentSnapshotRepository Snapshots { get; init; }

        public static async Task<Store> CreateAsync(string connectionString, string databaseName)
        {
            var settings = MongoClientSettings.FromConnectionString(connectionString);
#pragma warning disable CS0618
            settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
            var client = new MongoClient(settings);
            var database = client.GetDatabase(databaseName);
            await PlatformSchemaManifest.ApplyAsync(database, [SchemaProfile.WorkflowWorkCenter, SchemaProfile.Eventing]);

            var tenantContext = new TenantContext();
            tenantContext.SetTenant(Guid.NewGuid());
            var context = new PlatformDbContext(client, database);
            return new Store
            {
                Context = context,
                TenantContext = tenantContext,
                Instances = new WorkflowInstanceRepository(context, tenantContext),
                Tasks = new ApprovalTaskRepository(context, tenantContext),
                Logs = new WorkflowTransitionLogRepository(context, tenantContext),
                Templates = new WorkflowTemplateRepository(context, tenantContext),
                Versions = new WorkflowTemplateVersionRepository(context, tenantContext),
                Snapshots = new RuntimeAssignmentSnapshotRepository(context, tenantContext)
            };
        }

        /// <summary>
        /// The state production leaves behind: an Active instance on step-a with its task and its start snapshot
        /// (<paramref name="candidates"/> in resolver order, the first one shown as assignee) — or, with
        /// <paramref name="escalatedTo"/>, what <c>RunWorkflowEscalationsHandler</c> writes on top of it.
        /// </summary>
        public async Task<Seeded> SeedAsync(
            IReadOnlyList<string> candidates,
            bool twoSteps = false,
            IReadOnlyList<string>? escalatedTo = null,
            Guid? startedBy = null,
            WorkflowStartCheckpoint? trustedCheckpoint = null,
            DateTimeOffset? dueAt = null,
            Guid? firstTaskId = null)
        {
            var tenantId = TenantContext.TenantId;
            var template = await Templates.CreateAsync(new WorkflowTemplate
            {
                TenantId = tenantId,
                TemplateCode = "D1-" + Guid.NewGuid().ToString("N")[..10],
                Name = "Candidate decision",
                Status = WorkflowTemplateStatus.Published
            });
            var steps = twoSteps
                ? "{\"code\":\"step-a\"},{\"code\":\"step-b\",\"assignment\":{\"candidatePrincipalIds\":[\"" + D(NextApprover) + "\"]}}"
                : "{\"code\":\"step-a\"}";
            var definition = "{\"stages\":[{\"code\":\"stage-1\",\"steps\":[" + steps + "]}]}";
            var version = await Versions.CreateAsync(new WorkflowTemplateVersion
            {
                TenantId = tenantId,
                TemplateId = template.Id,
                VersionNumber = 1,
                DefinitionJson = definition,
                SchemaVersion = "1.0",
                ExpressionVersion = "1.0",
                Status = WorkflowTemplateVersionStatus.Published,
                IsImmutable = true
            });

            var starter = startedBy ?? Guid.NewGuid();
            var escalated = escalatedTo is not null;
            var instance = await Instances.CreateAsync(new WorkflowInstance
            {
                TenantId = tenantId,
                TemplateId = template.Id,
                WorkflowTemplateId = template.Id,
                TemplateVersionId = version.Id,
                TemplateCode = template.TemplateCode,
                ObjectType = "d1-object",
                ObjectId = Guid.NewGuid().ToString("D"),
                ObjectRef = "d1-object|" + Guid.NewGuid().ToString("N"),
                CurrentStage = "stage-1",
                CurrentStep = "step-a",
                Status = escalated ? WorkflowInstanceStatus.Escalated : WorkflowInstanceStatus.Active,
                StartedBy = "starter@example.test",
                StartedByUserId = starter,
                StartedAt = DateTimeOffset.UtcNow,
                // a TRUSTED start, caught at this checkpoint (null = an ordinary, public start)
                TrustedConsumerClientId = trustedCheckpoint is null ? null : Guid.NewGuid(),
                StartCheckpoint = trustedCheckpoint ?? WorkflowStartCheckpoint.None,
                DueAt = dueAt
            });

            var acting = escalatedTo ?? candidates;
            var task = new ApprovalTask
            {
                Id = firstTaskId ?? Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowInstanceId = instance.Id,
                StageCode = "stage-1",
                StepCode = "step-a",
                Status = escalated ? ApprovalTaskStatus.Escalated : ApprovalTaskStatus.WaitingApproval,
                AssigneeRef = acting[0],
                DueAt = dueAt,
                EscalationLevel = escalated ? 1 : 0,
                EscalatedAt = escalated ? DateTimeOffset.UtcNow : null
            };
            if (escalated)
            {
                // the snapshot the task had before it escalated stays in the store, exactly as production leaves it
                await Snapshots.CreateAsync(NewSnapshot(tenantId, instance.Id, task.Id, "runtime_candidates", candidates));
            }

            var snapshot = NewSnapshot(tenantId, instance.Id, task.Id,
                escalated ? "escalation_rules" : "runtime_candidates", acting);
            task.AssignmentSnapshotId = snapshot.Id;
            await Tasks.CreateAsync(task);
            await Snapshots.CreateAsync(snapshot);
            return new Seeded(instance, (await Tasks.GetByIdAsync(task.Id))!, snapshot);
        }

        private static RuntimeAssignmentSnapshot NewSnapshot(
            Guid tenantId, Guid instanceId, Guid taskId, string source, IReadOnlyList<string> principals) => new()
        {
            TenantId = tenantId,
            WorkflowInstanceId = instanceId,
            ApprovalTaskId = taskId,
            ResolverSource = source,
            ResolvedPrincipalId = principals[0],
            CandidatePrincipalIds = principals.ToList(),
            TieBreakExplanation = principals.Count == 1 ? "single_candidate" : "lexicographic_first_principal"
        };

        public Task<Diten.Platform.Application.Common.Response<WorkflowTaskTransitionResponse>> ApproveAsync(
            Guid taskId, string actor, string key, IPlatformTransactionExecutor? executor = null,
            IApprovalTaskRepository? tasks = null) =>
            new ApproveWorkflowTaskHandler(tasks ?? Tasks, Instances, Snapshots, Logs, Versions,
                    transactions: executor ?? new PlatformTransactionExecutor(Context),
                    events: new MongoIntentWriter(Context),
                    templates: Templates)
                .Handle(new ApproveWorkflowTaskCommand(taskId,
                    new ApproveWorkflowTaskRequest(actor, "APPROVED", key, null, null), "corr-" + key), default);

        public Task<Diten.Platform.Application.Common.Response<WorkflowTaskTransitionResponse>> RejectAsync(
            Guid taskId, string actor, string key, IPlatformTransactionExecutor? executor = null) =>
            new RejectWorkflowTaskHandler(Tasks, Instances, Snapshots, Logs,
                    transactions: executor ?? new PlatformTransactionExecutor(Context),
                    events: new MongoIntentWriter(Context),
                    templates: Templates)
                .Handle(new RejectWorkflowTaskCommand(taskId,
                    new RejectWorkflowTaskRequest(actor, "REJECTED", key, "no", null), "corr-" + key), default);

        public Task<Diten.Platform.Application.Common.Response<WorkflowTaskTransitionResponse>> RequestInfoAsync(
            Guid taskId, string actor, string key, IPlatformTransactionExecutor? executor = null) =>
            new RequestInfoWorkflowTaskHandler(Tasks, Instances, Snapshots, Logs,
                    executor ?? new PlatformTransactionExecutor(Context))
                .Handle(new RequestInfoWorkflowTaskCommand(taskId,
                    new RequestInfoWorkflowTaskRequest(actor, null, "INFO", key, "please add", null), "corr-" + key), default);

        public Task<Diten.Platform.Application.Common.Response<WorkflowTaskTransitionResponse>> DelegateAsync(
            Guid taskId, string actor, string target, string key, IPlatformTransactionExecutor? executor = null) =>
            new DelegateWorkflowTaskHandler(Tasks, Instances, Snapshots, Logs,
                    executor ?? new PlatformTransactionExecutor(Context))
                .Handle(new DelegateWorkflowTaskCommand(taskId,
                    new DelegateWorkflowTaskRequest(actor, target, "DELEGATED", key, null), "corr-" + key), default);

        public async Task<IReadOnlyList<WorkItemProjectionDto>> TaskCenterItemsAsync(Guid userId) =>
            await new WorkflowApprovalWorkItemProvider(
                    Tasks, Snapshots, Instances, new WorkItemProjectionService(Tasks_SlaForTests()))
                .GetWorkItemsAsync(ActorWithEveryPermission(userId));

        private static IWorkItemSlaCalculator Tasks_SlaForTests() => Diten.Platform.Application.Tests.Tasks.SlaForTests.Real();

        public async Task<IReadOnlyList<WorkflowTaskDto>> MyWorkflowTasksAsync(Guid userId) =>
            (await new Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers.GetMyWorkflowTasksHandler(
                    Tasks, Snapshots, new CurrentUser(userId))
                .Handle(new Diten.Platform.Application.Features.Workflow.Queries.GetMyWorkflowTasksQuery("corr"), default)).Data!;

        public Task<long> CompletionEventsAsync(Guid instanceId) =>
            Context.Database.GetCollection<BsonDocument>(MongoIntentWriter.Collection).CountDocumentsAsync(
                Builders<BsonDocument>.Filter.Eq("WorkflowInstanceId", instanceId.ToString("D")));
    }

    private sealed class CurrentUser(Guid userId) : Diten.Platform.Application.Contracts.ICurrentUserContext
    {
        public Guid UserId { get; } = userId;
        public string? Email => "d1@example.test";
        public string? DisplayName => "D1";
        public string ActorName => Email!;
        public bool IsAuthenticated => true;
    }

    /// <summary>The outbox row of a completion event, written in the caller's transaction — so an aborted body leaves none.</summary>
    private sealed class MongoIntentWriter(IPlatformDbContext context) : ITransactionalIntegrationEventWriter
    {
        public const string Collection = "d1_completion_intents";

        public async Task<EventEnvelope<TEvent>> EnqueueAsync<TEvent>(IPlatformTransactionSession session, TEvent @event,
            EventPublishOptions options, CancellationToken cancellationToken = default) where TEvent : IIntegrationEvent
        {
            var completed = Assert.IsType<Diten.Platform.Application.Features.Workflow.Events.WorkflowInstanceCompletedV1>(@event);
            var metadata = new EventMetadata(options.EventId!.Value, @event.EventName, @event.EventVersion,
                options.CorrelationId!.Value, null, options.TenantId, options.Producer!, options.OccurredAtUtc!.Value);
            await context.Database.GetCollection<BsonDocument>(Collection).InsertOneAsync(
                PlatformMongoTransactionSession.Require(session, context),
                new BsonDocument
                {
                    ["EventId"] = metadata.EventId.ToString(),
                    ["WorkflowInstanceId"] = completed.WorkflowInstanceId.ToString("D"),
                    ["CompletedBy"] = completed.CompletedBy ?? string.Empty
                },
                cancellationToken: cancellationToken);
            return new EventEnvelope<TEvent>(metadata, @event);
        }
    }

    /// <summary>Holds both callers at the door of the transaction, so both have READ the open task before either writes.</summary>
    private sealed class TwoCallerBarrier(IPlatformTransactionExecutor inner) : IPlatformTransactionExecutor
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public async Task<T> ExecuteAsync<T>(
            Func<IPlatformTransactionSession, CancellationToken, Task<T>> body,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrivals) == 2)
            {
                _release.TrySetResult();
            }

            await _release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return await inner.ExecuteAsync(body, cancellationToken);
        }
    }
}
