using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

/*
 * BL-482 — THE PRECONDITION EVERY "THE REAL UNIQUE INDEX REFUSES A DUPLICATE" TEST CHECKS BEFORE IT INSERTS ONE.
 *
 * Such a test writes the duplicate on purpose. If the index is missing at that moment, the write is ACCEPTED: the
 * test goes red (good), but the duplicate stays in the shared test database (bad), and from then on every schema
 * build of that collection fails with E11000 — measured 2026-09-30, reproduced 2026-10-01. So the test first reads
 * the collection's live indexes and fails with a message naming the missing index, BEFORE writing anything. A test
 * that cannot prove its premise must not leave its evidence behind.
 *
 * The check is a pure function (Check) so its failure modes are provable without a live server; RequireAsync is
 * the thin live wrapper the tests call.
 */
public static class UniqueIndexPrecondition
{
    /// <summary>
    /// Fails the calling test — before it writes anything — unless <paramref name="indexName"/> exists on the
    /// collection AND is unique.
    /// </summary>
    public static async Task RequireAsync<TDocument>(IMongoCollection<TDocument> collection, string indexName)
    {
        var indexes = await (await collection.Indexes.ListAsync()).ToListAsync();
        var failure = Check(indexes, collection.CollectionNamespace.FullName, indexName);
        if (failure is not null)
        {
            Assert.Fail(failure);
        }
    }

    /// <summary>Null when the index exists and is unique; otherwise the message the test fails with.</summary>
    public static string? Check(IEnumerable<BsonDocument> indexes, string collectionName, string indexName)
    {
        var index = indexes.FirstOrDefault(i =>
            i.TryGetValue("name", out var name) && name.IsString && name.AsString == indexName);

        if (index is null)
        {
            return $"BL-482: the unique index '{indexName}' is MISSING on '{collectionName}'. This test inserts a "
                + "duplicate to prove the index refuses it; without the index the duplicate would be ACCEPTED and "
                + "left behind, breaking every later schema build of this collection. Nothing was written. Fix the "
                + "schema build (see the harness output above), not this test.";
        }

        var unique = index.TryGetValue("unique", out var flag) && flag.ToBoolean();
        if (!unique)
        {
            return $"BL-482: the index '{indexName}' on '{collectionName}' exists but is NOT unique, so it cannot "
                + "refuse a duplicate. Nothing was written.";
        }

        return null;
    }
}
