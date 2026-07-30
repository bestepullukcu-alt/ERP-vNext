using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Behaviors;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.Tasks.Validators;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Faz 3b follow-up — a review requested with nobody to review it is refused at the WRITE, through the real
/// <see cref="TasksController"/> action and the real validation pipeline.
///
/// <para><b>The defect.</b> MOD-0023 refuses to start an instance with an empty candidate list. Review had no
/// write-time rule at all, so the form produced exactly that task: created 201, started 204, and then
/// <c>submitReview</c> answered 409 forever with no way out. Approval never had this hole because it has carried
/// a create-time reviewer... manager rule since Phase 3 — the rule below is its missing twin.</para>
///
/// <para><b>Why the pipeline is real.</b> <see cref="ValidatingMediator"/> runs the production
/// <see cref="ValidationBehavior{TRequest,TResponse}"/> over the production validator before reaching the
/// handler, so these tests see the response shape the wire actually carries. That matters here: the behaviour's
/// reflective lookup for a two-argument <c>Response&lt;T&gt;.Fail</c> never matches the real four-argument one, so
/// a FluentValidation failure is THROWN and rendered as a code-less ValidationProblemDetails. The review rule is
/// therefore enforced in the handlers, where it can answer with <c>REVIEW_REVIEWER_REQUIRED</c> — and this
/// harness is what proves the pipeline does not quietly shadow it with something else.</para>
/// </summary>
public sealed class TaskReviewerRequiredHttpTests
{
    private static readonly Guid Unit = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // ── Create ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Creating_a_review_gated_task_with_NO_reviewer_is_refused()
    {
        var harness = new Harness();

        var response = await harness.CreateAsync(reviewRequired: true, reviewer: null);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        // A reason code, not just a message: the client routes on this to point at the field.
        Assert.Equal(TaskReasonCodes.ReviewerRequired, response.ReasonCode);
        // Nothing was stored: this is a refusal, not a warning.
        Assert.Empty(harness.Tasks.Items);
    }

    [Fact]
    public async Task Creating_a_review_gated_task_WITH_a_reviewer_succeeds_and_the_review_can_start()
    {
        /*
         * Non-vacuity, and the whole point in one test: the rule must not merely refuse, it must let through
         * exactly the task whose review MOD-0023 will actually accept.
         */
        var harness = new Harness();

        var created = await harness.CreateAsync(reviewRequired: true, reviewer: TaskTestData.Rival);

        Assert.True(created.IsSuccessful);
        var stored = Assert.Single(harness.Tasks.Items);
        Assert.True(stored.ReviewRequired);
        Assert.Equal(TaskTestData.Rival, stored.ReviewerCandidateUserId);

        stored.Lifecycle = TaskLifecycle.InProgress;
        var submitted = await harness.SubmitReviewAsync(stored.Id, stored.Version);

        Assert.True(submitted.IsSuccessful);
        Assert.Equal(TaskLifecycle.PendingReview, harness.Tasks.Items.Single().Lifecycle);
        Assert.NotNull(harness.Tasks.Items.Single().ReviewWorkflowInstanceId);
    }

    [Fact]
    public async Task A_task_with_no_review_requirement_needs_no_reviewer()
    {
        // The rule must be conditional. Demanding a reviewer from every task would break every ordinary create.
        var harness = new Harness();

        var response = await harness.CreateAsync(reviewRequired: false, reviewer: null);

        Assert.True(response.IsSuccessful);
        Assert.Single(harness.Tasks.Items);
    }

    [Fact]
    public async Task An_EMPTY_guid_is_not_a_reviewer()
    {
        // What an unfilled form field deserializes to. Letting it through would trade a clear 400 for a review
        // MOD-0023 refuses to start.
        var harness = new Harness();

        var response = await harness.CreateAsync(reviewRequired: true, reviewer: Guid.Empty);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewerRequired, response.ReasonCode);
    }

    // ── Update: the full-replace trap ────────────────────────────────────────

    [Fact]
    public async Task An_edit_that_DROPS_the_reviewer_is_refused()
    {
        /*
         * UpdateTaskItemRequest is a FULL REPLACE — ReviewRequired is a plain bool, not the nullable
         * "not editing this" that ApprovalRequired uses — so a payload that simply omits the reviewer would
         * otherwise strip it from a task whose review requirement is still on, and leave a review that can never
         * start. The create-time rule alone does not cover this; that is why one rule serves both paths.
         */
        var harness = new Harness();
        await harness.CreateAsync(reviewRequired: true, reviewer: TaskTestData.Rival);
        var stored = harness.Tasks.Items.Single();

        var response = await harness.UpdateAsync(stored.Id, stored.Version, reviewRequired: true, reviewer: null);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewerRequired, response.ReasonCode);
        // The stored reviewer survived the refused edit.
        Assert.Equal(TaskTestData.Rival, harness.Tasks.Items.Single().ReviewerCandidateUserId);
    }

    [Fact]
    public async Task An_edit_that_switches_the_review_OFF_may_drop_the_reviewer()
    {
        // Non-vacuity for the test above: without the requirement there is nothing to route, so the rule must
        // not fire. A rule that refused this would make the switch impossible to turn off.
        var harness = new Harness();
        await harness.CreateAsync(reviewRequired: true, reviewer: TaskTestData.Rival);
        var stored = harness.Tasks.Items.Single();

        var response = await harness.UpdateAsync(stored.Id, stored.Version, reviewRequired: false, reviewer: null);

        Assert.True(response.IsSuccessful);
        Assert.False(harness.Tasks.Items.Single().ReviewRequired);
    }

    [Fact]
    public async Task An_edit_may_REPOINT_the_reviewer()
    {
        var harness = new Harness();
        await harness.CreateAsync(reviewRequired: true, reviewer: TaskTestData.Rival);
        var stored = harness.Tasks.Items.Single();

        var response = await harness.UpdateAsync(
            stored.Id, stored.Version, reviewRequired: true, reviewer: TaskTestData.Me);

        Assert.True(response.IsSuccessful);
        Assert.Equal(TaskTestData.Me, harness.Tasks.Items.Single().ReviewerCandidateUserId);
    }

    // ── The two reason codes are two facts ───────────────────────────────────

    [Fact]
    public async Task A_review_that_could_not_be_STARTED_does_not_report_itself_as_waiting()
    {
        /*
         * REVIEW_START_FAILED, not REVIEW_PENDING. Nothing is waiting: the handoff was refused, there is no
         * reviewer holding anything, and the caller can retry. Reporting "waiting for the reviewer" pointed the
         * user at somebody who was never asked — which is precisely what the live 409 did.
         */
        var harness = new Harness();
        harness.Reviews.CannotStart = true;
        await harness.CreateAsync(reviewRequired: true, reviewer: TaskTestData.Rival);
        var stored = harness.Tasks.Items.Single();
        stored.Lifecycle = TaskLifecycle.InProgress;

        var response = await harness.SubmitReviewAsync(stored.Id, stored.Version);

        Assert.False(response.IsSuccessful);
        Assert.Equal(TaskReasonCodes.ReviewStartFailed, response.ReasonCode);
        Assert.NotEqual(TaskReasonCodes.ReviewPending, response.ReasonCode);
        // And the task did not move — nothing claims to be under a review that was never opened.
        Assert.Equal(TaskLifecycle.InProgress, harness.Tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public void The_three_review_reason_codes_are_distinct()
    {
        // Non-vacuity for the test above and for the completion gate: collapsing any two of them would make the
        // assertions there pass while the client could no longer tell the situations apart.
        var codes = new[]
        {
            TaskReasonCodes.ReviewPending,
            TaskReasonCodes.ReviewStartFailed,
            TaskReasonCodes.ReviewerRequired
        };

        Assert.Equal(3, codes.Distinct(StringComparer.Ordinal).Count());
    }

    // ── The completion gate's fail-closed rule, pinned ───────────────────────

    [Fact]
    public async Task Completion_is_refused_while_a_required_review_has_no_instance()
    {
        /*
         * The gate cannot help here: with no instance it answers "no workflow", which the gate contract treats as
         * ALLOWED. So a review-gated task that was never submitted would complete straight through it and the
         * requirement would be decorative. This pins the handler's own refusal, with a gate that permits
         * everything — if this passes, the refusal cannot have come from the gate.
         */
        var harness = new Harness();
        await harness.CreateAsync(reviewRequired: true, reviewer: TaskTestData.Rival);
        var stored = harness.Tasks.Items.Single();
        stored.Lifecycle = TaskLifecycle.InProgress;

        var response = await harness.CompleteAsync(stored.Id, stored.Version);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(TaskReasonCodes.ReviewPending, response.ReasonCode);
        Assert.Equal(TaskLifecycle.InProgress, harness.Tasks.Items.Single().Lifecycle);
    }

    // ── harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// The real controller over the real validation pipeline. Everything MOD-0024 owns is real; only MOD-0023 and
    /// the stores are doubles.
    /// </summary>
    private sealed class Harness
    {
        private readonly TasksController _controller;

        public Harness()
        {
            Tasks = new FakeTaskItemRepository();
            Reviews = new FakeTaskReviewService();

            var create = new CreateTaskItemHandler(
                Tasks,
                new FakeTaskAssignmentRepository(),
                new FakeTaskWatcherRepository(),
                new FakePositionRepository(),
                new FakeOrganizationUnitRepository(new OrganizationUnit
                {
                    Id = Unit,
                    TenantId = TaskTestData.Tenant,
                    Code = "HQ",
                    Name = "Genel Merkez",
                    LegalEntityId = Guid.NewGuid()
                }),
                new FakePositionAssignmentRepository(),
                new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository()),
                new TaskLifecycleService(),
                new FakeTaskApprovalService(),
                new FakeChecklistTemplateRepository(),
                new FakeChecklistRunRepository(),
                new TaskChecklistService(),
                new NoOpNotificationDispatchAdapter(),
                new FakeCurrentUserContext(TaskTestData.Me),
                new FakeTenantContext(TaskTestData.Tenant),
                NullLogger<CreateTaskItemHandler>.Instance);

            var update = new UpdateTaskItemHandler(
                Tasks,
                new FakeOrganizationUnitRepository(new OrganizationUnit
                {
                    Id = Unit,
                    TenantId = TaskTestData.Tenant,
                    Code = "HQ",
                    Name = "Genel Merkez",
                    LegalEntityId = Guid.NewGuid()
                }),
                new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository()),
                new FakeCurrentUserContext(TaskTestData.Me),
                new FakeTaskApprovalService(),
                Reviews,
                NullLogger<UpdateTaskItemHandler>.Instance);

            var submit = new SubmitTaskForReviewHandler(
                Tasks,
                new TaskLifecycleService(),
                new FakeCurrentUserContext(TaskTestData.Me),
                Reviews,
                new FakeTaskApprovalService(),
                NullLogger<SubmitTaskForReviewHandler>.Instance);

            var transition = new TransitionTaskItemHandler(
                Tasks,
                new TaskLifecycleService(),
                new FakeCurrentUserContext(TaskTestData.Me),
                new FakeChecklistRunRepository(),
                new TaskChecklistService(),
                // Permits everything, so any refusal below is MOD-0024's own rule.
                new PassingWorkflowGate(),
                new FakeTaskDependencyRepository());

            var correlation = new CorrelationContext();
            correlation.SetCorrelationId("corr");
            _controller = new TasksController(
                new ValidatingMediator(create, update, submit, transition), correlation)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public FakeTaskItemRepository Tasks { get; }

        public FakeTaskReviewService Reviews { get; }

        public async Task<Response<Guid>> CreateAsync(bool reviewRequired, Guid? reviewer)
            => Unwrap<Guid>(await _controller.Create(
                new CreateTaskItemRequest(
                    Title: "İncelenecek iş",
                    Description: null,
                    Priority: TaskPriority.Medium,
                    AssignmentTarget: TaskAssignmentTarget.SelfAssigned,
                    AssigneeUserId: null,
                    PoolPositionId: null,
                    OrganizationUnitId: Unit,
                    DueAt: DateTimeOffset.UtcNow.AddDays(3),
                    StartAt: null,
                    PlannedDate: null,
                    EstimateHours: null,
                    Tags: null,
                    ReviewRequired: reviewRequired,
                    ApprovalRequired: false,
                    ApprovalManagerUserId: null,
                    EmailNotificationsEnabled: false,
                    DelegationAllowed: false,
                    FieldValues: null,
                    Watchers: null,
                    ReviewerCandidateUserId: reviewer),
                CancellationToken.None));

        public async Task<Response<NoContent>> UpdateAsync(
            Guid id, int expectedVersion, bool reviewRequired, Guid? reviewer)
            => Unwrap<NoContent>(await _controller.Update(
                id,
                new UpdateTaskItemRequest(
                    Title: "İncelenecek iş",
                    Description: null,
                    Priority: TaskPriority.Medium,
                    OrganizationUnitId: Unit,
                    DueAt: DateTimeOffset.UtcNow.AddDays(3),
                    StartAt: null,
                    PlannedDate: null,
                    EstimateHours: null,
                    Tags: null,
                    ReviewRequired: reviewRequired,
                    EmailNotificationsEnabled: false,
                    DelegationAllowed: false,
                    FieldValues: null,
                    ExpectedVersion: expectedVersion,
                    ReviewerCandidateUserId: reviewer),
                CancellationToken.None));

        public async Task<Response<NoContent>> SubmitReviewAsync(Guid id, int expectedVersion)
            => Unwrap<NoContent>(await _controller.SubmitReview(
                id, new TaskTransitionRequest(expectedVersion, null, null), CancellationToken.None));

        public async Task<Response<NoContent>> CompleteAsync(Guid id, int expectedVersion)
            => Unwrap<NoContent>(await _controller.Complete(
                id, new TaskTransitionRequest(expectedVersion, null, null), CancellationToken.None));

        /// <summary>
        /// The controller copies the response's status code onto the HTTP result verbatim, so asserting on the
        /// unwrapped envelope is asserting on the wire.
        /// </summary>
        private static Response<T> Unwrap<T>(IActionResult result)
            // 204 carries no body by design, so a NoContentResult IS the success envelope.
            => result is NoContentResult
                ? Response<T>.Success(204, "corr")
                : (Response<T>)((ObjectResult)result).Value!;
    }

    /// <summary>
    /// Dispatches through the production <see cref="ValidationBehavior{TRequest,TResponse}"/> with the production
    /// validator, then to the real handler — so a rule that exists in the handler but not the validator (or the
    /// reverse) is still covered, and a rule that exists in neither cannot slip through.
    /// </summary>
    private sealed class ValidatingMediator(
        CreateTaskItemHandler create,
        UpdateTaskItemHandler update,
        SubmitTaskForReviewHandler submit,
        TransitionTaskItemHandler transition) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => request switch
            {
                // The response type is concrete in each branch so the behaviour's own generic constraint holds —
                // the cast is to TResponse only, never around the pipeline.
                CreateTaskItemCommand command => (Task<TResponse>)(object)Through(
                    command, [new CreateTaskItemValidator()], () => create.Handle(command, ct)),
                UpdateTaskItemCommand command => (Task<TResponse>)(object)Through(
                    command, [], () => update.Handle(command, ct)),
                SubmitTaskForReviewCommand command => (Task<TResponse>)(object)Through(
                    command, [], () => submit.Handle(command, ct)),
                TransitionTaskItemCommand command => (Task<TResponse>)(object)Through(
                    command, [], () => transition.Handle(command, ct)),
                _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.")
            };

        private static Task<TResponse> Through<TRequest, TResponse>(
            TRequest request,
            IValidator<TRequest>[] validators,
            RequestHandlerDelegate<TResponse> next)
            where TRequest : IRequest<TResponse>
            => new ValidationBehavior<TRequest, TResponse>(validators)
                .Handle(request, next, CancellationToken.None);

        public Task<object?> Send(object request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification
            => throw new NotSupportedException();
    }
}
