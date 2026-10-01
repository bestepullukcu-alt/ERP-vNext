using System.Text.RegularExpressions;

namespace TenantArchitecture.ArchitectureTests;

public class PersistenceBoundaryTests
{
    [Fact]
    public void MongoClientInstantiation_MustStayInAllowedBootstrapFiles()
    {
        var repoRoot = FindRepoRoot();
        var serviceFiles = Directory.GetFiles(Path.Combine(repoRoot, "services"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("/obj/") && !path.Contains("/bin/"))
            .ToArray();

        var offenders = new List<string>();
        foreach (var file in serviceFiles)
        {
            var normalized = file.Replace('\\', '/');
            if (normalized.Contains("/tests/"))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            if (!content.Contains("new MongoClient("))
            {
                continue;
            }

            var allowed = normalized.Contains(".Persistence/")
                          || normalized.Contains("Infrastructure/DependencyInjection.cs")
                          || normalized.Contains("/Infrastructure/Persistence/")
                          || normalized.Contains("/EnterpriseStrategy.Persistence/Context/")
                          || NamedBootstrapExceptions.Any(exception => normalized.EndsWith(exception, StringComparison.Ordinal));

            if (!allowed)
            {
                offenders.Add(normalized);
            }
        }

        Assert.True(offenders.Count == 0,
            "MongoClient instantiation detected outside allowed bootstrap/persistence locations:\n" + string.Join("\n", offenders));
    }

    /*
     * One file, named in full — not a wider pattern. Platform keeps its persistence layer in
     * Diten.Platform.Infrastructure/Persistence/, which none of the patterns above matches (".Persistence/" needs a
     * project of that name, "/Infrastructure/Persistence/" needs a directory called exactly Infrastructure), so
     * Platform's one allowed client has always been the one in Infrastructure/DependencyInjection.cs.
     *
     * VerifiedMarketOperationalPersistenceServiceCollectionExtensions builds a SECOND client on purpose: it composes
     * the one-shot verified-market operational command (VerifiedMarketOperationalMode), which runs in its own small
     * container BEFORE the web host exists and must not pull in AddInfrastructure — that would run the migration and
     * seed chain for a command that only provisions reference data. It is a bootstrap file in the persistence
     * layer, which is what this rule allows; the path patterns simply do not reach it.
     * The file belongs to the Business Reference Data work package, so it is named here rather than moved (2026-10-01,
     * product-five takeover). If it moves or stops creating a client, the second test below fails until this entry goes.
     */
    private static readonly string[] NamedBootstrapExceptions =
    {
        "services/Diten.Platform/src/Diten.Platform.Infrastructure/Persistence/VerifiedMarketOperationalPersistenceServiceCollectionExtensions.cs"
    };

    [Fact]
    public void MongoClientBootstrapExceptions_StayHonest()
    {
        var repoRoot = FindRepoRoot();
        var stale = NamedBootstrapExceptions
            .Where(relative =>
            {
                var full = Path.Combine(repoRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                return !File.Exists(full) || !File.ReadAllText(full).Contains("new MongoClient(");
            })
            .ToArray();

        Assert.True(stale.Length == 0,
            "these files no longer create a MongoClient (or no longer exist) — remove them from NamedBootstrapExceptions:\n"
            + string.Join("\n", stale));
    }

    [Fact]
    public void TenantHeaderContract_MustExistInGatewayAndServices()
    {
        var repoRoot = FindRepoRoot();
        var requiredFiles = new List<string>
        {
            "gateway/Diten.ApiGateway/Middleware/TenantResolutionMiddleware.cs",
            "services/Diten.AuthService/src/Diten.AuthService.Infrastructure/Middleware/TenantResolutionMiddleware.cs",
            "services/Diten.Platform.Common/src/Diten.Platform.Common/Tenancy/TenantResolutionMiddleware.cs"
        };

        var optionalMdmFile = "services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Middleware/TenantResolutionMiddleware.cs";
        if (Directory.Exists(Path.Combine(repoRoot, "services", "Diten.MdmService")))
        {
            requiredFiles.Add(optionalMdmFile);
        }

        foreach (var file in requiredFiles)
        {
            var full = Path.Combine(repoRoot, file);
            Assert.True(File.Exists(full), $"Missing tenant middleware file: {file}");

            var content = File.ReadAllText(full);
            Assert.Matches(new Regex("X-Tenant-Id"), content);
        }
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
