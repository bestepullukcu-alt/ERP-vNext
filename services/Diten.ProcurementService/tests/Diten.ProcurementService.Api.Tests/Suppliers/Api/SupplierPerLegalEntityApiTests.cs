using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.BuildingBlocks.CentralAudit;
using Diten.ProcurementService.Api.Tests.TestInfrastructure;
using MongoDB.Driver;
using Xunit;

namespace Diten.ProcurementService.Api.Tests.Suppliers.Api;

/// <summary>
/// MOD-0140 DL-3 — <c>Suppliers:Model = PerLegalEntity</c> (the default and the roll-back position, K-3) on the REAL host:
/// today's per-company records keep working; the 1.3.0 eligibility surface is derived from them (one record = one
/// relationship); relationship-only endpoints (1.4.0) answer 404 because there is no tenant-wide record to relate to; the
/// central-audit record is still written in the write's own transaction.
/// </summary>
[Collection(ProcurementMongoCollection.Name)]
public sealed class SupplierPerLegalEntityApiTests(ProcurementMongoReplicaSet mongo) : IAsyncLifetime
{
    private static readonly string[] Operator =
        ["procurement.suppliers.read", "procurement.suppliers.create", "procurement.suppliers.update", "procurement.suppliers.delete"];

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _companyA = Guid.NewGuid();
    private readonly Guid _companyB = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();
    private ProcurementApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        await mongo.FreshDatabaseAsync();
        _factory = new ProcurementApiFactory(mongo, "PerLegalEntity");
        _ = _factory.Server;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Todays_records_answer_the_eligibility_surface_and_relationship_endpoints_are_404()
    {
        var a = _factory.ClientFor(_tenant, _companyA, _user, Operator);
        var created = await Data(await a.PostAsJsonAsync("/api/suppliers", Upsert("Legacy Co", "tr", "TR-1")), HttpStatusCode.Created);
        var code = created.GetProperty("supplierId").GetString()!;

        var eligibility = await Data(await a.GetAsync($"/api/suppliers/{code}/eligibility"));
        Assert.True(eligibility.GetProperty("eligibleForTransactions").GetBoolean());
        Assert.True(eligibility.GetProperty("relationshipExists").GetBoolean());
        Assert.Equal(created.GetProperty("id").GetString(), eligibility.GetProperty("supplierMasterId").GetString());
        Assert.Contains(code, (await Data(await a.GetAsync("/api/suppliers/eligible"))).GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("supplierId").GetString()));

        var row = (await Data(await a.PostAsJsonAsync("/api/suppliers/validate", new { supplierIds = new[] { code, "SUP-NOPE0000" } }))).GetProperty("results");
        Assert.True(row[0].GetProperty("eligibleForTransactions").GetBoolean());
        Assert.False(row[1].GetProperty("known").GetBoolean());

        Assert.Equal(HttpStatusCode.NotFound, (await a.GetAsync($"/api/suppliers/{code}/relationship")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsync($"/api/suppliers/{code}/relationship", null)).StatusCode);

        // today's rule: another company is not affected by this company's tax number (per-company records), B creates its own
        var b = _factory.ClientFor(_tenant, _companyB, _user, Operator);
        Assert.Equal(HttpStatusCode.Created, (await b.PostAsJsonAsync("/api/suppliers", Upsert("Legacy Co", "tr", "TR-1"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/suppliers/{code}")).StatusCode);

        // same company, same tax number → 409 naming the existing code
        var duplicate = await a.PostAsJsonAsync("/api/suppliers", Upsert("Legacy Co 2", "tr", "TR-1"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var body = await duplicate.Content.ReadFromJsonAsync<JsonElement>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(code, body.GetProperty("errorDetails").GetProperty("supplierId").GetString());

        // BL-501 holds in this model too: two creates → two events, each in its transaction
        var events = await _factory.Database.GetCollection<CentralAuditOutboxRecord>(CentralAuditOutboxRecord.CollectionName)
            .Find(r => r.TenantId == _tenant).ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.True(e.WrittenInCommandTransaction));
    }

    private static object Upsert(string name, string country, string taxId) =>
        new { name, country, taxId, contacts = Array.Empty<object>(), sourceSystem = (string?)null, externalRef = (string?)null };

    private static async Task<JsonElement> Data(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"{(int)response.StatusCode} {response.RequestMessage?.RequestUri}: {text}");
        return JsonDocument.Parse(text).RootElement.GetProperty("data");
    }
}
