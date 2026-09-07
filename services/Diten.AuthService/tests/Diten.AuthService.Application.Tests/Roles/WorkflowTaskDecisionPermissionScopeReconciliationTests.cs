using System.Reflection;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Roles;

[Collection(AuthPermissionOnboardingMongoCollectionDefinition.Name)]
public sealed class WorkflowTaskDecisionPermissionScopeReconciliationTests
{
    private const string DatabaseName = "diten_auth_permission_onboarding_itest";

    private static readonly IReadOnlySet<string> TenantExecutionKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "platform.workflow.instances.start",
        "platform.workflow.tasks.approve",
        "platform.workflow.tasks.reject"
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

    [Fact]
    public async Task Real_mongo_seed_reconciles_exact_three_tenant_execution_permissions_and_replay_is_stable()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
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
        Assert.Equal(3, permissions.Count(permission => permission.Scope == PermissionScope.Tenant));
        Assert.Equal(10, permissions.Count(permission => permission.Scope == PermissionScope.PlatformAdmin));
    }
}
