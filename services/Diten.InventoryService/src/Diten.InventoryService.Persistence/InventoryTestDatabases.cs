namespace Diten.InventoryService.Persistence;

/// <summary>
/// DB-010 test-database naming as practised in this repository: a test database carries a <c>test</c>, <c>tests</c> or
/// <c>itest</c> segment in its underscore-separated name (<c>diten_inventory_s1_tests</c>, <c>diten_platform_itest</c>).
/// Used by the startup gate (FU-S2-01) to tell a test database, where unscoped residue is tolerated with a warning, from
/// every other database, where it stops the service.
/// </summary>
public static class InventoryTestDatabases
{
    private static readonly HashSet<string> TestSegments = new(StringComparer.OrdinalIgnoreCase) { "test", "tests", "itest" };

    public static bool IsTestDatabase(string databaseName) =>
        !string.IsNullOrWhiteSpace(databaseName)
        && databaseName.Split('_', StringSplitOptions.RemoveEmptyEntries).Any(TestSegments.Contains);
}
