using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WCN-TASK-REQUEST-01 merge (FIX1) — THE WRITE GATE ANSWERS BEFORE ANYTHING IS WITHDRAWN. The merge put the task
/// request's single write gate (FIX2/FIX3) in front of the engine's withdraws (D2-FIX2..5) in every handler that withdraws
/// what a task started in MOD-0023. Were the gate moved below a withdraw, somebody who cannot read the task (or reads it
/// only as a watcher / as the person asked) would cancel another person's running review or approval before being told
/// 404 / 403 — and every older test would stay green, because the engine's tests use an open gate and the request's tests
/// hold no linked workflow.
///
/// <para>Each handler is measured twice on the SAME linked task: through the PRODUCTION gate over a read rule that admits
/// nobody (the gate's answer, and the withdraw services are never called), and through an open gate (non-vacuity: the
/// setup really reaches the withdraw — without it the first half could pass on a task that never asks).</para>
/// </summary>
public sealed class TaskWriteGateBeforeWithdrawTests
{
    private static readonly Guid Unit = Guid.Parse("0e0e0e0e-0e0e-0e0e-0e0e-0e0e0e0e0e0e");
    private static readonly Guid ReviewInstance = Guid.Parse("a1a1a1a1-a1a1-a1a1-a1a1-a1a1a1a1a1a1");
    private static readonly Guid ApprovalInstance = Guid.Parse("b2b2b2b2-b2b2-b2b2-b2b2-b2b2b2b2b2b2");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Release_asks_the_gate_before_it_withdraws_the_review(bool gateOpen)
    {
        var h = new Harness(gateOpen, pooled: true);
        var answer = await new ReleaseTaskItemHandler(h.Tasks, new FakeTaskAssignmentRepository(), h.User, h.Tenant, h.Gate, h.Reviews)
            .Handle(new ReleaseTaskItemCommand(h.Task.Id, new TaskTransitionRequest(h.Task.Version, null, null), "corr-release"),
                CancellationToken.None);

        h.AssertGateFirst(answer, reviewOnly: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Return_asks_the_gate_before_it_withdraws_the_review(bool gateOpen)
    {
        var h = new Harness(gateOpen);
        var answer = await new ReturnTaskItemHandler(h.Tasks, new FakeTaskAssignmentRepository(), h.User, h.Tenant, h.Gate,
                reviews: h.Reviews)
            .Handle(new ReturnTaskItemCommand(h.Task.Id, new ReturnTaskItemRequest(h.Task.Version, "Not mine"), "corr-return"),
                CancellationToken.None);

        h.AssertGateFirst(answer, reviewOnly: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reassign_asks_the_gate_before_it_withdraws_the_review(bool gateOpen)
    {
        var h = new Harness(gateOpen);
        var answer = await new ReassignTaskItemHandler(h.Tasks, new FakeTaskAssignmentRepository(), TaskAssignmentGuards.AdmitAll(),
                h.User, h.Tenant, h.Gate, h.Reviews)
            .Handle(new ReassignTaskItemCommand(h.Task.Id,
                new ReassignTaskItemRequest(h.Task.Version, TaskTestData.Rival, "Their skill"), "corr-reassign"), CancellationToken.None);

        h.AssertGateFirst(answer, reviewOnly: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_cancel_asks_the_gate_before_it_withdraws_the_review_and_the_approval(bool gateOpen)
    {
        // Cancelling is the REQUESTER's (403 CancelNotRequester otherwise): Me sent it here, so the open gate reaches the withdraw.
        var h = new Harness(gateOpen, requesterIsMe: true);
        var answer = await new TransitionTaskItemHandler(
                h.Tasks, new TaskLifecycleService(), h.User, new FakeChecklistRunRepository(), new TaskChecklistService(),
                new FakeWorkflowTransitionGate(), new FakeTaskDependencyRepository(), new FakeTaskTypeRepository(),
                new FakeTaskNotificationService(),
                new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
                new FakeTaskAttachmentRepository(), NullLogger<TransitionTaskItemHandler>.Instance,
                writeGate: h.Gate, approvals: h.Approvals, reviews: h.Reviews)
            .Handle(new TransitionTaskItemCommand(h.Task.Id, TaskLifecycle.Cancelled,
                new TaskTransitionRequest(h.Task.Version, null, null), "corr-cancel"), CancellationToken.None);

        h.AssertGateFirst(answer, reviewOnly: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_asks_the_gate_before_it_withdraws_the_review_and_the_approval(bool gateOpen)
    {
        var h = new Harness(gateOpen);
        var answer = await new DeleteTaskItemHandler(h.Tasks, h.Gate, h.Approvals, h.Reviews)
            .Handle(new DeleteTaskItemCommand(h.Task.Id, "corr-delete"), CancellationToken.None);

        h.AssertGateFirst(answer, reviewOnly: false);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_bulk_delete_asks_the_gate_before_it_withdraws_the_review_and_the_approval(bool gateOpen)
    {
        var h = new Harness(gateOpen);
        var answer = await new BulkDeleteTaskItemHandler(h.Tasks, h.Gate, h.Approvals, h.Reviews,
                NullLogger<BulkDeleteTaskItemHandler>.Instance)
            .Handle(new BulkDeleteTaskItemCommand(new BulkDeleteTaskItemRequest([h.Task.Id]), "corr-bulk"), CancellationToken.None);

        if (gateOpen)
        {
            h.AssertWithdrawn(reviewOnly: false);
            return;
        }

        // The bulk answer for a task the gate refuses is the delete's own (FIX2: skipped, never named) — and the task, its
        // review and its approval are all still there.
        Assert.Equal(204, answer.StatusCode);
        Assert.Null(answer.Data);
        Assert.NotNull(await h.Tasks.GetByIdAsync(h.Task.Id, CancellationToken.None));
        h.AssertNothingWithdrawn();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_edit_switching_approval_and_review_off_asks_the_gate_before_it_withdraws(bool gateOpen)
    {
        var h = new Harness(gateOpen);
        var answer = await new UpdateTaskItemHandler(
                h.Tasks,
                new FakeOrganizationUnitRepository(new OrganizationUnit
                {
                    Id = Unit, TenantId = TaskTestData.Tenant, Name = "HQ", Code = "HQ", LegalEntityId = Guid.NewGuid()
                }),
                new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
                h.User, h.Approvals, h.Reviews, NullLogger<UpdateTaskItemHandler>.Instance,
                TaskDocumentFreezerDoubles.OverAnEmptyRegister(), writeGate: h.Gate)
            .Handle(new UpdateTaskItemCommand(h.Task.Id, new UpdateTaskItemRequest(
                    Title: "Edited title", Description: null, Priority: TaskPriority.Medium, OrganizationUnitId: null, DueAt: null,
                    StartAt: null, PlannedDate: null, EstimateHours: null, Tags: null, ReviewRequired: false,
                    EmailNotificationsEnabled: false, DelegationAllowed: true, FieldValues: null, ExpectedVersion: h.Task.Version,
                    ApprovalRequired: false, ApprovalManagerUserId: null), "corr-edit"),
                CancellationToken.None);

        if (gateOpen)
        {
            h.AssertWithdrawn(reviewOnly: false);
            return;
        }

        Assert.Equal(404, answer.StatusCode);
        Assert.Equal(TaskReasonCodes.NotFound, answer.ReasonCode);
        h.AssertNothingWithdrawn();
        var stored = await h.Tasks.GetByIdAsync(h.Task.Id, CancellationToken.None);
        Assert.True(stored!.ApprovalRequired);                                       // the edit itself did not land either
        Assert.Equal(ApprovalInstance, stored.WorkflowInstanceId);
    }

    /// <summary>One linked task (a running review AND a running approval), held by Me, sent by Other (or by Me).</summary>
    private sealed class Harness
    {
        public Harness(bool gateOpen, bool pooled = false, bool requesterIsMe = false)
        {
            Task = new TaskItem
            {
                TenantId = TaskTestData.Tenant,
                Title = "Bağlı iş",
                Lifecycle = TaskLifecycle.InProgress,
                AssignmentTarget = pooled ? TaskAssignmentTarget.PositionPool : TaskAssignmentTarget.Person,
                PoolPositionId = pooled ? Guid.NewGuid() : null,
                AssigneeUserId = TaskTestData.Me,
                CreatedByUserId = requesterIsMe ? TaskTestData.Me : TaskTestData.Other,
                OrganizationUnitId = Unit,
                DelegationAllowed = true,
                ReviewRequired = true,
                ReviewWorkflowInstanceId = ReviewInstance,
                ApprovalRequired = true,
                ApprovalManagerUserId = TaskTestData.Other,
                WorkflowInstanceId = ApprovalInstance,
                Version = 1
            };
            Tasks = new FakeTaskItemRepository(Task);
            User = new FakeCurrentUserContext(TaskTestData.Me);
            Gate = gateOpen ? TaskWriteGateDoubles.Open : TaskWriteGateDoubles.Real(new NobodyReads(), User);
        }

        public TaskItem Task { get; }
        public FakeTaskItemRepository Tasks { get; }
        public FakeCurrentUserContext User { get; }
        public FakeTenantContext Tenant { get; } = new(TaskTestData.Tenant);
        public ITaskWriteGate Gate { get; }
        public FakeTaskReviewService Reviews { get; } = new();
        public FakeTaskApprovalService Approvals { get; } = new();

        public bool GateOpen => ReferenceEquals(Gate, TaskWriteGateDoubles.Open);

        public void AssertGateFirst(Response<NoContent> answer, bool reviewOnly)
        {
            if (GateOpen)
            {
                AssertWithdrawn(reviewOnly);
                return;
            }

            Assert.Equal(404, answer.StatusCode);
            Assert.Equal(TaskReasonCodes.NotFound, answer.ReasonCode);
            AssertNothingWithdrawn();
        }

        public void AssertNothingWithdrawn()
        {
            Assert.Empty(Reviews.Cancelled);
            Assert.Empty(Approvals.Cancelled);
        }

        /// <summary>Non-vacuity: with the gate open, the same task reaches the withdraw.</summary>
        public void AssertWithdrawn(bool reviewOnly)
        {
            Assert.Equal([Task.Id], Reviews.Cancelled);
            if (!reviewOnly)
            {
                Assert.Equal([Task.Id], Approvals.Cancelled);
            }
        }
    }

    /// <summary>A read rule that admits nobody: the production gate answers 404 for every task.</summary>
    private sealed class NobodyReads : ITaskReadAccessPolicy
    {
        public Task<bool> CanReadAsync(TaskItem task, Guid actorUserId, CancellationToken ct) => Task.FromResult(false);

        public Task<IReadOnlySet<Guid>> ResolveDataLegCandidatesAsync(TaskItem task, CancellationToken ct)
            => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
    }
}
