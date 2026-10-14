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
        var mine = outbox.Find(x => x.TenantId == s.Tenant).Single().Id;

        // The suite's database is shared (DB-010): other classes' rows wait in the same outbox. The batch is ONE row
        // (ConfiguredFactory) and this row is put first in the due order, so a cycle touches this row and nobody else's.
        void FirstInLine() => outbox.UpdateOne(x => x.Id == mine,
            Builders<CentralAuditOutboxRecord>.Update.Set(x => x.NextAttemptAtUtc, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        _factory.Platform.Next = HttpStatusCode.InternalServerError;
        FirstInLine();
        await dispatcher.RunOnceAsync(CancellationToken.None);
        var waiting = outbox.Find(x => x.Id == mine).Single();
        Assert.Equal(CentralAuditOutboxStatus.Pending, waiting.Status);
        Assert.Equal(1, waiting.Attempts);
        Assert.Contains("500", waiting.LastError, StringComparison.Ordinal);
        Assert.True(waiting.NextAttemptAtUtc > DateTime.UtcNow.AddSeconds(-5), "a retry is scheduled after the failure (backoff)");

        // Due again, and Platform is back.
        FirstInLine();
        _factory.Platform.Next = HttpStatusCode.Accepted;
        await dispatcher.RunOnceAsync(CancellationToken.None);

        var delivered = outbox.Find(x => x.Id == waiting.Id).Single();
        Assert.Equal(CentralAuditOutboxStatus.Delivered, delivered.Status);
        // Sent twice (500, then 202); both say Succeeded — the command confirmed this row.
        var body = _factory.Platform.Bodies.Last(b => b.Contains(waiting.Id.ToString(), StringComparison.Ordinal));
        Assert.Contains("\"outcome\":1", body, StringComparison.Ordinal);
        Assert.Contains("\"confirmedByCommand\":true", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// CT HOLD item 2 — a transaction that COMMITTED although its command then FAILED: releasing a reservation that does not
    /// exist opens the release transaction (the event is enlisted in it), answers "not found" inside it and commits; the
    /// command answers 404. The row is kept as Abandoned and is never among the rows the dispatcher sends
    /// (<c>RunOnceAsync</c> sends exactly what <c>GetDueAsync</c> returns) — even put first in line.
    /// </summary>
    [Fact]
    public async Task A_failed_command_whose_transaction_committed_leaves_an_abandoned_row_that_is_never_sent()
    {
        var s = new InventoryScenario(_factory);
        using var released = await s.ReleaseAsync(Guid.NewGuid().ToString(), "W/\"1\"");
        Assert.Equal(HttpStatusCode.NotFound, released.StatusCode);

        var outbox = CentralAuditInventoryTests.Outbox(_factory);
        var row = outbox.Find(x => x.TenantId == s.Tenant).Single();
        Assert.Equal("ReleaseReservationCommand", row.RequestType);
        Assert.True(row.WrittenInCommandTransaction);
        Assert.Equal(CentralAuditOutboxStatus.Abandoned, row.Status);

        outbox.UpdateOne(x => x.Id == row.Id,
            Builders<CentralAuditOutboxRecord>.Update.Set(x => x.NextAttemptAtUtc, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        using var scope = _factory.Services.CreateScope();
        var due = await scope.ServiceProvider.GetRequiredService<ICentralAuditOutboxStore>()
            .GetDueAsync(DateTime.UtcNow.AddYears(1), int.MaxValue, CancellationToken.None);
        Assert.DoesNotContain(due, r => r.Id == row.Id);
        Assert.DoesNotContain(_factory.Platform.Bodies, b => b.Contains(row.Id.ToString(), StringComparison.Ordinal));
        Assert.Equal(CentralAuditOutboxStatus.Abandoned, outbox.Find(x => x.Id == row.Id).Single().Status);
    }

    /// <summary>
    /// CT HOLD item 3 — a row whose command never confirmed it (the process stopped after the commit) is released, not
    /// lost, and goes to Platform as Unconfirmed (4), never as Succeeded (1).
    /// </summary>
    [Fact]
    public async Task An_orphan_row_is_released_and_delivered_as_unconfirmed_never_as_succeeded()
    {
        var s = new InventoryScenario(_factory);
        await s.PostExpectingAsync(s.Receipt("3"), 201);
        var outbox = CentralAuditInventoryTests.Outbox(_factory);
        var id = outbox.Find(x => x.TenantId == s.Tenant).Single().Id;

        // The crash, reproduced on the real row: committed (AwaitingCommand), never confirmed, older than the orphan window.
        outbox.UpdateOne(x => x.Id == id, Builders<CentralAuditOutboxRecord>.Update
            .Set(x => x.Status, CentralAuditOutboxStatus.AwaitingCommand)
            .Set(x => x.CreatedAtUtc, DateTime.UtcNow.AddHours(-1)));

        using (var scope = _factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ICentralAuditOutboxStore>();
            Assert.True(await store.ReleaseOrphansAsync(DateTime.UtcNow.AddMinutes(-10), CancellationToken.None) >= 1);
        }

        var orphan = outbox.Find(x => x.Id == id).Single();
        Assert.Equal(CentralAuditOutboxStatus.Pending, orphan.Status);
        Assert.False(orphan.ConfirmedByCommand);

        outbox.UpdateOne(x => x.Id == id,
            Builders<CentralAuditOutboxRecord>.Update.Set(x => x.NextAttemptAtUtc, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        _factory.Platform.Next = HttpStatusCode.Accepted;
        await _factory.Services.GetRequiredService<CentralAuditDispatcher>().RunOnceAsync(CancellationToken.None);

        Assert.Equal(CentralAuditOutboxStatus.Delivered, outbox.Find(x => x.Id == id).Single().Status);
        var body = Assert.Single(_factory.Platform.Bodies, b => b.Contains(id.ToString(), StringComparison.Ordinal));
        Assert.Contains("\"outcome\":4", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"outcome\":1", body, StringComparison.Ordinal);
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
            builder.UseSetting("CentralAudit:BatchSize", "1");
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
