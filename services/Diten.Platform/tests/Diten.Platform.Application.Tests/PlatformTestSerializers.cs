using System.Runtime.CompilerServices;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Diten.Platform.Application.Tests;

/// <summary>
/// Registers the BSON serializers production registers, once, BEFORE any test in this assembly runs.
///
/// ⚠ THE BUG THIS FIXES IS TIMING, NOT SCOPE. Registration in the MongoDB driver is process-global by
/// design, and <c>Diten.Platform.Infrastructure.DependencyInjection</c> does exactly this at startup:
///
///     BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
///     BsonSerializer.RegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
///
/// So "make it per-client in tests instead" is the wrong answer: it would give tests a representation
/// production does not have, which is the defect measured in BL-280 — a suite that was green for years
/// against a schema and an encoding the real system never used.
///
/// The defect was that tests registered it LAZILY, from inside MongoIntegrationHarness, so the global state
/// existed only if a class that happened to use the harness had already run. A class building its own
/// MongoClient therefore encoded Guids one way or the other DEPENDING ON TEST ORDER.
///
/// <para>MEASURED 2026-08-27, and this is the GSKU team's blocker in one line: the two small
/// BusinessReferenceData Mongo classes pass 13/13 when run alone, and produce 11 failures when a
/// harness-using class shares the process. The failures read as missing data — "Assert.NotNull() Failure:
/// Value is null" — because the rows were written with one Guid encoding and queried with the other. Nothing
/// in the message points at serialization, which is why this cost a round to find.</para>
///
/// <para>A <c>[ModuleInitializer]</c> runs at assembly load, before the first test case. After that there is
/// no "whichever class ran first": every client in this process, however it was built, sees the production
/// representation.</para>
/// </summary>
internal static class PlatformTestSerializers
{
    [ModuleInitializer]
    internal static void Register()
    {
        // Try*, not Register*: RegisterSerializer throws if a serializer is already registered, and this must
        // be safe to call again from MongoIntegrationHarness for the benefit of anyone reading that file.
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        BsonSerializer.TryRegisterSerializer(new DecimalSerializer(BsonType.Decimal128));

        /*
         * ⚠ AND THE OTHER HALF, WHICH IS WHAT ACTUALLY BROKE. Production does BOTH of these:
         *
         *     BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));   // global
         *     mongoClientSettings.GuidRepresentation = GuidRepresentation.Standard;                 // per client
         *
         * A client left at the driver's legacy default while a Standard GuidSerializer is registered globally
         * does not fail loudly — it reads and writes Guids in a way that does not match, so a query by id
         * finds nothing and the test reports missing data. MEASURED 2026-08-27: the two small
         * BusinessReferenceData Mongo classes build their own MongoClient and set neither, and they passed
         * ONLY because nothing had registered the global serializer yet. Register it and they fail 11 —
         * which means they were green against an encoding production does not use (the BL-280 shape again).
         *
         * MongoDefaults.GuidRepresentation is the driver-wide default every MongoClientSettings inherits, so
         * setting it here gives every client in this assembly the production shape WITHOUT each test having
         * to remember — the same "do not rely on each call site remembering" argument as the freezer.
         */
        MongoDefaults.GuidRepresentation = GuidRepresentation.Standard;

        // ⚠ NOTE WHAT IS DELIBERATELY ABSENT: no DateTimeOffsetSerializer. Production does not register one
        // either, which is why every DateTimeOffset lands on disk as a BSON array [ticks, offsetMinutes].
        // Registering one here would make these tests pass against a representation that does not exist in
        // production — see BL-030.
    }
}
