using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles.Commands;
using Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using System.Runtime.CompilerServices;

namespace Diten.AuthService.Application.Tests.Roles;

internal static class MongoSerializationTestBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
        => BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
}

/// <summary>
/// Real-Mongo proof for the production E11000 catch/query path. There is intentionally no
/// skip-if-unavailable fallback: a missing Mongo test dependency fails this acceptance gate.
/// </summary>
public sealed class RolePermissionRepositoryMongoRaceTests
{
    private static string MongoUri =>
        Environment.GetEnvironmentVariable("MONGO_TEST_URI") ?? "mongodb://localhost:27017";

    [Fact]
    public async Task Concurrent_exact_assignment_returns_one_insert_and_one_no_op()
    {
        var settings = MongoClientSettings.FromConnectionString(MongoUri);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        var databaseName = "diten_auth_ppm_race_" + Guid.NewGuid().ToString("N");
        var database = client.GetDatabase(databaseName);

        try
        {
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            var collection = database.GetCollection<RolePermission>("rolePermissions");
            await collection.Indexes.CreateOneAsync(new CreateIndexModel<RolePermission>(
                Builders<RolePermission>.IndexKeys
                    .Ascending(x => x.RoleId)
                    .Ascending(x => x.PermissionId)
                    .Ascending(x => x.TenantId),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = RolePermissionRepository.AssignmentUniqueIndexName
                }));

            var tenantId = Guid.NewGuid();
            var roleId = Guid.NewGuid();
            var permissionId = Guid.NewGuid();
            var tenantContext = new TestTenantContext(tenantId);
            var repository = new RolePermissionRepository(database, tenantContext);

            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<bool> InsertAsync(string actor)
            {
                await start.Task;
                return await repository.TryAssignAsync(
                    RolePermission.ManualGrant(roleId, permissionId, tenantId, actor),
                    CancellationToken.None);
            }

            var first = InsertAsync("actor-1");
            var second = InsertAsync("actor-2");
            start.SetResult();
            var results = await Task.WhenAll(first, second);

            Assert.Equal(1, results.Count(x => x));
            Assert.Equal(1, results.Count(x => !x));
            Assert.Equal(1, await collection.CountDocumentsAsync(Builders<RolePermission>.Filter.Empty));
            var row = await collection.Find(Builders<RolePermission>.Filter.Empty).SingleAsync();
            Assert.Equal(roleId, row.RoleId);
            Assert.Equal(permissionId, row.PermissionId);
            Assert.Equal(tenantId, row.TenantId);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task Concurrent_handlers_increment_version_and_write_audit_only_for_effective_insert()
    {
        var settings = MongoClientSettings.FromConnectionString(MongoUri);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        var databaseName = "diten_auth_ppm_handler_race_" + Guid.NewGuid().ToString("N");
        var database = client.GetDatabase(databaseName);

        try
        {
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            var collection = database.GetCollection<RolePermission>("rolePermissions");
            await collection.Indexes.CreateOneAsync(new CreateIndexModel<RolePermission>(
                Builders<RolePermission>.IndexKeys.Ascending(x => x.RoleId).Ascending(x => x.PermissionId).Ascending(x => x.TenantId),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = RolePermissionRepository.AssignmentUniqueIndexName
                }));

            var tenantId = Guid.NewGuid();
            var role = new Role("ppm-reviewer", "PPM Reviewer", null, tenantId);
            var permission = new Permission("ppm", "projects", "read", "Read projects", null);
            var tenantContext = new TestTenantContext(tenantId);
            var repository = new RolePermissionRepository(database, tenantContext);
            var version = new CountingVersion();
            var audit = new CountingAudit();

            AssignPermissionCommandHandler CreateHandler() => new(
                new SingleRoleRepository(role),
                new SinglePermissionRepository(permission),
                repository,
                version,
                tenantContext,
                audit,
                new Actor(Guid.NewGuid()));

            var command = new AssignPermissionCommand(role.Id, permission.Id);
            var responses = await Task.WhenAll(
                CreateHandler().Handle(command, CancellationToken.None),
                CreateHandler().Handle(command, CancellationToken.None));

            Assert.All(responses, response => Assert.True(response.IsSuccessful));
            Assert.Equal(1, await collection.CountDocumentsAsync(Builders<RolePermission>.Filter.Empty));
            Assert.Equal(1, version.Count);
            Assert.Equal(1, audit.Count);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    private sealed class TestTenantContext(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; private set; } = tenantId;
        public bool IsResolved { get; private set; } = true;
        public bool IsPlatformContext { get; private set; }
        public Guid? TargetTenantId { get; private set; }
        public void SetTenant(Guid value)
        {
            TenantId = value;
            IsResolved = true;
            IsPlatformContext = false;
            TargetTenantId = null;
        }
        public void SetPlatformContext(Guid targetTenantId)
        {
            TenantId = targetTenantId;
            IsResolved = true;
            IsPlatformContext = true;
            TargetTenantId = targetTenantId;
        }
    }

    private sealed class Actor(Guid userId) : ICurrentUserAccessor
    {
        public Guid? UserId { get; } = userId;
    }

    private sealed class CountingVersion : IRoleAssignmentVersionService
    {
        private int _count;
        public int Count => _count;
        public Task<long> GetAsync(Guid tenantId, CancellationToken ct) => Task.FromResult((long)_count);
        public Task<long> IncrementAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult((long)Interlocked.Increment(ref _count));
    }

    private sealed class CountingAudit : IRbacAuditRecorder
    {
        private int _count;
        public int Count => _count;
        public Task RecordAsync(string eventName, Guid tenantId, object metadata, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _count);
            return Task.CompletedTask;
        }
    }

    private sealed class SingleRoleRepository(Role role) : IRoleRepository
    {
        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct)
            => Task.FromResult(role.Id == id && role.TenantId == tenantId ? role : null);
        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> CreateAsync(Role value, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpdateAsync(Role value, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class SinglePermissionRepository(Permission permission) : IPermissionRepository
    {
        public Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(permission.Id == id ? permission : null);
        public Task<Permission?> GetByKeyAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission?> GetByKeyIncludingDeletedAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task ReactivateAsync(Guid id, string displayName, string? description, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetByModuleAsync(string module, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission> CreateAsync(Permission value, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(Permission value, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }
}
