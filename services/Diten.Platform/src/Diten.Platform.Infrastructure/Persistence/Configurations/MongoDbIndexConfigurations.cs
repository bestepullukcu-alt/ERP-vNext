using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// The production schema entry point. Unchanged signature, unchanged meaning: after this returns, the
/// database carries the ENTIRE platform schema and both startup data jobs have run.
///
/// WHAT MOVED, AND WHY. This used to be one 1,850-line method that declared 82 collections, built 218 index
/// models, dropped 13 superseded indexes and ran two DATA jobs — all under a name that promised only
/// indexes. Every Mongo integration test called it against a database of its own, so a test touching four
/// collections paid for all 82; that is what exhausts the per-process file-descriptor limit and makes
/// mongod fassert. The schema is now declared once in <see cref="PlatformSchemaManifest"/>, which a test can
/// ask for BY PROFILE, and the imperative startup steps live in <see cref="PlatformSchemaMigrations"/>.
///
/// ⚠ THIS PATH MUST STAY THE UNION. It builds every profile, not a list someone maintains by hand — see
/// <see cref="PlatformSchemaManifest.All"/>. A new profile that was not added to the union would narrow what
/// production builds while every test stayed green, because a missing index in Mongo raises no error: the
/// query just runs unindexed.
/// </summary>
public static class MongoDbIndexConfigurations
{
    public static async Task EnsureIndexesAsync(IMongoDatabase database)
    {
        // Drops and data repairs FIRST: an index whose definition changed must not be rebuilt under its old
        // options, and both data jobs are startup obligations that predate the manifest.
        await PlatformSchemaMigrations.RunAsync(database);

        await PlatformSchemaManifest.ApplyAllAsync(database);
    }
}
