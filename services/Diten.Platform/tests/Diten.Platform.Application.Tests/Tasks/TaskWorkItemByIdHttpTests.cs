using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-414 — <c>GET api/v1/work-items/{id}</c> on the WIRE, with the REAL read rule.
///
/// <para>Real routing, the real <c>WorkItemsController</c> and its <c>[HasPermission]</c> gate, the real
/// <see cref="GetTaskWorkItemByIdHandler"/>, the real <see cref="TaskReadAccessPolicy"/> (its pool leg through the
/// real <see cref="TaskNotificationService"/> over one seat table, its scope leg through the real
/// <see cref="TaskAssignmentScopeResolver"/>, its read-all leg through the real claims reader) and the real
/// <see cref="TaskWorkItemProvider"/>. Only the stores are doubles. <c>TaskReadAccessWiringTests</c> proves wiring
/// against a policy double; this file proves each LEG reaches the wire as a 200, and each refusal as the same
/// 404.</para>
///
/// <para>The caller is always <see cref="TaskTestData.Me"/>; what varies is what Me is to the task.</para>
/// </summary>
public sealed class TaskWorkItemByIdHttpTests
{
    private static readonly Guid Me = TaskTestData.Me;
    private static readonly Guid Rival = TaskTestData.Rival;
    private static readonly Guid Other = TaskTestData.Other;
    private static readonly Guid PoolPosition = Guid.Parse("41400000-0000-0000-0000-0000000000a1");

    /// <summary>What a Task Center reader ordinarily holds: the surface key, the record key and the action keys —
    /// but NOT delete, so no administrative cancel can make a bystander's projection look like a holder's.</summary>
    private static readonly string[] ReaderPermissions =
    [
        WorkAggregationPermissions.InboxView,
        TaskPermissions.Read,
        TaskPermissions.Update,
        TaskPermissions.Claim,
        TaskPermissions.Complete,
        TaskPermissions.Cancel,
        TaskPermissions.Assign
    ];

    // ── every leg of the read rule answers 200 with the list's projection ───────────────────────────────────

    [Fact]
    public async Task The_assignee_reads_the_task()
    {
        var task = NewTask(assignee: Me, createdBy: Other);
        using var host = new Host([task]);

        var (status, body) = await host.GetAsync(task.Id);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains($"\"id\":\"{task.Id}\"", body);
        Assert.Contains("\"providerCode\":\"tasks\"", body);
        // The source door stays the RECORD page even when the item is read by id (TaskLinks.Record).
        Assert.Contains($"\"deepLink\":\"/Tasks/{task.Id}\"", body);
        // The holder's own act, computed for the caller.
        Assert.Contains("\"code\":\"accept\"", body);
        // WP-WCN-KANBAN-01 — proved on the real wire, not a unit serializer double: accept from Open carries
        // its Kanban drag target as a JSON string (mirrors AcceptTaskItemHandler; CT decision 2026-09-17).
        Assert.Contains("\"targetStatus\":\"InProgress\"", body);
    }

    [Fact]
    public async Task The_creator_reads_the_task_they_handed_to_somebody_else()
    {
        var task = NewTask(assignee: Rival, createdBy: Me);
        using var host = new Host([task]);

        var (status, body) = await host.GetAsync(task.Id);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains($"\"id\":\"{task.Id}\"", body);
        // The SAME relation the Outbox row states (BL-016) — decided by id exactly as the list decides it.
        Assert.Contains($"\"viewerRelation\":\"{WorkItemContract.ViewerRelationInitiator}\"", body);
    }

    [Fact]
    public async Task A_holder_of_the_pool_position_reads_the_unclaimed_pool_task()
    {
        var task = NewTask(target: TaskAssignmentTarget.PositionPool, poolPositionId: PoolPosition, createdBy: Other);
        using var host = new Host([task], seats: [Seat(PoolPosition, Me)]);

        var (status, body) = await host.GetAsync(task.Id);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains($"\"id\":\"{task.Id}\"", body);
        Assert.Contains("\"code\":\"claim\"", body);
    }

    [Fact]
    public async Task A_watcher_who_holds_nothing_reads_the_task_and_is_offered_no_action()
    {
        var task = NewTask(assignee: Rival, createdBy: Other);
        using var host = new Host(
            [task],
            watchers: [new TaskWatcher { TenantId = TaskTestData.Tenant, TaskItemId = task.Id, UserId = Me }]);

        var (status, body) = await host.GetAsync(task.Id);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains($"\"id\":\"{task.Id}\"", body);
        // Reading a task never adds a button: a watcher is neither holder nor requester.
        Assert.Contains("\"actions\":[]", body);
    }

    [Fact]
    public async Task A_ReadAll_holder_reads_a_task_they_have_no_relationship_to()
    {
        var task = NewTask(assignee: Rival, createdBy: Other);
        using var host = new Host([task]);

        var (status, body) = await host.GetAsync(task.Id, [.. ReaderPermissions, TaskPermissions.ReadAll]);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains($"\"id\":\"{task.Id}\"", body);
        Assert.Contains("\"actions\":[]", body);
    }

    [Fact]
    public async Task A_reader_whose_org_scope_covers_the_tasks_unit_reads_it()
    {
        var unit = new OrganizationUnit
        {
            Id = Guid.NewGuid(), TenantId = TaskTestData.Tenant, Code = "QA", Name = "Quality",
            LegalEntityId = Guid.NewGuid()
        };
        var task = NewTask(assignee: Rival, createdBy: Other, unitId: unit.Id);
        using var host = new Host(
            [task],
            units: [unit],
            scopes: [new EntitlementDataScope(EntitlementDataScopeKind.OrgUnit, unit.Id, "granted")]);

        var (status, body) = await host.GetAsync(task.Id);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains($"\"id\":\"{task.Id}\"", body);
    }

    // ── every refusal is the same 404 ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_unrelated_reader_gets_the_byte_identical_404_a_missing_id_gets()
    {
        var task = NewTask(assignee: Rival, createdBy: Other);
        using var host = new Host([task]);

        var (unreadableStatus, unreadableBody) = await host.GetAsync(task.Id);
        var (missingStatus, missingBody) = await host.GetAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, missingStatus);
        Assert.Contains(TaskReasonCodes.NotFound, missingBody);
        Assert.Equal(HttpStatusCode.NotFound, unreadableStatus);
        Assert.Equal(missingBody, unreadableBody);
        Assert.DoesNotContain(task.Title, unreadableBody);
    }

    [Fact]
    public async Task Another_tenants_task_is_the_same_404_even_for_its_own_assignee()
    {
        var foreign = NewTask(assignee: Me, createdBy: Me, tenant: TaskTestData.OtherTenant);
        using var host = new Host([foreign]);

        var (foreignStatus, foreignBody) = await host.GetAsync(foreign.Id);
        var (_, missingBody) = await host.GetAsync(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, foreignStatus);
        Assert.Equal(missingBody, foreignBody);
    }

    // ── the endpoint gates ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Without_tasks_read_the_read_is_refused_even_for_the_assignee()
    {
        // The record endpoint demands platform.tasks.read for this very read; without it here, this endpoint would
        // widen the rule it reuses.
        var task = NewTask(assignee: Me, createdBy: Other);
        using var host = new Host([task]);

        var (status, body) = await host.GetAsync(task.Id, ReaderPermissions.Where(k => k != TaskPermissions.Read).ToArray());

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.DoesNotContain(task.Title, body);
    }

    [Fact]
    public async Task The_inbox_key_is_no_longer_asked_for_the_read()
    {
        // DCP-004 "Decision amendment 2026-09-15" (BL-410) — the Task Center is every tenant user's surface; the read
        // rule and platform.tasks.read decide, not inbox.view.
        var task = NewTask(assignee: Me, createdBy: Other);
        using var host = new Host([task]);

        var (status, body) = await host.GetAsync(
            task.Id, ReaderPermissions.Where(k => k != WorkAggregationPermissions.InboxView).ToArray());

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains($"\"id\":\"{task.Id}\"", body);
    }

    [Fact]
    public async Task Without_a_token_the_read_is_401()
    {
        var task = NewTask(assignee: Me, createdBy: Other);
        using var host = new Host([task]);

        var response = await host.CreateAnonymousClient().GetAsync($"/api/v1/work-items/{task.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────

    private static TaskItem NewTask(
        Guid? assignee = null,
        Guid? createdBy = null,
        TaskAssignmentTarget target = TaskAssignmentTarget.Person,
        Guid? poolPositionId = null,
        Guid? tenant = null,
        Guid? unitId = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenant ?? TaskTestData.Tenant,
        Title = "Read-by-id probe",
        Lifecycle = TaskLifecycle.Open,
        AssignmentTarget = target,
        AssigneeUserId = assignee,
        PoolPositionId = poolPositionId,
        CreatedByUserId = createdBy ?? Other,
        OrganizationUnitId = unitId ?? Guid.NewGuid(),
        CreatedBy = "tester",
        Version = 1
    };

    private static PositionAssignment Seat(Guid positionId, Guid userId) => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = positionId,
        UserId = userId,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30)
    };

    private sealed class Host : IDisposable
    {
        private readonly TestServer _server;

        public Host(
            TaskItem[] tasks,
            TaskWatcher[]? watchers = null,
            PositionAssignment[]? seats = null,
            OrganizationUnit[]? units = null,
            EntitlementDataScope[]? scopes = null)
        {
            var taskRepository = new FakeTaskItemRepository(tasks);
            var watcherRepository = new FakeTaskWatcherRepository();
            foreach (var watcher in watchers ?? [])
            {
                watcherRepository.CreateAsync(watcher).GetAwaiter().GetResult();
            }

            // ONE seat table behind both the read rule's pool leg and the projection's claim/initiator decisions.
            var seatDirectory = new FakePositionAssignmentRepository(seats ?? []);
            var unitRepository = new FakeOrganizationUnitRepository(units ?? []);
            var me = new FakeCurrentUserContext(Me);

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
                    services.AddScoped<ICurrentUserContext>(_ => me);
                    // The caller's permissions as the REAL claims reader sees them — the principal the endpoint's
                    // filters just authorised, so the read-all leg cannot answer from a different truth.
                    services.AddScoped<IActorPermissionContext>(sp =>
                        new ClaimsActorPermissionContext(sp.GetRequiredService<IHttpContextAccessor>()));
                    services.AddScoped<ITaskReadAccessPolicy>(sp => new TaskReadAccessPolicy(
                        taskRepository,
                        watcherRepository,
                        new TaskNotificationService(
                            new RecordingNotificationDispatchAdapter(),
                            new FakeNotificationLocaleResolver(),
                            new FakeTaskNotificationRecipientResolver(),
                            seatDirectory,
                            new FakeUserNotificationRepository(),
                            new FakeTenantContext(TaskTestData.Tenant),
                            NullLogger<TaskNotificationService>.Instance),
                        unitRepository,
                        new TaskAssignmentScopeResolver(
                            new FakeDataScopeResolver(scopes ?? []),
                            new FakePositionRepository(),
                            unitRepository,
                            new FakeTenantContext(TaskTestData.Tenant),
                            me),
                        // Nobody reports to Me here; the subordinate leg is measured in TaskTeamReadParityTests and
                        // WorkItemsForEveryTenantUserHttpTests.
                        new FakeTaskTeamResolver(),
                        sp.GetRequiredService<IActorPermissionContext>(),
                        me));
                    services.AddScoped<IEnumerable<IWorkItemProvider>>(sp => new IWorkItemProvider[]
                    {
                        new TaskWorkItemProvider(
                            taskRepository,
                            seatDirectory,
                            new TaskLifecycleService(),
                            new TaskAssignmentResolver(),
                            new FakeUserDisplayNameResolver(),
                            new FakeChecklistRunRepository(),
                            new FakeTaskApprovalService(),
                            new FakeTaskDependencyRepository(),
                            new FakeTaskCommentRepository(),
                            new FakeTaskTransitionRepository(),
                            new FakeTaskPersonalOverlayRepository(),
                            watcherRepository,
                            sp.GetRequiredService<IActorPermissionContext>(),
                            new FakePositionRepository(),
                            unitRepository,
                            SlaForTests.Real(),
                            new FakeTaskFieldDefinitionRepository(),
                            new FakeTaskTypeRepository())
                    });
                    services.AddScoped<IEnumerable<IWorkItemActionDispatcher>>(_ => Array.Empty<IWorkItemActionDispatcher>());
                    services.AddScoped<IMediator>(sp => new SingleQueryMediator(new GetTaskWorkItemByIdHandler(
                        taskRepository,
                        sp.GetRequiredService<ITaskReadAccessPolicy>(),
                        sp.GetRequiredService<ICurrentUserContext>(),
                        sp.GetRequiredService<IEnumerable<IWorkItemProvider>>())));
                    services.AddControllers().AddApplicationPart(typeof(WorkItemsController).Assembly);
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

        public async Task<(HttpStatusCode Status, string Body)> GetAsync(Guid id, string[]? permissions = null)
        {
            var client = _server.CreateClient();
            client.DefaultRequestHeaders.Add(
                HeaderAuthentication.PermissionsHeader, string.Join(' ', permissions ?? ReaderPermissions));
            var response = await client.GetAsync($"/api/v1/work-items/{id}");
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        public HttpClient CreateAnonymousClient() => _server.CreateClient();

        public void Dispose() => _server.Dispose();
    }

    /// <summary>Routes exactly one query — <see cref="GetTaskWorkItemByIdQuery"/> — to the real handler.</summary>
    private sealed class SingleQueryMediator(GetTaskWorkItemByIdHandler handler) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => request is GetTaskWorkItemByIdQuery query
                ? (Task<TResponse>)(object)handler.Handle(query, ct)
                : throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.");

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

    /// <summary>A tenant user whose permission claims are whatever the test puts in one header — the shape
    /// <c>RelatedRecordsHttpTests</c> and <c>TaskLifecycleAuthorityHttpTests</c> already use.</summary>
    private sealed class HeaderAuthentication(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string PermissionsHeader = "X-Test-Permissions";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(PermissionsHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new("actor_type", "tenant_user"),
                new("tenant_id", TaskTestData.Tenant.ToString()),
                new("sub", Me.ToString())
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
