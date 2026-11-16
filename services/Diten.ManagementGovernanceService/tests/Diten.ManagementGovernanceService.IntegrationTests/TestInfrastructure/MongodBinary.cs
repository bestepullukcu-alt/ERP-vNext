namespace Diten.ManagementGovernanceService.IntegrationTests;

/// <summary>
/// Where the integration tests' own mongod comes from (owner 'Evet, düzelt' 2026-11-06). Three fixtures used a fixed
/// /opt/homebrew/bin/mongod. That path exists only on a Mac, so CI's Linux runner never started one and the project
/// never ran in CI. The order is DITEN_TEST_MONGOD first (CI's install step sets it), then the usual Mac / Linux paths.
/// When none exists, it returns the variable's value or the Mac path, so each fixture's own "mongod is unavailable"
/// check still fails closed as before. Each fixture still owns its process, port and data directory.
/// </summary>
internal static class MongodBinary
{
    private static readonly string[] Candidates =
        ["/opt/homebrew/bin/mongod", "/usr/local/bin/mongod", "/opt/local/bin/mongod", "/usr/bin/mongod"];

    public static string Location
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("DITEN_TEST_MONGOD");
            var found = new[] { configured }.Concat(Candidates)
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
            return found ?? (string.IsNullOrWhiteSpace(configured) ? Candidates[0] : configured);
        }
    }
}
