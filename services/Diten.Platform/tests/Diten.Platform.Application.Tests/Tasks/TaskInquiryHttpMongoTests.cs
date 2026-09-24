using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Application.Features.WorkAggregation.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Queries;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-439, E4 — "Bilgi bekle" end to end, ON THE WIRE, over a REAL MongoDB.
///
/// <para>Real JWT bearer validation, the real tenant-resolution middleware, the real <see cref="WorkItemsController"/>
/// (the list, the read by id and the one dispatch address), the real <see cref="TaskWorkItemActionDispatcher"/>, the
/// real inquire / answer / transition handlers, the real <see cref="TaskReadAccessPolicy"/>, the real
/// <see cref="TaskWorkItemProvider"/> and the real <see cref="TaskItemRepository"/> + <see cref="TaskTransitionRepository"/>
/// against Mongo — each resolved per request with the tenant the token names. Doubles: the org chart (seats, positions,
/// units), the display-name directory and the notification sink.</para>
///
/// <para>Four people: <see cref="Holder"/> parks the task, <see cref="Asked"/> is asked, <see cref="Bystander"/> is
/// anybody else in the tenant, and <see cref="Asked"/> again under ANOTHER tenant's token — the same user id, which is
/// the case a tenant boundary exists for.</para>
///
/// <para>Shared test database, isolation by a fresh tenant per test (<see cref="MongoIntegrationHarness.CreateAsync"/>);
/// never the dev database, never DefaultTenant. This tenant's rows are deleted on dispose.</para>
/// </summary>
public sealed class TaskInquiryHttpMongoTests : IAsyncLifetime
{
    private static readonly Guid Holder = Guid.Parse("43900000-0000-4000-8000-0000000000a1");
    private static readonly Guid Asked = Guid.Parse("43900000-0000-4000-8000-0000000000a2");
    private static readonly Guid Bystander = Guid.Parse("43900000-0000-4000-8000-0000000000a3");
    private static readonly Guid OtherTenant = Guid.Parse("43900000-0000-4000-8000-0000000000ff");
    private const string Question = "Lot 42 sertifikası ne zaman gelir?";
    private const string Answer = "Cuma günü tedarikçiden.";

    /// <summary>What an ordinary Task Center user holds — no delete, no read-all.</summary>
    private static readonly string[] UserKeys =
    [
        TaskPermissions.Read, TaskPermissions.Update, TaskPermissions.Complete,
        TaskPermissions.Cancel, TaskPermissions.Claim, TaskPermissions.Assign
    ];

    private MongoIntegrationHarness _harness = null!;
    private Host _host = null!;
    private TaskItem _task = null!;

    public async Task InitializeAsync()
    {
        _harness = await MongoIntegrationHarness.CreateAsync(SchemaProfile.WorkflowWorkCenter);
        _host = new Host(_harness);

        _task = new TaskItem
        {
            TenantId = _harness.TenantId,
            Title = "Lot 42 serbest bırakma",
            AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = Holder,
            CreatedByUserId = Holder,
            OrganizationUnitId = _host.UnitId,
            Lifecycle = TaskLifecycle.InProgress,
            CreatedBy = "e4"
        };
        _task.CloseAcceptanceGate(Holder);
        await TasksIn(_harness.TenantId).CreateAsync(_task);
    }

    public async Task DisposeAsync()
    {
        _host.Dispose();
        var tenants = new[] { _harness.TenantId, OtherTenant };
        await _harness.Database.GetCollection<TaskItem>(PlatformCollections.TaskItems)
            .DeleteManyAsync(Builders<TaskItem>.Filter.In(x => x.TenantId, tenants));
        await _harness.Database.GetCollection<TaskTransition>(PlatformCollections.TaskTransitions)
            .DeleteManyAsync(Builders<TaskTransition>.Filter.In(x => x.TenantId, tenants));
        await _harness.DisposeAsync();
    }

    // ── the whole flow, two people and a bystander ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Asked_answered_and_closed_again_across_two_users_on_the_wire()
    {
        var holder = _host.Token(Holder, _harness.TenantId, UserKeys);
        var asked = _host.Token(Asked, _harness.TenantId, UserKeys);
        var bystander = _host.Token(Bystander, _harness.TenantId, UserKeys);

        // The holder parks the task, naming the person.
        var inquire = await _host.PostActionAsync(_task.Id, "inquire", holder,
            new { expectedVersion = await VersionAsync(), reason = Question, waitingOnUserId = Asked });
        Assert.True(inquire.Status == HttpStatusCode.OK, inquire.Body);
        Assert.Contains(_host.Notifications.Notifications,
            n => n.EventCode == TaskNotificationEvents.InquiryAsked && n.Candidates.SequenceEqual([Asked]));

        // The asked person's inbox holds the QUESTION — the question text, one action, nothing of the task's body.
        var askedBoard = await _host.GetAsync("/api/v1/work-items/mine", asked);
        var question = Assert.Single(Items(askedBoard.Body), item => item.GetProperty("id").GetString() == _task.Id.ToString());
        Assert.Equal("inquiry", question.GetProperty("workIntent").GetString());
        Assert.Equal(Question, question.GetProperty("summary").GetProperty("text").GetString());
        Assert.Equal("answer", Assert.Single(question.GetProperty("actions").EnumerateArray()).GetProperty("code").GetString());
        Assert.False(question.TryGetProperty("activity", out _));
        Assert.False(question.TryGetProperty("checklist", out _));

        // …and not the holder's, whose own row is the task, waiting.
        var holderRow = Assert.Single(Items((await _host.GetAsync("/api/v1/work-items/mine", holder)).Body),
            item => item.GetProperty("id").GetString() == _task.Id.ToString());
        Assert.Equal("task", holderRow.GetProperty("workIntent").GetString());
        Assert.Equal("Waiting", holderRow.GetProperty("normalizedStatus").GetString());

        // The bystander sees nothing and may open nothing.
        Assert.DoesNotContain(Items((await _host.GetAsync("/api/v1/work-items/mine", bystander)).Body),
            item => item.GetProperty("id").GetString() == _task.Id.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _host.GetAsync($"/api/v1/work-items/{_task.Id}", bystander)).Status);

        // The asked person may open it by id — as the question — while it stands.
        var byId = await _host.GetAsync($"/api/v1/work-items/{_task.Id}", asked);
        Assert.Equal(HttpStatusCode.OK, byId.Status);
        Assert.Contains("\"workIntent\":\"inquiry\"", byId.Body);

        // Only the asked person may answer — the bystander is refused on the wire and nothing moves.
        var refused = await _host.PostActionAsync(_task.Id, "answer", bystander,
            new { expectedVersion = await VersionAsync(), answer = "Ben de bilmiyorum." });
        Assert.Equal(HttpStatusCode.Forbidden, refused.Status);
        Assert.Equal(TaskReasonCodes.InquiryNotAddressee, ReasonCode(refused.Body));
        Assert.Equal(TaskLifecycle.Waiting, (await StoredAsync()).Lifecycle);

        // The answer.
        var answered = await _host.PostActionAsync(_task.Id, "answer", asked,
            new { expectedVersion = await VersionAsync(), answer = Answer });
        Assert.True(answered.Status == HttpStatusCode.OK, answered.Body);

        var stored = await StoredAsync();
        Assert.Equal(TaskLifecycle.InProgress, stored.Lifecycle);
        Assert.Null(stored.WaitingOnUserId);
        Assert.Null(stored.WaitingReason);
        var history = await TransitionsIn(_harness.TenantId).ListByTaskIdAsync(_task.Id);
        var entry = Assert.Single(history, e => e.Kind == TaskTransitionKind.InquiryAnswered);
        Assert.Equal(Asked, entry.ActorUserId);
        Assert.Equal(Answer, entry.Reason);
        Assert.Contains(_host.Notifications.Notifications,
            n => n.EventCode == TaskNotificationEvents.InquiryAnswered && n.Candidates.SequenceEqual([Holder]));

        // The holder's card now says who answered, and what.
        var afterRow = Assert.Single(Items((await _host.GetAsync("/api/v1/work-items/mine", holder)).Body),
            item => item.GetProperty("id").GetString() == _task.Id.ToString());
        var answer = afterRow.GetProperty("inquiryAnswer");
        Assert.Equal(Asked.ToString(), answer.GetProperty("answeredBy").GetProperty("id").GetString());
        Assert.Equal(Answer, answer.GetProperty("answer").GetProperty("text").GetString());

        // And the asked person's access ended with the question.
        Assert.DoesNotContain(Items((await _host.GetAsync("/api/v1/work-items/mine", asked)).Body),
            item => item.GetProperty("id").GetString() == _task.Id.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _host.GetAsync($"/api/v1/work-items/{_task.Id}", asked)).Status);
        var again = await _host.PostActionAsync(_task.Id, "answer", asked,
            new { expectedVersion = await VersionAsync(), answer = "Bir şey daha." });
        Assert.Equal(HttpStatusCode.Forbidden, again.Status);
    }

    // ── the tenant boundary ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// MUTATION TARGET (tenant). The SAME user id, under another tenant's token, finds nothing: no question in the
    /// inbox, no task by id, and an answer that cannot reach the task. Every read runs through the tenant the
    /// token names.
    /// </summary>
    [Fact]
    public async Task The_asked_persons_id_under_another_tenants_token_never_reaches_the_question()
    {
        var holder = _host.Token(Holder, _harness.TenantId, UserKeys);
        await _host.PostActionAsync(_task.Id, "inquire", holder,
            new { expectedVersion = await VersionAsync(), reason = Question, waitingOnUserId = Asked });
        var foreign = _host.Token(Asked, OtherTenant, UserKeys);

        var board = await _host.GetAsync("/api/v1/work-items/mine", foreign);
        Assert.Equal(HttpStatusCode.OK, board.Status);
        Assert.Empty(Items(board.Body));

        Assert.Equal(HttpStatusCode.NotFound, (await _host.GetAsync($"/api/v1/work-items/{_task.Id}", foreign)).Status);

        var answer = await _host.PostActionAsync(_task.Id, "answer", foreign,
            new { expectedVersion = await VersionAsync(), answer = Answer });
        Assert.Equal(HttpStatusCode.NotFound, answer.Status);

        var stored = await StoredAsync();
        Assert.Equal(TaskLifecycle.Waiting, stored.Lifecycle);
        Assert.Equal(Asked, stored.WaitingOnUserId);
    }

    // ── withdrawing the question ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_holder_resuming_withdraws_the_question_from_the_asked_persons_inbox()
    {
        var holder = _host.Token(Holder, _harness.TenantId, UserKeys);
        var asked = _host.Token(Asked, _harness.TenantId, UserKeys);
        await _host.PostActionAsync(_task.Id, "inquire", holder,
            new { expectedVersion = await VersionAsync(), reason = Question, waitingOnUserId = Asked });
        Assert.Contains(Items((await _host.GetAsync("/api/v1/work-items/mine", asked)).Body),
            item => item.GetProperty("id").GetString() == _task.Id.ToString());

        var resume = await _host.PostActionAsync(_task.Id, "start", holder,
            new { expectedVersion = await VersionAsync() });
        Assert.True(resume.Status == HttpStatusCode.OK, resume.Body);

        Assert.DoesNotContain(Items((await _host.GetAsync("/api/v1/work-items/mine", asked)).Body),
            item => item.GetProperty("id").GetString() == _task.Id.ToString());
        var late = await _host.PostActionAsync(_task.Id, "answer", asked,
            new { expectedVersion = await VersionAsync(), answer = Answer });
        Assert.Equal(HttpStatusCode.Forbidden, late.Status);
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────────────

    private TaskItemRepository TasksIn(Guid tenant)
    {
        var context = new TenantContext();
        context.SetTenant(tenant);
        return new TaskItemRepository(_harness.DbContext, context, TransitionsIn(tenant));
    }

    private TaskTransitionRepository TransitionsIn(Guid tenant)
    {
        var context = new TenantContext();
        context.SetTenant(tenant);
        return new TaskTransitionRepository(_harness.DbContext, context);
    }

    private async Task<TaskItem> StoredAsync() => (await TasksIn(_harness.TenantId).GetByIdAsync(_task.Id))!;

    private async Task<int> VersionAsync() => (await StoredAsync()).Version;

    private static IReadOnlyList<JsonElement> Items(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(item => item.Clone())
            .ToList();
    }

    private static string? ReasonCode(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("reason_code", out var code) ? code.GetString() : null;
    }

    private sealed class Host : IDisposable
    {
        private const string Issuer = "diten-auth-bl439-e4";
        private const string Audience = "diten-platform-bl439-e4";
        private const string Secret = "BL-439 inquiry E4 signing key, test only, 0123456789abcdef0123456789";

        private readonly TestServer _server;

        public Host(MongoIntegrationHarness harness)
        {
            /*
             * The org chart is a DOUBLE, and the doubles answer for TaskTestData.Tenant only (they mirror the
             * tenant filter with that fixed id). It is not the subject here — the tasks and their history are, and
             * those live in Mongo under the harness's own tenant, read through the token's tenant on every request.
             */
            var orgTenant = TaskTestData.Tenant;
            var unit = new OrganizationUnit
            {
                TenantId = orgTenant, Code = "OU-E4", Name = "Kalite",
                LegalEntityId = Guid.NewGuid(), Status = OrgUnitStatus.Active
            };
            var position = new Position
            {
                TenantId = orgTenant, Code = "POS-E4", Name = "Uzman",
                OrganizationUnitId = unit.Id, Status = PositionStatus.Active
            };
            UnitId = unit.Id;
            var units = new FakeOrganizationUnitRepository(unit);
            var positions = new FakePositionRepository(position);
            var seats = new FakePositionAssignmentRepository(new[] { Holder, Asked, Bystander }
                .Select(user => new PositionAssignment
                {
                    TenantId = orgTenant, PositionId = position.Id, UserId = user,
                    EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30)
                })
                .ToArray());
            var names = new FakeUserDisplayNameResolver(
                (Holder, "Ali Tufanoğlu"), (Asked, "Ayşe Yılmaz"), (Bystander, "Can Demir"));

            var builder = new WebHostBuilder()
                .UseEnvironment("Test")
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(options =>
                        {
                            options.MapInboundClaims = false;
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidateAudience = true,
                                ValidateLifetime = true,
                                ValidateIssuerSigningKey = true,
                                ValidIssuer = Issuer,
                                ValidAudience = Audience,
                                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                                ClockSkew = TimeSpan.Zero
                            };
                        });
                    services.AddAuthorization();
                    services.AddHttpContextAccessor();

                    // Per request, from the token — the tenant every Mongo read below is bound to.
                    services.AddScoped<ITenantContext, TenantContext>();
                    services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                    services.AddScoped<ICorrelationContext>(_ =>
                    {
                        var correlation = new CorrelationContext();
                        correlation.SetCorrelationId("corr-e4");
                        return correlation;
                    });
                    services.AddScoped<IActorPermissionContext>(sp =>
                        new ClaimsActorPermissionContext(sp.GetRequiredService<IHttpContextAccessor>()));

                    // The REAL stores, on the real database, in the request's tenant.
                    services.AddScoped<ITaskTransitionRepository>(sp =>
                        new TaskTransitionRepository(harness.DbContext, sp.GetRequiredService<ITenantContext>()));
                    services.AddScoped<ITaskItemRepository>(sp => new TaskItemRepository(
                        harness.DbContext,
                        sp.GetRequiredService<ITenantContext>(),
                        sp.GetRequiredService<ITaskTransitionRepository>()));

                    services.AddScoped<ITaskReadAccessPolicy>(sp => new TaskReadAccessPolicy(
                        sp.GetRequiredService<ITaskItemRepository>(),
                        new FakeTaskWatcherRepository(),
                        new FakeTaskNotificationService(),
                        units,
                        new EmptyScope(),
                        new FakeTaskTeamResolver(),
                        sp.GetRequiredService<IActorPermissionContext>(),
                        sp.GetRequiredService<ICurrentUserContext>()));
                    services.AddScoped<IEnumerable<IWorkItemProvider>>(sp => new IWorkItemProvider[]
                    {
                        new TaskWorkItemProvider(
                            sp.GetRequiredService<ITaskItemRepository>(),
                            seats,
                            new TaskLifecycleService(),
                            new TaskAssignmentResolver(),
                            names,
                            new FakeChecklistRunRepository(),
                            new FakeTaskApprovalService(),
                            new FakeTaskDependencyRepository(),
                            new FakeTaskCommentRepository(),
                            sp.GetRequiredService<ITaskTransitionRepository>(),
                            new FakeTaskPersonalOverlayRepository(),
                            new FakeTaskWatcherRepository(),
                            sp.GetRequiredService<IActorPermissionContext>(),
                            positions,
                            units,
                            SlaForTests.Real(),
                            new FakeTaskFieldDefinitionRepository(),
                            new FakeTaskTypeRepository())
                    });
                    services.AddScoped<IMediator>(sp => new Router(sp, seats, positions, units, Notifications));
                    services.AddScoped<IEnumerable<IWorkItemActionDispatcher>>(sp =>
                        new IWorkItemActionDispatcher[] { new TaskWorkItemActionDispatcher(sp.GetRequiredService<IMediator>()) });
                    services.AddControllers().AddApplicationPart(typeof(WorkItemsController).Assembly);
                })
                .Configure(app =>
                {
                    // Same order as Diten.Platform.API Program.cs.
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseTenantResolution();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });

            _server = new TestServer(builder);
        }

        public Guid UnitId { get; }

        /// <summary>Everything the handlers asked to send, across every request.</summary>
        public FakeTaskNotificationService Notifications { get; } = new();

        public string Token(Guid user, Guid tenant, params string[] keys)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.ToString()),
                new(JwtRegisteredClaimNames.Email, $"{user:N}@tenant.example"),
                new("tenant_id", tenant.ToString()),
                new("actor_type", "tenant_user")
            };
            claims.AddRange(keys.Select(key => new Claim("permission", key)));

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<(HttpStatusCode Status, string Body)> GetAsync(string path, string bearer)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            var response = await _server.CreateClient().SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        public async Task<(HttpStatusCode Status, string Body)> PostActionAsync(
            Guid itemId, string actionCode, string bearer, object payload)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/work-items/{itemId}/actions/{actionCode}")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { providerCode = "tasks", payload }),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            var response = await _server.CreateClient().SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        public void Dispose() => _server.Dispose();
    }

    private sealed class EmptyScope : ITaskAssignmentScopeResolver
    {
        public Task<TaskAssignmentScope> ResolveAsync(CancellationToken ct) => Task.FromResult(TaskAssignmentScope.Empty);
    }

    /// <summary>Routes each request this flow sends to its REAL handler, built over the request's own services.</summary>
    private sealed class Router(
        IServiceProvider services,
        FakePositionAssignmentRepository seats,
        FakePositionRepository positions,
        FakeOrganizationUnitRepository units,
        FakeTaskNotificationService notifications) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            var tasks = services.GetRequiredService<ITaskItemRepository>();
            var currentUser = services.GetRequiredService<ICurrentUserContext>();
            object response = request switch
            {
                GetMyWorkItemsQuery query => await new GetMyWorkItemsHandler(
                    services.GetRequiredService<IEnumerable<IWorkItemProvider>>(),
                    currentUser,
                    Options.Create(new WorkAggregationResilienceOptions()),
                    NullLogger<GetMyWorkItemsHandler>.Instance).Handle(query, ct),
                GetTaskWorkItemByIdQuery query => await new GetTaskWorkItemByIdHandler(
                    tasks,
                    services.GetRequiredService<ITaskReadAccessPolicy>(),
                    currentUser,
                    services.GetRequiredService<IEnumerable<IWorkItemProvider>>()).Handle(query, ct),
                InquireTaskItemCommand command => await new InquireTaskItemHandler(
                    tasks, new TaskLifecycleService(), currentUser, seats, positions, units,
                    notifications, NullLogger<InquireTaskItemHandler>.Instance).Handle(command, ct),
                AnswerInquiryCommand command => await new AnswerInquiryHandler(
                    tasks, services.GetRequiredService<ITaskTransitionRepository>(), new TaskLifecycleService(),
                    currentUser, new FakeWorkflowTransitionGate(), new FakeTaskDependencyRepository(),
                    notifications, NullLogger<AnswerInquiryHandler>.Instance).Handle(command, ct),
                TransitionTaskItemCommand command => await new TransitionTaskItemHandler(
                    tasks, new TaskLifecycleService(), currentUser,
                    new FakeChecklistRunRepository(), new TaskChecklistService(),
                    new FakeWorkflowTransitionGate(), new FakeTaskDependencyRepository(),
                    new FakeTaskTypeRepository(), notifications,
                    new TaskFieldDefinitionService(
                        new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
                    new FakeTaskAttachmentRepository(),
                    NullLogger<TransitionTaskItemHandler>.Instance).Handle(command, ct),
                _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.")
            };
            return (TResponse)response;
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
