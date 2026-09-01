using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

// THE LIVE MEASUREMENT BEHIND BL-030's SECOND HALF.
//
// No DateTimeOffsetSerializer is registered — not in production (Infrastructure.DependencyInjection), not in
// this assembly (PlatformTestSerializers says so in as many words). So the driver stores every DateTimeOffset
// as a BSON ARRAY: [ticks, offsetMinutes].
//
// MongoDB compares arrays element-wise by EXTREMUM, and which extremum it picks depends on the direction:
//   • ASCENDING  uses the SMALLEST element of the array — which for [ticks, offsetMinutes] is the OFFSET
//     (a number in -300..+180), never the ticks (~6.4e17). Ascending therefore sorts BY TIME ZONE.
//   • DESCENDING uses the LARGEST element — which IS the ticks. Descending is correct BY ACCIDENT.
//
// ⚠ AND THE SECOND ACCIDENT THAT HIDES IT: in dev every row is written at +03:00, so every offset element is
// identical and the ascending comparison falls through to a stable tie. The bug is INVISIBLE until rows with
// DIFFERENT offsets coexist — which is exactly what this test writes and nothing else in the suite does.
//
// ⚠ This is a property of the DATA REPRESENTATION, not of any index: an index-free COLLSCAN sorts the same
// arrays the same wrong way. Adding an index does not help and removing one does not hurt.
public sealed class DateTimeOffsetAscendingSortMongoTests : IAsyncLifetime
{
    // A scratch database of its own, under a FIXED name so it can never accumulate across runs (the rule
    // MongoIntegrationHarness arrived at). Dropped on the way in AND on the way out.
    private const string ScratchDatabaseName = "diten_bl030_dtosort_proof";

    private readonly IMongoClient _client = new MongoClient(MongoIntegrationHarness.ConnectionString);

    private IMongoCollection<Row> _rows = null!;

    // Four rows whose TRUE chronological order is v3 < v1 < v2 < v4, written across three different offsets
    // so that ordering by offset and ordering by instant disagree.
    private static readonly (string Label, DateTimeOffset At)[] Seed =
    [
        ("v1", new DateTimeOffset(2026, 3, 1, 12, 00, 0, TimeSpan.FromHours(3))),   // 09:00Z  offset +180
        ("v2", new DateTimeOffset(2026, 3, 1, 10, 00, 0, TimeSpan.Zero)),           // 10:00Z  offset    0
        ("v3", new DateTimeOffset(2026, 3, 1, 3, 00, 0, TimeSpan.FromHours(-5))),   // 08:00Z  offset -300
        ("v4", new DateTimeOffset(2026, 3, 1, 14, 00, 0, TimeSpan.FromHours(3))),   // 11:00Z  offset +180
    ];

    public async Task InitializeAsync()
    {
        await _client.DropDatabaseAsync(ScratchDatabaseName);
        _rows = _client.GetDatabase(ScratchDatabaseName).GetCollection<Row>("rows");
        await _rows.InsertManyAsync(Seed.Select(s => new Row { Label = s.Label, OccurredAt = s.At }));
    }

    public async Task DisposeAsync() => await _client.DropDatabaseAsync(ScratchDatabaseName);

    // The representation itself: an ARRAY, not a date. Everything else follows from this one fact.
    [Fact]
    public async Task DateTimeOffset_is_stored_as_a_two_element_bson_array()
    {
        var raw = await _client.GetDatabase(ScratchDatabaseName)
            .GetCollection<BsonDocument>("rows")
            .Find(Builders<BsonDocument>.Filter.Eq("Label", "v3"))
            .FirstAsync();

        var stored = raw["OccurredAt"];

        Assert.True(
            stored.IsBsonArray,
            $"BL-030 assumes DateTimeOffset lands as a BSON array; it landed as {stored.BsonType}. If a "
            + "DateTimeOffsetSerializer has been registered, this whole guard and the descending-only sorts "
            + "it protects can be revisited.");
        Assert.Equal(2, stored.AsBsonArray.Count);

        // Element 1 is the offset in minutes: -300 for the -05:00 row. It is SMALLER than the ticks, and that
        // single fact is the entire bug.
        Assert.Equal(-300, stored.AsBsonArray[1].ToInt32());
        Assert.True(stored.AsBsonArray[0].ToInt64() > stored.AsBsonArray[1].ToInt32());
    }

    // ⚠ THE BUG, MEASURED. Ascending does not return chronological order; it returns time-zone order.
    // This test asserts the WRONG order on purpose — it characterises the server we actually run against.
    // If it ever fails, the driver or the representation changed and the descending-only fix can be reopened.
    [Fact]
    public async Task Ascending_sort_orders_by_time_zone_not_by_instant()
    {
        var ascending = await _rows.Find(FilterDefinition<Row>.Empty)
            .SortBy(x => x.OccurredAt)
            .ToListAsync();

        var actual = ascending.Select(x => x.Label).ToArray();

        // Chronological would be v3, v1, v2, v4. What Mongo returns is offset order: -300, 0, +180, +180.
        Assert.Equal(["v3", "v2", "v1", "v4"], actual);
        Assert.NotEqual(["v3", "v1", "v2", "v4"], actual);
    }

    // ⚠ THE ASSUMPTION THAT DID NOT SURVIVE MEASUREMENT, AND IT IS THE IMPORTANT ONE.
    //
    // BL-030 records descending as "accidentally correct", on the reasoning that descending compares the
    // LARGEST array element and that element is the ticks. The first half is right. The second half is not:
    // the ticks the driver writes are LOCAL WALL-CLOCK ticks, not UTC ticks. Measured — v1 is 12:00+03:00 and
    // lands as [639079632000000000, 180], which is exactly DateTime(2026-03-01 12:00).Ticks, the wall-clock
    // reading, NOT the 09:00Z instant.
    //
    // So descending orders by what the clock on the wall said, ignoring which wall it was. It is BETTER than
    // ascending — wall-clock order is at least within one offset-spread of the truth, where time-zone order
    // is unrelated to it — but it is NOT chronological, and "switch everything to descending" therefore does
    // not fix ordering. It narrows the error.
    //
    // Confirmed against real data, not just this fixture: on diten_personalization_dev's task_items.DueAt
    // (22 rows at offset 0, 144 at +180) a descending query inverted at row 14, returning 2026-09-29 21:00Z
    // ahead of 2026-09-30 00:00Z.
    [Fact]
    public async Task Descending_sort_orders_by_wall_clock_reading_not_by_instant()
    {
        var descending = await _rows.Find(FilterDefinition<Row>.Empty)
            .SortByDescending(x => x.OccurredAt)
            .ToListAsync();

        var actual = descending.Select(x => x.Label).ToArray();

        // True reverse-chronological is v4, v2, v1, v3 (11:00Z, 10:00Z, 09:00Z, 08:00Z).
        // What comes back is wall-clock order: 14:00, 12:00, 10:00, 03:00.
        Assert.Equal(["v4", "v1", "v2", "v3"], actual);
        Assert.NotEqual(["v4", "v2", "v1", "v3"], actual);
    }

    // ⚠ WHY THIS WENT UNSEEN, AND WHY THE ANSWER IS WORSE THAN "IT WAS FINE IN DEV".
    //
    // Write the same four instants at ONE shared offset and every array's smallest element is the same number,
    // so the ascending comparison TIES on every pair and the server falls back to natural order — which is
    // INSERTION order, not chronological order.
    //
    // Measured: the rows come back v1, v2, v3, v4, the order they were inserted in, while chronological is
    // v3, v1, v2, v4. So ascending is not "correct when offsets are uniform"; it is merely ARBITRARY in a way
    // that usually flatters the reader, because rows are typically inserted in roughly the order they happen.
    // That coincidence is the whole reason 26 broken call sites looked healthy for months.
    [Fact]
    public async Task Ascending_falls_back_to_insertion_order_when_every_row_shares_one_offset()
    {
        var uniform = _client.GetDatabase(ScratchDatabaseName).GetCollection<Row>("uniform");

        // Deliberately inserted in an order that is NOT chronological, so insertion order and true order
        // cannot be confused for one another.
        await uniform.InsertManyAsync(Seed.Select(s => new Row
        {
            Label = s.Label,
            OccurredAt = s.At.ToOffset(TimeSpan.FromHours(3)),
        }));

        var ascending = await uniform.Find(FilterDefinition<Row>.Empty)
            .SortBy(x => x.OccurredAt)
            .ToListAsync();

        var actual = ascending.Select(x => x.Label).ToArray();

        Assert.Equal(["v1", "v2", "v3", "v4"], actual);
        Assert.NotEqual(["v3", "v1", "v2", "v4"], actual);
    }

    private sealed class Row
    {
        [BsonId]
        public ObjectId Id { get; set; }

        public string Label { get; set; } = string.Empty;

        public DateTimeOffset OccurredAt { get; set; }
    }
}
