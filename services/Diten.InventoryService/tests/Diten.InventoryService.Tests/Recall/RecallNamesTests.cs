using System.Security.Claims;
using System.Text.Json;
using Diten.InventoryService.Persistence.Recall;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>
/// WP-MVP1-NAMES1 — RECALL 1.8.0 <c>UserRef.name</c> / <c>UserAt.name</c> (owner decision A): a person who DID the action is named with the name AT
/// THE TIME of the action — the record's own <c>ActorName</c>, else the central audit queue's display name of the same correlation and the same
/// user, this tenant and this company; neither → no <c>name</c> member (the screen says "unknown user"). An assigned person (DrillPlan.owner)
/// gets no name from the service. A list answer reads the queue ONCE for all its rows (the production reader, counted).
/// </summary>
public sealed class RecallNamesTests : IClassFixture<RecallApiFactory>
{
    private const string Ayse = "Ayşe Demir";
    private const string Mehmet = "Mehmet Kaya";
    private readonly RecallApiFactory _factory;

    public RecallNamesTests(RecallApiFactory factory) => _factory = factory;

    private IMongoCollection<BsonDocument> Queue => _factory.Database.GetCollection<BsonDocument>("central_audit_outbox");

    private static BsonDocument Of(RecallScenario s, Guid user) => new() { ["TenantId"] = s.Tenant.ToString("D"), ["ActorId"] = user.ToString("D") };

    /// <summary>The queue rows of a user get the name their (named) token would have carried — the way rows written by unnamed tokens are named.</summary>
    private void NameInQueue(RecallScenario s, Guid user, string name)
        => Assert.True(Queue.UpdateMany(Of(s, user), new BsonDocument("$set", new BsonDocument("ActorDisplayName", name))).ModifiedCount > 0);

    private static string? Name(JsonElement person) => person.TryGetProperty("name", out var n) ? n.GetString() : null;

    [Fact]
    public async Task The_opener_is_named_from_the_name_the_event_kept()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var named = TestTokens.Mint(w.S.Tenant, w.S.Ledger.LegalEntity, w.S.Qa1, RecallR3.QaKeys, [new Claim("given_name", "Ayşe"), new Claim("family_name", "Demir")]);
        var (status, opened, _) = await w.S.PostOpenAsync(named, RecallScenario.Open(w.Fg));
        Assert.True(status == 201, opened.ToString());
        Assert.Equal(Ayse, Name(opened.GetProperty("owner")));                                  // the command answer too
        // the queue row is gone (pruned) — the event's own ActorName still names the opener
        Queue.DeleteMany(Of(w.S, w.S.Qa1));
        var detail = await w.S.GetAsync(RecallScenario.Id(opened));
        Assert.Equal(Ayse, Name(detail.GetProperty("owner")));
    }

    [Fact]
    public async Task Events_without_a_kept_name_are_named_from_the_queue_of_the_same_correlation_and_user()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var (caseId, _) = await RecallR3.ApproveAsync(w.S, w.Fg);                                // unnamed tokens: no ActorName, no queue name
        var before = await w.S.GetAsync(caseId);
        Assert.Null(Name(before.GetProperty("owner")));
        Assert.Null(Name(before.GetProperty("scopeApprovedBy")));

        NameInQueue(w.S, w.S.Qa1, Ayse);
        NameInQueue(w.S, w.S.Qa2, Mehmet);
        var after = await w.S.GetAsync(caseId);
        Assert.Equal(Ayse, Name(after.GetProperty("owner")));
        Assert.Equal(Ayse, Name(after.GetProperty("scopeSubmittedBy")));
        Assert.Equal(Mehmet, Name(after.GetProperty("scopeApprovedBy")));
        var timeline = await w.S.ExpectAsync(HttpMethod.Get, $"/api/recall/cases/{caseId}/timeline?page=1&pageSize=50", w.S.Qa1Token, null, 200);
        Assert.All(timeline.GetProperty("items").EnumerateArray().Where(e => e.TryGetProperty("actor", out _)),
            e => Assert.Equal(e.GetProperty("actor").GetProperty("userId").GetString() == w.S.Qa2.ToString("D") ? Mehmet : Ayse, Name(e.GetProperty("actor"))));
    }

    [Theory]
    [InlineData("CompanyId")]
    [InlineData("TenantId")]
    public async Task A_queue_row_of_another_company_or_tenant_never_names_anyone(string field)
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        var (caseId, _) = await RecallR3.ApproveAsync(w.S, w.Fg);
        NameInQueue(w.S, w.S.Qa1, Ayse);
        NameInQueue(w.S, w.S.Qa2, Mehmet);
        var other = Guid.NewGuid();
        if (field == "TenantId") _factory.TrackTenant(other);
        Queue.UpdateMany(new BsonDocument("TenantId", w.S.Tenant.ToString("D")), new BsonDocument("$set", new BsonDocument(field, other.ToString("D"))));

        var detail = await w.S.GetAsync(caseId);
        Assert.Null(Name(detail.GetProperty("owner")));
        Assert.Null(Name(detail.GetProperty("scopeApprovedBy")));
    }

    [Fact]
    public async Task The_case_list_names_its_rows_with_one_queue_read_bound_to_the_company()
    {
        var w = await RecallTraceWorld.CreateAsync(_factory);
        for (var i = 0; i < 3; i++) await w.S.OpenAsync((await w.S.LotAsync()).ToString());
        NameInQueue(w.S, w.S.Qa1, Ayse);

        var reads = _factory.AuditNameReads.Count(r => r.Tenant == w.S.Tenant);
        var page = await w.S.ExpectAsync(HttpMethod.Get, "/api/recall/cases?type=Real&page=1&pageSize=50", w.S.Qa1Token, null, 200);
        var rows = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal(Ayse, Name(r.GetProperty("owner"))));
        var read = Assert.Single(_factory.AuditNameReads.Where(r => r.Tenant == w.S.Tenant).Skip(reads));   // ONE read for the page, not one per row
        Assert.Equal((3, (Guid?)w.S.Ledger.LegalEntity), (read.Keys, read.Company));
    }
}
