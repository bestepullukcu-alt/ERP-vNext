using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Services.WorkAggregation;
using Diten.Platform.Infrastructure.Settings;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// WP-PSS-MOD0024-CLOSURE-ENVELOPE-2A-REST-01 — the one gap named by Faz 2a's own boundary note: a type with a
/// REQUIRED closure field could not be completed from the Task Center, because the generic dispatch envelope had
/// nowhere to carry the value.
///
/// <para>Three promises, each its own group below: (a) ONLY `complete` reads <c>ClosureFieldValues</c> — the
/// other four actions that share the SAME <c>TaskTransitionRequest</c> object (accept, release, start,
/// submitReview, cancel) never carry it onward, exactly as before this field existed; (b) MOD-0023 (the remote
/// bridge) never sees it, regardless of what it carries, and a null one produces a BYTE-IDENTICAL outbound body;
/// (c) the real handler chain — 400 without a required value, success with one, and the value lands in
/// <c>TaskItem.FieldValues</c>.</para>
/// </summary>
public sealed class ClosureFieldDispatchTests
{
    private static readonly Guid Tenant = TaskTestData.Tenant;
    private static readonly WorkItemActor Actor = new(TaskTestData.Me, true, new HashSet<string>());

    private static readonly WorkItemFieldValueDto[] OneValue =
        [new WorkItemFieldValueDto("closure.note", "Text", "root cause found")];

    // ── (a) every action that shares `transition`: only complete is rewritten ──────────────────────────────

    [Theory]
    [InlineData("accept")]
    [InlineData("release")]
    [InlineData("start")]
    [InlineData("submitReview")]
    [InlineData("cancel")]
    public async Task The_shared_transition_object_never_carries_ClosureFieldValues_for_any_OTHER_action(string actionCode)
    {
        /*
         * ⚠ SABOTAGE THIS CATCHES: if the dispatcher started building `transition` WITH `ClosureFieldValues` set
         * (instead of only overriding it on the `complete` branch via `with`), every one of these five actions
         * would start carrying it — accept/release/start/submitReview/cancel all take the exact same
         * `TaskTransitionRequest` the `complete` branch rewrites, so this is the one place the "complete-only"
         * promise can silently stop being true.
         */
        var mediator = new RecordingMediator();
        var dispatcher = new TaskWorkItemActionDispatcher(mediator);

        await dispatcher.DispatchAsync(new WorkItemActionDispatchRequest(
            Guid.NewGuid(), actionCode,
            new WorkItemActionPayloadDto(ExpectedVersion: 1, ClosureFieldValues: OneValue),
            Actor, "corr"));

        Assert.NotNull(mediator.Captured);
        Assert.Null(mediator.Captured!.ClosureFieldValues);
    }

    [Fact]
    public async Task Complete_is_the_one_exception__its_transition_DOES_carry_the_mapped_values()
    {
        var mediator = new RecordingMediator();
        var dispatcher = new TaskWorkItemActionDispatcher(mediator);

        await dispatcher.DispatchAsync(new WorkItemActionDispatchRequest(
            Guid.NewGuid(), "complete",
            new WorkItemActionPayloadDto(ExpectedVersion: 1, ClosureFieldValues: OneValue),
            Actor, "corr"));

        var carried = Assert.Single(mediator.Captured!.ClosureFieldValues!);
        Assert.Equal("closure.note", carried.DefinitionCode);
        Assert.Equal(TaskFieldValueType.Text, carried.ValueType);
        Assert.Equal("root cause found", carried.Value);
    }

    /// <summary>
    /// Captures the <c>TaskTransitionRequest</c> off WHATEVER command arrives — accept/release/start/
    /// submitReview/cancel are four different command TYPES that all happen to carry one as their own
    /// <c>Request</c> property, and reflection here is what lets one test harness cover all five without a
    /// handler for each.
    /// </summary>
    private sealed class RecordingMediator : IMediator
    {
        public TaskTransitionRequest? Captured { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            Captured = request.GetType().GetProperty("Request", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(request) as TaskTransitionRequest;
            return Task.FromResult((TResponse)(object)Response<NoContent>.Success(204, "corr"));
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task Publish(object notification, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => Task.CompletedTask;
    }

    // ── (b) HttpWorkItemActionDispatcher: MOD-0023 never sees it, and absence changes nothing on the wire ───

    [Fact]
    public async Task A_null_ClosureFieldValues_produces_a_body_with_no_such_key_at_all()
    {
        string? body = null;

        await Send(new WorkItemActionPayloadDto(ExpectedVersion: 3, ReasonCode: "OK"), capturedInto: b => body = b);

        Assert.DoesNotContain("closureFieldValues", body);
    }

    [Fact]
    public async Task A_populated_ClosureFieldValues_is_stripped_before_MOD_0023_ever_sees_the_request()
    {
        string? body = null;

        await Send(
            new WorkItemActionPayloadDto(ExpectedVersion: 3, ClosureFieldValues: OneValue),
            capturedInto: b => body = b);

        Assert.DoesNotContain("closureFieldValues", body);
        Assert.DoesNotContain("root cause found", body);
    }

    [Fact]
    public async Task The_rest_of_the_body_is_untouched_whether_or_not_ClosureFieldValues_was_sent()
    {
        string? withoutIt = null;
        string? withIt = null;

        await Send(new WorkItemActionPayloadDto(ExpectedVersion: 3, ReasonCode: "OK"), b => withoutIt = b);
        await Send(new WorkItemActionPayloadDto(ExpectedVersion: 3, ReasonCode: "OK", ClosureFieldValues: OneValue), b => withIt = b);

        Assert.Equal(withoutIt, withIt);
    }

    private static async Task Send(WorkItemActionPayloadDto payload, Action<string?> capturedInto)
    {
        var row = new RemoteWorkItemProviderOptions { ProviderCode = "approvals", ContractVersion = "1.0", BaseUrl = "http://approvals.local" };
        row.Actions["approve"] = "approvals.approve";
        string? captured = null;

        var factory = new CapturingClientFactory(async (req, ct) =>
        {
            // Read it HERE, while the request's content is still alive — by the time DispatchAsync returns,
            // HttpClient has already disposed it.
            captured = req.Content is null ? null : await req.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Response<WorkItemActionResultDto>.Success(
                    new WorkItemActionResultDto("x", "approvals", "approve")))
            };
        });
        var gateway = new RemoteWorkItemGateway(factory, new NullHttpContextAccessor(), new FakeTenantContext(Tenant));
        var dispatcher = new HttpWorkItemActionDispatcher(
            row, gateway, Options.Create(new WorkAggregationResilienceOptions { ProviderTimeout = TimeSpan.FromSeconds(5) }),
            NullLogger<HttpWorkItemActionDispatcher>.Instance);

        await dispatcher.DispatchAsync(new WorkItemActionDispatchRequest(Guid.NewGuid(), "approve", payload, Actor, "corr"));

        capturedInto(captured);
    }

    private sealed class CapturingClientFactory(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply)
        : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler(reply)) { Timeout = Timeout.InfiniteTimeSpan };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => reply(request, ct);
    }

    private sealed class NullHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; } = new DefaultHttpContext();
    }

    // ── (c) the real handler chain, end to end ───────────────────────────────────────────────────────────────

    private static TaskFieldDefinition ClosureField(string code, bool required) => new()
    {
        TenantId = Tenant, Code = code, LabelText = code, ValueType = TaskFieldValueType.Text, Section = "Closure",
        Stage = TaskFieldStage.Closure, IsRequired = required
    };

    private static TaskItem OpenTask() => new()
    {
        TenantId = Tenant,
        OrganizationUnitId = Guid.Parse("66666666-6666-6666-6666-666666666666"),
        Title = "Close me",
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Me,
        Lifecycle = TaskLifecycle.InProgress
    };

    private static (TaskWorkItemActionDispatcher Dispatcher, TaskItem Task) RealDispatcher(
        ITaskFieldDefinitionService fieldDefinitions)
    {
        var task = OpenTask();
        var tasks = new FakeTaskItemRepository(task);
        var handler = new TransitionTaskItemHandler(
            tasks, new TaskLifecycleService(), new FakeCurrentUserContext(TaskTestData.Me),
            new FakeChecklistRunRepository(), new TaskChecklistService(), new FakeWorkflowTransitionGate(),
            new FakeTaskDependencyRepository(), new FakeTaskTypeRepository(), new FakeTaskNotificationService(),
            fieldDefinitions, new FakeTaskAttachmentRepository(), NullLogger<TransitionTaskItemHandler>.Instance);
        // (a) above already proves the other four actions never reach this far with a closure value, so only
        // `TransitionTaskItemCommand` needs a real handler — the shared double every other Tasks test uses.
        return (new TaskWorkItemActionDispatcher(new DirectMediator(handler)), task);
    }

    [Fact]
    public async Task End_to_end__completing_with_no_payload_refuses_with_the_pack_s_own_code()
    {
        var service = new TaskFieldDefinitionService(
            new FakeTaskFieldDefinitionRepository(ClosureField("closure.note", required: true)),
            TaskRecordSourceDoubles.None, TaskActors.PermitAll());
        var (dispatcher, task) = RealDispatcher(service);

        var result = await dispatcher.DispatchAsync(new WorkItemActionDispatchRequest(
            task.Id, "complete", new WorkItemActionPayloadDto(ExpectedVersion: task.Version), Actor, "corr"));

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ClosureFieldRequired, result.ReasonCode);
    }

    [Fact]
    public async Task End_to_end__completing_WITH_the_payload_closes_the_task_and_stores_the_value()
    {
        var service = new TaskFieldDefinitionService(
            new FakeTaskFieldDefinitionRepository(ClosureField("closure.note", required: true)),
            TaskRecordSourceDoubles.None, TaskActors.PermitAll());
        var (dispatcher, task) = RealDispatcher(service);

        var result = await dispatcher.DispatchAsync(new WorkItemActionDispatchRequest(
            task.Id, "complete",
            new WorkItemActionPayloadDto(ExpectedVersion: task.Version, ClosureFieldValues: OneValue),
            Actor, "corr"));

        Assert.True(result.IsSuccessful);
        Assert.Equal(TaskLifecycle.Done, task.Lifecycle);
        Assert.Equal("root cause found", task.FieldValues.Single(v => v.DefinitionCode == "closure.note").Value);
    }

}
