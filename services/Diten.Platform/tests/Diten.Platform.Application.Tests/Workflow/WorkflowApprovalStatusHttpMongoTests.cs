using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Middleware;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>One disposable mongod for this class (never the shared 27017); a fixed database name (DB-010).</summary>
public sealed class WorkflowStatusMongoFixture : IAsyncLifetime
{
    private DisposableStandaloneMongo? _mongo;

    public IPlatformDbContext DbContext { get; private set; } = null!;
    public IMongoDatabase Database { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        PlatformTestSerializers.Register();
        _mongo = await DisposableStandaloneMongo.StartAsync();
        var settings = MongoClientSettings.FromConnectionString($"mongodb://127.0.0.1:{_mongo.Port}/?directConnection=true");
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        var client = new MongoClient(settings);
        Database = client.GetDatabase("diten_platform_standalone_workflow_status");
        DbContext = new PlatformDbContext(client, Database);
        await PlatformSchemaManifest.ApplyAsync(Database, [SchemaProfile.WorkflowWorkCenter]);
    }

    public async Task DisposeAsync()
    {
        if (_mongo is not null)
        {
            await _mongo.DisposeAsync();
        }
    }
}

/// <summary>
/// WP-WORKFLOW-APPROVAL-STATUS-01 — MOD-0023's own statuses as their consumers read them, through the REAL approve,
/// reject and cancel routes (no seeded <c>Approved</c> — seeding it is exactly how the B1 defect hid).
/// <list type="bullet">
/// <item>B1: MOD-0023 closes an approved instance as <c>Completed</c>; MOD-0024 must read that as approved — the task's
/// <c>start</c> opens in the Task Center, and review stops being outstanding.</item>
/// <item>B2: whoever STARTED an instance cannot approve it, decided by user id (the name-vs-id comparison never fired);
/// an instance from before the field existed keeps the old comparison.</item>
/// <item>B3: an ESCALATED approval task can still be cancelled by the object's owner.</item>
/// </list>
/// </summary>
public sealed class WorkflowApprovalStatusHttpMongoTests : IClassFixture<WorkflowStatusMongoFixture>, IAsyncLifetime
{
    private readonly WorkflowStatusMongoFixture _fixture;
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _holder = Guid.NewGuid();
    private readonly Guid _requester = Guid.NewGuid();
    private readonly Guid _manager = Guid.NewGuid();
    private Host _host = null!;

    public WorkflowApprovalStatusHttpMongoTests(WorkflowStatusMongoFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.Database.GetCollection<Tenant>(PlatformCollections.Tenants).InsertOneAsync(new Tenant
        {
            Id = _tenant, Code = "T" + _tenant.ToString("N")[..8], Slug = "t-" + _tenant.ToString("N")[..8],
            Name = "Workflow tenant", DisplayName = "Workflow tenant", Domain = _tenant.ToString("N")[..8] + ".example",
            Country = "TR", DefaultTimezone = "Europe/Istanbul", Settings = new TenantSettings { Timezone = "Europe/Istanbul" }
        });
        _host = new Host(_fixture.DbContext);
    }

    public Task DisposeAsync()
    {
        _host.Dispose();
        return Task.CompletedTask;
    }

    // ── B1 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task After_a_REAL_approval_the_tasks_start_opens_and_APPROVAL_PENDING_is_gone()
    {
        var task = await SeedApprovalTaskAsync(manager: _manager);
        var instanceId = await StartTaskApprovalAsync(task, startedBy: _holder);

        // Non-vacuity: while MOD-0023 owes the decision, start is closed with APPROVAL_PENDING.
        var before = StartAction(await MyItemAsync(_holder, task.Id));
        Assert.False(before.GetProperty("enabled").GetBoolean());
        Assert.Equal(TaskReasonCodes.ApprovalPending, before.GetProperty("disabledReasonCode").GetString());

        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(_manager, instanceId, "approve")).Status);
        Assert.Equal(WorkflowInstanceStatus.Completed, (await InstanceAsync(instanceId)).Status); // what MOD-0023 really writes

        var after = StartAction(await MyItemAsync(_holder, task.Id));
        Assert.True(after.GetProperty("enabled").GetBoolean());
        Assert.NotEqual(TaskReasonCodes.ApprovalPending, after.TryGetProperty("disabledReasonCode", out var code) ? code.GetString() : null);
    }

    [Fact]
    public async Task After_a_REAL_review_approval_the_review_is_no_longer_outstanding()
    {
        var started = await _host.PostAsync("/api/v1/workflow/definitions", Designer(_holder),
            new { templateCode = "review-" + Guid.NewGuid().ToString("N")[..6], name = "Review", description = (string?)null });
        var definitionId = started.Data.GetProperty("id").GetGuid();
        await PublishAsync(definitionId, _holder);
        var reviewTaskId = Guid.NewGuid();
        var instance = await StartInstanceAsync(definitionId, _holder, _requester, TaskReviewService.ReviewObjectType, reviewTaskId);
        var task = new TaskItem
        {
            Id = reviewTaskId, TenantId = _tenant, Title = "Reviewed", AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = _holder, CreatedByUserId = _requester, OrganizationUnitId = Guid.NewGuid(),
            Lifecycle = TaskLifecycle.PendingReview, ReviewRequired = true, ReviewWorkflowInstanceId = instance.InstanceId,
            DueAt = DateTimeOffset.UtcNow.AddDays(5), Version = 1
        };

        Assert.True(TaskReviewView.Resolve(task, await StatesAsync(instance.InstanceId)).Outstanding);

        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(_requester, instance.InstanceId, "approve")).Status);

        var (outstanding, rejected) = TaskReviewView.Resolve(task, await StatesAsync(instance.InstanceId));
        Assert.False(outstanding);
        Assert.False(rejected);
    }

    [Theory]
    [InlineData(WorkflowInstanceStatus.Approved, true)]
    [InlineData(WorkflowInstanceStatus.Completed, true)]
    [InlineData(WorkflowInstanceStatus.Escalated, false)]
    [InlineData(WorkflowInstanceStatus.TimedOut, false)]
    [InlineData(WorkflowInstanceStatus.Active, false)]
    [InlineData(WorkflowInstanceStatus.Rejected, false)]
    public void Only_Approved_or_Completed_is_an_approval(WorkflowInstanceStatus status, bool approved)
        => Assert.Equal(approved, WorkflowInstanceOutcome.IsApproved(status));

    // ── B2 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Whoever_started_an_instance_cannot_approve_it_even_as_its_only_candidate()
    {
        var starter = Guid.NewGuid();
        var definitionId = await PlainDefinitionAsync(starter);
        var instance = await StartInstanceAsync(definitionId, starter, starter, "task", Guid.NewGuid());
        Assert.Equal(starter, (await InstanceAsync(instance.InstanceId)).StartedByUserId);

        var approve = await DecideAsync(starter, instance.InstanceId, "approve");

        Assert.Equal(HttpStatusCode.Conflict, approve.Status);
        Assert.Equal(WorkflowReasonCodes.SodViolation, approve.ReasonCode);
        Assert.Equal(WorkflowInstanceStatus.Active, (await InstanceAsync(instance.InstanceId)).Status);
    }

    [Fact]
    public async Task A_task_approval_the_creator_routed_to_themselves_cannot_be_approved_by_them()
    {
        var task = await SeedApprovalTaskAsync(manager: _holder);
        var instanceId = await StartTaskApprovalAsync(task, startedBy: _holder);

        var approve = await DecideAsync(_holder, instanceId, "approve");

        Assert.Equal(HttpStatusCode.Conflict, approve.Status);
        Assert.Equal(WorkflowReasonCodes.SodViolation, approve.ReasonCode);
    }

    [Fact]
    public async Task An_instance_from_before_the_field_keeps_the_old_comparison()
    {
        var starter = Guid.NewGuid();
        var definitionId = await PlainDefinitionAsync(starter);
        var instance = await StartInstanceAsync(definitionId, starter, starter, "task", Guid.NewGuid());
        await _fixture.Database.GetCollection<BsonDocument>(PlatformCollections.WorkflowInstances).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(instance.InstanceId, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Update.Unset("StartedByUserId"));

        // The legacy check compared the starter's NAME with the approver's id — it never matched, and still does not.
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(starter, instance.InstanceId, "approve")).Status);
    }

    // ── B3 (v2 revert): the PUBLIC cancel never cancels an escalated task ─────────────────────────────────────

    [Fact]
    public async Task The_public_cancel_endpoint_refuses_an_escalated_approval_task()
    {
        var definitionId = await PlainDefinitionAsync(_requester);
        var instance = await StartInstanceAsync(definitionId, _requester, _manager, "task", Guid.NewGuid());
        await SetStatusAsync(PlatformCollections.ApprovalTasks, instance.ApprovalTaskId, (int)ApprovalTaskStatus.Escalated);
        await SetStatusAsync(PlatformCollections.WorkflowInstances, instance.InstanceId, (int)WorkflowInstanceStatus.Escalated);

        var cancelled = await _host.PostAsync($"/api/v1/workflow/tasks/{instance.ApprovalTaskId}/cancel",
            _host.Token(_requester, _tenant, WorkflowPermissions.TasksCancel),
            new { reasonCode = "WITHDRAWN", idempotencyKey = Guid.NewGuid().ToString("N"), comment = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, cancelled.Status);
        Assert.Equal(WorkflowReasonCodes.WorkflowTaskInvalidState, cancelled.ReasonCode);
        Assert.Equal(WorkflowInstanceStatus.Escalated, (await InstanceAsync(instance.InstanceId)).Status);
    }

    // ── B4: the actor is ALWAYS the signed-in user ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("delegate")]
    [InlineData("request-info")]
    [InlineData("cancel")]
    public async Task Naming_someone_else_as_the_actor_is_403_and_changes_nothing(string action)
    {
        // The approval is assigned to the manager; the requester holds every task permission but is not them.
        var definitionId = await PlainDefinitionAsync(_requester);
        var instance = await StartInstanceAsync(definitionId, _requester, _manager, "task", Guid.NewGuid());

        var impersonated = await ActAsync(_requester, instance.ApprovalTaskId, action, claimedActor: _manager);

        Assert.Equal(HttpStatusCode.Forbidden, impersonated.Status);
        Assert.Equal(WorkflowReasonCodes.WorkflowActorMismatch, impersonated.ReasonCode);
        Assert.Equal(WorkflowInstanceStatus.Active, (await InstanceAsync(instance.InstanceId)).Status);
        var task = await _fixture.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks)
            .Find(t => t.Id == instance.ApprovalTaskId).SingleAsync();
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, task.Status);
        Assert.Equal(_manager.ToString(), task.AssigneeRef);
    }

    [Theory]
    [InlineData("approve", WorkflowInstanceStatus.Completed)]
    [InlineData("reject", WorkflowInstanceStatus.Rejected)]
    public async Task The_assigned_person_acts_with_no_actor_in_the_body_or_with_their_own(string action, WorkflowInstanceStatus expected)
    {
        var definitionId = await PlainDefinitionAsync(_requester);
        var withoutActor = await StartInstanceAsync(definitionId, _requester, _manager, "task", Guid.NewGuid());
        var withOwnActor = await StartInstanceAsync(definitionId, _requester, _manager, "task", Guid.NewGuid());

        Assert.Equal(HttpStatusCode.OK, (await ActAsync(_manager, withoutActor.ApprovalTaskId, action, claimedActor: null)).Status);
        Assert.Equal(HttpStatusCode.OK, (await ActAsync(_manager, withOwnActor.ApprovalTaskId, action, claimedActor: _manager)).Status);
        Assert.Equal(expected, (await InstanceAsync(withoutActor.InstanceId)).Status);
        Assert.Equal(expected, (await InstanceAsync(withOwnActor.InstanceId)).Status);
    }

    // ── B2 follow-up: the Task Center shows the starter a CLOSED approve, with its reason ──────────────────────

    [Fact]
    public void The_starter_sees_approve_disabled_with_SELF_APPROVAL_NOT_ALLOWED_and_anyone_else_sees_it_enabled()
    {
        var starter = Guid.NewGuid();
        var task = new ApprovalTask
        {
            TenantId = _tenant, WorkflowInstanceId = Guid.NewGuid(), StageCode = "stage-1", StepCode = "step-1",
            Status = ApprovalTaskStatus.WaitingApproval, AssigneeRef = starter.ToString()
        };
        var instance = new WorkflowInstance
        {
            TenantId = _tenant, TemplateId = Guid.NewGuid(), WorkflowTemplateId = Guid.NewGuid(), ObjectType = "task",
            ObjectId = Guid.NewGuid().ToString(), ObjectRef = "tasks|task|x", StartedByUserId = starter
        };
        var projection = new WorkItemProjectionService(SlaForTests.Real());

        var mine = projection.Project(task, instance, new WorkItemActor(starter, true, new HashSet<string>()),
            WorkItemContract.ProviderCodeWorkflow, "1.0")!.Actions.Single(a => a.Code == "approve");
        var theirs = projection.Project(task, instance, new WorkItemActor(Guid.NewGuid(), true, new HashSet<string>()),
            WorkItemContract.ProviderCodeWorkflow, "1.0")!.Actions.Single(a => a.Code == "approve");

        Assert.False(mine.Enabled);
        Assert.Equal(WorkAggregationReasonCodes.SelfApprovalNotAllowed, mine.DisabledReasonCode);
        Assert.Equal("WorkAggregation_ActionDisabled_SelfApproval", mine.DisabledReason!.Key);
        Assert.True(theirs.Enabled);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    private async Task<TaskItem> SeedApprovalTaskAsync(Guid manager)
    {
        var task = new TaskItem
        {
            // Self-assigned: admitted at once, so `start` (not `accept`) is the action the approval gates.
            TenantId = _tenant, Title = "Needs approval", AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
            AssigneeUserId = _holder, CreatedByUserId = _holder, OrganizationUnitId = Guid.NewGuid(),
            Lifecycle = TaskLifecycle.Open, ApprovalRequired = true, ApprovalManagerUserId = manager,
            DueAt = DateTimeOffset.UtcNow.AddDays(10), Version = 1
        };
        await _fixture.Database.GetCollection<TaskItem>(PlatformCollections.TaskItems).InsertOneAsync(task);
        return task;
    }

    /// <summary>MOD-0024's own approval start, run as the given user (so MOD-0023 records who started it).</summary>
    private async Task<Guid> StartTaskApprovalAsync(TaskItem task, Guid startedBy)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(JwtRegisteredClaimNames.Sub, startedBy.ToString()), new Claim(JwtRegisteredClaimNames.Email, $"{startedBy:N}@x.example")],
                "test"))
        };
        using (TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), _tenant))
        {
            var instanceId = await scope.ServiceProvider.GetRequiredService<ITaskApprovalService>().TryStartApprovalAsync(task, default);
            Assert.NotNull(instanceId);
            await _fixture.Database.GetCollection<TaskItem>(PlatformCollections.TaskItems).UpdateOneAsync(
                t => t.Id == task.Id, Builders<TaskItem>.Update.Set(t => t.WorkflowInstanceId, instanceId));
            return instanceId!.Value;
        }
    }

    private async Task<IReadOnlyDictionary<Guid, TaskApprovalState>> StatesAsync(Guid instanceId)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        using (TenantScope.Begin(scope.ServiceProvider.GetRequiredService<ITenantContext>(), _tenant))
        {
            return await scope.ServiceProvider.GetRequiredService<ITaskApprovalService>().GetStatesAsync([instanceId], default);
        }
    }

    private string Designer(Guid user) => _host.Token(user, _tenant,
        WorkflowPermissions.DefinitionsManage, WorkflowPermissions.DefinitionsPublish, WorkflowPermissions.InstancesStart);

    private async Task<Guid> PlainDefinitionAsync(Guid designer)
    {
        var created = await _host.PostAsync("/api/v1/workflow/definitions", Designer(designer),
            new { templateCode = "plain-" + Guid.NewGuid().ToString("N")[..6], name = "Plain", description = (string?)null });
        Assert.True(created.Status is HttpStatusCode.OK or HttpStatusCode.Created, created.ToString());
        var id = created.Data.GetProperty("id").GetGuid();
        await PublishAsync(id, designer);
        return id;
    }

    private async Task PublishAsync(Guid definitionId, Guid designer)
    {
        var published = await _host.PostAsync($"/api/v1/workflow/definitions/{definitionId}/publish", Designer(designer), new
        {
            definitionJson = """{ "name": "Plain", "stages": [ { "code": "stage-1", "steps": [ { "code": "approve", "type": "approval" } ] } ] }""",
            schemaVersion = "1.0", expressionVersion = "1.0", publishReason = "test"
        });
        Assert.True(published.Status == HttpStatusCode.OK, published.ToString());
    }

    private async Task<(Guid InstanceId, Guid ApprovalTaskId)> StartInstanceAsync(
        Guid definitionId, Guid starter, Guid candidate, string objectType, Guid objectId)
    {
        var started = await _host.PostAsync("/api/v1/workflow/instances", Designer(starter), new
        {
            templateId = definitionId, objectType, objectId = objectId.ToString(),
            candidatePrincipalIds = new[] { candidate.ToString() }, idempotencyKey = Guid.NewGuid().ToString("N"),
            commentRequired = false, evidenceRequired = false
        });
        Assert.True(started.Status is HttpStatusCode.OK or HttpStatusCode.Created, started.ToString());
        return (started.Data.GetProperty("workflowInstanceId").GetGuid(), started.Data.GetProperty("approvalTaskId").GetGuid());
    }

    /// <summary>Posts a task action as <paramref name="caller"/>, naming <paramref name="claimedActor"/> in the body (or no
    /// actor at all when null).</summary>
    private Task<ApiResult> ActAsync(Guid caller, Guid approvalTaskId, string action, Guid? claimedActor)
    {
        var token = _host.Token(caller, _tenant,
            WorkflowPermissions.TasksApprove, WorkflowPermissions.TasksReject, WorkflowPermissions.TasksDelegate,
            WorkflowPermissions.TasksRequestInfo, WorkflowPermissions.TasksCancel);
        var body = new Dictionary<string, object?>
        {
            ["reasonCode"] = "OK", ["idempotencyKey"] = Guid.NewGuid().ToString("N"), ["comment"] = "c",
            ["delegatePrincipalId"] = Guid.NewGuid().ToString(), ["targetPrincipalId"] = null, ["evidenceRef"] = null
        };
        if (claimedActor is { } actor)
        {
            body["actorId"] = actor.ToString();
        }

        return _host.PostAsync($"/api/v1/workflow/tasks/{approvalTaskId}/{action}", token, body);
    }

    private async Task<ApiResult> DecideAsync(Guid actor, Guid instanceId, string action)
    {
        var task = await _fixture.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks)
            .Find(t => t.WorkflowInstanceId == instanceId && t.Status == ApprovalTaskStatus.WaitingApproval).SingleAsync();
        return await _host.PostAsync($"/api/v1/workflow/tasks/{task.Id}/{action}",
            _host.Token(actor, _tenant, WorkflowPermissions.TasksApprove, WorkflowPermissions.TasksReject),
            new { actorId = actor.ToString(), reasonCode = "OK", idempotencyKey = Guid.NewGuid().ToString("N"), comment = "ok", evidenceRef = (string?)null });
    }

    private Task<WorkflowInstance> InstanceAsync(Guid id)
        => _fixture.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).Find(i => i.Id == id).SingleAsync();

    private Task SetStatusAsync(string collection, Guid id, int status)
        => _fixture.Database.GetCollection<BsonDocument>(collection).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(id, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Update.Set("Status", status));

    private async Task<JsonElement> MyItemAsync(Guid user, Guid taskId)
    {
        var mine = await _host.GetAsync("/api/v1/work-items/mine", _host.Token(user, _tenant, TaskPermissions.Read, TaskPermissions.Update));
        Assert.True(mine.Status == HttpStatusCode.OK, mine.ToString());
        return mine.Data.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == taskId.ToString()).Clone();
    }

    private static JsonElement StartAction(JsonElement item)
    {
        var actions = item.GetProperty("actions").EnumerateArray().ToList();
        return actions.SingleOrDefault(a => a.GetProperty("code").GetString() == "start") is { ValueKind: JsonValueKind.Object } start
            ? start
            : throw new Xunit.Sdk.XunitException("no start action; item = " + item.GetRawText());
    }

    public sealed record ApiResult(HttpStatusCode Status, string Body)
    {
        public string? ReasonCode
        {
            get
            {
                using var document = JsonDocument.Parse(Body);
                return document.RootElement.TryGetProperty("reason_code", out var code) ? code.GetString() : null;
            }
        }

        public JsonElement Data
        {
            get
            {
                using var document = JsonDocument.Parse(Body);
                return document.RootElement.GetProperty("data").Clone();
            }
        }

        public override string ToString() => $"{(int)Status} {Body}";
    }

    /// <summary>The real wire: JWT, tenant resolution, [HasPermission], the REAL MediatR pipeline (validation), the real
    /// MOD-0023 handlers and repositories, the real task approval service and task work-item provider.</summary>
    private sealed class Host : IDisposable
    {
        private const string Issuer = "diten-auth-workflow-status";
        private const string Audience = "diten-platform-workflow-status";
        private const string Secret = "WP-WORKFLOW-APPROVAL-STATUS-01 signing key, test only, 0123456789abcdef01";
        private readonly TestServer _server;

        public Host(IPlatformDbContext db)
        {
            var builder = new WebHostBuilder()
                .UseEnvironment("Test")
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
                    {
                        options.MapInboundClaims = false;
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                            ValidIssuer = Issuer, ValidAudience = Audience,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), ClockSkew = TimeSpan.Zero
                        };
                    });
                    services.AddAuthorization();
                    services.AddHttpContextAccessor();
                    services.AddProblemDetails();
                    services.AddExceptionHandler<GlobalExceptionHandler>();
                    services.AddScoped<ITenantContext, TenantContext>();
                    services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                    services.AddScoped<ICorrelationContext>(_ =>
                    {
                        var correlation = new CorrelationContext();
                        correlation.SetCorrelationId(Guid.NewGuid().ToString());
                        return correlation;
                    });
                    services.AddScoped<IActorPermissionContext>(sp => new ClaimsActorPermissionContext(sp.GetRequiredService<IHttpContextAccessor>()));
                    services.AddSingleton(db);

                    services.AddScoped<IWorkflowTemplateRepository, WorkflowTemplateRepository>();
                    services.AddScoped<IWorkflowTemplateVersionRepository, WorkflowTemplateVersionRepository>();
                    services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();
                    services.AddScoped<IApprovalTaskRepository, ApprovalTaskRepository>();
                    services.AddScoped<IRuntimeAssignmentSnapshotRepository, RuntimeAssignmentSnapshotRepository>();
                    services.AddScoped<IWorkflowTransitionLogRepository, WorkflowTransitionLogRepository>();
                    services.AddScoped<ISlaEscalationRuleRepository, SlaEscalationRuleRepository>();
                    services.AddScoped<ITaskTransitionRepository, TaskTransitionRepository>();
                    services.AddScoped<ITaskItemRepository, TaskItemRepository>();
                    services.AddSingleton(Options.Create(new TaskApprovalOptions()));
                    services.AddScoped<ITaskApprovalService, TaskApprovalService>();

                    // MOD-0024's real Task Center provider, with the REAL approval service; its other seams are the suite's doubles.
                    services.AddScoped<IEnumerable<IWorkItemProvider>>(sp => new IWorkItemProvider[]
                    {
                        new TaskWorkItemProvider(
                            sp.GetRequiredService<ITaskItemRepository>(),
                            new FakePositionAssignmentRepository(),
                            new TaskLifecycleService(),
                            new TaskAssignmentResolver(),
                            new FakeUserDisplayNameResolver(),
                            new FakeChecklistRunRepository(),
                            sp.GetRequiredService<ITaskApprovalService>(),
                            new FakeTaskDependencyRepository(),
                            new FakeTaskCommentRepository(),
                            sp.GetRequiredService<ITaskTransitionRepository>(),
                            new FakeTaskPersonalOverlayRepository(),
                            new FakeTaskWatcherRepository(),
                            sp.GetRequiredService<IActorPermissionContext>(),
                            new FakePositionRepository(),
                            new FakeOrganizationUnitRepository(),
                            SlaForTests.Real(),
                            new FakeTaskFieldDefinitionRepository(),
                            new FakeTaskTypeRepository())
                    });
                    services.AddScoped<IEnumerable<Application.Features.WorkAggregation.Dispatch.IWorkItemActionDispatcher>>(_ =>
                        Array.Empty<Application.Features.WorkAggregation.Dispatch.IWorkItemActionDispatcher>());
                    services.AddSingleton(Options.Create(new WorkAggregationResilienceOptions()));

                    var application = typeof(WorkflowPermissions).Assembly;
                    services.AddMediatR(cfg =>
                    {
                        cfg.RegisterServicesFromAssembly(application);
                        cfg.AddOpenBehavior(typeof(Application.Contracts.Behaviors.ValidationBehavior<,>));
                    });
                    services.AddValidatorsFromAssembly(application);
                    services.AddControllers().AddApplicationPart(typeof(WorkflowDefinitionsController).Assembly);
                })
                .Configure(app =>
                {
                    app.UseExceptionHandler();
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseTenantResolution();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            _server = new TestServer(builder);
        }

        public IServiceProvider Services => _server.Services;

        public string Token(Guid user, Guid tenant, params string[] permissions)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.ToString()),
                new(JwtRegisteredClaimNames.Email, $"{user:N}@tenant.example"),
                new("tenant_id", tenant.ToString()),
                new("actor_type", "tenant_user")
            };
            claims.AddRange(permissions.Select(key => new Claim("permission", key)));
            return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
                Issuer, Audience, claims, expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256)));
        }

        public Task<ApiResult> GetAsync(string path, string bearer) => SendAsync(HttpMethod.Get, path, bearer, null);

        public Task<ApiResult> PostAsync(string path, string bearer, object body) => SendAsync(HttpMethod.Post, path, bearer, body);

        private async Task<ApiResult> SendAsync(HttpMethod method, string path, string bearer, object? body)
        {
            var request = new HttpRequestMessage(method, path);
            if (body is not null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            var response = await _server.CreateClient().SendAsync(request);
            return new ApiResult(response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        public void Dispose() => _server.Dispose();
    }
}
