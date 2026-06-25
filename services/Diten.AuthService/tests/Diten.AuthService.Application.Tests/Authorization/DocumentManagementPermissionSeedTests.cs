using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Authorization;

public sealed class DocumentManagementPermissionSeedTests
{
    [Theory]
    [InlineData("platform\", \"document-management.contract\", \"view")]
    [InlineData("platform\", \"document-management.collection-definitions\", \"list")]
    [InlineData("platform\", \"document-management.collection-definitions\", \"view")]
    [InlineData("platform\", \"document-management.baseline-releases\", \"list")]
    [InlineData("platform\", \"document-management.corporate-root\", \"initialize")]
    [InlineData("platform\", \"document-management.collection-instances\", \"view")]
    public void Fu01_permission_is_present_in_canonical_seed(string permissionConstructor)
    {
        var seederSource = File.ReadAllText(GetDataSeederPath());

        Assert.Contains(permissionConstructor, seederSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("platform\", \"document-management.qms-baselines\", \"import")]
    [InlineData("platform\", \"document-management.qms-baselines\", \"view")]
    [InlineData("platform\", \"document-management.qms-baselines\", \"publish")]
    public void Fu02_permission_is_present_in_canonical_seed(string permissionConstructor)
    {
        var seederSource = File.ReadAllText(GetDataSeederPath());

        Assert.Contains(permissionConstructor, seederSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("platform\", \"document-management.qms-baselines\", \"create")]
    [InlineData("platform\", \"document-management.qms-baselines\", \"validate")]
    [InlineData("platform\", \"document-management.collection-definitions\", \"create")]
    [InlineData("platform\", \"document-management.collection-definitions\", \"edit")]
    [InlineData("platform\", \"document-management.collection-definitions\", \"move")]
    [InlineData("platform\", \"document-management.collection-definitions\", \"delete")]
    public void Fu04_permission_is_present_in_canonical_seed(string permissionConstructor)
    {
        var seederSource = File.ReadAllText(GetDataSeederPath());

        Assert.Contains(permissionConstructor, seederSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("platform\", \"document-management.controlled-documents\", \"view")]
    [InlineData("platform\", \"document-management.controlled-documents\", \"create")]
    [InlineData("platform\", \"document-management.controlled-documents.version\", \"create")]
    [InlineData("platform\", \"document-management.controlled-documents.version\", \"view")]
    [InlineData("platform\", \"document-management.controlled-documents\", \"share")]
    [InlineData("platform\", \"document-management.controlled-documents.access\", \"manage")]
    [InlineData("platform\", \"document-management.templates\", \"view")]
    [InlineData("platform\", \"document-management.templates\", \"create")]
    [InlineData("platform\", \"document-management.templates.version\", \"create")]
    [InlineData("platform\", \"document-management.templates\", \"share")]
    [InlineData("platform\", \"document-management.folder-documents\", \"upload")]
    [InlineData("platform\", \"document-management.folder-documents.access\", \"manage")]
    [InlineData("platform\", \"document-management.folder-shares\", \"create")]
    [InlineData("platform\", \"document-management.folder-shares\", \"view")]
    public void Mod0029Fu01_layer1_permission_is_present_in_canonical_seed(string permissionConstructor)
    {
        var seederSource = File.ReadAllText(GetDataSeederPath());

        Assert.Contains(permissionConstructor, seederSource, StringComparison.Ordinal);
    }

    private static string GetDataSeederPath()
    {
        var directory = Path.GetDirectoryName(typeof(DataSeeder).Assembly.Location)
            ?? throw new InvalidOperationException("Unable to resolve DataSeeder assembly directory.");

        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "Seed", "DataSeeder.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        var repoCandidate = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "Diten.AuthService.Persistence",
            "Seed",
            "DataSeeder.cs"));

        if (File.Exists(repoCandidate))
        {
            return repoCandidate;
        }

        throw new FileNotFoundException("DataSeeder.cs could not be found.");
    }
}
