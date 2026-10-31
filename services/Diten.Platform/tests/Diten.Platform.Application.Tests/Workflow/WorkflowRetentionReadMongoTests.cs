using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Middleware;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// MOD-0023 §17 DL-1 — retention and the reconciliation reads, over the REAL repositories on Mongo (§17.9/1c: a fake store
/// does not reproduce <c>ExecutionFilter</c>, so its result is not evidence). A deleted row is the fixture <c>IsDeleted = true,
/// DeletedAt = null</c>: what the inherited delete used to leave, and what the handler's <c>DeletedAt</c> filter cannot see.
/// <list type="bullet">
/// <item>AC-W15: the inherited delete refuses on all three repositories, through every reference, and the row stays as it was.</item>
/// <item>AC-W13: a deleted row is hidden by default and comes back flagged under <c>includeDeleted=true</c>.</item>
/// <item>AC-W16: the flag stays inside the tenant and the permission.</item>
/// <item>AC-W2 / AC-W3: the stored keys reach the wire; the existing fields keep name and JSON type.</item>
/// </list>
/// </summary>
[Trait("Category", "Mongo")]
public sealed class WorkflowRetentionReadMongoTests : IAsyncLifetime
{
    private const string ObjectType = "treasury.payment";
    private const string Correlation = "6f0f1a1e-5b7e-4c55-9d41-0000000000d1";

    private MongoIntegrationHarness _a = null!;
    private MongoIntegrationHarness _b = null!;

    public async Task InitializeAsync()
    {
        _a = await MongoIntegrationHarness.CreateAsync(SchemaProfile.WorkflowWorkCenter);
        _b = await MongoIntegrationHarness.CreateAsync(SchemaProfile.WorkflowWorkCenter);
    }

    public async Task DisposeAsync()
    {
        await _a.DisposeAsync();
        await _b.DisposeAsync();
    }

    // ── AC-W15 · the inherited delete refuses ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC_W15_WorkflowInstanceRepository_refuses_delete_and_the_row_is_unchanged()
    {
        var repository = new WorkflowInstanceRepository(_a.DbContext, _a.TenantContext);
        var row = Instance(_a, NewObject());
        row.UpdatedAt = LongAgo;
        await AssertDeleteRefusedAsync(row, PlatformCollections.WorkflowInstances, repository.DeleteAsync, repository);
    }

    [Fact]
    public async Task AC_W15_ApprovalTaskRepository_refuses_delete_and_the_row_is_unchanged()
    {
        var repository = new ApprovalTaskRepository(_a.DbContext, _a.TenantContext);
        var row = Task(_a, Guid.NewGuid());
        row.UpdatedAt = LongAgo;
        await AssertDeleteRefusedAsync(row, PlatformCollections.ApprovalTasks, repository.DeleteAsync, repository);
    }

    [Fact]
    public async Task AC_W15_WorkflowTransitionLogRepository_refuses_delete_and_the_row_is_unchanged()
    {
        var repository = new WorkflowTransitionLogRepository(_a.DbContext, _a.TenantContext);
        var row = Log(_a, Guid.NewGuid(), Guid.NewGuid(), WorkflowTransitionAction.Approve, 1, "delete-refused");
        row.UpdatedAt = LongAgo;
        await AssertDeleteRefusedAsync(row, PlatformCollections.WorkflowTransitionLogs, repository.DeleteAsync, repository);
    }

    private async Task AssertDeleteRefusedAsync<T>(
        T row,
        string collection,
        Func<Guid, CancellationToken, Task> derivedDelete,
        TenantRepository<T> baseReference) where T : TenantScopedEntity
    {
        await _a.Database.GetCollection<T>(collection).InsertOneAsync(row);
        var before = await RawAsync(collection, row.Id);
        Assert.False(before["IsDeleted"].AsBoolean);

        await Assert.ThrowsAsync<NotSupportedException>(() => derivedDelete(row.Id, default));
        await Assert.ThrowsAsync<NotSupportedException>(() => baseReference.DeleteAsync(row.Id));
        await Assert.ThrowsAsync<NotSupportedException>(() => ((Diten.Platform.Common.Persistence.RepositoryBase<T>)baseReference).DeleteAsync(row.Id));

        var after = await RawAsync(collection, row.Id);
        Assert.False(after["IsDeleted"].AsBoolean);
        Assert.Equal(before["UpdatedAt"], after["UpdatedAt"]);
        Assert.Equal(before, after);
        Assert.NotNull(await baseReference.GetByIdAsync(row.Id)); // still a live row of this tenant
    }

    // ── AC-W13 · hidden by default, flagged on request ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AC_W13_by_default_a_row_deleted_by_IsDeleted_alone_is_hidden()
    {
        var objectId = NewObject();
        var live = await SeedInstanceAsync(_a, objectId);
        await SeedInstanceAsync(_a, objectId, deleted: true);

        var row = Assert.Single((await ByObjectsAsync(_a, objectId)).Single().Instances);

        Assert.Equal(live.Id, row.WorkflowInstanceId);
        Assert.False(row.IsDeleted);
    }

    [Fact]
    public async Task AC_W13_includeDeleted_returns_the_deleted_rows_flagged_whatever_their_DeletedAt_says()
    {
        var objectId = NewObject();
        var live = await SeedInstanceAsync(_a, objectId);
        var markedOnce = await SeedInstanceAsync(_a, objectId, deleted: true);
        var markedTwice = await SeedInstanceAsync(_a, objectId, deleted: true, deletedAt: DateTimeOffset.UtcNow);

        var rows = (await ByObjectsAsync(_a, objectId, includeDeleted: true)).Single().Instances
            .ToDictionary(x => x.WorkflowInstanceId);

        Assert.Equal(3, rows.Count);
        Assert.False(rows[live.Id].IsDeleted);
        Assert.True(rows[markedOnce.Id].IsDeleted);
        Assert.True(rows[markedTwice.Id].IsDeleted);
    }

    [Fact]
    public async Task AC_W13_the_flag_changes_nothing_for_an_object_with_no_deleted_row()
    {
        var objectId = NewObject();
        var live = await SeedInstanceAsync(_a, objectId, key: "no-deleted-" + objectId);

        var plain = Assert.Single((await ByObjectsAsync(_a, objectId)).Single().Instances);
        var flagged = Assert.Single((await ByObjectsAsync(_a, objectId, includeDeleted: true)).Single().Instances);

        Assert.Equal(live.Id, plain.WorkflowInstanceId);
        Assert.Equal(plain, flagged);
    }

    [Fact]
    public async Task AC_W15_history_and_the_single_read_answer_404_for_a_deleted_instance()
    {
        var deleted = await SeedInstanceAsync(_a, NewObject(), deleted: true);

        await AssertNotFoundAsync(await HistoryAsync(_a, deleted.Id));
        await AssertNotFoundAsync(await ById(_a).Handle(new GetWorkflowInstanceByIdQuery(deleted.Id, Correlation), default));
    }

    [Fact]
    public async Task AC_W15_history_filters_a_deleted_log_row_and_every_row_it_returns_is_not_deleted()
    {
        var instance = await SeedInstanceAsync(_a, NewObject());
        var task = Task(_a, instance.Id);
        await _a.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).InsertOneAsync(task);
        var logs = _a.Database.GetCollection<WorkflowTransitionLog>(PlatformCollections.WorkflowTransitionLogs);
        await logs.InsertOneAsync(Log(_a, instance.Id, task.Id, WorkflowTransitionAction.Start, 1, "kept-" + instance.Id));
        var gone = Log(_a, instance.Id, task.Id, WorkflowTransitionAction.Approve, 2, "gone-" + instance.Id);
        gone.IsDeleted = true;
        await logs.InsertOneAsync(gone);

        var response = await HistoryAsync(_a, instance.Id);

        var row = Assert.Single(response.Data!);
        Assert.Equal(1L, row.SequenceNo);
        Assert.False(row.IsDeleted);
    }

    // ── AC-W16 · inside the tenant ──────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AC_W16_another_tenants_object_answers_200_with_an_empty_list(bool includeDeleted)
    {
        var objectId = NewObject();
        await SeedInstanceAsync(_b, objectId);
        await SeedInstanceAsync(_b, objectId, deleted: true);

        var response = await ByObjects(_a).Handle(
            new GetWorkflowInstancesByObjectsQuery(ObjectType, [objectId], Correlation, includeDeleted), default);

        Assert.Equal(200, response.StatusCode);
        var entry = Assert.Single(response.Data!);
        Assert.Equal(objectId, entry.ObjectId);
        Assert.Empty(entry.Instances);

        // Non-vacuity: the fixture is there, and its own tenant reads it.
        Assert.Equal(includeDeleted ? 2 : 1, (await ByObjectsAsync(_b, objectId, includeDeleted)).Single().Instances.Count);
    }

    [Fact]
    public async Task AC_W16_includeDeleted_returns_no_row_of_another_tenant_for_a_shared_object_id()
    {
        var objectId = NewObject();
        var own = await SeedInstanceAsync(_a, objectId, deleted: true);
        await SeedInstanceAsync(_b, objectId);
        await SeedInstanceAsync(_b, objectId, deleted: true);

        var row = Assert.Single((await ByObjectsAsync(_a, objectId, includeDeleted: true)).Single().Instances);

        Assert.Equal(own.Id, row.WorkflowInstanceId);
        Assert.True(row.IsDeleted);
    }

    [Fact]
    public async Task AC_W16_history_and_the_single_read_of_another_tenants_instance_are_a_non_leaking_404()
    {
        var objectId = NewObject();
        foreach (var foreign in new[] { await SeedInstanceAsync(_b, objectId), await SeedInstanceAsync(_b, objectId, deleted: true) })
        {
            await AssertNotFoundAsync(await HistoryAsync(_a, foreign.Id));
            await AssertNotFoundAsync(await ById(_a).Handle(new GetWorkflowInstanceByIdQuery(foreign.Id, Correlation), default));
        }
    }

    // ── AC-W2 · the stored keys, keyed and unkeyed ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AC_W2_by_objects_returns_the_stored_key_and_null_for_an_unkeyed_row()
    {
        var keyedObject = NewObject();
        var unkeyedObject = NewObject();
        var key = "start-" + keyedObject;
        await SeedInstanceAsync(_a, keyedObject, key: key);
        await SeedInstanceAsync(_a, unkeyedObject);

        var rows = (await ByObjectsAsync(_a, keyedObject, unkeyedObject)).ToDictionary(x => x.ObjectId);

        Assert.Equal(key, Assert.Single(rows[keyedObject].Instances).IdempotencyKey);
        Assert.Null(Assert.Single(rows[unkeyedObject].Instances).IdempotencyKey);
    }

    [Fact]
    public async Task AC_W2_history_returns_the_stored_task_id_and_key_and_null_for_an_unkeyed_row()
    {
        var instance = await SeedInstanceAsync(_a, NewObject());
        var task = Task(_a, instance.Id);
        await _a.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).InsertOneAsync(task);
        var key = "approve-" + instance.Id;
        var logs = _a.Database.GetCollection<WorkflowTransitionLog>(PlatformCollections.WorkflowTransitionLogs);
        await logs.InsertOneAsync(Log(_a, instance.Id, task.Id, WorkflowTransitionAction.Start, 1, key: null));
        await logs.InsertOneAsync(Log(_a, instance.Id, task.Id, WorkflowTransitionAction.Approve, 2, key));
        await logs.InsertOneAsync(Log(_a, instance.Id, approvalTaskId: null, WorkflowTransitionAction.Cancel, 3, key: null));

        var rows = (await HistoryAsync(_a, instance.Id)).Data!;

        Assert.Equal([1L, 2L, 3L], rows.Select(x => x.SequenceNo));
        Assert.Equal([task.Id, task.Id, null], rows.Select(x => x.ApprovalTaskId));
        Assert.Equal([null, key, null], rows.Select(x => x.IdempotencyKey));
        Assert.All(rows, row => Assert.False(row.IsDeleted));
    }

    // ── the real wire: JWT, tenant resolution, [HasPermission], query binding, JSON ─────────────────────────────

    [Fact]
    public async Task AC_W13_on_the_wire_the_query_parameter_reaches_the_read_and_isDeleted_is_in_the_body()
    {
        using var host = new Host(_a.DbContext);
        var objectId = NewObject();
        var key = "wire-" + objectId;
        var deleted = await SeedInstanceAsync(_a, objectId, deleted: true, key: key);
        var token = host.Token(Guid.NewGuid(), _a.TenantId, WorkflowPermissions.InstancesView);
        var path = $"/api/v1/workflow/instances/by-objects?objectType={ObjectType}&objectIds={objectId}";

        var plain = await host.GetAsync(path, token);
        var explicitFalse = await host.GetAsync(path + "&includeDeleted=false", token);
        var flagged = await host.GetAsync(path + "&includeDeleted=true", token);

        Assert.Equal(HttpStatusCode.OK, plain.Status);
        Assert.Equal(0, plain.Data[0].GetProperty("instances").GetArrayLength());
        Assert.Equal(0, explicitFalse.Data[0].GetProperty("instances").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, flagged.Status);
        var row = flagged.Data[0].GetProperty("instances")[0];
        Assert.Equal(deleted.Id, row.GetProperty("workflowInstanceId").GetGuid());
        Assert.True(row.GetProperty("isDeleted").GetBoolean());
        Assert.Equal(key, row.GetProperty("idempotencyKey").GetString());
    }

    [Fact]
    public async Task AC_W3_by_objects_and_history_keep_every_existing_field_and_add_the_new_ones()
    {
        using var host = new Host(_a.DbContext);
        var objectId = NewObject();
        var instance = await SeedInstanceAsync(_a, objectId);
        var task = Task(_a, instance.Id);
        await _a.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).InsertOneAsync(task);
        var key = "compat-" + objectId;
        await _a.Database.GetCollection<WorkflowTransitionLog>(PlatformCollections.WorkflowTransitionLogs)
            .InsertOneAsync(Log(_a, instance.Id, task.Id, WorkflowTransitionAction.Approve, 1, key));
        var token = host.Token(Guid.NewGuid(), _a.TenantId, WorkflowPermissions.InstancesView);

        var status = (await host.GetAsync(
            $"/api/v1/workflow/instances/by-objects?objectType={ObjectType}&objectIds={objectId}", token))
            .Data[0].GetProperty("instances")[0];
        AssertKind(status, "workflowInstanceId", JsonValueKind.String);
        AssertKind(status, "status", JsonValueKind.String);
        AssertKind(status, "outcome", JsonValueKind.Null);
        AssertKind(status, "currentStageCode", JsonValueKind.String);
        AssertKind(status, "currentStepCode", JsonValueKind.String);
        AssertKind(status, "startedAt", JsonValueKind.String);
        AssertKind(status, "completedAt", JsonValueKind.Null);
        AssertKind(status, "idempotencyKey", JsonValueKind.Null);
        AssertKind(status, "isDeleted", JsonValueKind.False);

        var row = (await host.GetAsync($"/api/v1/workflow/instances/{instance.Id}/history", token)).Data[0];
        AssertKind(row, "sequenceNo", JsonValueKind.Number);
        AssertKind(row, "action", JsonValueKind.String);
        AssertKind(row, "actorId", JsonValueKind.Null);
        AssertKind(row, "actorDisplay", JsonValueKind.Null);
        AssertKind(row, "fromStageCode", JsonValueKind.String);
        AssertKind(row, "fromStepCode", JsonValueKind.String);
        AssertKind(row, "toStageCode", JsonValueKind.Null);
        AssertKind(row, "toStepCode", JsonValueKind.Null);
        AssertKind(row, "stepName", JsonValueKind.Null);
        AssertKind(row, "comment", JsonValueKind.Null);
        AssertKind(row, "reasonCode", JsonValueKind.Null);
        AssertKind(row, "occurredAt", JsonValueKind.String);
        AssertKind(row, "signatureId", JsonValueKind.Null);
        Assert.Equal(task.Id, row.GetProperty("approvalTaskId").GetGuid());
        Assert.Equal(key, row.GetProperty("idempotencyKey").GetString());
        AssertKind(row, "isDeleted", JsonValueKind.False);
    }

    [Fact]
    public async Task AC_W16_on_the_wire_a_caller_without_instances_view_is_refused_on_all_three_actions()
    {
        using var host = new Host(_a.DbContext);
        var objectId = NewObject();
        var instance = await SeedInstanceAsync(_a, objectId);
        var token = host.Token(Guid.NewGuid(), _a.TenantId, WorkflowPermissions.DefinitionsView);
        var allowed = host.Token(Guid.NewGuid(), _a.TenantId, WorkflowPermissions.InstancesView);
        var paths = new[]
        {
            $"/api/v1/workflow/instances/by-objects?objectType={ObjectType}&objectIds={objectId}",
            $"/api/v1/workflow/instances/by-objects?objectType={ObjectType}&objectIds={objectId}&includeDeleted=true",
            $"/api/v1/workflow/instances/{instance.Id}",
            $"/api/v1/workflow/instances/{instance.Id}/history"
        };

        foreach (var path in paths)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await host.GetAsync(path, token)).Status);
            Assert.Equal(HttpStatusCode.OK, (await host.GetAsync(path, allowed)).Status); // non-vacuity: the path itself answers
        }
    }

    [Fact]
    public async Task AC_W16_on_the_wire_another_tenants_records_stay_out_of_reach()
    {
        using var host = new Host(_a.DbContext);
        var objectId = NewObject();
        var foreign = await SeedInstanceAsync(_b, objectId);
        await SeedInstanceAsync(_b, objectId, deleted: true);
        var token = host.Token(Guid.NewGuid(), _a.TenantId, WorkflowPermissions.InstancesView);
        var byObjects = $"/api/v1/workflow/instances/by-objects?objectType={ObjectType}&objectIds={objectId}";

        foreach (var path in new[] { byObjects, byObjects + "&includeDeleted=true" })
        {
            var answer = await host.GetAsync(path, token);
            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.Equal(0, answer.Data[0].GetProperty("instances").GetArrayLength());
        }

        foreach (var path in new[] { $"/api/v1/workflow/instances/{foreign.Id}", $"/api/v1/workflow/instances/{foreign.Id}/history" })
        {
            var answer = await host.GetAsync(path, token);
            Assert.Equal(HttpStatusCode.NotFound, answer.Status);
            Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, answer.ReasonCode);
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static readonly DateTimeOffset LongAgo = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static string NewObject() => "payment-" + Guid.NewGuid().ToString("N");

    private static WorkflowInstance Instance(MongoIntegrationHarness tenant, string objectId, string? key = null) => new()
    {
        TenantId = tenant.TenantId, TemplateId = Guid.NewGuid(), WorkflowTemplateId = Guid.NewGuid(), ObjectType = ObjectType,
        ObjectId = objectId, ObjectRef = $"treasury|{ObjectType}|{objectId}", Status = WorkflowInstanceStatus.Active,
        StartedAt = DateTimeOffset.UtcNow, IdempotencyKey = key
    };

    private static ApprovalTask Task(MongoIntegrationHarness tenant, Guid instanceId) => new()
    {
        TenantId = tenant.TenantId, WorkflowInstanceId = instanceId, StageCode = "stage-1", StepCode = "step-1",
        Status = ApprovalTaskStatus.WaitingApproval, AssigneeRef = "approver"
    };

    private static WorkflowTransitionLog Log(MongoIntegrationHarness tenant, Guid instanceId, Guid? approvalTaskId,
        WorkflowTransitionAction action, long sequenceNo, string? key) => new()
    {
        TenantId = tenant.TenantId, WorkflowInstanceId = instanceId, ApprovalTaskId = approvalTaskId, Action = action,
        SequenceNo = sequenceNo, IdempotencyKey = key
    };

    private static async Task<WorkflowInstance> SeedInstanceAsync(MongoIntegrationHarness tenant, string objectId,
        bool deleted = false, DateTimeOffset? deletedAt = null, string? key = null)
    {
        // A raw insert: no production path writes a deleted row any more, which is what these reads must still survive.
        var row = Instance(tenant, objectId, key);
        row.IsDeleted = deleted;
        row.DeletedAt = deletedAt;
        await tenant.Database.GetCollection<WorkflowInstance>(PlatformCollections.WorkflowInstances).InsertOneAsync(row);
        return row;
    }

    private Task<BsonDocument> RawAsync(string collection, Guid id) =>
        _a.Database.GetCollection<BsonDocument>(collection)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(id, GuidRepresentation.Standard)))
            .SingleAsync();

    private static GetWorkflowInstancesByObjectsHandler ByObjects(MongoIntegrationHarness tenant) =>
        new(new WorkflowInstanceRepository(tenant.DbContext, tenant.TenantContext));

    private static GetWorkflowInstanceByIdHandler ById(MongoIntegrationHarness tenant) =>
        new(new WorkflowInstanceRepository(tenant.DbContext, tenant.TenantContext));

    private static Task<IReadOnlyList<WorkflowObjectInstancesDto>> ByObjectsAsync(MongoIntegrationHarness tenant, string objectId,
        bool includeDeleted = false) => ByObjectsCoreAsync(tenant, [objectId], includeDeleted);

    private static Task<IReadOnlyList<WorkflowObjectInstancesDto>> ByObjectsAsync(MongoIntegrationHarness tenant, string first, string second) =>
        ByObjectsCoreAsync(tenant, [first, second], includeDeleted: false);

    private static async Task<IReadOnlyList<WorkflowObjectInstancesDto>> ByObjectsCoreAsync(MongoIntegrationHarness tenant,
        IReadOnlyList<string> objectIds, bool includeDeleted)
    {
        var response = await ByObjects(tenant).Handle(
            new GetWorkflowInstancesByObjectsQuery(ObjectType, objectIds, Correlation, includeDeleted), default);
        Assert.True(response.IsSuccessful, string.Join(",", response.Errors));
        return response.Data!;
    }

    private static Task<Response<IReadOnlyList<WorkflowInstanceHistoryEntryDto>>> HistoryAsync(MongoIntegrationHarness tenant, Guid id) =>
        new GetWorkflowInstanceHistoryHandler(
                new WorkflowInstanceRepository(tenant.DbContext, tenant.TenantContext),
                new WorkflowTransitionLogRepository(tenant.DbContext, tenant.TenantContext),
                new ApprovalTaskRepository(tenant.DbContext, tenant.TenantContext),
                new WorkflowTemplateVersionRepository(tenant.DbContext, tenant.TenantContext))
            .Handle(new GetWorkflowInstanceHistoryQuery(id, Correlation), default);

    private static Task AssertNotFoundAsync<T>(Response<T> response)
    {
        Assert.Equal(404, response.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, response.ReasonCode);
        Assert.Null(response.Data);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    private static void AssertKind(JsonElement element, string property, JsonValueKind kind) =>
        Assert.Equal(kind, element.GetProperty(property).ValueKind);

    private sealed record ApiResult(HttpStatusCode Status, string Body)
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
    }

    /// <summary>The real wire over the real repositories: JWT, tenant resolution, [HasPermission], MediatR, the read handlers.</summary>
    private sealed class Host : IDisposable
    {
        private const string Issuer = "diten-auth-workflow-retention";
        private const string Audience = "diten-platform-workflow-retention";
        private const string Secret = "MOD-0023 S17 DL-1 retention read signing key, test only, 0123456789abcdef";
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
                    services.AddSingleton(db);
                    services.AddScoped<IWorkflowTemplateVersionRepository, WorkflowTemplateVersionRepository>();
                    services.AddScoped<IWorkflowInstanceRepository, WorkflowInstanceRepository>();
                    services.AddScoped<IApprovalTaskRepository, ApprovalTaskRepository>();
                    services.AddScoped<IWorkflowTransitionLogRepository, WorkflowTransitionLogRepository>();
                    services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(WorkflowPermissions).Assembly));
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

        public async Task<ApiResult> GetAsync(string path, string bearer)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            var response = await _server.CreateClient().SendAsync(request);
            return new ApiResult(response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        public void Dispose() => _server.Dispose();
    }
}
