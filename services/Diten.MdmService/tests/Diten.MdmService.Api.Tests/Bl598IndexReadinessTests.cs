using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Application.Features.ProductMaster;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
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

        // Mongo is back (the same database name, so the same preparation key): the failure was not cached.
        var back = new ProductLegalEntityScopePolicyRepository(Back(name), new Tenant(tenant));
        var written = await back.CreateAsync(Policy(tenant, Guid.NewGuid()));

        Assert.True(written.Succeeded, "the write after Mongo came back");
        var indexes = await (await Back(name).GetCollection<BsonDocument>(ProductLegalEntityScopePolicyRepository.CollectionName)
            .Indexes.ListAsync()).ToListAsync();
        Assert.Contains(indexes, index => index["name"] == "ux_mdm_product_legal_entity_scope_policies_tenant_product");
    }

    // An index dropped under a running process (an operator, a restore) is not "ready" for ever: the first write after
    // the revalidation window builds it again — the write never runs for long without its UNIQUE index.
    [Fact]
    public async Task Bl598_A_dropped_unique_index_is_built_again_by_the_first_write_after_the_revalidation_window()
    {
        var name = FreshDatabase();
        var tenant = Guid.NewGuid();
        Assert.True((await new ProductLegalEntityScopePolicyRepository(Back(name), new Tenant(tenant))
            .CreateAsync(Policy(tenant, Guid.NewGuid()))).Succeeded);
        var collection = Back(name).GetCollection<BsonDocument>(ProductLegalEntityScopePolicyRepository.CollectionName);
        await collection.Indexes.DropOneAsync("ux_mdm_product_legal_entity_scope_policies_tenant_product");

        var previous = MongoIndexReadiness.Revalidate;
        MongoIndexReadiness.Revalidate = TimeSpan.Zero;
        try
        {
            Assert.True((await new ProductLegalEntityScopePolicyRepository(Back(name), new Tenant(tenant))
                .CreateAsync(Policy(tenant, Guid.NewGuid()))).Succeeded);
        }
        finally
        {
            MongoIndexReadiness.Revalidate = previous;
        }

        var indexes = await (await collection.Indexes.ListAsync()).ToListAsync();
        Assert.Contains(indexes, index => index["name"] == "ux_mdm_product_legal_entity_scope_policies_tenant_product");
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
