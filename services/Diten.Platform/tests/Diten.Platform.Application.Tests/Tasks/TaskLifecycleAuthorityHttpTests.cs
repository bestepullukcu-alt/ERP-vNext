using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-361 at the WIRE — <c>start</c>/<c>complete</c>/<c>submitReview</c> refuse a non-holder and <c>plan</c>
/// refuses anybody but the holder or the requester, through the real routing, the real <c>[HasPermission]</c>
/// filter and the real handlers. Only the repositories/MOD-0023 seams are doubles.
///
/// <para>The actor's IDENTITY is a fixed double (<see cref="FakeCurrentUserContext"/>) rather than derived from a
/// claim, exactly as <see cref="TaskAssignmentWriteGuardHttpTests"/> already does — the permission GATE is what
/// this level exists to prove is real; who the caller is varies through the TASK's own fields between cases.</para>
/// </summary>
public sealed class TaskLifecycleAuthorityHttpTests
{
    private const string UpdateOnly = TaskPermissions.Update;
    private const string CompleteOnly = TaskPermissions.Complete;

    // ── AC1/AC2 — start ───────────────────────────────────────────────────────

    [Fact]
    public async Task Start_by_a_NON_HOLDER_is_403_and_the_task_is_untouched()
    {
        using var host = new Host();
        var task = host.Seed(assignee: TaskTestData.Other, creator: TaskTestData.Rival);

        var response = await host.PostAsync($"/api/v1/tasks/{task.Id}/start", UpdateOnly, ExpectedVersion(task));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(TaskLifecycle.Open, host.Tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task Start_by_the_HOLDER_succeeds()
    {
        using var host = new Host();
        var task = host.Seed(assignee: TaskTestData.Me, creator: TaskTestData.Rival);

        var response = await host.PostAsync($"/api/v1/tasks/{task.Id}/start", UpdateOnly, ExpectedVersion(task));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(TaskLifecycle.InProgress, host.Tasks.Items.Single().Lifecycle);
    }

    // ── AC3 — complete, submitReview ──────────────────────────────────────────

    [Fact]
    public async Task Complete_by_a_NON_HOLDER_is_403_and_the_task_is_untouched()
    {
        using var host = new Host();
        var task = host.Seed(assignee: TaskTestData.Other, creator: TaskTestData.Rival, lifecycle: TaskLifecycle.InProgress);

        var response = await host.PostAsync($"/api/v1/tasks/{task.Id}/complete", CompleteOnly, ExpectedVersion(task));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(TaskLifecycle.InProgress, host.Tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task Complete_by_the_HOLDER_succeeds()
    {
        using var host = new Host();
        var task = host.Seed(assignee: TaskTestData.Me, creator: TaskTestData.Rival, lifecycle: TaskLifecycle.InProgress);

        var response = await host.PostAsync($"/api/v1/tasks/{task.Id}/complete", CompleteOnly, ExpectedVersion(task));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(TaskLifecycle.Done, host.Tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task SubmitReview_by_a_NON_HOLDER_is_403_and_the_task_is_untouched()
    {
        using var host = new Host();
        var task = host.Seed(
            assignee: TaskTestData.Other, creator: TaskTestData.Rival, lifecycle: TaskLifecycle.InProgress);
        task.ReviewRequired = true;

        var response = await host.PostAsync($"/api/v1/tasks/{task.Id}/submitReview", UpdateOnly, ExpectedVersion(task));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(TaskLifecycle.InProgress, host.Tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task SubmitReview_by_the_HOLDER_succeeds()
    {
        using var host = new Host();
        var task = host.Seed(assignee: TaskTestData.Me, creator: TaskTestData.Rival, lifecycle: TaskLifecycle.InProgress);
        task.ReviewRequired = true;

        var response = await host.PostAsync($"/api/v1/tasks/{task.Id}/submitReview", UpdateOnly, ExpectedVersion(task));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(TaskLifecycle.PendingReview, host.Tasks.Items.Single().Lifecycle);
    }

    // ── AC4 — plan: holder yes, requester yes, third party no ────────────────

    [Fact]
    public async Task Plan_by_the_HOLDER_succeeds()
    {
        using var host = new Host();
        var task = host.Seed(assignee: TaskTestData.Me, creator: TaskTestData.Rival);

        var response = await host.PostPlanAsync(task.Id, ExpectedVersion(task));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(TaskLifecycle.Planned, host.Tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task Plan_by_the_REQUESTER_who_does_NOT_hold_it_succeeds()
    {
        using var host = new Host();
        var task = host.Seed(assignee: TaskTestData.Other, creator: TaskTestData.Me);

        var response = await host.PostPlanAsync(task.Id, ExpectedVersion(task));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(TaskLifecycle.Planned, host.Tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task Plan_by_a_THIRD_PARTY_neither_holder_nor_requester_is_403_and_the_task_is_untouched()
    {
        using var host = new Host();
        var task = host.Seed(assignee: TaskTestData.Other, creator: TaskTestData.Rival);

        var response = await host.PostPlanAsync(task.Id, ExpectedVersion(task));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(TaskLifecycle.Open, host.Tasks.Items.Single().Lifecycle);
    }

    // ── AC5 — self-opened, self-held ──────────────────────────────────────────

    [Fact]
    public async Task A_task_I_opened_for_MYSELF_lets_me_start_plan_and_complete_it()
    {
        using var host = new Host();
        var task = host.Seed(assignee: TaskTestData.Me, creator: TaskTestData.Me);

        var planned = await host.PostPlanAsync(task.Id, ExpectedVersion(task));
        Assert.Equal(HttpStatusCode.NoContent, planned.StatusCode);

        var started = await host.PostAsync(
            $"/api/v1/tasks/{task.Id}/start", UpdateOnly, ExpectedVersion(host.Tasks.Items.Single()));
        Assert.Equal(HttpStatusCode.NoContent, started.StatusCode);

        var completed = await host.PostAsync(
            $"/api/v1/tasks/{task.Id}/complete", CompleteOnly, ExpectedVersion(host.Tasks.Items.Single()));
        Assert.Equal(HttpStatusCode.NoContent, completed.StatusCode);
        Assert.Equal(TaskLifecycle.Done, host.Tasks.Items.Single().Lifecycle);
    }

    // ── AC8 — the actor check does not bypass the approval gate ─────────────

    [Fact]
    public async Task An_APPROVAL_PENDING_task_still_refuses_the_HOLDERs_start_with_409()
    {
        using var host = new Host();
        var task = host.Seed(assignee: TaskTestData.Me, creator: TaskTestData.Rival);
        task.ApprovalRequired = true;
        task.WorkflowInstanceId = Guid.NewGuid();
        host.WorkflowGate.Blocked = true;

        var response = await host.PostAsync($"/api/v1/tasks/{task.Id}/start", UpdateOnly, ExpectedVersion(task));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode); // 409
        Assert.Equal(TaskLifecycle.Open, host.Tasks.Items.Single().Lifecycle);
        // The gate really was asked FOR this actor — the holder check did not short-circuit around it.
        Assert.Single(host.WorkflowGate.Calls);
    }

    private static int ExpectedVersion(TaskItem task) => task.Version;

    private sealed class Host : IDisposable
    {
        private readonly TestServer _server;

        public Host()
        {
            var builder = new WebHostBuilder()
                .UseEnvironment("Test")
                .ConfigureServices(services =>
                {
                    services
                        .AddAuthentication(options =>
                        {
                            options.DefaultAuthenticateScheme = HeaderAuthentication.SchemeName;
                            options.DefaultChallengeScheme = HeaderAuthentication.SchemeName;
                        })
                        .AddScheme<AuthenticationSchemeOptions, HeaderAuthentication>(
                            HeaderAuthentication.SchemeName, _ => { });
                    services.AddAuthorization();
                    services.AddHttpContextAccessor();
                    services.AddScoped<ICorrelationContext>(_ =>
                    {
                        var correlation = new CorrelationContext();
                        correlation.SetCorrelationId("corr");
                        return correlation;
                    });
                    services.AddScoped<IMediator>(_ => Mediator());
                    services.AddControllers().AddApplicationPart(typeof(TasksController).Assembly);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });

            _server = new TestServer(builder);
        }

        public FakeTaskItemRepository Tasks { get; } = new();

        public FakeWorkflowTransitionGate WorkflowGate { get; } = new();

        /// <summary>A task seeded directly into the store, bypassing Create — this suite is about the LIFECYCLE
        /// verbs, not creation.</summary>
        public TaskItem Seed(Guid assignee, Guid creator, TaskLifecycle lifecycle = TaskLifecycle.Open)
        {
            var task = new TaskItem
            {
                TenantId = TaskTestData.Tenant,
                Title = "Yetki denemesi",
                AssignmentTarget = TaskAssignmentTarget.Person,
                AssigneeUserId = assignee,
                CreatedByUserId = creator,
                OrganizationUnitId = Guid.NewGuid(),
                Lifecycle = lifecycle,
                DelegationAllowed = true,
                Version = 1
            };
            // Synchronous wait is safe: the fake's CreateAsync does no real I/O (Task.FromResult), and this runs
            // before the host's async test body starts, never nested inside one.
            Tasks.CreateAsync(task, CancellationToken.None).GetAwaiter().GetResult();
            return task;
        }

        public async Task<HttpResponseMessage> PostAsync(string path, string permissions, int expectedVersion)
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add(HeaderAuthentication.PermissionsHeader, permissions);
            return await client.PostAsJsonAsync(path, new TaskTransitionRequest(expectedVersion, null, null));
        }

        public async Task<HttpResponseMessage> PostPlanAsync(Guid taskId, int expectedVersion)
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add(HeaderAuthentication.PermissionsHeader, TaskPermissions.Update);
            return await client.PostAsJsonAsync(
                $"/api/v1/tasks/{taskId}/plan",
                new PlanTaskItemRequest(expectedVersion, DateTimeOffset.UtcNow.AddDays(3)));
        }

        public void Dispose() => _server.Dispose();

        /// <summary>Routes exactly the four commands this suite exercises to the REAL handlers, over the same
        /// FakeTaskItemRepository every request in this host shares — no ValidationBehavior pipeline needed, since
        /// none of the four commands carries a registered FluentValidation validator.</summary>
        private IMediator Mediator()
        {
            var me = new FakeCurrentUserContext(TaskTestData.Me);

            var transitionHandler = new TransitionTaskItemHandler(
                Tasks,
                new TaskLifecycleService(),
                me,
                new FakeChecklistRunRepository(),
                new TaskChecklistService(),
                WorkflowGate,
                new FakeTaskDependencyRepository(),
                new FakeTaskTypeRepository(),
                new FakeTaskNotificationService(),
                new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
                new FakeTaskAttachmentRepository(),
                NullLogger<TransitionTaskItemHandler>.Instance);

            var submitReviewHandler = new SubmitTaskForReviewHandler(
                Tasks,
                new TaskLifecycleService(),
                me,
                new FakeTaskReviewService(),
                new FakeTaskApprovalService(),
                NullLogger<SubmitTaskForReviewHandler>.Instance);

            var planHandler = new PlanTaskItemHandler(Tasks, new TaskLifecycleService(), me);

            return new RoutingMediator(transitionHandler, submitReviewHandler, planHandler);
        }
    }

    private sealed class RoutingMediator(
        TransitionTaskItemHandler transition,
        SubmitTaskForReviewHandler submitReview,
        PlanTaskItemHandler plan) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => request switch
            {
                TransitionTaskItemCommand command => (Task<TResponse>)(object)transition.Handle(command, ct),
                SubmitTaskForReviewCommand command => (Task<TResponse>)(object)submitReview.Handle(command, ct),
                PlanTaskItemCommand command => (Task<TResponse>)(object)plan.Handle(command, ct),
                _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.")
            };

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

    /// <summary>A tenant user whose permission claims are whatever the test puts in one header — the same shape
    /// <see cref="TaskAssignmentWriteGuardHttpTests"/> uses.</summary>
    private sealed class HeaderAuthentication(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string PermissionsHeader = "X-Test-Permissions";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new("actor_type", "tenant_user"),
                new("tenant_id", TaskTestData.Tenant.ToString()),
                new("sub", TaskTestData.Me.ToString())
            };

            foreach (var key in Request.Headers[PermissionsHeader].ToString()
                         .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                claims.Add(new Claim("permission", key));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
