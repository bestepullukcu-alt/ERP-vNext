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
                          || normalized.Contains("/EnterpriseStrategy.Persistence/Context/");

            if (!allowed)
            {
                offenders.Add(normalized);
            }
        }

        Assert.True(offenders.Count == 0,
            "MongoClient instantiation detected outside allowed bootstrap/persistence locations:\n" + string.Join("\n", offenders));
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
