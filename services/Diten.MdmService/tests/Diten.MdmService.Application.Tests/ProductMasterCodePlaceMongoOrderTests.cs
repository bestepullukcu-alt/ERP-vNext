using Diten.MdmService.Application.Tests.Audit;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// BL-603 (WP-CT-LANE-DEBT-01) — THE PLACEHOLDER OF AN UNREADABLE PRODUCT-MASTER ROW STANDS WHERE THE REAL STORE'S $sort PUT
/// IT, for every <see cref="BsonType"/>. S4 FIX4 orders a placeholder by its code's BSON type bracket and then its code
/// (<c>ProductMasterReadStore.CodePlace</c>); this theory measures it against the store itself, not against a hand list: for each
/// type, a document whose CanonicalCode is a value of that type is sorted by a disposable mongod among reference documents
/// (code missing, null, numbers, text, a document, a boolean), and wherever the placeholder order DECIDES between two rows
/// (their placeholder keys differ) it must decide as the store did. Inside one bracket the store's own order is deliberately
/// not reproduced (CodePlace says so) — equal keys are not compared.
/// The test-owned standalone mongod of <see cref="AuditIntentTemporalMongoFixture"/>; a fresh collection per case.
/// </summary>
public sealed class ProductMasterCodePlaceMongoOrderTests(AuditIntentTemporalMongoFixture fixture)
    : IClassFixture<AuditIntentTemporalMongoFixture>
{
    private const string Damaged = "damaged";

    private static readonly (string Id, BsonValue? Code)[] References =
    [
        ("ref-missing", null),
        ("ref-null", BsonNull.Value),
        ("ref-int", new BsonInt32(5)),
        ("ref-double", new BsonDouble(2.5)),
        ("ref-text-A", new BsonString("A")),
        ("ref-text-M", new BsonString("M")),
        ("ref-text-Z", new BsonString("Z")),
        ("ref-document", new BsonDocument("x", 1)),
        ("ref-boolean", BsonBoolean.True)
    ];

    public static TheoryData<BsonType> EveryStorableType()
    {
        var data = new TheoryData<BsonType>();
        foreach (var type in Enum.GetValues<BsonType>())
            if (type != BsonType.EndOfDocument) data.Add(type);
        return data;
    }

    private static BsonValue Sample(BsonType type) => type switch
    {
        BsonType.Double => new BsonDouble(1.5),
        BsonType.String => new BsonString("N"),
        BsonType.Document => new BsonDocument("a", 1),
        BsonType.Array => new BsonArray { "N" },
        BsonType.Binary => new BsonBinaryData([1, 2, 3]),
        BsonType.Undefined => BsonUndefined.Value,
        BsonType.ObjectId => ObjectId.GenerateNewId(),
        BsonType.Boolean => BsonBoolean.False,
        BsonType.DateTime => new BsonDateTime(new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc)),
        BsonType.Null => BsonNull.Value,
        BsonType.RegularExpression => new BsonRegularExpression("a"),
        BsonType.JavaScript => new BsonJavaScript("x"),
        BsonType.Symbol => BsonSymbolTable.Lookup("N"),
        BsonType.JavaScriptWithScope => new BsonJavaScriptWithScope("x", new BsonDocument("y", 1)),
        BsonType.Int32 => new BsonInt32(7),
        BsonType.Timestamp => new BsonTimestamp(1, 1),
        BsonType.Int64 => new BsonInt64(9),
        BsonType.Decimal128 => new BsonDecimal128(1m),
        BsonType.MinKey => BsonMinKey.Value,
        BsonType.MaxKey => BsonMaxKey.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "a BsonType without a sample: add one")
    };

    private static BsonDocument Row(string id, BsonValue? code)
    {
        var row = new BsonDocument("_id", id);
        if (code is not null) row["CanonicalCode"] = code;
        return row;
    }

    private static int Compare((int Bracket, string? Code) left, (int Bracket, string? Code) right)
    {
        var bracket = left.Bracket.CompareTo(right.Bracket);
        return bracket != 0 ? bracket : StringComparer.Ordinal.Compare(left.Code, right.Code);
    }

    [Theory]
    [MemberData(nameof(EveryStorableType))]
    public async Task A_placeholder_stands_where_the_store_sorts_a_code_of_this_type(BsonType type)
    {
        var database = new MongoClient(fixture.StandaloneConnectionString).GetDatabase("diten_mdm_bl603_code_place_itest");
        var collection = database.GetCollection<BsonDocument>($"codes_{type}_{Guid.NewGuid():N}");
        var rows = References.Select(r => Row(r.Id, r.Code)).Append(Row(Damaged, Sample(type))).ToList();
        await collection.InsertManyAsync(rows);

        var sorted = await collection.Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Ascending("CanonicalCode").Ascending("_id")).ToListAsync();
        var storeOrder = sorted.Select(row => row["_id"].AsString).ToList();
        var placeholder = ProductMasterReadStore.CodePlace(rows.Last());

        var decided = 0;
        foreach (var reference in rows.SkipLast(1))
        {
            var referencePlace = ProductMasterReadStore.CodePlace(reference);
            var byPlaceholder = Compare(placeholder, referencePlace);
            if (byPlaceholder == 0) continue; // the same bracket and code: an order the placeholder does not decide
            decided++;
            var byStore = storeOrder.IndexOf(Damaged).CompareTo(storeOrder.IndexOf(reference["_id"].AsString));
            Assert.True(Math.Sign(byPlaceholder) == Math.Sign(byStore),
                $"{type}: the placeholder ({placeholder.Bracket}, {placeholder.Code ?? "-"}) is put "
                + (byPlaceholder < 0 ? "before" : "after") + $" {reference["_id"]} ({referencePlace.Bracket}, {referencePlace.Code ?? "-"}),"
                + $" the store sorts it " + (byStore < 0 ? "before" : "after") + $". Store order: {string.Join(", ", storeOrder)}");
        }

        Assert.True(decided > 0, $"{type}: no reference row was ordered against the placeholder");
        await database.DropCollectionAsync(collection.CollectionNamespace.CollectionName);
    }

    [Fact]
    public void MinKey_and_undefined_first_MaxKey_last_and_symbol_sorts_with_text()
    {
        Assert.Equal((ProductMasterCodeBracket.First, (string?)null), ProductMasterReadStore.Place(BsonMinKey.Value));
        // Found by the theory above on the real store: the legacy undefined sorts before missing / null, with MinKey.
        Assert.Equal((ProductMasterCodeBracket.First, (string?)null), ProductMasterReadStore.Place(BsonUndefined.Value));
        Assert.Equal((ProductMasterCodeBracket.Last, (string?)null), ProductMasterReadStore.Place(BsonMaxKey.Value));
        Assert.Equal((ProductMasterCodeBracket.Text, (string?)"N"), ProductMasterReadStore.Place(BsonSymbolTable.Lookup("N")));
        Assert.Equal((ProductMasterCodeBracket.First, (string?)null), ProductMasterReadStore.Place(new BsonArray { "N", BsonMinKey.Value }));
    }
}
