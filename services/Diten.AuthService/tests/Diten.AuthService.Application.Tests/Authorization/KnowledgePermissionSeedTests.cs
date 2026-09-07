using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// WP-SCMM-05-S1 — canonical-seed literal tests for the 11 SCMM concept-foundation <c>crm.knowledge.*</c> permission
/// keys and the tenant-97c5 Admin grant. Mirrors <c>TerritoryPermissionSeedTests</c> (source-text assertions over
/// DataSeeder.cs) so the catalog keys and grant wiring cannot silently drift. The out-of-scope ContentEngagementJourney
/// (FU05) keys are asserted absent from the seed.
/// </summary>
public sealed class KnowledgePermissionSeedTests
{
    // The 11 SCMM concept-foundation permission catalog literals (module", "resource", "action form).
    [Theory]
    [InlineData("crm\", \"knowledge.concept\", \"read")]
    [InlineData("crm\", \"knowledge.concept\", \"manage")]
    [InlineData("crm\", \"knowledge.concept-template\", \"manage")]
    [InlineData("crm\", \"knowledge.concept-link\", \"manage")]
    [InlineData("crm\", \"knowledge\", \"read")]
    [InlineData("crm\", \"knowledge\", \"manage")]
    [InlineData("crm\", \"knowledge.subject\", \"read")]
    [InlineData("crm\", \"knowledge.subject\", \"manage")]
    [InlineData("crm\", \"knowledge.path\", \"read")]
    [InlineData("crm\", \"knowledge.path\", \"manage")]
    [InlineData("crm\", \"knowledge.path\", \"publish")]
    public void Scmm_knowledge_permission_is_present_in_canonical_seed(string permissionConstructor)
    {
        var seederSource = File.ReadAllText(GetDataSeederPath());

        Assert.Contains(permissionConstructor, seederSource, StringComparison.Ordinal);
    }

    // SCMM knowledge keys are tenant-scoped (module code "crm-knowledge").
    [Fact]
    public void Knowledge_keys_use_crm_knowledge_module_override()
    {
        var seederSource = File.ReadAllText(GetDataSeederPath());

        Assert.Contains("moduleOverride: \"crm-knowledge\"", seederSource, StringComparison.Ordinal);
    }

    // Out-of-scope FU05 journey keys must NOT be seeded by S1.
    [Theory]
    [InlineData("crm\", \"knowledge.content-engagement-journey\", \"read")]
    [InlineData("crm\", \"knowledge.content-engagement-journey\", \"manage")]
    [InlineData("crm\", \"knowledge.content-engagement-journey\", \"publish")]
    public void Journey_permission_is_absent_from_canonical_seed(string permissionConstructor)
    {
        var seederSource = File.ReadAllText(GetDataSeederPath());

        Assert.DoesNotContain(permissionConstructor, seederSource, StringComparison.Ordinal);
    }

    // The tenant-97c5 Admin grant method exists, is wired into SeedAsync, and uses the explicit 11-key allowlist.
    [Theory]
    [InlineData("private static async Task SeedTenant97c5CrmKnowledgeGrantAsync")]
    [InlineData("await SeedTenant97c5CrmKnowledgeGrantAsync(database);")]
    [InlineData("\"crm.knowledge.concept.read\"")]
    [InlineData("\"crm.knowledge.concept.manage\"")]
    [InlineData("\"crm.knowledge.concept-template.manage\"")]
    [InlineData("\"crm.knowledge.concept-link.manage\"")]
    [InlineData("\"crm.knowledge.read\"")]
    [InlineData("\"crm.knowledge.manage\"")]
    [InlineData("\"crm.knowledge.subject.read\"")]
    [InlineData("\"crm.knowledge.subject.manage\"")]
    [InlineData("\"crm.knowledge.path.read\"")]
    [InlineData("\"crm.knowledge.path.manage\"")]
    [InlineData("\"crm.knowledge.path.publish\"")]
    public void Tenant97c5_knowledge_grant_is_wired(string expected)
    {
        var seederSource = File.ReadAllText(GetDataSeederPath());

        Assert.Contains(expected, seederSource, StringComparison.Ordinal);
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

        var probe = new DirectoryInfo(AppContext.BaseDirectory);
        var relative = Path.Combine("services", "Diten.AuthService", "src",
            "Diten.AuthService.Persistence", "Seed", "DataSeeder.cs");
        while (probe is not null)
        {
            var candidate = Path.Combine(probe.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            probe = probe.Parent;
        }

        throw new FileNotFoundException("DataSeeder.cs could not be found.");
    }
}
