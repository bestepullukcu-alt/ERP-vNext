using Diten.SupplyChainService.Application.Common;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
namespace Diten.SupplyChainService.Tests.Common;

/// <summary>
/// H2's "legal-entity failing path per gate", measured through a real host with an AuthService-shaped token.
/// LegalEntityMiddlewareWiringTests already prove each middleware ACTS on a refusal, but they call the middleware
/// directly with a hand-built principal; nothing proved the whole pipeline — authentication of the real token shape,
/// routing, the gate, the controller — refuses, and that the refusal writes nothing. These do, per gate.
/// </summary>
public static class LegalEntityGate
{
    /// <summary>The host's stub (registered by <see cref="TestHostLegalEntities.StubLegalEntityValidation"/>).</summary>
    public static StubLegalEntityScopeValidator Stub(IServiceProvider services) =>
        (StubLegalEntityScopeValidator)services.GetRequiredService<ILegalEntityScopeValidator>();

    /// <summary>Every document of <paramref name="tenant"/> in every collection of the database, whatever its Guid form.</summary>
    public static async Task<long> TenantDocuments(IMongoDatabase db, Guid tenant)
    {
        var filter = new BsonDocument("$or", new BsonArray
        {
            new BsonDocument("TenantId", tenant.ToString()),
            new BsonDocument("TenantId", new BsonBinaryData(tenant, GuidRepresentation.Standard))
        });
        long total = 0;
        foreach (var name in await (await db.ListCollectionNamesAsync()).ToListAsync())
            total += await db.GetCollection<BsonDocument>(name).CountDocumentsAsync(filter);
        return total;
    }

    /// <summary>
    /// Sets MDM's answer to <paramref name="outcome"/> for one request, asserts the status, that the gate asked MDM,
    /// and that the tenant's document count did not move. The stub is put back to Valid even when an assertion fails.
    /// </summary>
    public static async Task AssertRefusedWithoutWrites(StubLegalEntityScopeValidator stub, IMongoDatabase db, Guid tenant,
        LegalEntityScopeOutcome outcome, int expectedStatus, Func<Task<int>> send)
    {
        var before = await TenantDocuments(db, tenant);
        int asked;
        lock (stub.Calls) asked = stub.Calls.Count;
        stub.Outcome = outcome;
        int status;
        try { status = await send(); }
        finally { stub.Outcome = LegalEntityScopeOutcome.Valid; }
        Assert.Equal(expectedStatus, status);
        lock (stub.Calls) Assert.True(stub.Calls.Count > asked, "The gate answered without asking MDM.");
        Assert.Equal(before, await TenantDocuments(db, tenant));
    }
}
