using System.Security.Claims;
using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// WP-MVP1-NAMES1 — INVENTORY 1.5.0 <c>TransactionItem.createdBy</c> (FU-0173-UI-USER-01, owner decision A): <c>userId</c> = the posting user;
/// <c>name</c> = the name AT THE TIME of the posting — the central audit queue's display name of the posting correlation and the same user,
/// this tenant AND this company only; absent when none was recorded (older movements), never the live label. The names of a page are read
/// in ONE batch (the production reader, counted).
/// </summary>
public sealed class TransactionCreatedByTests : IClassFixture<InventoryApiFactory>
{
    private const string CentralAudit = "central_audit_outbox";
    private readonly InventoryApiFactory _factory;

    public TransactionCreatedByTests(InventoryApiFactory factory) => _factory = factory;

    private static string Named(InventoryScenario s, string given, string family)
        => TestTokens.Mint(s.Tenant, s.LegalEntity, s.User, TestTokens.AllPermissions, [new Claim("given_name", given), new Claim("family_name", family)]);

    private static async Task<List<JsonElement>> RowsAsync(InventoryScenario s)
    {
        using var response = await s.GetAsync($"/api/inventory/transactions?itemId={s.Item}&pageSize=50");
        Assert.Equal(200, (int)response.StatusCode);
        return (await s.ReadAsync(response)).GetProperty("items").EnumerateArray().ToList();
    }

    private BsonDocument Filter(InventoryScenario s) => new("TenantId", s.Tenant.ToString("D"));

    /// <summary>
    /// WP-MVP1-FU-CLEAN2: a movement posted before FU-0173-MOV-ACTORNAME has no stored <c>CreatedByName</c> (no migration) — the queue
    /// fallback tests make their movements such "older" ones; the stored name is tested on its own below.
    /// </summary>
    private void ForgetStoredNames(InventoryScenario s)
        => _factory.Collection("inventory_transactions").UpdateMany(Filter(s), new BsonDocument("$unset", new BsonDocument("CreatedByName", "")));

    [Fact]
    public async Task A_posting_answers_its_poster_and_the_name_at_the_time_of_the_posting()
    {
        var s = new InventoryScenario(_factory);
        using (var posted = await s.PostAsync(s.Receipt("5"), token: Named(s, "Ayşe", "Demir"))) Assert.Equal(201, (int)posted.StatusCode);

        var by = Assert.Single(await RowsAsync(s)).GetProperty("createdBy");
        Assert.Equal(s.User.ToString("D"), by.GetProperty("userId").GetString());
        Assert.Equal("Ayşe Demir", by.GetProperty("name").GetString());
    }

    [Fact]
    public async Task An_older_movement_without_a_recorded_name_answers_the_user_only()
    {
        var s = new InventoryScenario(_factory);
        using (var unnamed = await s.PostAsync(s.Receipt("1"))) Assert.Equal(201, (int)unnamed.StatusCode);      // a token without names
        using (var named = await s.PostAsync(s.Receipt("2"), token: Named(s, "Ayşe", "Demir"))) Assert.Equal(201, (int)named.StatusCode);
        // the named movement becomes an "older" one: its queue row is gone (written before BL-501 / delivered and pruned)
        _factory.Collection(CentralAudit).DeleteMany(Filter(s).Add("ActorDisplayName", "Ayşe Demir"));
        ForgetStoredNames(s); // WP-MVP1-FU-CLEAN2: an older movement has no stored name either

        var rows = await RowsAsync(s);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r =>
        {
            var by = r.GetProperty("createdBy");
            Assert.Equal(s.User.ToString("D"), by.GetProperty("userId").GetString());
            Assert.False(by.TryGetProperty("name", out _), "no recorded name → no name member (the screen says unknown user)");
        });
    }

    [Theory]
    [InlineData("CompanyId")]
    [InlineData("TenantId")]
    public async Task A_queue_row_of_another_company_or_tenant_never_names_the_poster(string field)
    {
        var s = new InventoryScenario(_factory);
        using (var posted = await s.PostAsync(s.Receipt("3"), token: Named(s, "Ayşe", "Demir"))) Assert.Equal(201, (int)posted.StatusCode);
        // the same correlation and user, but the row now belongs to another company (same tenant) / another tenant
        var other = Guid.NewGuid();
        if (field == "TenantId") _factory.TrackTenant(other);
        var moved = _factory.Collection(CentralAudit).UpdateMany(Filter(s), new BsonDocument("$set", new BsonDocument(field, other.ToString("D"))));
        Assert.True(moved.ModifiedCount >= 1);
        ForgetStoredNames(s); // WP-MVP1-FU-CLEAN2: the QUEUE fallback is under test (an older movement)

        var by = Assert.Single(await RowsAsync(s)).GetProperty("createdBy");
        Assert.Equal(s.User.ToString("D"), by.GetProperty("userId").GetString());
        Assert.False(by.TryGetProperty("name", out _), $"a row of another {field} must not give a name");
    }

    [Fact]
    public async Task The_names_of_a_page_are_one_read_bound_to_the_company()
    {
        var s = new InventoryScenario(_factory);
        foreach (var quantity in new[] { "1", "2", "3", "4" })
        {
            using var posted = await s.PostAsync(s.Receipt(quantity), token: Named(s, "Ayşe", "Demir"));
            Assert.Equal(201, (int)posted.StatusCode);
        }

        ForgetStoredNames(s); // WP-MVP1-FU-CLEAN2: older movements — the page's names come from the queue
        var before = _factory.AuditNameReads.Count(r => r.Tenant == s.Tenant);
        var rows = await RowsAsync(s);
        Assert.Equal(4, rows.Count);
        Assert.All(rows, r => Assert.Equal("Ayşe Demir", r.GetProperty("createdBy").GetProperty("name").GetString()));
        var reads = _factory.AuditNameReads.Where(r => r.Tenant == s.Tenant).Skip(before).ToList();
        var read = Assert.Single(reads);                 // ONE read for the page, not one per row
        Assert.Equal((4, (Guid?)s.LegalEntity), (read.Keys, read.Company));
    }

    // ── WP-MVP1-FU-CLEAN2 (FU-0173-MOV-ACTORNAME): the name stored at posting time ─────────────────────────────────────────────────

    [Fact]
    public async Task A_posting_stores_the_token_name_on_the_movement()
    {
        var s = new InventoryScenario(_factory);
        using (var posted = await s.PostAsync(s.Receipt("5"), token: Named(s, "Ayşe", "Demir"))) Assert.Equal(201, (int)posted.StatusCode);
        using (var unnamed = await s.PostAsync(s.Receipt("6"))) Assert.Equal(201, (int)unnamed.StatusCode);

        var stored = s.Persisted("inventory_transactions");
        Assert.Equal(2, stored.Count);
        Assert.Single(stored, d => d.Contains("CreatedByName") && d["CreatedByName"] == "Ayşe Demir");
        Assert.Single(stored, d => !d.Contains("CreatedByName") || d["CreatedByName"].IsBsonNull);    // no claims → nothing invented
    }

    [Fact]
    public async Task The_stored_name_wins_over_the_queue_name_and_asks_the_queue_nothing()
    {
        var s = new InventoryScenario(_factory);
        using (var posted = await s.PostAsync(s.Receipt("5"), token: Named(s, "Ayşe", "Demir"))) Assert.Equal(201, (int)posted.StatusCode);
        // the queue now says another name for the same correlation and user: the stored one is the record of the posting
        var renamed = _factory.Collection(CentralAudit).UpdateMany(Filter(s), new BsonDocument("$set", new BsonDocument("ActorDisplayName", "Kuyruk Adı")));
        Assert.True(renamed.ModifiedCount >= 1);

        var before = _factory.AuditNameReads.Count(r => r.Tenant == s.Tenant);
        var by = Assert.Single(await RowsAsync(s)).GetProperty("createdBy");
        Assert.Equal("Ayşe Demir", by.GetProperty("name").GetString());
        Assert.All(_factory.AuditNameReads.Where(r => r.Tenant == s.Tenant).Skip(before), r => Assert.Equal(0, r.Keys));
    }

    [Fact]
    public async Task Another_tenants_or_companys_token_never_sees_a_stored_name()
    {
        var s = new InventoryScenario(_factory);
        using (var posted = await s.PostAsync(s.Receipt("5"), token: Named(s, "Ayşe", "Demir"))) Assert.Equal(201, (int)posted.StatusCode);
        var otherTenant = Guid.NewGuid();
        _factory.TrackTenant(otherTenant);
        foreach (var token in new[]
                 {
                     TestTokens.Mint(s.Tenant, Guid.NewGuid(), s.User, TestTokens.AllPermissions),       // same tenant, another company
                     TestTokens.Mint(otherTenant, s.LegalEntity, s.User, TestTokens.AllPermissions)    // another tenant
                 })
        {
            using var response = await s.GetAsync($"/api/inventory/transactions?itemId={s.Item}&pageSize=50", token);
            if ((int)response.StatusCode != 200) continue;                                              // refused outright: nothing shown
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Ayşe Demir", body, StringComparison.Ordinal);
        }
    }
}
