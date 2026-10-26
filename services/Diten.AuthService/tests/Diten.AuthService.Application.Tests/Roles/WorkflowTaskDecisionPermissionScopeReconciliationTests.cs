using Diten.AuthService.Application.Tests.Persistence;
using System.Reflection;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Roles;

// Main sync FIX1 (CT decision C, BL-595 Auth): it used to write to the SHARED mongodb://localhost:27017 (measured: 2250
// connection samples in one Auth App run). Now on the OWNED disposable mongod (DITEN_TEST_MONGOD, the
// AuthReconciliationMongoCollection fixture) — skipped BY NAME without one (OwnedMongoFact), red under DITEN_REQUIRE_OWNED_MONGO=1.
[Collection(AuthReconciliationMongoCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class WorkflowTaskDecisionPermissionScopeReconciliationTests(DisposableAuthMongoReplicaSet ownedMongo)
{
    private const string DatabaseName = "diten_auth_permission_onboarding_itest";

    // WP-LANE-MAIN-SYNC-02 A1 (owner decision 2026-10-14): the seven Tenant workflow runtime keys by name — the lane's
    // three plus main's WP-0023-FX four (instances.view, tasks.cancel, tasks.delegate, tasks.request-info).
    private static readonly IReadOnlySet<string> TenantExecutionKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "platform.workflow.instances.start",
        "platform.workflow.instances.view",
        "platform.workflow.tasks.approve",
        "platform.workflow.tasks.cancel",
        "platform.workflow.tasks.delegate",
        "platform.workflow.tasks.reject",
        "platform.workflow.tasks.request-info"
    };

    private static readonly IReadOnlySet<string> WorkflowKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "platform.workflow.definitions.view",
        "platform.workflow.definitions.manage",
        "platform.workflow.definitions.publish",
        "platform.workflow.instances.start",
        "platform.workflow.instances.view",
        "platform.workflow.tasks.approve",
        "platform.workflow.tasks.reject",
        "platform.workflow.tasks.delegate",
        "platform.workflow.tasks.request-info",
        "platform.workflow.tasks.cancel",
        "platform.workflow.transitions.evaluate",
        "platform.workflow.escalations.manage",
        "platform.workflow.escalations.run"
    };

    [OwnedMongoFact]
    public async Task Real_mongo_seed_reconciles_exact_seven_tenant_execution_permissions_and_replay_is_stable()
    {
        var settings = MongoClientSettings.FromConnectionString(ownedMongo.ConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        var database = client.GetDatabase(DatabaseName);
        var permissions = database.GetCollection<Permission>("permissions");
        var original = await permissions.Find(_ => true).ToListAsync();
        var originalIds = original.Select(permission => permission.Id).ToHashSet();

        try
        {
            await permissions.DeleteManyAsync(
                Builders<Permission>.Filter.In(permission => permission.Key, WorkflowKeys));
            await permissions.InsertManyAsync(WorkflowKeys.Select(CreateLegacyDivergentPermission));

            await InvokeSeedPermissionsAsync(database);
            var first = await ReadWorkflowPermissionsAsync(permissions);
            AssertExactScopePartition(first);
            var firstIds = first.ToDictionary(permission => permission.Key, permission => permission.Id, StringComparer.Ordinal);

            await InvokeSeedPermissionsAsync(database);
            var replay = await ReadWorkflowPermissionsAsync(permissions);
            AssertExactScopePartition(replay);
            Assert.Equal(
                firstIds.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                replay.ToDictionary(permission => permission.Key, permission => permission.Id, StringComparer.Ordinal)
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal));
        }
        finally
        {
            await permissions.DeleteManyAsync(
                Builders<Permission>.Filter.Nin(permission => permission.Id, originalIds));
            foreach (var permission in original)
            {
                await permissions.ReplaceOneAsync(
                    existing => existing.Id == permission.Id,
                    permission,
                    new ReplaceOptions { IsUpsert = true });
            }
        }
    }

    private static Permission CreateLegacyDivergentPermission(string key)
    {
        var actionSeparator = key.LastIndexOf('.');
        return new Permission(
            "platform",
            key["platform.".Length..actionSeparator],
            key[(actionSeparator + 1)..],
            key,
            null,
            moduleOverride: "WORKFLOW",
            scope: TenantExecutionKeys.Contains(key) ? PermissionScope.PlatformAdmin : PermissionScope.Tenant);
    }

    private static async Task InvokeSeedPermissionsAsync(IMongoDatabase database)
    {
        var method = typeof(DataSeeder).GetMethod(
            "SeedPermissionsAsync",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("DataSeeder.SeedPermissionsAsync was not found.");
        var task = method.Invoke(null, [database]) as Task
            ?? throw new InvalidOperationException("DataSeeder.SeedPermissionsAsync did not return a Task.");
        await task;
    }

    private static async Task<List<Permission>> ReadWorkflowPermissionsAsync(
        IMongoCollection<Permission> permissions)
        => await permissions.Find(
            Builders<Permission>.Filter.In(permission => permission.Key, WorkflowKeys)).ToListAsync();

    private static void AssertExactScopePartition(IReadOnlyCollection<Permission> permissions)
    {
        Assert.Equal(WorkflowKeys.Count, permissions.Count);
        Assert.Equal(WorkflowKeys, permissions.Select(permission => permission.Key).ToHashSet(StringComparer.Ordinal));
        Assert.All(permissions, permission =>
        {
            Assert.Equal(
                TenantExecutionKeys.Contains(permission.Key) ? PermissionScope.Tenant : PermissionScope.PlatformAdmin,
                permission.Scope);
        });
        Assert.All(
            permissions.Where(permission => TenantExecutionKeys.Contains(permission.Key)),
            permission => Assert.Equal("workflow", permission.Module));
        Assert.Equal(7, permissions.Count(permission => permission.Scope == PermissionScope.Tenant));
        Assert.Equal(6, permissions.Count(permission => permission.Scope == PermissionScope.PlatformAdmin));
    }
}
