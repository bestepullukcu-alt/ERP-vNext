using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Diten.AuthService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;

namespace Diten.AuthService.Application.Tests.Persistence;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AuthReconciliationMongoCollection : ICollectionFixture<DisposableAuthMongoReplicaSet>
{ public const string Name = "Owned Auth reconciliation replica"; }

/// <summary>No application URI is accepted. One child, fixed test DB, exact-row cleanup, no database drop.</summary>
public sealed class DisposableAuthMongoReplicaSet : IAsyncLifetime
{
    public const string DatabaseName = "diten_auth_entitlement_command_itest";
    private Process? _process;
    private string? _root;
    private DateTime _started;
    public int Port { get; private set; }
    public string ConnectionString { get; private set; } = "";
    public IMongoDatabase Database { get; private set; } = null!;
    public IMongoClient Client { get; private set; } = null!;
    public static readonly string[] Collections = ["users", "roles", "permissions", "userRoles", "rolePermissions",
        "refreshTokens", "tenant_user_memberships", "auth_role_assignment_versions", "authAuditLogs"];

    public async Task InitializeAsync()
    {
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        ConventionRegistry.Register("OwnedAuthReconciliation", new ConventionPack { new IgnoreExtraElementsConvention(true) }, _ => true);
        var executable = Environment.GetEnvironmentVariable("DITEN_TEST_MONGOD") ?? "C:/Program Files/MongoDB/Server/7.0/bin/mongod.exe";
        Assert.True(Path.IsPathFullyQualified(executable) && File.Exists(executable), "An installed mongod binary is required.");
        using (var listener = new TcpListener(IPAddress.Loopback, 0))
        { listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port; }
        Assert.InRange(Port, 27022, 65535); Assert.NotEqual(27017, Port);
        _root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "diten-auth-reconciliation-" + Guid.NewGuid().ToString("N")));
        var data = Path.Combine(_root, "data"); Directory.CreateDirectory(data);
        var start = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "--bind_ip", "127.0.0.1", "--port", Port.ToString(), "--dbpath", data,
            "--logpath", Path.Combine(_root, "mongod.log"), "--replSet", "auth-reconciliation-owned",
            "--setParameter", "enableTestCommands=1" }) start.ArgumentList.Add(argument);
        try
        {
            _process = Process.Start(start) ?? throw new InvalidOperationException("Owned mongod failed to start.");
            _started = _process.StartTime;
            _ = _process.StandardOutput.ReadToEndAsync(); _ = _process.StandardError.ReadToEndAsync();
            ConnectionString = $"mongodb://127.0.0.1:{Port}/?directConnection=true";
            var bootstrap = NewClient();
            await WaitAsync(async () => { await bootstrap.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1)); return true; });
            await bootstrap.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("replSetInitiate", new BsonDocument
            { { "_id", "auth-reconciliation-owned" }, { "members", new BsonArray { new BsonDocument { { "_id", 0 }, { "host", $"127.0.0.1:{Port}" } } } } }));
            ConnectionString += "&replicaSet=auth-reconciliation-owned"; Client = NewClient();
            await WaitAsync(async () => (await Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1)))
                .GetValue("isWritablePrimary", false).ToBoolean());
            Database = Client.GetDatabase(DatabaseName); await ResetAsync();
            using var session = await Client.StartSessionAsync();
            session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority));
            await Database.GetCollection<BsonDocument>("roles").Find(session, FilterDefinition<BsonDocument>.Empty).ToListAsync();
            await session.AbortTransactionAsync();
            Console.WriteLine($"OWNED_AUTH_MONGO_STARTED pid={_process.Id} port={Port} database={DatabaseName}");
        }
        catch { await DisposeAsync(); throw; }
    }

    public IMongoClient NewClient(MongoCommandProbe? probe = null, string? applicationName = null)
    {
        Assert.Contains($"127.0.0.1:{Port}", ConnectionString, StringComparison.Ordinal); Assert.NotEqual(27017, Port);
        var settings = MongoClientSettings.FromConnectionString(ConnectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        settings.ApplicationName = applicationName ?? EntitlementReconciliationOperationStore.ApplicationName;
        settings.RetryWrites = false; settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2); settings.ConnectTimeout = TimeSpan.FromSeconds(2);
        settings.ReadPreference = ReadPreference.Primary; settings.ReadConcern = ReadConcern.Majority; settings.WriteConcern = WriteConcern.WMajority;
        if (probe is not null) settings.ClusterConfigurator = builder =>
        { builder.Subscribe<CommandStartedEvent>(probe.Started); builder.Subscribe<CommandSucceededEvent>(probe.Succeeded); };
        return new MongoClient(settings);
    }

    public async Task ResetAsync()
    {
        Assert.NotNull(_process); Assert.False(_process.HasExited); Assert.Equal(_started, _process.StartTime);
        Assert.Equal(DatabaseName, Database.DatabaseNamespace.DatabaseName); Assert.NotEqual("DitenERP_Dev", DatabaseName);
        await Client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument { { "configureFailPoint", "failCommand" }, { "mode", "off" } });
        using var cursor = await Database.ListCollectionNamesAsync(); var names = await cursor.ToListAsync();
        Assert.All(names, name => Assert.Contains(name, Collections));
        foreach (var name in names)
        {
            var collection = Database.GetCollection<BsonDocument>(name);
            var ids = await collection.Find(FilterDefinition<BsonDocument>.Empty).Project(new BsonDocument("_id", 1)).ToListAsync();
            if (ids.Count != 0) await collection.DeleteManyAsync(new BsonDocument("_id", new BsonDocument("$in", new BsonArray(ids.Select(x => x["_id"])))));
            Assert.Equal(0, await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        }
        foreach (var name in Collections.Except(names)) await Database.CreateCollectionAsync(name);
        await Index("users", "Email_1_TenantId_1", new() { { "Email", 1 }, { "TenantId", 1 } }, true);
        await Index("roles", "uq_roles_tenant_name_active", new() { { "TenantId", 1 }, { "Name", 1 } }, true, new("IsDeleted", false));
        await Index("permissions", "Key_1", new("Key", 1), true);
        await Index("permissions", "Module_1_Resource_1_Action_1", new() { { "Module", 1 }, { "Resource", 1 }, { "Action", 1 } }, true);
        await Index("userRoles", "UserId_1_RoleId_1_TenantId_1", new() { { "UserId", 1 }, { "RoleId", 1 }, { "TenantId", 1 } }, true);
        await Index("userRoles", "UserId_1_TenantId_1", new() { { "UserId", 1 }, { "TenantId", 1 } });
        await Index("rolePermissions", "RoleId_1_PermissionId_1_TenantId_1", new() { { "RoleId", 1 }, { "PermissionId", 1 }, { "TenantId", 1 } }, true);
        await Index("rolePermissions", "RoleId_1_TenantId_1", new() { { "RoleId", 1 }, { "TenantId", 1 } });
        await Index("refreshTokens", "Token_1", new("Token", 1), true);
        await Index("refreshTokens", "UserId_1_TenantId_1", new() { { "UserId", 1 }, { "TenantId", 1 } });
        await Index("refreshTokens", "ExpiresAt_1", new("ExpiresAt", 1), ttl: TimeSpan.Zero);
        await Index("authAuditLogs", "TenantId_1_OccurredAt_1", new() { { "TenantId", 1 }, { "OccurredAt", 1 } });
        await Index("authAuditLogs", "EventName_1_OccurredAt_1", new() { { "EventName", 1 }, { "OccurredAt", 1 } });
    }
    private Task Index(string collection, string name, BsonDocument keys, bool unique = false, BsonDocument? partial = null, TimeSpan? ttl = null) =>
        Database.GetCollection<BsonDocument>(collection).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(keys,
            new CreateIndexOptions<BsonDocument> { Name = name, Unique = unique, PartialFilterExpression = partial, ExpireAfter = ttl }));

    public async Task DisposeAsync()
    {
        try { if (Database is not null && _process is { HasExited: false }) await ResetAsync(); }
        finally
        {
            if (_process is not null)
            {
                Assert.Equal(_started, _process.StartTime); var pid = _process.Id;
                if (!_process.HasExited) { _process.Kill(); await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); }
                Assert.True(_process.HasExited); _process.Dispose(); _process = null;
                Console.WriteLine($"OWNED_AUTH_MONGO_STOPPED pid={pid} port={Port}");
            }
            if (_root is not null && Directory.Exists(_root))
            {
                var resolved = Path.GetFullPath(_root);
                Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()) + "diten-auth-reconciliation-", resolved, StringComparison.OrdinalIgnoreCase);
                for (var attempt = 0; Directory.Exists(resolved); attempt++)
                {
                    try { Directory.Delete(resolved, true); }
                    catch (IOException) when (attempt < 20) { await Task.Delay(250); }
                }
                Assert.False(Directory.Exists(resolved));
            }
            if (Port != 0) { using var listener = new TcpListener(IPAddress.Loopback, Port); listener.Start(); }
        }
    }
    private async Task WaitAsync(Func<Task<bool>> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(35);
        while (DateTime.UtcNow < deadline)
        {
            Assert.NotNull(_process); Assert.False(_process.HasExited);
            try { if (await predicate()) return; } catch (Exception ex) when (ex is MongoException or TimeoutException) { }
            await Task.Delay(150);
        }
        throw new TimeoutException("Owned Mongo readiness timed out.");
    }
}

public sealed class MongoCommandProbe
{
    private readonly ConcurrentDictionary<int, BsonDocument> _requests = new();
    public ConcurrentQueue<(string Name, BsonDocument Command)> Commands { get; } = new();
    public ConcurrentQueue<(string Name, BsonDocument Reply)> Replies { get; } = new();
    public Action<string, BsonDocument>? Before { get; set; }
    public Action<string, BsonDocument>? After { get; set; }
    public void Started(CommandStartedEvent value)
    {
        var command = value.Command.DeepClone().AsBsonDocument; _requests[value.RequestId] = command;
        Commands.Enqueue((value.CommandName, command)); Before?.Invoke(value.CommandName, command);
    }
    public void Succeeded(CommandSucceededEvent value)
    {
        Replies.Enqueue((value.CommandName, value.Reply.DeepClone().AsBsonDocument));
        if (_requests.TryRemove(value.RequestId, out var command)) After?.Invoke(value.CommandName, command);
    }
    public IEnumerable<(string Name, BsonDocument Command)> Writes => Commands.Where(x =>
        new[] { "insert", "update", "delete", "findAndModify", "create", "createIndexes", "drop", "dropDatabase", "collMod" }.Contains(x.Name));
}
