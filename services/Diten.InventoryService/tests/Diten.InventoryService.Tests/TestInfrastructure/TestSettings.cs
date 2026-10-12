namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// Process-wide test configuration. Supplied to the host through ENVIRONMENT VARIABLES because Program.cs reads
/// secrets and upstream settings while the builder is still being assembled — environment variables are the one
/// source that is both visible that early and higher-priority than the Api's appsettings.json.
///
/// DB-010 (mongo-indexing.md §Test Databases): ONE fixed database for the whole suite, isolation by a fresh TenantId
/// per test — never a database per run. The name is dedicated to these tests (never the dev / mock / prod database).
/// </summary>
public static class TestSettings
{
    public const string DatabaseName = "diten_inventory_s1_tests";

    public const string Issuer = "diten-auth-service";

    public const string Audience = "diten-erp";

    /// <summary>Signing secret minted per test process; never a checked-in value.</summary>
    public static readonly string JwtSecret = "inventory-tests-" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    /// <summary>
    /// FU-0173-TEST-01 (WP-0173-PM1): <c>INVENTORY_TEST_MONGO</c> when set (refused if it names port 27017 — the developer's
    /// live mongod); otherwise the suite starts its OWN single-node replica set (<see cref="TestMongo"/>). There is no default
    /// address any more.
    /// </summary>
    public static string MongoConnectionString => TestMongo.ConnectionString;

    private static readonly object Gate = new();
    private static bool _applied;

    public static void Apply()
    {
        lock (Gate)
        {
            if (_applied)
            {
                return;
            }

            Environment.SetEnvironmentVariable("Mongo__ConnectionString", MongoConnectionString);
            Environment.SetEnvironmentVariable("Mongo__DatabaseName", DatabaseName);
            Environment.SetEnvironmentVariable("JwtSettings__Secret", JwtSecret);
            Environment.SetEnvironmentVariable("JwtSettings__Issuer", Issuer);
            Environment.SetEnvironmentVariable("JwtSettings__Audience", Audience);
            // Upstream masters are replaced by in-process stubs in API tests; the URLs only have to be well-formed.
            Environment.SetEnvironmentVariable("Inventory__Upstream__ProductMasterBaseUrl", "http://127.0.0.1:9/");
            Environment.SetEnvironmentVariable("Inventory__Upstream__LocationMasterBaseUrl", "http://127.0.0.1:9/");
            Environment.SetEnvironmentVariable("Inventory__Upstream__LegalEntityMasterBaseUrl", "http://127.0.0.1:9/");
            // MOD-0173-FU02: a closed port — the stub replaces it, except in RealOrgUnitClientApiFactory (measured 503 path).
            Environment.SetEnvironmentVariable("Inventory__Upstream__OrgUnitDirectoryBaseUrl", "http://127.0.0.1:9/");
            // S2: the periodic expiry sweeper is off in API tests so the in-transaction expiry path is measured alone;
            // the sweeper itself is driven directly through IReservationExpirySweeper (AC-R7).
            Environment.SetEnvironmentVariable("Inventory__Reservations__SweeperEnabled", "false");
            // MOD-0174: the periodic lot expiry sweeper is off in API tests; it is driven directly through ILotExpirySweeper.
            Environment.SetEnvironmentVariable("Inventory__Trace__SweeperEnabled", "false");
            // MOD-0175: the periodic intake sweep is off in API tests (it scans every tenant of the shared database); it is
            // driven directly through QuarantineIntakeSweeper, limited to the test's own tenant.
            Environment.SetEnvironmentVariable("Inventory__Quarantine__IntakeSweeperEnabled", "false");
            // MOD-0175 Q4: the retest hold sweeper (start + periodic) scans every tenant too; tests drive RetestHoldSweeper directly.
            Environment.SetEnvironmentVariable("Inventory__Quarantine__RetestSweeperEnabled", "false");
            _applied = true;
        }
    }
}
