using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Schema;

/// <summary>
/// The single owner of the platform's Mongo schema: which collections exist, which profile each belongs to,
/// and the exact index models each carries.
///
/// WHY THIS EXISTS. Production used to build the whole schema from one 1,850-line method, and every
/// integration test called that same method against a database of its own — 82 collections and 218 index
/// models per test class, whatever the test actually touched. On macOS that walks past the 10,240
/// open-files-per-process limit and mongod kills itself with fassert; when it dies mid-run the disposal that
/// drops those databases never executes, so the next run starts on the wreckage of the last.
///
/// The fix is NOT a second, smaller schema for tests. A second schema is how a test goes green against
/// indexes production does not have — and a missing index in Mongo raises NO error, the query simply runs
/// unindexed. So there is one manifest, and a profile is a SUBSET of it, never a variant of it.
///
/// ⚠ PRODUCTION READS THE UNION. <see cref="All"/> is every profile, and
/// <c>PlatformSchemaManifestTests.ProductionPathBuildsTheUnionOfEveryProfile</c> pins that: adding a
/// profile without adding it to the union would silently narrow what production builds.
/// </summary>
public static partial class PlatformSchemaManifest
{
    private static SchemaCollection Collection<TDocument>(
        SchemaProfile profile,
        string name,
        Func<CreateIndexModel<TDocument>[]> models,
        string? failureHint = null)
        => new SchemaCollection<TDocument>(name, profile, models, failureHint);

    private static readonly Lazy<IReadOnlyList<SchemaCollection>> AllCollections =
        new(() => CoreCollections
            .Concat(AccessGovernanceCollections)
            .Concat(BusinessReferenceDataCollections)
            .Concat(EventingCollections)
            .Concat(NotificationCollections)
            .Concat(OrganizationCollections)
            .Concat(WorkflowWorkCenterCollections)
            .Concat(DocumentManagementCollections)
            .ToArray());

    /// <summary>Every collection in every profile — what the production path builds.</summary>
    public static IReadOnlyList<SchemaCollection> All => AllCollections.Value;

    /// <summary>Every profile the manifest knows about.</summary>
    public static IReadOnlyList<SchemaProfile> KnownProfiles { get; } =
        Enum.GetValues<SchemaProfile>().OrderBy(p => (int)p).ToArray();

    /// <summary>
    /// The collections belonging to the requested profiles.
    ///
    /// ⚠ FAIL-CLOSED, DELIBERATELY. No profiles means no schema, and a test that silently got no schema
    /// would fail later, somewhere else, as a confusing query result rather than as "you asked for nothing".
    /// An undefined enum value is rejected for the same reason: a cast integer must not quietly resolve to
    /// an empty slice.
    /// </summary>
    public static IReadOnlyList<SchemaCollection> For(params SchemaProfile[] profiles)
    {
        if (profiles is null || profiles.Length == 0)
        {
            throw new ArgumentException(
                "No schema profile requested. Name the profiles this test needs — an empty request builds "
                + "nothing, and the failure would surface later as a missing index rather than here.",
                nameof(profiles));
        }

        var unknown = profiles.Where(p => !Enum.IsDefined(p)).ToArray();
        if (unknown.Length > 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(profiles),
                $"Unknown schema profile(s): {string.Join(", ", unknown.Select(p => (int)p))}. "
                + $"Known profiles: {string.Join(", ", KnownProfiles)}.");
        }

        var wanted = profiles.Distinct().ToHashSet();
        return All.Where(c => wanted.Contains(c.Profile)).ToArray();
    }

    /// <summary>Builds the requested profiles' collections and indexes into <paramref name="database"/>.</summary>
    public static async Task ApplyAsync(
        IMongoDatabase database,
        SchemaProfile[] profiles,
        CancellationToken cancellationToken = default)
    {
        foreach (var collection in For(profiles))
        {
            await collection.ApplyAsync(database, cancellationToken);
        }
    }

    /// <summary>Builds the ENTIRE schema — the production path.</summary>
    public static async Task ApplyAllAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
    {
        foreach (var collection in All)
        {
            await collection.ApplyAsync(database, cancellationToken);
        }
    }
}
