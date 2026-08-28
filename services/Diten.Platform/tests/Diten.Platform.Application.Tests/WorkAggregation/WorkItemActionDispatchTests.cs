using System.Security.Claims;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.Workflow;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

/// <summary>
/// WC-D2 (DCP-004 §2 D2) — THE GUARDS FOR "A BUTTON REACHES A BACKEND".
///
/// <para>The measured defect: the projection publishes an authoritative actions[] and names no endpoint, so the
/// browser held a one-entry address book (<c>providerCode === 'tasks'</c>). MOD-0023 has had four live approval
/// endpoints behind its items since WC-1 and not one button ever reached them.</para>
///
/// <para>Each test below is one of the guards the round was scoped around, and the important one is the last
/// group: an action from a provider that is NOT <c>tasks</c> arriving at the command that owns it. Proving the
/// seam on MOD-0024 alone would be the documented defect re-shipped with a nicer URL.</para>
/// </summary>
public sealed class WorkItemActionDispatchTests
{
    // ── (a) EVERY PROVIDER THAT PUBLISHES ACTIONS HAS A DISPATCHER ────────────

    /// <summary>
    /// Assembly-wide, so a THIRD provider added tomorrow cannot ship dead buttons: it either dispatches or it
    /// fails here, before a user ever presses anything.
    /// </summary>
    [Fact]
    public void Every_provider_in_the_assembly_has_a_dispatcher_for_its_code()
    {
        var providers = ConcreteImplementations<IWorkItemProvider>();
        var dispatchers = ConcreteImplementations<IWorkItemActionDispatcher>();

        // Non-vacuity: this must not pass because the seam was renamed and nothing was found.
        Assert.True(providers.Count >= 2, "expected at least the workflow and task providers");

        var dispatcherCodes = dispatchers
            .Select(t => ((IWorkItemActionDispatcher)Activator.CreateInstance(t, new RecordingMediator())!).ProviderCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var providerCode in new[] { WorkItemContract.ProviderCodeTasks, WorkItemContract.ProviderCodeWorkflow })
        {
            Assert.Contains(providerCode, dispatcherCodes);
        }

        Assert.Equal(providers.Count, dispatchers.Count);
    }

    /// <summary>
    /// The action codes are not restated here — real items are PROJECTED and whatever they publish must be
    /// dispatchable. A provider that grows a twelfth action without teaching the dispatcher about it fails here.
    /// </summary>
    [Theory]
    [InlineData(TaskAssignmentTarget.SelfAssigned, TaskLifecycle.Open)]
    [InlineData(TaskAssignmentTarget.SelfAssigned, TaskLifecycle.InProgress)]
    [InlineData(TaskAssignmentTarget.SelfAssigned, TaskLifecycle.Waiting)]
    [InlineData(TaskAssignmentTarget.Person, TaskLifecycle.Open)]
    [InlineData(TaskAssignmentTarget.Person, TaskLifecycle.Planned)]
    public async Task Every_action_the_task_provider_projects_is_dispatchable(
        TaskAssignmentTarget target, TaskLifecycle lifecycle)
    {
        var task = SelfTask();
        task.AssignmentTarget = target;
        task.Lifecycle = lifecycle;
        task.CreatedByUserId = Guid.NewGuid();   // a separate requester, so `return` is offered too

        var provider = TaskProvider(task);
        var items = await provider.GetWorkItemsAsync(
            GrantedActor(provider.RequiredActionPermissions), CancellationToken.None);

        var dispatcher = new TaskWorkItemActionDispatcher(new RecordingMediator());
        var codes = Assert.Single(items).Actions.Select(a => a.Code).ToList();

        Assert.NotEmpty(codes);
        foreach (var code in codes)
        {
            Assert.True(dispatcher.CanDispatch(code), $"'{code}' is projected but has no dispatch path.");
        }
    }

    [Fact]
    public void Every_action_the_workflow_provider_projects_is_dispatchable()
    {
        var projection = new WorkItemProjectionService(SlaForTests.Real())
            .Project(ApprovalTaskAt(ApprovalTaskStatus.WaitingApproval), Instance(), AllWorkflowPermissions(),
                WorkItemContract.ProviderCodeWorkflow, "1.0");

        var dispatcher = new WorkflowApprovalWorkItemActionDispatcher(new RecordingMediator());
        var codes = projection!.Actions.Select(a => a.Code).ToList();

        Assert.NotEmpty(codes);
        foreach (var code in codes)
        {
            Assert.True(dispatcher.CanDispatch(code), $"'{code}' is projected but has no dispatch path.");
        }
    }

    /// <summary>
    /// The read seam stays READ-ONLY, in the compiler rather than in a comment. Its own header promises this and
    /// the aggregation handler's per-provider isolation is argued from it.
    /// </summary>
    [Fact]
    public void The_read_provider_interface_has_no_write_method()
    {
        var names = typeof(IWorkItemProvider).GetMethods().Select(m => m.Name).ToList();

        Assert.DoesNotContain(names, n => n.Contains("Dispatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Execute", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Perform", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A dispatcher may not invent a permission. Every key it names must already be declared by the matching
    /// provider — which is what the API layer evaluates from claims — so the two cannot drift into two lists.
    /// </summary>
    [Fact]
    public void No_dispatcher_names_a_permission_its_provider_does_not_declare()
    {
        AssertPermissionsDeclared(
            new TaskWorkItemActionDispatcher(new RecordingMediator()),
            TaskProvider(SelfTask()).RequiredActionPermissions);

        AssertPermissionsDeclared(
            new WorkflowApprovalWorkItemActionDispatcher(new RecordingMediator()),
            new WorkflowApprovalWorkItemProvider(null!, null!, null!, null!).RequiredActionPermissions);
    }

    // ── (b) A PROVIDER WITH NO DISPATCHER IS REFUSED, NOT IGNORED ─────────────

    [Fact]
    public async Task A_bound_provider_with_no_dispatcher_answers_a_stable_code_never_silence()
    {
        // A provider on the board with nothing behind it — the exact shape MOD-0023 was in before this round.
        var controller = Controller(
            providers: [new StubProvider("crm")],
            dispatchers: []);

        var result = await controller.DispatchAction(
            Guid.NewGuid(), "approve", new WorkItemActionRequestDto("crm"), CancellationToken.None);

        var response = Payload(result);
        Assert.False(response.IsSuccessful);
        Assert.Equal(WorkItemActionReasonCodes.ProviderNotDispatchable, response.ReasonCode);
        Assert.Equal(501, response.StatusCode);
    }

    [Fact]
    public async Task A_provider_nobody_bound_is_a_DIFFERENT_answer_from_one_that_cannot_write()
    {
        var controller = Controller(providers: [], dispatchers: []);

        var response = Payload(await controller.DispatchAction(
            Guid.NewGuid(), "approve", new WorkItemActionRequestDto("nowhere"), CancellationToken.None));

        Assert.Equal(WorkItemActionReasonCodes.ProviderUnknown, response.ReasonCode);
        Assert.Equal(404, response.StatusCode);
    }

    // ── (c) PERMISSION IS DECIDED ON THE SERVER ───────────────────────────────

    [Fact]
    public async Task A_caller_without_the_permission_is_refused_before_anything_is_dispatched()
    {
        var mediator = new RecordingMediator();
        var controller = Controller(
            providers: [new StubProvider(WorkItemContract.ProviderCodeWorkflow, WorkflowPermissions.TasksApprove)],
            dispatchers: [new WorkflowApprovalWorkItemActionDispatcher(mediator)],
            claims: []);   // authenticated, holding nothing

        var response = Payload(await controller.DispatchAction(
            Guid.NewGuid(), "approve",
            new WorkItemActionRequestDto(WorkItemContract.ProviderCodeWorkflow), CancellationToken.None));

        Assert.Equal(403, response.StatusCode);
        Assert.Equal(WorkItemActionReasonCodes.ActionForbidden, response.ReasonCode);
        // NOTHING reached the module: a refusal that still writes is not a refusal.
        Assert.Empty(mediator.Sent);
    }

    // ── (d) AN UNKNOWN ACTION CODE IS AN EXPLICIT ERROR ───────────────────────

    [Fact]
    public async Task An_action_the_provider_does_not_publish_is_refused_by_code()
    {
        var mediator = new RecordingMediator();
        var controller = Controller(
            providers: [new StubProvider(WorkItemContract.ProviderCodeTasks)],
            dispatchers: [new TaskWorkItemActionDispatcher(mediator)]);

        var response = Payload(await controller.DispatchAction(
            Guid.NewGuid(), "obliterate",
            new WorkItemActionRequestDto(WorkItemContract.ProviderCodeTasks), CancellationToken.None));

        Assert.Equal(400, response.StatusCode);
        Assert.Equal(WorkItemActionReasonCodes.ActionUnknown, response.ReasonCode);
        Assert.Empty(mediator.Sent);
    }

    // ── (e) THE MEASURE OF THE ROUND ──────────────────────────────────────────

    /// <summary>
    /// An action on an item owned by a provider that is NOT <c>tasks</c> reaches the server that owns it. This
    /// is the sentence DCP-004 §2 D2 says was false, made true.
    /// </summary>
    [Theory]
    [InlineData("approve", typeof(ApproveWorkflowTaskCommand))]
    [InlineData("reject", typeof(RejectWorkflowTaskCommand))]
    [InlineData("requestInfo", typeof(RequestInfoWorkflowTaskCommand))]
    public async Task A_workflow_providers_action_reaches_MOD_0023(string actionCode, Type expectedCommand)
    {
        var mediator = new RecordingMediator();
        var taskId = Guid.NewGuid();
        var controller = Controller(
            providers: [new StubProvider(WorkItemContract.ProviderCodeWorkflow)],
            dispatchers: [new WorkflowApprovalWorkItemActionDispatcher(mediator)],
            isPlatformActor: true);

        var response = Payload(await controller.DispatchAction(
            taskId, actionCode,
            new WorkItemActionRequestDto(
                WorkItemContract.ProviderCodeWorkflow,
                new WorkItemActionPayloadDto(Reason: "because")),
            CancellationToken.None));

        Assert.True(response.IsSuccessful);
        var sent = Assert.Single(mediator.Sent);
        Assert.IsType(expectedCommand, sent);
    }

    [Fact]
    public async Task The_approval_actor_is_the_signed_in_user_never_the_request_body()
    {
        // The MOD-0023 request DTOs carry an ActorId because they were written for service-to-service callers.
        // A browser-supplied one would let a caller decide on somebody else's behalf.
        var mediator = new RecordingMediator();
        var me = Guid.NewGuid();
        var controller = Controller(
            providers: [new StubProvider(WorkItemContract.ProviderCodeWorkflow)],
            dispatchers: [new WorkflowApprovalWorkItemActionDispatcher(mediator)],
            isPlatformActor: true,
            userId: me);

        await controller.DispatchAction(
            Guid.NewGuid(), "approve",
            new WorkItemActionRequestDto(WorkItemContract.ProviderCodeWorkflow), CancellationToken.None);

        var command = Assert.IsType<ApproveWorkflowTaskCommand>(Assert.Single(mediator.Sent));
        Assert.Equal(me.ToString(), command.Request.ActorId);
        Assert.False(string.IsNullOrWhiteSpace(command.Request.IdempotencyKey));
        Assert.False(string.IsNullOrWhiteSpace(command.Request.ReasonCode));
    }

    [Fact]
    public async Task A_delegation_with_nobody_to_delegate_to_is_refused_rather_than_sent()
    {
        var mediator = new RecordingMediator();
        var dispatcher = new WorkflowApprovalWorkItemActionDispatcher(mediator);

        var response = await dispatcher.DispatchAsync(new WorkItemActionDispatchRequest(
            Guid.NewGuid(), "delegate", new WorkItemActionPayloadDto(), PlatformActor(), "corr"));

        Assert.Equal(WorkItemActionReasonCodes.PayloadInvalid, response.ReasonCode);
        Assert.Empty(mediator.Sent);
    }

    // ── MOD-0024 keeps reaching its OWN commands, unchanged ───────────────────

    [Theory]
    [InlineData("start", typeof(TransitionTaskItemCommand))]
    [InlineData("complete", typeof(TransitionTaskItemCommand))]
    [InlineData("claim", typeof(ClaimTaskItemCommand))]
    [InlineData("accept", typeof(AcceptTaskItemCommand))]
    [InlineData("release", typeof(ReleaseTaskItemCommand))]
    [InlineData("submitReview", typeof(SubmitTaskForReviewCommand))]
    [InlineData("cancel", typeof(TransitionTaskItemCommand))]
    public async Task A_task_action_still_reaches_the_command_TasksController_sends(
        string actionCode, Type expectedCommand)
    {
        var mediator = new RecordingMediator();
        var dispatcher = new TaskWorkItemActionDispatcher(mediator);

        await dispatcher.DispatchAsync(new WorkItemActionDispatchRequest(
            Guid.NewGuid(), actionCode, new WorkItemActionPayloadDto(ExpectedVersion: 3), PlatformActor(), "corr"));

        Assert.IsType(expectedCommand, Assert.Single(mediator.Sent));
    }

    [Fact]
    public async Task The_three_actions_with_a_required_field_refuse_rather_than_guess()
    {
        // BL-043 again, one layer over: `inquire`/`return`/`reassign` need a REASON the DTO makes mandatory.
        // Refusing here names the missing field; forwarding an empty one would answer the caller with a
        // FluentValidation sentence in English about a property they have never heard of.
        var mediator = new RecordingMediator();
        var dispatcher = new TaskWorkItemActionDispatcher(mediator);

        foreach (var code in new[] { "inquire", "return", "reassign" })
        {
            var response = await dispatcher.DispatchAsync(new WorkItemActionDispatchRequest(
                Guid.NewGuid(), code, new WorkItemActionPayloadDto(ExpectedVersion: 1), PlatformActor(), "corr"));

            Assert.Equal(WorkItemActionReasonCodes.PayloadInvalid, response.ReasonCode);
        }

        Assert.Empty(mediator.Sent);
    }

    [Fact]
    public async Task Plan_carries_the_date_through_to_the_module_command()
    {
        var mediator = new RecordingMediator();
        var dispatcher = new TaskWorkItemActionDispatcher(mediator);
        var when = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

        await dispatcher.DispatchAsync(new WorkItemActionDispatchRequest(
            Guid.NewGuid(), "plan",
            new WorkItemActionPayloadDto(ExpectedVersion: 7, PlannedDate: when), PlatformActor(), "corr"));

        var command = Assert.IsType<PlanTaskItemCommand>(Assert.Single(mediator.Sent));
        Assert.Equal(when, command.Request.PlannedDate);
        Assert.Equal(7, command.Request.ExpectedVersion);
    }

    /// <summary>
    /// A module's refusal must arrive intact. The Task Center resolves its sentences from stable codes in seven
    /// languages, so a dispatcher that flattened a 409 into its own shape would make every refusal read "an
    /// error occurred".
    /// </summary>
    [Fact]
    public async Task A_modules_refusal_code_survives_the_dispatch()
    {
        var mediator = new RecordingMediator(
            Response<NoContent>.Fail("nope", 409, "TASK_CONCURRENCY_CONFLICT", "corr"));
        var dispatcher = new TaskWorkItemActionDispatcher(mediator);

        var response = await dispatcher.DispatchAsync(new WorkItemActionDispatchRequest(
            Guid.NewGuid(), "start", new WorkItemActionPayloadDto(ExpectedVersion: 1), PlatformActor(), "corr"));

        Assert.Equal(409, response.StatusCode);
        Assert.Equal("TASK_CONCURRENCY_CONFLICT", response.ReasonCode);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void AssertPermissionsDeclared(
        IWorkItemActionDispatcher dispatcher, IReadOnlyCollection<string> declared)
    {
        foreach (var code in dispatcher.SupportedActionCodes)
        {
            var key = dispatcher.RequiredPermission(code);
            Assert.False(string.IsNullOrWhiteSpace(key), $"'{code}' names no permission.");
            Assert.Contains(key!, declared, StringComparer.OrdinalIgnoreCase);
        }
    }

    private static List<Type> ConcreteImplementations<T>()
        => typeof(IWorkItemProvider).Assembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(T).IsAssignableFrom(t))
            .ToList();

    private static WorkItemsController Controller(
        IEnumerable<IWorkItemProvider> providers,
        IEnumerable<IWorkItemActionDispatcher> dispatchers,
        string[]? claims = null,
        bool isPlatformActor = false,
        Guid? userId = null)
    {
        var correlation = new CorrelationContext();
        correlation.SetCorrelationId("corr");

        var identityClaims = new List<Claim>();
        if (isPlatformActor)
        {
            identityClaims.Add(new Claim("actor_type", "platform_admin"));
        }

        foreach (var claim in claims ?? [])
        {
            identityClaims.Add(new Claim("permission", claim));
        }

        return new WorkItemsController(
            new RecordingMediator(),
            correlation,
            providers,
            dispatchers,
            new StubCurrentUser(userId ?? Guid.NewGuid()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(identityClaims, "test"))
                }
            }
        };
    }

    private static Response<WorkItemActionResultDto> Payload(IActionResult result)
        => (Response<WorkItemActionResultDto>)(result switch
        {
            ObjectResult obj => obj.Value!,
            _ => throw new InvalidOperationException($"unexpected result {result.GetType().Name}")
        });

    private static WorkItemActor PlatformActor()
        => new(Guid.NewGuid(), IsPlatformActor: true, new HashSet<string>());

    private static WorkItemActor GrantedActor(IEnumerable<string> permissions)
        => new(TaskTestData.Me, IsPlatformActor: false,
            new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase));

    private static WorkItemActor AllWorkflowPermissions()
        => new(WorkflowMe, IsPlatformActor: true, new HashSet<string>());

    private static readonly Guid WorkflowMe = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private static ApprovalTask ApprovalTaskAt(ApprovalTaskStatus status) => new()
    {
        TenantId = TaskTestData.Tenant,
        WorkflowInstanceId = Guid.NewGuid(),
        StageCode = "stage-1",
        StepCode = "step-1",
        Status = status,
        AssigneeRef = WorkflowMe.ToString()
    };

    private static WorkflowInstance Instance() => new()
    {
        TenantId = TaskTestData.Tenant,
        TemplateId = Guid.NewGuid(),
        WorkflowTemplateId = Guid.NewGuid(),
        ObjectType = "invoice",
        ObjectId = "INV-1",
        ObjectRef = "finance|invoice|INV-1"
    };

    private static TaskItem SelfTask() => new()
    {
        DelegationAllowed = true,
        TenantId = TaskTestData.Tenant,
        Title = "Write the report",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open,
        Version = 1
    };

    private static TaskWorkItemProvider TaskProvider(TaskItem task)
        => new(
            new FakeTaskItemRepository(task),
            new FakePositionAssignmentRepository(),
            new Application.Features.Tasks.Services.TaskLifecycleService(),
            new Application.Features.Tasks.Services.TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(), new FakeTaskApprovalService(), new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(),
            new FakeTaskPersonalOverlayRepository(), new FakeTaskWatcherRepository(), TaskActors.PermitAll(),
            new FakePositionRepository(), new FakeOrganizationUnitRepository(), SlaForTests.Real(),
            new FakeTaskFieldDefinitionRepository(), new FakeTaskTypeRepository());

    /// <summary>A provider that exists on the board and does nothing else — enough to answer "is it bound?".</summary>
    private sealed class StubProvider(string code, params string[] permissions) : IWorkItemProvider
    {
        public string ProviderCode { get; } = code;

        public string ProviderContractVersion => "1.0";

        public IReadOnlyCollection<string> RequiredActionPermissions { get; } = permissions;

        public Task<IReadOnlyList<WorkItemProjectionDto>> GetWorkItemsAsync(
            WorkItemActor actor, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WorkItemProjectionDto>>([]);
    }

    private sealed class StubCurrentUser(Guid userId) : ICurrentUserContext
    {
        public Guid UserId { get; } = userId;
        public string? Email => "me@diten.local";
        public string? DisplayName => "Me";
        public string ActorName => Email!;
        public bool IsAuthenticated => true;
    }

    /// <summary>
    /// Records what was sent WITHOUT running a handler: this suite asks "did the action reach the module that
    /// owns it", which is a routing question. Whether MOD-0023 then approves is MOD-0023's own suite.
    /// </summary>
    private sealed class RecordingMediator(object? answer = null) : IMediator
    {
        private readonly object? _answer = answer;

        public List<object> Sent { get; } = [];

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            Sent.Add(request);
            if (_answer is TResponse typed)
            {
                return Task.FromResult(typed);
            }

            var successful = typeof(TResponse)
                .GetMethod("Success", [typeof(int), typeof(string)])!
                .Invoke(null, [200, "corr"]);
            return Task.FromResult((TResponse)successful!);
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default)
            where TRequest : IRequest => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => Task.CompletedTask;
    }
}
