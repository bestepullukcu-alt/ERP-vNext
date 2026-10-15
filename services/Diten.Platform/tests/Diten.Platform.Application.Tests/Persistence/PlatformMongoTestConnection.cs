using Diten.Platform.Tests.Shared;
using MongoDB.Driver;

namespace Diten.Platform.Application.Tests.Persistence;

/// <summary>
/// The one place the Platform Mongo tests take their connection string from (Q131a).
///
/// The URI comes from <c>DITEN_PLATFORM_TEST_MONGO_URI</c> when the lane sets it for its own slot
/// (<c>scripts/test-env/mvp6-test-mongo-env.sh</c>); a remote host, a URI with credentials or an operational MongoDB port
/// fails the test with a clear message instead of quietly using the developer's MongoDB (Q121b D2: 49 test databases
/// were created there). Main sync (CT decision C): when it is NOT set, the test process starts its own mongod from
/// <c>DITEN_TEST_MONGOD</c> (CI sets it); with neither, red by name. There is still no default and no fallback port.
/// </summary>
internal static class PlatformMongoTestConnection
{
    public const string EnvironmentVariableName = PlatformTestMongo.UriVariable;

    /// <summary>
    /// WP-CT-LANE-MAIN-SYNC-01 (CT decision C): the address of this test process's Mongo — DITEN_PLATFORM_TEST_MONGO_URI
    /// if given (main's check, unchanged), else a mongod this process starts from DITEN_TEST_MONGOD, else red by name.
    /// Never 27017. See <see cref="PlatformTestMongo"/>.
    /// </summary>
    public static Task<string> RequireConnectionStringAsync() => PlatformTestMongo.ConnectionStringAsync();

    /// <summary>
    /// Pure check, so its tests never change the process environment (which parallel test classes share).
    /// Messages never repeat the URI. Main's Q131a rule — the shared source holds it now.
    /// </summary>
    internal static string Validate(string? connectionString) => PlatformTestMongo.ValidateUri(connectionString);
}
