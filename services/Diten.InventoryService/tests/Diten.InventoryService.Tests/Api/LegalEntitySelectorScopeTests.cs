using System.Text.Json;
using Diten.InventoryService.Tests.TestInfrastructure;
using MongoDB.Bson;
using Xunit;

namespace Diten.InventoryService.Tests.Api;

/// <summary>
/// WP-0173-LE1 — pack §19 waiver W-0173-02 (company selector), measured through the real pipeline with an in-process
/// MDM legal-entity master that answers for the caller's tenant.
/// <list type="bullet">
/// <item>L1 a token WITH <c>legal_entity_id</c> behaves as before: the header may only confirm it; MDM is never asked.</item>
/// <item>L2 a claim-less token without the header → 403 "select a company", nothing read or written.</item>
/// <item>L3 a claim-less token + an active legal entity of the tenant → 2xx; audit <c>LeSource</c> =
/// <c>header-unverified-membership</c> (postings and reservations).</item>
/// <item>L4 unknown / inactive / another tenant's legal entity, or MDM refusing the caller → 403 CROSS_LE_FORBIDDEN.</item>
/// <item>L5 MDM unavailable → 503 LEGAL_ENTITY_MASTER_UNAVAILABLE, nothing written.</item>
/// <item>L6 two companies of one tenant are separate ledgers: stock of LE-A never shows under LE-B.</item>
/// </list>
/// </summary>
public sealed class LegalEntitySelectorScopeTests : IClassFixture<InventoryApiFactory>
{
    private const string Header = "X-Legal-Entity-Id";
    private readonly InventoryApiFactory _factory;

    public LegalEntitySelectorScopeTests(InventoryApiFactory factory) => _factory = factory;

    private static string ClaimLess(InventoryScenario s) => TestTokens.Mint(s.Tenant, null, s.User, TestTokens.AllPermissions);

    private static Action<HttpRequestMessage> Company(Guid legalEntity) => r => r.Headers.TryAddWithoutValidation(Header, legalEntity.ToString());

    private static async Task<(int Status, string? Code, string Message)> Refusal(InventoryScenario s, HttpResponseMessage response)
    {
        var json = await s.ReadAsync(response);
        var error = json.GetProperty("error");
        var code = error.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
        return ((int)response.StatusCode, code, error.GetProperty("message").GetString() ?? string.Empty);
    }

    // ── L1 ─────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task L1_a_token_with_a_legal_entity_claim_decides_and_a_differing_header_is_403()
    {
        var s = new InventoryScenario(_factory);
        var other = Guid.NewGuid();
        _factory.LegalEntities.Register(s.Tenant, other); // even an ACTIVE company of the tenant cannot override the claim

        using (var mismatch = await s.PostAsync(s.Receipt("10"), configure: Company(other)))
        {
            var (status, code, _) = await Refusal(s, mismatch);
            Assert.Equal(403, status);
            Assert.Equal("CROSS_LE_FORBIDDEN", code);
        }

        using (var read = await s.GetAsync("/api/inventory/balance", configure: Company(other))) Assert.Equal(403, (int)read.StatusCode);
        using (var same = await s.PostAsync(s.Receipt("10"), configure: Company(s.LegalEntity))) Assert.Equal(201, (int)same.StatusCode);
        using (var none = await s.PostAsync(s.Receipt("5"))) Assert.Equal(201, (int)none.StatusCode);

        Assert.Equal(0, _factory.LegalEntities.CallsFor(other));
        Assert.Equal(0, _factory.LegalEntities.CallsFor(s.LegalEntity));
        Assert.All(s.Persisted("inventory_audit"), a => Assert.Equal("token", a["LeSource"].AsString));
        Assert.All(s.Persisted("inventory_transactions"), t => Assert.Equal(s.LegalEntity.ToString(), t["LegalEntityId"].AsString));
    }

    // ── L2 ─────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-uuid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task L2_a_claim_less_token_without_a_valid_company_header_is_403_select_a_company(string? header)
    {
        var s = new InventoryScenario(_factory);
        var token = ClaimLess(s);
        void Configure(HttpRequestMessage r) { if (header is not null) r.Headers.TryAddWithoutValidation(Header, header); }

        using var post = await s.PostAsync(s.Receipt("10"), token: token, configure: Configure);
        using var read = await s.GetAsync("/api/inventory/balance", token, Configure);

        var (status, _, message) = await Refusal(s, post);
        Assert.Equal(403, status);
        Assert.Contains("Select a company", message);
        Assert.Equal(403, (int)read.StatusCode);
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    // ── L3 ─────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task L3_a_claim_less_token_with_an_active_company_of_its_tenant_posts_and_reads_with_the_header_source()
    {
        var s = new InventoryScenario(_factory);
        _factory.LegalEntities.Register(s.Tenant, s.LegalEntity);
        var token = ClaimLess(s);

        using (var post = await s.PostAsync(s.Receipt("40"), token: token, configure: Company(s.LegalEntity))) Assert.Equal(201, (int)post.StatusCode);
        using (var reserve = await s.ReserveAsync(s.Reservation("15"), token, Company(s.LegalEntity))) Assert.Equal(201, (int)reserve.StatusCode);
        using (var balance = await s.GetAsync("/api/inventory/balance", token, Company(s.LegalEntity)))
        {
            Assert.Equal(200, (int)balance.StatusCode);
            Assert.Equal(1, (await s.ReadAsync(balance)).GetProperty("total").GetInt32());
        }

        var transaction = Assert.Single(s.Persisted("inventory_transactions"));
        Assert.Equal(s.LegalEntity.ToString(), transaction["LegalEntityId"].AsString);
        var audits = s.Persisted("inventory_audit");
        Assert.Contains(audits, a => a["Action"].AsString == "InventoryMovementPosted" && a["LeSource"].AsString == "header-unverified-membership");
        Assert.Contains(audits, a => a["Action"].AsString == "InventoryReservationCreated" && a["LeSource"].AsString == "header-unverified-membership");
    }

    // ── L4 ─────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("unknown")]
    [InlineData("inactive")]
    [InlineData("other-tenant")]
    [InlineData("caller-refused")]
    public async Task L4_a_company_that_is_not_an_active_one_of_the_callers_tenant_is_403_and_nothing_is_written(string kind)
    {
        var s = new InventoryScenario(_factory);
        var company = Guid.NewGuid();
        switch (kind)
        {
            case "inactive": _factory.LegalEntities.Register(s.Tenant, company, active: false); break;
            case "other-tenant": _factory.LegalEntities.Register(Guid.NewGuid(), company); break;
            case "caller-refused": _factory.LegalEntities.Register(s.Tenant, company); _factory.LegalEntities.RefuseCallersOf(s.Tenant); break;
        }

        var token = ClaimLess(s);
        using var post = await s.PostAsync(s.Receipt("10"), token: token, configure: Company(company));
        using var read = await s.GetAsync("/api/inventory/transactions", token, Company(company));

        var (status, code, _) = await Refusal(s, post);
        Assert.Equal(403, status);
        Assert.Equal("CROSS_LE_FORBIDDEN", code);
        Assert.Equal(403, (int)read.StatusCode);
        Assert.Empty(s.Persisted("inventory_transactions"));
        Assert.Empty(s.Persisted("inventory_audit"));
    }

    // ── L5 ─────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task L5_MDM_unavailable_is_503_and_nothing_is_written()
    {
        var s = new InventoryScenario(_factory);
        _factory.LegalEntities.Register(s.Tenant, s.LegalEntity);
        _factory.LegalEntities.MarkDown(s.Tenant);

        using var post = await s.PostAsync(s.Receipt("10"), token: ClaimLess(s), configure: Company(s.LegalEntity));

        var (status, code, _) = await Refusal(s, post);
        Assert.Equal(503, status);
        Assert.Equal("LEGAL_ENTITY_MASTER_UNAVAILABLE", code);
        Assert.Empty(s.Persisted("inventory_transactions"));
    }

    // ── L6 ─────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task L6_stock_entered_under_company_A_is_invisible_under_company_B_of_the_same_tenant()
    {
        var s = new InventoryScenario(_factory);
        var companyB = Guid.NewGuid();
        _factory.LegalEntities.Register(s.Tenant, s.LegalEntity);
        _factory.LegalEntities.Register(s.Tenant, companyB);
        var token = ClaimLess(s);

        using (var post = await s.PostAsync(s.Receipt("25"), token: token, configure: Company(s.LegalEntity))) Assert.Equal(201, (int)post.StatusCode);

        using (var balanceA = await s.GetAsync($"/api/inventory/balance?itemId={s.Item}", token, Company(s.LegalEntity)))
            Assert.Equal(1, (await s.ReadAsync(balanceA)).GetProperty("total").GetInt32());

        using (var balanceB = await s.GetAsync($"/api/inventory/balance?itemId={s.Item}", token, Company(companyB)))
        {
            Assert.Equal(200, (int)balanceB.StatusCode);
            Assert.Equal(0, (await s.ReadAsync(balanceB)).GetProperty("total").GetInt32());
        }

        using (var transactionsB = await s.GetAsync($"/api/inventory/transactions?itemId={s.Item}", token, Company(companyB)))
        {
            Assert.Equal(200, (int)transactionsB.StatusCode);
            Assert.Equal(0, (await s.ReadAsync(transactionsB)).GetProperty("total").GetInt32());
        }

        using (var availabilityB = await s.GetAsync($"/api/inventory/availability?itemId={s.Item}", token, Company(companyB)))
        {
            Assert.Equal(200, (int)availabilityB.StatusCode);
            var json = await s.ReadAsync(availabilityB);
            Assert.Equal(0m, decimal.Parse(json.GetProperty("onHand").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        }

        using (var availabilityA = await s.GetAsync($"/api/inventory/availability?itemId={s.Item}", token, Company(s.LegalEntity)))
            Assert.Equal(25m, decimal.Parse((await s.ReadAsync(availabilityA)).GetProperty("onHand").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
    }

    // ── query / body never carry the company ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_company_in_the_query_is_still_400_even_for_a_claim_less_token()
    {
        var s = new InventoryScenario(_factory);
        _factory.LegalEntities.Register(s.Tenant, s.LegalEntity);

        using var read = await s.GetAsync($"/api/inventory/balance?legalEntityId={s.LegalEntity}", ClaimLess(s), Company(s.LegalEntity));

        Assert.Equal(400, (int)read.StatusCode);
    }

    // ── cache: positive answers only, per token ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_confirmed_company_is_cached_per_token_and_a_refusal_is_never_cached()
    {
        var s = new InventoryScenario(_factory);
        var company = Guid.NewGuid();
        _factory.LegalEntities.Register(s.Tenant, company);
        var tokenA = ClaimLess(s);
        var tokenB = TestTokens.Mint(s.Tenant, null, Guid.NewGuid(), TestTokens.AllPermissions);

        for (var i = 0; i < 3; i++)
        {
            using var read = await s.GetAsync("/api/inventory/balance", tokenA, Company(company));
            Assert.Equal(200, (int)read.StatusCode);
        }

        Assert.Equal(1, _factory.LegalEntities.CallsFor(company));

        using (var other = await s.GetAsync("/api/inventory/balance", tokenB, Company(company))) Assert.Equal(200, (int)other.StatusCode);
        Assert.Equal(2, _factory.LegalEntities.CallsFor(company));

        var unknown = Guid.NewGuid();
        for (var i = 0; i < 2; i++)
        {
            using var read = await s.GetAsync("/api/inventory/balance", tokenA, Company(unknown));
            Assert.Equal(403, (int)read.StatusCode);
        }

        Assert.Equal(2, _factory.LegalEntities.CallsFor(unknown));
    }
}
