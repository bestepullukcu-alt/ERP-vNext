namespace Diten.SupplyChainService.Tests.SupplierPerformance;
/// <summary>
/// MOD-0147 DL-1 tests run on the SupplyChain suite's owned replica set (MOD0183_TEST_MONGO, set by
/// scripts/test-env/supplychain-ci-mongo.sh) in a database of their own that each test drops. No fallback port.
/// </summary>
internal static class SupplierPerformanceMongo
{
    public static string Connection => Environment.GetEnvironmentVariable("MOD0183_TEST_MONGO")
        ?? throw new InvalidOperationException("MOD0183_TEST_MONGO must point to an isolated replica set.");
}
