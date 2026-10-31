namespace Diten.SupplyChainService.Tests.SupplierPerformance;
/// <summary>
/// MOD-0147 DL-1 tests run on the SupplyChain suite's owned replica set (MOD0183_TEST_MONGO, set by
/// scripts/test-env/supplychain-ci-mongo.sh; the name is that script's, the variable is not MOD-0183's alone). No fallback port.
/// Database shape (DB-010): a fixed database name with a new tenant per test. <see cref="UniqueIndexDatabase"/> is the one fixed name
/// dropped before a race test, so the schema gate builds its unique indexes again and an index sabotage is measured.
/// </summary>
internal static class SupplierPerformanceMongo
{
    public const string SharedDatabase = "diten_mod0147_tests";
    public const string StorageDatabase = "diten_mod0147_storage";
    public const string UniqueIndexDatabase = "diten_mod0147_unique";
    public static string Connection => Environment.GetEnvironmentVariable("MOD0183_TEST_MONGO")
        ?? throw new InvalidOperationException("MOD0183_TEST_MONGO must point to an isolated replica set.");
}
