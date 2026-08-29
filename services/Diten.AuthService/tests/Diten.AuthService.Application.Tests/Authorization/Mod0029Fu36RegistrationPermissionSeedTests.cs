using Diten.AuthService.Persistence.Seed;
using Xunit;

namespace Diten.AuthService.Application.Tests.Authorization;

public sealed class Mod0029Fu36RegistrationPermissionSeedTests
{
    private static readonly string[] ExactConstructors =
    [
        "platform\", \"document-management.master-register.registration\", \"view",
        "platform\", \"document-management.master-register.registration\", \"create",
        "platform\", \"document-management.master-register.registration\", \"reconcile"
    ];

    [Fact]
    public void Dedicated_registration_permissions_are_seeded_exactly_once()
    {
        var source = File.ReadAllText(FindRepoFile(
            "services", "Diten.AuthService", "src", "Diten.AuthService.Persistence", "Seed", "DataSeeder.cs"));

        foreach (var literal in ExactConstructors)
        {
            Assert.Equal(1, Count(source, literal));
        }
    }

    [Fact]
    public void Dedicated_registration_permissions_are_not_added_to_default_tenant_role_templates()
    {
        var source = File.ReadAllText(FindRepoFile(
            "services", "Diten.AuthService", "src", "Diten.AuthService.Domain", "Authorization",
            "DefaultRolePermissionTemplate.cs"));

        Assert.DoesNotContain("master-register.registration", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tenant_97c5_designated_operator_receives_unified_create_permissions_only_through_dedicated_role()
    {
        var source = File.ReadAllText(FindRepoFile(
            "services", "Diten.AuthService", "src", "Diten.AuthService.Persistence", "Seed", "DataSeeder.cs"));

        Assert.Contains("Tenant97c5Id = Guid.Parse(\"97c59330-dbc4-4665-b29c-0c26dbb5cc93\")", source);
        Assert.Contains("Tenant97c5WorkflowOperatorEmail = \"bestepullukcu@gmail.com\"", source);
        Assert.Contains("Tenant97c5MasterRegisterLinkRole = \"DocumentMasterRegisterLinker\"", source);

        var requiredKeys = new[]
        {
            "platform.document-management.master-register.registration.create",
            "platform.document-management.master-register.manage",
            "platform.document-management.master-register.link",
            "platform.document-management.controlled-documents.create",
            "platform.document-management.controlled-documents.view"
        };
        foreach (var key in requiredKeys)
        {
            Assert.Contains($"\"{key}\"", source, StringComparison.Ordinal);
        }

        Assert.Contains("u.Email == Tenant97c5WorkflowOperatorEmail && u.TenantId == Tenant97c5Id", source);
        Assert.Contains("rp.RoleId == role.Id && rp.TenantId == Tenant97c5Id", source);
        Assert.Contains("ur.TenantId == Tenant97c5Id", source);
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }

    private static string FindRepoFile(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}
