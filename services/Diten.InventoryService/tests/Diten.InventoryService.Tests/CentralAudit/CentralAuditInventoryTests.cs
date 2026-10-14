using System.Net;
using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.CentralAudit;

/*
 * BL-501 — INVENTORY'S COMMANDS LAND IN THE CENTRAL AUDIT OUTBOX, IN THEIR OWN TRANSACTION.
 *
 * The real service (Program.cs: AddCentralAudit + RequireEnlistment, the real MediatR pipeline, the real ledger
 * repository on the suite's replica set) — what is read back is the shared package's outbox collection in Inventory's
 * own database:
 *   a posted movement            → exactly one event: caller's tenant / company / user, InventoryTransaction + its id
 *   the same key again (replay)  → still one (a replay wrote nothing)
 *   insufficient stock (422)     → none: the ledger transaction that enlisted it was ABORTED and took the row with it
 *   a 400 before any transaction → none
 *   two tenants                  → each event under its own tenant
 * The stock ledger's own audit row (inventory_audit) is still written next to it — the central record adds, never replaces.
 */
public sealed class CentralAuditInventoryTests : IClassFixture<InventoryApiFactory>
{
    private readonly InventoryApiFactory _factory;

    public CentralAuditInventoryTests(InventoryApiFactory factory) => _factory = factory;

    [Fact]
    public async Task A_posted_movement_leaves_one_event_in_its_transaction_with_the_callers_scope_and_the_ledger_audit_stays()
    {
        var s = new InventoryScenario(_factory);

        var posted = await s.PostExpectingAsync(s.Receipt("100"), 201);

        var row = Assert.Single(Events(s.Tenant));
        Assert.Equal(s.LegalEntity, row.CompanyId);
        Assert.Equal(s.User, row.ActorId);
        Assert.Equal(CentralAuditActorTypes.TenantUser, row.ActorType);
        Assert.Equal("Diten.InventoryService", row.SourceService);
        Assert.Equal("INVENTORY-LEDGER", row.SourceModule);
        Assert.Equal("MasterData", row.Category);
        Assert.Equal("Create", row.Operation);
        Assert.Equal("InventoryTransaction", row.EntityType);
        Assert.Equal(Guid.Parse(posted.GetProperty("transactionId").GetString()!), row.EntityId);
        Assert.Equal("PostMovementCommand", row.RequestType);
        Assert.True(row.WrittenInCommandTransaction);
        Assert.Equal(CentralAuditOutboxStatus.Pending, row.Status);
        Assert.NotEqual(Guid.Empty, row.CorrelationId);

        Assert.Single(s.Persisted("inventory_audit"));
    }

    [Fact]
    public async Task A_replay_of_the_same_key_files_no_second_event()
    {
        var s = new InventoryScenario(_factory);
        var body = s.Receipt("100");

        await s.PostExpectingAsync(body, 201);
        await s.PostExpectingAsync(body, 200);

        Assert.Single(Events(s.Tenant));
    }

    /// <summary>s2 — the event was enlisted INSIDE the ledger transaction; the refusal aborted it, and the row with it.</summary>
    [Fact]
    public async Task An_aborted_ledger_transaction_leaves_no_event()
    {
        var s = new InventoryScenario(_factory);

        await s.PostExpectingAsync(s.Issue("5"), 422);

        Assert.Empty(Events(s.Tenant));
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    [Fact]
    public async Task A_request_refused_before_any_transaction_leaves_no_event()
    {
        var s = new InventoryScenario(_factory);
        var invalid = new Dictionary<string, object?>(s.Receipt("100")) { ["quantity"] = "-3" };

        await s.PostExpectingAsync(invalid, 400);

        Assert.Empty(Events(s.Tenant));
    }

    /// <summary>s7 at the service — each tenant's command is filed under that tenant, never another's.</summary>
    [Fact]
    public async Task Each_tenants_event_is_filed_under_that_tenant()
    {
        var a = new InventoryScenario(_factory);
        var b = new InventoryScenario(_factory);

        await a.PostExpectingAsync(a.Receipt("10"), 201);
        await b.PostExpectingAsync(b.Receipt("20"), 201);

        Assert.Equal(a.User, Assert.Single(Events(a.Tenant)).ActorId);
        Assert.Equal(b.User, Assert.Single(Events(b.Tenant)).ActorId);
    }

    [Fact]
    public async Task Without_platform_settings_the_events_wait_and_the_health_check_says_so()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("1"), 201);

        using var client = _factory.CreateClient();
        using var health = await client.GetAsync("/health/central-audit");
        var text = await health.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("Degraded", text);
        Assert.Equal(CentralAuditOutboxStatus.Pending, Assert.Single(Events(s.Tenant)).Status);
    }

    private List<CentralAuditOutboxRecord> Events(Guid tenant) => Outbox(_factory)
        .Find(x => x.TenantId == tenant)
        .ToList();

    internal static IMongoCollection<CentralAuditOutboxRecord> Outbox(InventoryApiFactory factory)
    {
        // The host registers the record's BSON shape; reading through the factory's own client needs the host built.
        _ = factory.Services;
        return factory.Database.GetCollection<CentralAuditOutboxRecord>(CentralAuditOutboxRecord.CollectionName);
    }
}

/// <summary>
/// Delivery from Inventory's real outbox (Mongo) to a scripted Platform: 500 → the row stays with a retry, then 202 →
/// Delivered. The background dispatcher is off; the test drives <see cref="CentralAuditDispatcher.RunOnceAsync"/>.
/// </summary>
public sealed class CentralAuditInventoryDeliveryTests : IClassFixture<CentralAuditInventoryDeliveryTests.ConfiguredFactory>
{
    private readonly ConfiguredFactory _factory;

    public CentralAuditInventoryDeliveryTests(ConfiguredFactory factory) => _factory = factory;

    /// <summary>s3 — a failed delivery never removes the row from Inventory's outbox.</summary>
    [Fact]
    public async Task Platform_failing_keeps_the_row_and_a_later_success_marks_it_delivered()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("7"), 201);
        var dispatcher = _factory.Services.GetRequiredService<CentralAuditDispatcher>();
        var outbox = CentralAuditInventoryTests.Outbox(_factory);

        _factory.Platform.Next = HttpStatusCode.InternalServerError;
        await dispatcher.RunOnceAsync(CancellationToken.None);
        var waiting = outbox.Find(x => x.TenantId == s.Tenant).Single();
        Assert.Equal(CentralAuditOutboxStatus.Pending, waiting.Status);
        Assert.Equal(1, waiting.Attempts);
        Assert.Contains("500", waiting.LastError, StringComparison.Ordinal);

        // Due again (the scripted backoff is zero), and Platform is back.
        outbox.UpdateOne(x => x.Id == waiting.Id, Builders<CentralAuditOutboxRecord>.Update.Set(x => x.NextAttemptAtUtc, DateTime.UtcNow.AddSeconds(-1)));
        _factory.Platform.Next = HttpStatusCode.Accepted;
        await dispatcher.RunOnceAsync(CancellationToken.None);

        var delivered = outbox.Find(x => x.Id == waiting.Id).Single();
        Assert.Equal(CentralAuditOutboxStatus.Delivered, delivered.Status);
        Assert.Contains(_factory.Platform.Bodies, b => b.Contains(waiting.Id.ToString(), StringComparison.Ordinal));
    }

    public sealed class ConfiguredFactory : InventoryApiFactory
    {
        public ScriptedPlatform Platform { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("PlatformRegistration:BaseUrl", "http://platform.central-audit.test");
            builder.UseSetting("PlatformRegistration:InternalApiKey", "inventory-central-audit-test-key");
            builder.UseSetting("CentralAudit:DispatcherEnabled", "false");
            builder.ConfigureTestServices(services =>
                services.AddHttpClient(PlatformCentralAuditClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Platform));
        }
    }

    public sealed class ScriptedPlatform : HttpMessageHandler
    {
        public HttpStatusCode Next { get; set; } = HttpStatusCode.Accepted;

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Only the central-audit append reaches this handler; module registration is skipped in tests (no hosted push here).
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(Next) { Content = new StringContent("{}") };
        }
    }
}
