using Diten.MdmService.Application.Features.Approvals;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Infrastructure;
using Diten.MdmService.Persistence.Repositories;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests.Approvals;

/// <summary>
/// WP-VW-W4 (W4-BE-b1) — the infrastructure half: the explicit-tenant CAS store and the inbox against a REAL Mongo (the
/// other MDM Mongo tests' runner, <c>MDM_TEST_MONGO</c> or localhost:27017, one throw-away database per test), the
/// configuration, and the start-up with no RabbitMQ (InMemory: no bus at all; RabbitMQ on a closed port: the bus connects in
/// the background and the host still starts).
/// </summary>
public sealed class WorkflowApprovalInfrastructureTests
{
    private static readonly Guid Tenant = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly Guid OtherTenant = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public async Task The_mongo_store_loads_with_the_given_tenant_only_and_replaces_only_on_the_expected_version()
    {
        await using var scope = await MongoScope.CreateAsync();
        var collection = scope.Database.GetCollection<TestApprovable>("mdm_test_approvables");
        var record = new TestApprovable { TenantId = Tenant, CreatedBy = "u1", Version = 2 };
        await collection.InsertOneAsync(record);
        var store = new MongoWorkflowApprovalStore<TestApprovable>(scope.Database, "mdm_test_approvables");

        Assert.Null(await store.GetAsync(OtherTenant, record.Id, default));
        Assert.Null(await store.GetAsync(Guid.Empty, record.Id, default));
        var first = (await store.GetAsync(Tenant, record.Id, default))!;
        var second = (await store.GetAsync(Tenant, record.Id, default))!;

        first.Status = TestApprovableStatuses.InReview;
        first.ReviewRounds.Add(new WorkflowApprovalRound { WorkflowInstanceId = Guid.NewGuid(), RoundNo = 1, SubmittedAt = DateTimeOffset.UtcNow });
        Assert.True(await store.ReplaceAsync(first, first.Version, default));
        Assert.Equal(3, first.Version);

        second.Status = "stale";
        Assert.False(await store.ReplaceAsync(second, second.Version, default)); // the CAS: someone else wrote version 3
        Assert.Equal(2, second.Version);

        var foreign = (await store.GetAsync(Tenant, record.Id, default))!;
        foreign.TenantId = OtherTenant;
        Assert.False(await store.ReplaceAsync(foreign, foreign.Version, default)); // never writes across tenants

        var stored = await collection.Find(x => x.Id == record.Id).SingleAsync();
        Assert.Equal((TestApprovableStatuses.InReview, 3, Tenant), (stored.Status, stored.Version, stored.TenantId));
        Assert.Equal(1, Assert.Single(stored.ReviewRounds).RoundNo);
    }

    [Fact]
    public async Task The_flow_answers_409_when_the_mongo_record_moved_under_it()
    {
        await using var scope = await MongoScope.CreateAsync();
        var collection = scope.Database.GetCollection<TestApprovable>("mdm_test_approvables");
        var record = new TestApprovable { TenantId = Tenant, CreatedBy = "u1", Version = 0 };
        await collection.InsertOneAsync(record);
        var store = new MongoWorkflowApprovalStore<TestApprovable>(scope.Database, "mdm_test_approvables");
        var stale = (await store.GetAsync(Tenant, record.Id, default))!;
        stale.Version = 7; // the caller's copy is behind: every replace (and the reload retry) loses

        // A store whose reload also returns a stale copy: the flow gives up after its one retry with 409.
        var staleStore = new StaleStore(store);
        var flow = new WorkflowApprovalFlow(new FakeWorkflowClient(), new FixedActor("u2"),
            new WorkflowApprovalOutcomeApplier([TestApprovableKind.Target(store)]));
        var result = await flow.SubmitAsync<TestApprovable, bool>(staleStore, stale, TestApprovableKind.Kind, "T",
            new WorkflowDisplayContext("t", null, "MOD-0290", "/x", []), TestApprovableStatuses.InReview, default);

        Assert.Equal((409, WorkflowApprovalErrors.ConcurrencyConflict), (result!.StatusCode, result.Errors[0]));
        Assert.Equal(0, (await collection.Find(x => x.Id == record.Id).SingleAsync()).Version);
    }

    [Fact]
    public async Task The_mongo_inbox_accepts_an_event_id_once()
    {
        await using var scope = await MongoScope.CreateAsync();
        var inbox = new MdmEventInboxRepository(scope.Database);
        var eventId = Guid.NewGuid();
        Assert.True(await inbox.TryInsertAsync(eventId, "platform.workflow.instance.completed.v1", Tenant));
        Assert.False(await inbox.TryInsertAsync(eventId, "platform.workflow.instance.completed.v1", Tenant));
        Assert.True(await inbox.TryInsertAsync(Guid.NewGuid(), "platform.workflow.instance.completed.v1", Tenant));
        Assert.Equal("mdm_event_inbox", MdmEventInboxRepository.CollectionName);
    }

    // ── configuration + start-up without RabbitMQ ──────────────────────────────────────────────────────

    [Fact]
    public void Without_RabbitMQ_no_bus_is_registered_and_the_approval_services_resolve()
    {
        using var provider = Services(new Dictionary<string, string?>()).BuildServiceProvider();
        Assert.Null(provider.GetService<IBusControl>());
        Assert.DoesNotContain(provider.GetServices<IHostedService>(), s => s.GetType().FullName!.Contains("MassTransit"));

        using var scope = provider.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IMdmApprovalSettings>();
        Assert.Equal("MDM-COMP-PROPOSAL", settings.CompetitorProposalTemplateCode);
        Assert.Equal(120, settings.ReconcileAfterSeconds);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IMdmWorkflowClient>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<WorkflowApprovalFlow>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<WorkflowApprovalReconciler>());
        Assert.False(scope.ServiceProvider.GetRequiredService<WorkflowApprovalOutcomeApplier>().CanApply("mdm.competitor-proposal")); // b2 registers it
    }

    [Fact]
    public async Task With_RabbitMQ_configured_but_down_the_host_still_starts_and_the_consumer_is_registered()
    {
        await using var provider = Services(new Dictionary<string, string?>
        {
            ["Eventing:Transport"] = "RabbitMQ",
            ["Eventing:Host"] = "127.0.0.1",
            ["Eventing:Port"] = "1", // nothing listens here
            ["Mdm:CompetitorProposals:Workflow:TemplateCode"] = "MDM-COMP-PROPOSAL-X"
        }).BuildServiceProvider();

        Assert.NotNull(provider.GetService<IBusControl>());
        var hosted = provider.GetServices<IHostedService>().ToList();
        Assert.Contains(hosted, s => s.GetType().FullName!.Contains("MassTransit"));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var started = System.Diagnostics.Stopwatch.StartNew();
        foreach (var service in hosted)
        {
            await service.StartAsync(timeout.Token); // must not throw or wait for the broker
        }

        Assert.True(started.Elapsed < TimeSpan.FromSeconds(20));
        Assert.Equal("MDM-COMP-PROPOSAL-X", provider.GetRequiredService<IMdmApprovalSettings>().CompetitorProposalTemplateCode);
        foreach (var service in hosted)
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await service.StopAsync(stop.Token); } catch (OperationCanceledException) { }
        }
    }

    [Fact]
    public void The_shipped_settings_keep_the_service_up_without_a_broker_and_name_the_competitor_template()
    {
        var api = Path.Combine(RepoRoot(), "services", "Diten.MdmService", "src", "Diten.MdmService.Api");
        var production = new ConfigurationBuilder().AddJsonFile(Path.Combine(api, "appsettings.json")).Build();
        Assert.Equal("InMemory", production["Eventing:Transport"]);
        Assert.Equal("MDM-COMP-PROPOSAL", production["Mdm:CompetitorProposals:Workflow:TemplateCode"]);
        var development = new ConfigurationBuilder().AddJsonFile(Path.Combine(api, "appsettings.Development.json")).Build();
        Assert.Equal("RabbitMQ", development["Eventing:Transport"]); // the CRM's local broker
        Assert.Equal("localhost", development["Eventing:Host"]);
        Assert.Equal("5672", development["Eventing:Port"]);
        Assert.Equal("http://localhost:5000", development["Gateway:BaseUrl"]);
    }

    private static IServiceCollection Services(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        services.AddScoped<IMdmEventInboxRepository, InMemoryInbox>();
        return services;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "services", "Diten.MdmService"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("repo root");
    }

    private sealed class StaleStore(IWorkflowApprovalStore<TestApprovable> inner) : IWorkflowApprovalStore<TestApprovable>
    {
        public async Task<TestApprovable?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
        {
            var record = await inner.GetAsync(tenantId, id, ct);
            if (record is not null) record.Version = 7;
            return record;
        }

        public Task<bool> ReplaceAsync(TestApprovable record, int expectedVersion, CancellationToken ct) => inner.ReplaceAsync(record, expectedVersion, ct);
    }

    private sealed class MongoScope : IAsyncDisposable
    {
        private readonly IMongoClient _client;
        private readonly string _name;

        private MongoScope(IMongoClient client, string name)
        {
            _client = client;
            _name = name;
            Database = client.GetDatabase(name);
        }

        public IMongoDatabase Database { get; }

        public static async Task<MongoScope> CreateAsync()
        {
            var settings = MongoClientSettings.FromConnectionString(Environment.GetEnvironmentVariable("MDM_TEST_MONGO") ?? "mongodb://localhost:27017");
#pragma warning disable CS0618
            settings.GuidRepresentation = MongoDB.Bson.GuidRepresentation.Standard;
#pragma warning restore CS0618
            var client = new MongoClient(settings);
            var scope = new MongoScope(client, $"diten_mdm_approval_tests_{Guid.NewGuid():N}");
            await scope.Database.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1));
            return scope;
        }

        public async ValueTask DisposeAsync() => await _client.DropDatabaseAsync(_name);
    }
}
