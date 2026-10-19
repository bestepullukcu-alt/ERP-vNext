using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductMaster;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-BL598-01 — the scope policy store prepares its UNIQUE indexes lazily, once per process: the constructor never
/// goes to Mongo; a write waits for the indexes and is refused (nothing written) when they cannot be built; a failed
/// preparation is not kept; and two racing writes before the indexes exist still leave ONE policy per product.
/// Mongo "away" is a closed loopback port with a short server-selection timeout — never the shared server; Mongo "back"
/// is this host's own throwaway replica-set mongod.
/// </summary>
[Collection(MdmHttpCollection.Name)]
public sealed class Bl598IndexReadinessTests(MdmHttpHost host)
{
    // A loopback port nothing listens on (bound, read, released — the OS will not hand it out again at once).
    private static int ClosedPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static IMongoDatabase Away(string databaseName)
    {
        var settings = MongoClientSettings.FromConnectionString(
            $"mongodb://127.0.0.1:{ClosedPort()}/?directConnection=true");
        settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(400);
        settings.ConnectTimeout = TimeSpan.FromMilliseconds(400);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        return new MongoClient(settings).GetDatabase(databaseName);
    }

    private IMongoDatabase Back(string databaseName)
    {
        var settings = MongoClientSettings.FromConnectionString(host.MongoConnectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        return new MongoClient(settings).GetDatabase(databaseName);
    }

    private static string FreshDatabase() => "bl598_" + Guid.NewGuid().ToString("N")[..12];

    [Fact]
    public void Bl598_The_policy_store_constructor_does_not_go_to_Mongo()
    {
        var database = Away(FreshDatabase());
        var key = MongoIndexReadiness.KeyOf(database.GetCollection<ProductLegalEntityScopePolicy>(
            ProductLegalEntityScopePolicyRepository.CollectionName), ProductLegalEntityScopePolicyRepository.IndexSet);
        var clock = Stopwatch.StartNew();

        _ = new ProductLegalEntityScopePolicyRepository(database, new Tenant(Guid.NewGuid()));

        // No index build started, and no wait for a server that is not there (its timeout is 400 ms).
        Assert.False(MongoIndexReadiness.StartedForTests.ContainsKey(key));
        Assert.True(clock.ElapsedMilliseconds < 300, $"the constructor took {clock.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task Bl598_A_write_while_Mongo_is_away_is_refused_as_not_ready_and_nothing_is_written()
    {
        var tenant = Guid.NewGuid();
        var repository = new ProductLegalEntityScopePolicyRepository(Away(FreshDatabase()), new Tenant(tenant))
        {
            WriteIndexBudget = TimeSpan.FromSeconds(5)
        };

        var result = await repository.CreateAsync(Policy(tenant, Guid.NewGuid()));

        Assert.False(result.Succeeded);
        Assert.True(result.IndexesNotReady);
        Assert.True(result.VerifiedZeroMutation);
    }

    [Fact]
    public async Task Bl598_A_failed_preparation_is_not_kept_and_the_next_write_in_the_same_process_succeeds()
    {
        var name = FreshDatabase();
        var tenant = Guid.NewGuid();
        var away = new ProductLegalEntityScopePolicyRepository(Away(name), new Tenant(tenant))
        {
            WriteIndexBudget = TimeSpan.FromSeconds(5)
        };
        Assert.True((await away.CreateAsync(Policy(tenant, Guid.NewGuid()))).IndexesNotReady);

        // Mongo is back (the same database name, so the same preparation key): the failure was not cached — past its
        // cool-down (BL-598 FIX2: zero here; the cool-down itself has its own test).
        var back = new ProductLegalEntityScopePolicyRepository(Back(name), new Tenant(tenant));
        var cooldown = MongoIndexReadiness.FailureCooldown;
        MongoIndexReadiness.FailureCooldown = TimeSpan.Zero;
        ProductLegalEntityScopePolicyWriteResult written;
        try { written = await back.CreateAsync(Policy(tenant, Guid.NewGuid())); }
        finally { MongoIndexReadiness.FailureCooldown = cooldown; }

        Assert.True(written.Succeeded, "the write after Mongo came back");
        var indexes = await (await Back(name).GetCollection<BsonDocument>(ProductLegalEntityScopePolicyRepository.CollectionName)
            .Indexes.ListAsync()).ToListAsync();
        Assert.Contains(indexes, index => index["name"] == "ux_mdm_product_legal_entity_scope_policies_tenant_product");
    }

    // BL-598 FIX2 (weak test made real): End and reads keep the ten-minute window — inside it they do not re-assert the
    // indexes (an update cannot insert, a read only wants speed); past it (here: a zero window) End builds a dropped
    // unique index again. (Create re-asserts on every call — its own test below.)
    [Fact]
    public async Task Bl598_End_and_reads_keep_the_window_and_End_rebuilds_a_dropped_index_once_it_has_passed()
    {
        var name = FreshDatabase();
        var tenant = Guid.NewGuid();
        var repository = new ProductLegalEntityScopePolicyRepository(Back(name), new Tenant(tenant));
        var first = Policy(tenant, Guid.NewGuid());
        var second = Policy(tenant, Guid.NewGuid());
        Assert.True((await repository.CreateAsync(first)).Succeeded);
        Assert.True((await repository.CreateAsync(second)).Succeeded);
        var collection = Back(name).GetCollection<BsonDocument>(ProductLegalEntityScopePolicyRepository.CollectionName);
        var key = MongoIndexReadiness.KeyOf(Back(name).GetCollection<ProductLegalEntityScopePolicy>(
            ProductLegalEntityScopePolicyRepository.CollectionName), ProductLegalEntityScopePolicyRepository.IndexSet);
        await collection.Indexes.DropOneAsync("ux_mdm_product_legal_entity_scope_policies_tenant_product");
        var started = MongoIndexReadiness.StartedForTests[key];

        Assert.NotNull(await repository.GetByGlobalProductIdAsync(first.GlobalProductId));
        End(first);
        Assert.True((await repository.UpdateAsync(first, 0)).Succeeded);
        Assert.Equal(started, MongoIndexReadiness.StartedForTests[key]);
        Assert.DoesNotContain(await (await collection.Indexes.ListAsync()).ToListAsync(),
            index => index["name"] == "ux_mdm_product_legal_entity_scope_policies_tenant_product");

        var window = MongoIndexReadiness.Revalidate;
        MongoIndexReadiness.Revalidate = TimeSpan.Zero;
        try
        {
            End(second);
            Assert.True((await repository.UpdateAsync(second, 0)).Succeeded);
        }
        finally
        {
            MongoIndexReadiness.Revalidate = window;
        }
        Assert.Contains(await (await collection.Indexes.ListAsync()).ToListAsync(),
            index => index["name"] == "ux_mdm_product_legal_entity_scope_policies_tenant_product");
    }

    // BL-598 FIX2 item 4: "made during this call" is a sequence number, not the clock — two Creates one after the other
    // (inside one clock tick or not) each re-assert the indexes.
    [Fact]
    public async Task Bl598_Fix2_Every_create_re_asserts_the_indexes_even_one_right_after_another()
    {
        var name = FreshDatabase();
        var tenant = Guid.NewGuid();
        var repository = new ProductLegalEntityScopePolicyRepository(Back(name), new Tenant(tenant));
        var key = MongoIndexReadiness.KeyOf(Back(name).GetCollection<ProductLegalEntityScopePolicy>(
            ProductLegalEntityScopePolicyRepository.CollectionName), ProductLegalEntityScopePolicyRepository.IndexSet);

        for (var create = 0; create < 3; create++)
            Assert.True((await repository.CreateAsync(Policy(tenant, Guid.NewGuid()))).Succeeded);

        Assert.Equal(3, MongoIndexReadiness.StartedForTests[key]);
    }

    // BL-598 FIX2 (minor): a failed preparation answers the writes of its cool-down at once — one preparation, ONE log
    // line — and is tried again after it.
    [Fact]
    public async Task Bl598_Fix2_A_failure_is_tried_and_said_once_per_cool_down()
    {
        var key = "bl598-fix2/" + Guid.NewGuid().ToString("N");
        var logs = new Recording();
        var prepared = 0;
        Task Failing(CancellationToken _) { Interlocked.Increment(ref prepared); throw new InvalidOperationException("index option conflict"); }

        for (var write = 0; write < 3; write++)
            await Assert.ThrowsAsync<MongoIndexNotReadyException>(() => MongoIndexReadiness.EnsureForWriteAsync(key, Failing,
                TimeSpan.FromSeconds(5), CancellationToken.None, logger: logs));

        Assert.Equal(1, prepared);
        Assert.Single(logs.Entries);
        var cooldown = MongoIndexReadiness.FailureCooldown;
        MongoIndexReadiness.FailureCooldown = TimeSpan.Zero;
        try
        {
            await Assert.ThrowsAsync<MongoIndexNotReadyException>(() => MongoIndexReadiness.EnsureForWriteAsync(key, Failing,
                TimeSpan.FromSeconds(5), CancellationToken.None, logger: logs));
        }
        finally
        {
            MongoIndexReadiness.FailureCooldown = cooldown;
        }
        Assert.Equal(2, prepared);
        Assert.Equal(2, logs.Entries.Count);
    }

    // BL-598 FIX2 item 2: a preparation a READ started says its own failure — no write needs to follow.
    [Fact]
    public async Task Bl598_Fix2_A_failure_of_a_preparation_a_read_started_is_logged()
    {
        var logs = new Recording();
        var repository = new ProductLegalEntityScopePolicyRepository(Away(FreshDatabase()), new Tenant(Guid.NewGuid()), logs);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.GetByGlobalProductIdAsync(Guid.NewGuid()));

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (logs.Entries.Count == 0 && DateTime.UtcNow < deadline) await Task.Delay(50);
        var line = Assert.Single(logs.Entries);
        Assert.Contains(MongoIndexReadiness.NotReady, line.Message);
    }

    // BL-598 FIX2 (minor): a refusal for good (credential / authorization) is its own named failure — not "getting
    // ready" (the write fails as a configuration defect, 500) — and an empty server code name is said as "none".
    [Fact]
    public async Task Bl598_Fix2_A_refused_credential_is_a_named_failure_not_getting_ready()
    {
        var endPoint = new System.Net.DnsEndPoint("127.0.0.1", 1);
        var connection = new MongoDB.Driver.Core.Connections.ConnectionId(
            new MongoDB.Driver.Core.Servers.ServerId(new MongoDB.Driver.Core.Clusters.ClusterId(), endPoint));
        var logs = new Recording();

        var refused = await Assert.ThrowsAsync<MongoIndexPreparationRefusedException>(() => MongoIndexReadiness.EnsureForWriteAsync(
            "bl598-fix2/" + Guid.NewGuid().ToString("N"),
            _ => throw new MongoAuthenticationException(connection, "bad credentials"),
            TimeSpan.FromSeconds(5), CancellationToken.None, logger: logs));
        Assert.Equal(MongoIndexReadiness.PreparationRefused, refused.Message);
        Assert.Contains(MongoIndexReadiness.PreparationRefused, Assert.Single(logs.Entries).Message);

        var unnamed = new Recording();
        await Assert.ThrowsAsync<MongoIndexNotReadyException>(() => MongoIndexReadiness.EnsureForWriteAsync(
            "bl598-fix2/" + Guid.NewGuid().ToString("N"),
            _ => throw new MongoCommandException(connection, "failed", new BsonDocument("createIndexes", "x"),
                new BsonDocument { { "ok", 0 }, { "code", 85 }, { "codeName", "" } }),
            TimeSpan.FromSeconds(5), CancellationToken.None, logger: unnamed));
        Assert.Contains("server code none", Assert.Single(unnamed.Entries).Message);
    }

    // BL-598 FIX2 item 3: the logger reaches the store through DI — both public constructors. The container picks the
    // four-parameter one (the guarded session is registered); with only a logger in the container, the three-parameter
    // one takes ILogger<ProductLegalEntityScopePolicyRepository> from it.
    [Fact]
    public async Task Bl598_Fix2_The_store_built_by_the_container_logs_a_failed_preparation()
    {
        var away = Away(FreshDatabase());
        await using var variant = host.Variant(services =>
        {
            services.RemoveAll<IMongoClient>();
            services.AddSingleton(away.Client);
            services.RemoveAll<IMongoDatabase>();
            services.AddScoped(_ => away);
        });
        var before = host.Logs.Entries.Count;
        using (var scope = variant.Services.CreateScope())
        {
            var tenant = Guid.NewGuid();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenant);
            var store = scope.ServiceProvider.GetRequiredService<IProductLegalEntityScopePolicyRepository>();
            Assert.True((await store.CreateAsync(Policy(tenant, Guid.NewGuid()))).IndexesNotReady);
        }
        Assert.Contains(host.Logs.Entries.Skip(before), entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Error
            && entry.Category.EndsWith(nameof(ProductLegalEntityScopePolicyRepository), StringComparison.Ordinal)
            && entry.Message.Contains(MongoIndexReadiness.NotReady, StringComparison.Ordinal));

        var sink = new LogSink();
        var services = new ServiceCollection().AddLogging().AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(sink)
            .BuildServiceProvider();
        var tenant3 = Guid.NewGuid();
        var threeParameter = ActivatorUtilities.CreateInstance<ProductLegalEntityScopePolicyRepository>(services,
            Away(FreshDatabase()), new Tenant(tenant3));
        Assert.True((await threeParameter.CreateAsync(Policy(tenant3, Guid.NewGuid()))).IndexesNotReady);
        Assert.Contains(sink.Entries, entry => entry.Level == Microsoft.Extensions.Logging.LogLevel.Error
            && entry.Message.Contains(MongoIndexReadiness.NotReady, StringComparison.Ordinal));
    }

    // The UNIQUE index is what keeps one policy per product: writes racing BEFORE it exists all wait for it, so the
    // store can never hold two policies of one product (one is written, the others are refused).
    [Fact]
    public async Task Bl598_Racing_writes_before_the_unique_index_exists_leave_one_policy_per_product()
    {
        var name = FreshDatabase();
        var tenant = Guid.NewGuid();
        var product = Guid.NewGuid();
        var racers = Enumerable.Range(0, 16).Select(_ =>
        {
            var repository = new ProductLegalEntityScopePolicyRepository(Back(name), new Tenant(tenant));
            return (repository, policy: Policy(tenant, product));
        }).ToArray();

        var results = await Task.WhenAll(racers.Select(racer => racer.repository.CreateAsync(racer.policy)));

        Assert.Equal(1, results.Count(result => result.Succeeded));
        var stored = await Back(name).GetCollection<BsonDocument>(ProductLegalEntityScopePolicyRepository.CollectionName)
            .CountDocumentsAsync(new BsonDocument("GlobalProductId", new BsonBinaryData(product, GuidRepresentation.Standard)));
        Assert.Equal(1, stored);
    }

    // Over HTTP, the whole host with Mongo away (the measured BEFORE: both answered an uncoded 500, the request dying in
    // DI resolution): the S4 read answers the contract's 503 PRODUCT_MASTER_UNAVAILABLE; a scope-policy mutation is refused
    // by its write fence with the fence's coded 503 before its handler is resolved.
    [Fact]
    public async Task Bl598_With_Mongo_away_the_S4_read_and_a_policy_write_answer_a_coded_503_not_a_500()
    {
        var away = Away(MdmHttpHost.DatabaseName);
        await using var variant = host.Variant(services =>
        {
            services.RemoveAll<IMongoClient>();
            services.AddSingleton(away.Client);
            services.RemoveAll<IMongoDatabase>();
            services.AddScoped(_ => away);
        });
        // The variant really took the unreachable client (never the shared server).
        Assert.Equal(away.Client.Settings.Server.Port,
            variant.Services.GetRequiredService<IMongoClient>().Settings.Servers.Single().Port);
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();

        var reader = Client(variant, host.Token(tenant, user, ProductMasterContract.ReadPermission));
        var read = await reader.GetAsync($"/api/product-master/items/{Guid.NewGuid():D}");
        var readBody = await read.Content.ReadAsStringAsync();
        Assert.True(503 == (int)read.StatusCode, $"read: {(int)read.StatusCode} {readBody}");
        using (var document = JsonDocument.Parse(readBody))
            Assert.Equal(ProductMasterContract.Unavailable,
                document.RootElement.GetProperty("error").GetProperty("code").GetString());

        var writer = Client(variant, host.Token(tenant, user, "mdm.product-legal-entity-scopes.configure"));
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"/api/global-products/{Guid.NewGuid():D}/legal-entity-scope-policy")
        {
            Content = Envelope.Json(new { mode = (int)ProductLegalEntityScopeMode.GroupWide, legalEntityIds = Array.Empty<Guid>() })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        var write = await writer.SendAsync(request);
        var writeBody = await write.Content.ReadAsStringAsync();
        Assert.True(503 == (int)write.StatusCode, $"write: {(int)write.StatusCode} {writeBody}");
        Assert.Equal(ProductLegalEntityScopeWriteFenceCoordinator.RolloutStoreUnavailableCode,
            Envelope.FirstError(await Envelope.ReadAsync(write)));
    }

    private static HttpClient Client(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Diten.MdmService.Api.Controllers.GskusController> factory,
        string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ================================================================================== BL-598 FIX1

    // D1: the read / End re-assertion window is ten minutes by default (Create re-asserts on every call).
    [Fact]
    public void Bl598_Fix1_The_default_revalidation_window_is_ten_minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(10), MongoIndexReadiness.Revalidate);
    }

    // Item 1 (CT decision): a unique index dropped under the running process is built again by the very next Create —
    // with the DEFAULT window — so two racing Creates of one product leave ONE policy.
    [Fact]
    public async Task Bl598_Fix1_After_a_dropped_unique_index_the_next_creates_rebuild_it_and_leave_one_policy_per_product()
    {
        var name = FreshDatabase();
        var tenant = Guid.NewGuid();
        Assert.True((await new ProductLegalEntityScopePolicyRepository(Back(name), new Tenant(tenant))
            .CreateAsync(Policy(tenant, Guid.NewGuid()))).Succeeded);
        var collection = Back(name).GetCollection<BsonDocument>(ProductLegalEntityScopePolicyRepository.CollectionName);
        await collection.Indexes.DropOneAsync("ux_mdm_product_legal_entity_scope_policies_tenant_product");

        var product = Guid.NewGuid();
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            new ProductLegalEntityScopePolicyRepository(Back(name), new Tenant(tenant)).CreateAsync(Policy(tenant, product))));

        Assert.Equal(1, results.Count(result => result.Succeeded));
        Assert.Equal(1, await collection.CountDocumentsAsync(
            new BsonDocument("GlobalProductId", new BsonBinaryData(product, GuidRepresentation.Standard))));
        var indexes = await (await collection.Indexes.ListAsync()).ToListAsync();
        Assert.Contains(indexes, index => index["name"] == "ux_mdm_product_legal_entity_scope_policies_tenant_product");
    }

    // Item 2: a failed preparation is said once (collection + failure type + code, no tenant datum) — not silently a 503.
    [Fact]
    public async Task Bl598_Fix1_A_failed_preparation_is_logged_once_per_failure()
    {
        var logs = new Recording();
        var tenant = Guid.NewGuid();
        var repository = new ProductLegalEntityScopePolicyRepository(Away(FreshDatabase()), new Tenant(tenant), logs)
        {
            WriteIndexBudget = TimeSpan.FromSeconds(5)
        };

        Assert.True((await repository.CreateAsync(Policy(tenant, Guid.NewGuid()))).IndexesNotReady);

        var line = Assert.Single(logs.Entries);
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Error, line.Level);
        Assert.Contains(MongoIndexReadiness.NotReady, line.Message);
        Assert.Contains(ProductLegalEntityScopePolicyRepository.CollectionName, line.Message);
        Assert.Contains(nameof(TimeoutException), line.Message);
        Assert.DoesNotContain(tenant.ToString("D"), line.Message);
    }

    // D2: End (an update) waits for the indexes too — refused as not ready while Mongo is away, nothing written.
    [Fact]
    public async Task Bl598_Fix1_An_update_while_Mongo_is_away_is_refused_as_not_ready()
    {
        var tenant = Guid.NewGuid();
        var repository = new ProductLegalEntityScopePolicyRepository(Away(FreshDatabase()), new Tenant(tenant))
        {
            WriteIndexBudget = TimeSpan.FromSeconds(5)
        };
        var policy = Policy(tenant, Guid.NewGuid());
        policy.Id = Guid.NewGuid();
        policy.EndCurrent(0, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = await repository.UpdateAsync(policy, 0);

        Assert.True(result.IndexesNotReady);
        Assert.True(result.VerifiedZeroMutation);
    }

    // D5: a read starts the preparation (without waiting) — the first write then finds it under way or done.
    [Fact]
    public async Task Bl598_Fix1_A_read_starts_the_preparation()
    {
        var database = Back(FreshDatabase());
        var repository = new ProductLegalEntityScopePolicyRepository(database, new Tenant(Guid.NewGuid()));
        var key = MongoIndexReadiness.KeyOf(database.GetCollection<ProductLegalEntityScopePolicy>(
            ProductLegalEntityScopePolicyRepository.CollectionName), ProductLegalEntityScopePolicyRepository.IndexSet);

        Assert.Null(await repository.GetByGlobalProductIdAsync(Guid.NewGuid()));

        Assert.True(MongoIndexReadiness.StartedForTests.ContainsKey(key));
    }

    private sealed class Recording : Microsoft.Extensions.Logging.ILogger<ProductLegalEntityScopePolicyRepository>
    {
        public List<(Microsoft.Extensions.Logging.LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Entries) Entries.Add((logLevel, formatter(state, exception)));
        }
    }

    // The policy ended as the End handler ends it (one end audit intent, version + 1).
    private static void End(ProductLegalEntityScopePolicy policy)
    {
        var now = DateTimeOffset.UtcNow;
        var command = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var before = policy.GetEffectivePeriod(now);
        policy.EndCurrent(0, command, actor, now);
        policy.AuditIntents.Add(Diten.MdmService.Application.Features.ProductLegalEntityScopes
            .ProductLegalEntityScopeAuditIntentFactory.Create(policy, ProductAuditOperation.ProductLegalEntityScopePolicyEnded,
                0, policy.Version, command, actor, now, before, after: null));
    }

    // A new policy as the create handler builds it (one creation audit intent), for the store's own tenant.
    private static ProductLegalEntityScopePolicy Policy(Guid tenant, Guid product)
    {
        var now = DateTimeOffset.UtcNow;
        var command = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var policy = ProductLegalEntityScopePolicy.Create(tenant, product, command,
            ProductLegalEntityScopeMode.GroupWide, [], actor, now);
        policy.AuditIntents.Add(Diten.MdmService.Application.Features.ProductLegalEntityScopes
            .ProductLegalEntityScopeAuditIntentFactory.Create(policy, ProductAuditOperation.ProductLegalEntityScopePolicyCreated,
                -1, 0, command, actor, now, before: null, after: policy.GetEffectivePeriod(now)));
        return policy;
    }

    private sealed class Tenant(Guid id) : ITenantContext
    {
        public Guid TenantId { get; private set; } = id;
        public bool IsResolved => TenantId != Guid.Empty;
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }
}
