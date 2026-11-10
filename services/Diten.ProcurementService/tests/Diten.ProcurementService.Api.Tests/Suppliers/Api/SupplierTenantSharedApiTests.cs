using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.BuildingBlocks.CentralAudit;
using Diten.ProcurementService.Api.Tests.TestInfrastructure;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Diten.ProcurementService.Api.Tests.Suppliers.Api;

/// <summary>
/// MOD-0140 DL-3 on the REAL host with <c>Suppliers:Model = TenantShared</c> (§16, §17): one supplier per tenant, one
/// relationship per company; eligibility (1.3.0) and relationship (1.4.0) endpoints; the K-1 master.update rule; K-6 409 +
/// code; S-1; F-2 paging; the permission matrix; BL-501 records written in the command's own transaction (§21).
/// </summary>
[Collection(ProcurementMongoCollection.Name)]
public sealed class SupplierTenantSharedApiTests(ProcurementMongoReplicaSet mongo) : IAsyncLifetime
{
    private const string Read = "procurement.suppliers.read";
    private const string EligibilityRead = "procurement.suppliers.eligibility.read";
    private static readonly string[] Operator =
        [Read, "procurement.suppliers.create", "procurement.suppliers.update", "procurement.suppliers.delete", "procurement.suppliers.bulk-delete"];
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _companyA = Guid.NewGuid();
    private readonly Guid _companyB = Guid.NewGuid();
    private readonly Guid _companyC = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();
    private ProcurementApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        await SupplierTenantModelIndexConfiguration.EnsureIndexesAsync(await mongo.FreshDatabaseAsync());
        _factory = new ProcurementApiFactory(mongo, "TenantShared");
        _ = _factory.Server;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task One_supplier_per_tenant_K6_409_names_it_and_link_gives_company_B_its_own_relationship()
    {
        var a = Client(_companyA, Operator);
        var b = Client(_companyB, Operator);
        var created = await Data(await a.PostAsJsonAsync("/api/suppliers", Upsert("Acme", "TR", "123 45-6.7")), HttpStatusCode.Created);
        var code = created.GetProperty("supplierId").GetString()!;
        var masterId = created.GetProperty("id").GetString()!;

        // K-2 / K-6: the same (country, normalized tax number) in another company → 409 + the existing code, nothing written
        var duplicate = await b.PostAsJsonAsync("/api/suppliers", Upsert("Acme Ltd", "tr", "1234567"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var body = await duplicate.Content.ReadFromJsonAsync<JsonElement>(Web);
        Assert.Equal("DUPLICATE_SUPPLIER", body.GetProperty("errors")[0].GetString());
        Assert.Equal(code, body.GetProperty("errorDetails").GetProperty("supplierId").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/suppliers/{code}")).StatusCode);

        // link (explicit company decision) → B's relationship; the same key again → the same record; a second link → 409
        var linked = await Data(await Send(b, HttpMethod.Post, $"/api/suppliers/{code}/relationship", key: "link-1"), HttpStatusCode.Created);
        Assert.Equal(masterId, linked.GetProperty("supplierMasterId").GetString());
        Assert.True(linked.GetProperty("purchasingEnabled").GetBoolean());          // K-4
        var replay = await Data(await Send(b, HttpMethod.Post, $"/api/suppliers/{code}/relationship", key: "link-1"), HttpStatusCode.Created);
        Assert.Equal(linked.GetProperty("relationshipId").GetString(), replay.GetProperty("relationshipId").GetString());
        var again = await Send(b, HttpMethod.Post, $"/api/suppliers/{code}/relationship", key: "link-2");
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains("RELATIONSHIP_EXISTS", await again.Content.ReadAsStringAsync());

        Assert.Equal(1, await Masters().CountDocumentsAsync(m => m.TenantId == _tenant));
        Assert.Equal(2, await Relationships().CountDocumentsAsync(r => r.TenantId == _tenant && !r.IsDeleted));

        // A blocks and sets payment terms on ITS relationship; B sees neither (§8: company A's data never reaches B)
        var aRelationship = await Data(await a.GetAsync($"/api/suppliers/{code}/relationship"));
        await Data(await Send(a, HttpMethod.Put, $"/api/suppliers/{code}/relationship/payment-terms", new { paymentTermsCode = "NET30" },
            ifMatch: aRelationship.GetProperty("version").GetInt32()));
        aRelationship = await Data(await a.GetAsync($"/api/suppliers/{code}/relationship"));
        await Data(await Send(a, HttpMethod.Put, $"/api/suppliers/{code}/relationship/status", new { status = "Blocked" },
            ifMatch: aRelationship.GetProperty("version").GetInt32()));
        var stale = await Send(a, HttpMethod.Put, $"/api/suppliers/{code}/relationship/status", new { status = "Active" },
            ifMatch: aRelationship.GetProperty("version").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var bRelationship = await Data(await b.GetAsync($"/api/suppliers/{code}/relationship"));
        Assert.Equal(JsonValueKind.Null, bRelationship.GetProperty("paymentTermsCode").ValueKind);
        Assert.Equal("Active", bRelationship.GetProperty("status").GetString());

        // eligibility: A Blocked → false, B Active → true; uuid = code; C (no relationship) → 404 / known:false / not listed
        var aEligibility = await Data(await a.GetAsync($"/api/suppliers/{code}/eligibility"));
        Assert.False(aEligibility.GetProperty("eligibleForTransactions").GetBoolean());
        Assert.Equal("Blocked", aEligibility.GetProperty("status").GetString());
        var bEligibility = await Data(await b.GetAsync($"/api/suppliers/{masterId}/eligibility"));
        Assert.True(bEligibility.GetProperty("eligibleForTransactions").GetBoolean());
        Assert.Equal(code, bEligibility.GetProperty("supplierId").GetString());

        var c = Client(_companyC, Operator);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/suppliers/{code}/eligibility")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/suppliers/{masterId}/eligibility")).StatusCode);
        var cRow = (await Data(await c.PostAsJsonAsync("/api/suppliers/validate", new { supplierIds = new[] { code } }))).GetProperty("results")[0];
        Assert.False(cRow.GetProperty("known").GetBoolean());
        Assert.False(cRow.GetProperty("relationshipExists").GetBoolean());
        foreach (var nullField in new[] { "supplierMasterId", "status", "onboardingStatus", "purchasingEnabled" })
        {
            Assert.Equal(JsonValueKind.Null, cRow.GetProperty(nullField).ValueKind);
        }

        Assert.False(cRow.GetProperty("eligibleForTransactions").GetBoolean());

        Assert.DoesNotContain(code, await EligibleCodes(a));
        Assert.Contains(code, await EligibleCodes(b));
        Assert.Empty(await EligibleCodes(c));

        // another tenant never sees it
        var stranger = _factory.ClientFor(Guid.NewGuid(), _companyA, _user, Operator);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/suppliers/{code}/eligibility")).StatusCode);
        Assert.Empty(await EligibleCodes(stranger));
    }

    [Fact]
    public async Task Common_fields_need_master_update_and_without_it_only_relationship_fields_change()
    {
        var a = Client(_companyA, Operator);
        var created = await Data(await a.PostAsJsonAsync("/api/suppliers", Upsert("Beta", "de", "DE-1")), HttpStatusCode.Created);
        var code = created.GetProperty("supplierId").GetString()!;
        var version = created.GetProperty("version").GetInt32();

        var forbidden = await Send(a, HttpMethod.Patch, $"/api/suppliers/{code}", Upsert("Beta Renamed", "de", "DE-1"), ifMatch: version);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Contains("MASTER_UPDATE_FORBIDDEN", await forbidden.Content.ReadAsStringAsync());
        Assert.Equal("Beta", (await Data(await a.GetAsync($"/api/suppliers/{code}"))).GetProperty("name").GetString());

        var relationOnly = await Data(await Send(a, HttpMethod.Patch, $"/api/suppliers/{code}",
            new { name = "Beta", country = "de", taxId = "DE-1", contacts = Array.Empty<object>(), sourceSystem = "SAP", externalRef = "V-9" }, ifMatch: version));
        Assert.Equal("V-9", relationOnly.GetProperty("externalRef").GetString());

        var master = Client(_companyA, [.. Operator, "procurement.suppliers.master.update"]);
        var stale = await Send(master, HttpMethod.Patch, $"/api/suppliers/{code}", Upsert("Beta GmbH", "de", "DE-1"), ifMatch: version);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var renamed = await Data(await Send(master, HttpMethod.Patch, $"/api/suppliers/{code}", Upsert("Beta GmbH", "de", "DE-1"),
            ifMatch: relationOnly.GetProperty("version").GetInt32()));
        Assert.Equal("Beta GmbH", renamed.GetProperty("name").GetString());

        // the common field is tenant-wide: company B, once linked, reads the new name
        var b = Client(_companyB, Operator);
        await Data(await Send(b, HttpMethod.Post, $"/api/suppliers/{code}/relationship"), HttpStatusCode.Created);
        Assert.Equal("Beta GmbH", (await Data(await b.GetAsync($"/api/suppliers/{code}"))).GetProperty("name").GetString());
    }

    [Fact]
    public async Task Permission_matrix_eligibility_read_or_read_opens_only_the_three_eligibility_endpoints()
    {
        var code = (await Data(await Client(_companyA, Operator).PostAsJsonAsync("/api/suppliers", Upsert("Gamma", "fr", null)), HttpStatusCode.Created))
            .GetProperty("supplierId").GetString()!;

        foreach (var permissions in new[] { new[] { EligibilityRead }, new[] { Read } })
        {
            var client = Client(_companyA, permissions);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/suppliers/eligible")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/suppliers/{code}/eligibility")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/suppliers/validate", new { supplierIds = new[] { code } })).StatusCode);
        }

        var narrow = Client(_companyA, [EligibilityRead]);
        Assert.Equal(HttpStatusCode.Forbidden, (await narrow.GetAsync("/api/suppliers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await narrow.GetAsync($"/api/suppliers/{code}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await narrow.GetAsync($"/api/suppliers/{code}/onboarding")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await narrow.GetAsync($"/api/suppliers/{code}/relationship")).StatusCode);

        var none = Client(_companyA, ["procurement.suppliers.create"]);
        Assert.Equal(HttpStatusCode.Forbidden, (await none.GetAsync("/api/suppliers/eligible")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await none.GetAsync($"/api/suppliers/{code}/eligibility")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await none.PostAsJsonAsync("/api/suppliers/validate", new { supplierIds = new[] { code } })).StatusCode);
    }

    [Fact]
    public async Task Eligible_pages_by_code_only_every_supplier_exactly_once_and_S1_keeps_unapproved_active_relationships()
    {
        // 120 Active relationships whose names run AGAINST their codes, 30 Blocked ones in between (F-2); some Active ones
        // with onboarding InReview / Rejected or purchasingEnabled false — still eligible (S-1)
        var masters = new List<SupplierMaster>();
        var relationships = new List<SupplierCompanyRelationship>();
        for (var i = 1; i <= 150; i++)
        {
            var master = new SupplierMaster { Id = Guid.NewGuid(), TenantId = _tenant, SupplierId = $"SUP-{i:D8}", Name = $"Name {1000 - i:D4}" };
            var blocked = i % 5 == 0;
            masters.Add(master);
            relationships.Add(new SupplierCompanyRelationship
            {
                Id = Guid.NewGuid(), TenantId = _tenant, LegalEntityId = _companyA, SupplierRef = master.Id,
                Status = blocked ? SupplierStatus.Blocked : SupplierStatus.Active,
                OnboardingStatus = (i % 3) switch { 0 => OnboardingStatus.InReview, 1 => OnboardingStatus.Rejected, _ => OnboardingStatus.Draft },
                PurchasingEnabled = i % 7 != 0
            });
        }

        await Masters().InsertManyAsync(masters);
        await Relationships().InsertManyAsync(relationships);

        var client = Client(_companyA, [EligibilityRead]);
        var seen = new List<string>();
        var pages = new List<int>();
        string? cursor = null;
        do
        {
            var page = await Data(await client.GetAsync("/api/suppliers/eligible" + (cursor is null ? string.Empty : $"?cursor={cursor}")));
            var items = page.GetProperty("items").EnumerateArray().ToList();
            Assert.All(items, item => Assert.True(item.GetProperty("eligibleForTransactions").GetBoolean()));
            pages.Add(items.Count);
            seen.AddRange(items.Select(item => item.GetProperty("supplierId").GetString()!));
            cursor = page.GetProperty("nextCursor").ValueKind == JsonValueKind.Null ? null : page.GetProperty("nextCursor").GetString();
        }
        while (cursor is not null);

        var expected = relationships.Where(r => r.Status == SupplierStatus.Active)
            .Select(r => masters.Single(m => m.Id == r.SupplierRef).SupplierId)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
        Assert.Equal([50, 50, 20], pages);
        Assert.Equal(expected, seen);

        // the /eligible set equals the validate eligibleForTransactions=true set (S-1: no extra condition)
        var rows = (await Data(await client.PostAsJsonAsync("/api/suppliers/validate", new { supplierIds = masters.Select(m => m.SupplierId) })))
            .GetProperty("results").EnumerateArray()
            .Where(r => r.GetProperty("eligibleForTransactions").GetBoolean())
            .Select(r => r.GetProperty("supplierId").GetString()!)
            .ToList();
        Assert.Equal(expected, rows);
    }

    [Fact]
    public async Task Legacy_code_resolves_and_a_deleted_relationship_is_gone_for_its_company_only()
    {
        var a = Client(_companyA, Operator);
        var code = (await Data(await a.PostAsJsonAsync("/api/suppliers", Upsert("Delta", "it", "IT-7")), HttpStatusCode.Created)).GetProperty("supplierId").GetString()!;
        await Relationships().UpdateOneAsync(r => r.TenantId == _tenant && r.LegalEntityId == _companyA,
            Builders<SupplierCompanyRelationship>.Update.Set(r => r.LegacySupplierCodes, ["OLD-A-7"]));

        var byLegacy = await Data(await a.GetAsync("/api/suppliers/OLD-A-7/eligibility"));
        Assert.Equal(code, byLegacy.GetProperty("supplierId").GetString());
        var row = (await Data(await a.PostAsJsonAsync("/api/suppliers/validate", new { supplierIds = new[] { "OLD-A-7" } }))).GetProperty("results")[0];
        Assert.Equal("OLD-A-7", row.GetProperty("supplierId").GetString());
        Assert.True(row.GetProperty("known").GetBoolean());

        // another company's legacy code is not this company's
        var b = Client(_companyB, Operator);
        await Data(await Send(b, HttpMethod.Post, $"/api/suppliers/{code}/relationship"), HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync("/api/suppliers/OLD-A-7/eligibility")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await a.DeleteAsync($"/api/suppliers/{code}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/suppliers/{code}/eligibility")).StatusCode);
        var deleted = (await Data(await a.PostAsJsonAsync("/api/suppliers/validate", new { supplierIds = new[] { code } }))).GetProperty("results")[0];
        Assert.False(deleted.GetProperty("relationshipExists").GetBoolean());
        Assert.True((await Data(await b.GetAsync($"/api/suppliers/{code}/eligibility"))).GetProperty("eligibleForTransactions").GetBoolean());
    }

    [Fact]
    public async Task Every_marked_write_files_one_central_record_in_its_own_transaction_and_a_replay_or_failure_files_none()
    {
        Assert.True(_factory.Services.GetRequiredService<IOptions<CentralAuditOptions>>().Value.RequireEnlistment);

        var a = Client(_companyA, Operator);
        var first = await Data(await Send(a, HttpMethod.Post, "/api/suppliers", Upsert("Epsilon", "es", "ES-1"), key: "create-1"), HttpStatusCode.Created);
        var replay = await Data(await Send(a, HttpMethod.Post, "/api/suppliers", Upsert("Epsilon", "es", "ES-1"), key: "create-1"), HttpStatusCode.Created);
        Assert.Equal(first.GetProperty("supplierId").GetString(), replay.GetProperty("supplierId").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await a.PostAsJsonAsync("/api/suppliers", Upsert("Epsilon 2", "es", "ES-1"))).StatusCode);

        var records = await Outbox();
        var create = Assert.Single(records);
        Assert.Equal("CreateSupplierCommand", create.RequestType);
        Assert.Equal(("MasterData", "Create", "Supplier", "procurement-suppliers"), (create.Category, create.Operation, create.EntityType, create.SourceModule));
        Assert.Equal(Guid.Parse(first.GetProperty("id").GetString()!), create.EntityId);
        Assert.Equal(_companyA, create.CompanyId);
        Assert.Equal(_user, create.ActorId);
        Assert.Equal(CentralAuditActorTypes.TenantUser, create.ActorType);
        Assert.True(create.WrittenInCommandTransaction);

        var code = first.GetProperty("supplierId").GetString()!;
        var linked = await Data(await Send(Client(_companyB, Operator), HttpMethod.Post, $"/api/suppliers/{code}/relationship"), HttpStatusCode.Created);
        var link = Assert.Single(await Outbox(), r => r.RequestType == "LinkSupplierToCompanyCommand");
        Assert.Equal(("Assign", "SupplierCompanyRelationship"), (link.Operation, link.EntityType));
        Assert.Equal(Guid.Parse(linked.GetProperty("relationshipId").GetString()!), link.EntityId);
        Assert.Equal(_companyB, link.CompanyId);
        Assert.All(await Outbox(), r => Assert.True(r.WrittenInCommandTransaction));
        Assert.Equal(2, (await Outbox()).Count);
    }

    private HttpClient Client(Guid company, IEnumerable<string> permissions) => _factory.ClientFor(_tenant, company, _user, permissions);

    private IMongoCollection<SupplierMaster> Masters() =>
        _factory.Database.GetCollection<SupplierMaster>(SupplierTenantModelIndexConfiguration.MasterCollectionName);

    private IMongoCollection<SupplierCompanyRelationship> Relationships() =>
        _factory.Database.GetCollection<SupplierCompanyRelationship>(SupplierTenantModelIndexConfiguration.RelationshipCollectionName);

    private async Task<List<CentralAuditOutboxRecord>> Outbox() =>
        await _factory.Database.GetCollection<CentralAuditOutboxRecord>(CentralAuditOutboxRecord.CollectionName).Find(r => r.TenantId == _tenant).ToListAsync();

    private static async Task<List<string>> EligibleCodes(HttpClient client) =>
        (await Data(await client.GetAsync("/api/suppliers/eligible"))).GetProperty("items").EnumerateArray()
        .Select(i => i.GetProperty("supplierId").GetString()!).ToList();

    private static object Upsert(string name, string country, string? taxId) =>
        new { name, country, taxId, contacts = Array.Empty<object>(), sourceSystem = (string?)null, externalRef = (string?)null };

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object? body = null, string? key = null, int? ifMatch = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch.Value.ToString());
        }

        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> Data(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"{(int)response.StatusCode} {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: {text}");
        return JsonDocument.Parse(text).RootElement.GetProperty("data");
    }
}
