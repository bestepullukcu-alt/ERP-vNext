namespace Diten.Platform.Infrastructure.Persistence.Schema;

/// <summary>
/// A profile's LOGICAL schema budget: how many collections and how many indexes it is allowed to carry.
///
/// ⚠ LOGICAL, NOT PHYSICAL. The failure this whole round exists to stop is a file-descriptor exhaustion, so
/// the tempting acceptance criterion is "how many files did the run create". That number is not portable —
/// it moves with the Mongo storage engine, the version, and the operating system, and a test pinned to it
/// would be red on one machine and green on another for reasons that have nothing to do with the schema.
/// Collections and indexes are what the manifest actually controls, and they are the same number everywhere.
///
/// ⚠ A BUDGET IS DECLARED ONLY WHERE AN OWNER GAVE A NUMBER. Inventing a ceiling for a profile nobody has
/// sized would produce a test that goes red on the next legitimate index and teaches the reader to raise the
/// number instead of looking at it. Profiles without an entry here are MEASURED and reported, not pinned.
/// </summary>
public sealed record SchemaProfileBudget(SchemaProfile Profile, int MaxCollections, int MaxLogicalIndexes)
{
    /// <summary>
    /// Business Reference Data — the numbers the GSKU owners set (2026-08-26, index ceiling raised
    /// 2026-08-28): at most 8 business collections and at most 19 real indexes, counting the implicit
    /// <c>_id</c> on each collection.
    ///
    /// ⚠ THE INDEX CEILING WENT 18 → 19 ON AN OWNER DECISION, NOT TO FIT A CHANGE. That distinction is the
    /// whole point of the header above. BL-279 measured business_reference_data_validation_results and found
    /// the one read this profile could not serve — 250 documents examined and a blocking SORT to return 25 —
    /// and BL-298 took the measurement back to the owners who set 18 rather than editing the number to make
    /// the build green. They raised the INDEX ceiling by exactly one and left the COLLECTION ceiling at 8.
    /// The next index is in the same position this one was: it goes to the owners with a measurement, and
    /// PlatformSchemaManifestTests.TheDeclaredBudgetsAreTheNumbersTheOwnersApproved is what forces that.
    /// </summary>
    public static readonly SchemaProfileBudget BusinessReferenceData =
        new(SchemaProfile.BusinessReferenceData, MaxCollections: 8, MaxLogicalIndexes: 19);

    public static IReadOnlyList<SchemaProfileBudget> Declared { get; } = new[] { BusinessReferenceData };

    public static SchemaProfileBudget? ForOrDefault(SchemaProfile profile)
        => Declared.FirstOrDefault(b => b.Profile == profile);
}
