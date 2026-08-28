using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

public sealed class WorkflowTransitionGateMongoRepositoryTests
{
    private static readonly Guid TenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private const string ObjectType = "ModuleCatalogItem";
    private const string ObjectId = "b76e73ba-558d-4b06-870b-e900d178b448";
    private const string ObjectRef = "ModuleCatalogItem:B09-WF-GATE-FIXTURE";

    [Fact]
    public async Task Active_bound_instance_returns_blocked_pending_approval_without_mongo_sort_failure()
    {
        var mongo = MongoTestSettings.FromEnvironment();
        var settings = MongoClientSettings.FromConnectionString(mongo.ConnectionString);
        /*
         * ⚠ THE SAME GUID REPRESENTATION PRODUCTION USES, AND IT IS NOT OPTIONAL. MongoIntegrationHarness
         * registers a PROCESS-GLOBAL GuidSerializer(Standard) when it runs, so a client built without this
         * line encodes Guids one way or the other depending on WHICH TEST CLASS RAN FIRST. That is the
         * classic "passes alone, fails in the suite" shape: the query stops matching its own inserted rows
         * and the failure looks like data loss. Pinning it here mirrors
         * Diten.Platform.Infrastructure.DependencyInjection and makes the order irrelevant.
         */
        settings.GuidRepresentation = GuidRepresentation.Standard;
        var client = new MongoClient(settings);
        /*
         * ⚠ A FIXED NAME AND A REAL SCHEMA. This used to be a Guid-named database with NO indexes at all —
         * which is a strange way to run the ONE test whose subject is a Mongo sort failure: the query it
         * guards ("cannot sort with keys that are parallel arrays") behaves differently depending on whether
         * the index backing the sort exists. It now builds the WorkflowWorkCenter profile, and only that.
         * The tenant id here is a fixture constant, so the database is dropped and reused rather than shared.
         */
        var databaseName = $"{mongo.DatabaseName}_workflow_transition_gate";
        await client.DropDatabaseAsync(databaseName);
        var database = client.GetDatabase(databaseName);

        try
        {
            await PlatformSchemaManifest.ApplyAsync(database, new[] { SchemaProfile.WorkflowWorkCenter });

            var tenantContext = new TenantContext();
            tenantContext.SetTenant(TenantId);
            var dbContext = new PlatformDbContext(client, database);
            var instances = new WorkflowInstanceRepository(dbContext, tenantContext);
            var tasks = new ApprovalTaskRepository(dbContext, tenantContext);
            var handler = new EvaluateWorkflowTransitionGateHandler(instances, tasks);

            await instances.CreateAsync(NewInstance(WorkflowInstanceStatus.Completed, DateTimeOffset.UtcNow.AddDays(-2)));
            var active = await instances.CreateAsync(NewInstance(WorkflowInstanceStatus.Active, DateTimeOffset.UtcNow.AddMinutes(-5)));
            var activeTask = await tasks.CreateAsync(new ApprovalTask
            {
                TenantId = TenantId,
                WorkflowInstanceId = active.Id,
                Status = ApprovalTaskStatus.WaitingApproval,
                AssigneeRef = "actor-001"
            });

            var response = await handler.Handle(
                new EvaluateWorkflowTransitionGateQuery(
                    new EvaluateWorkflowTransitionGateRequest(
                        ObjectType,
                        ObjectId,
                        ObjectRef,
                        "Activate",
                        "Active",
                        "system:module-catalog-activate",
                        "B09_SMOKE"),
                    "mongo-transition-gate-test"),
                CancellationToken.None);

            Assert.True(response.IsSuccessful);
            Assert.Equal(WorkflowTransitionGateDecision.Blocked, response.Data!.Decision);
            Assert.Equal(WorkflowTransitionGateStatus.PendingApproval, response.Data.GateStatus);
            Assert.Equal(active.Id, response.Data.WorkflowInstanceId);
            Assert.Equal(activeTask.Id, response.Data.ActiveTaskId);
            Assert.Equal(WorkflowReasonCodes.WorkflowPendingApproval, response.Data.BlockingReasonCode);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    private static WorkflowInstance NewInstance(WorkflowInstanceStatus status, DateTimeOffset startedAt) =>
        new()
        {
            TenantId = TenantId,
            TemplateId = Guid.NewGuid(),
            WorkflowTemplateId = Guid.NewGuid(),
            TemplateVersionId = Guid.NewGuid(),
            ObjectType = ObjectType,
            ObjectId = ObjectId,
            ObjectRef = ObjectRef,
            Status = status,
            StartedAt = startedAt,
            LastTransitionAt = startedAt
        };

    private sealed record MongoTestSettings(string ConnectionString, string DatabaseName)
    {
        public static MongoTestSettings FromEnvironment() =>
            new(
                Get("MongoDbSettings__ConnectionString", "mongodb://localhost:27017"),
                Get("WorkflowGate__MongoDb__DatabaseName", "workflow_gate_repository_tests"));

        private static string Get(string key, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
