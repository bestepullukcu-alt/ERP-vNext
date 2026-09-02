using System.Text.RegularExpressions;
using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling;
using Xunit;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.ProcessModeling.Catalog;

public sealed class CatalogFoundationArchitectureTests
{
    private static readonly string[] Commands =
    [
        "CreateProcessArchitecture", "UpdateProcessArchitecture", "ArchiveProcessArchitecture",
        "CreateProcessDomain", "UpdateProcessDomain", "ArchiveProcessDomain",
        "CreateProcessFamily", "UpdateProcessFamily", "ArchiveProcessFamily",
        "CreateProcessDefinition", "UpdateProcessDefinition", "ArchiveProcessDefinition"
    ];

    private static readonly string[] Queries =
    [
        "GetCatalogTree", "GetProcessDefinitionById"
    ];

    [Fact]
    public void Catalog_surface_is_exactly_twelve_commands_and_two_queries_with_one_file_per_type()
    {
        var application = CatalogDirectory("Application");
        var files = Directory.GetFiles(application, "*.cs", SearchOption.AllDirectories);

        Assert.Equal(12, Commands.Length);
        Assert.Equal(2, Queries.Length);
        foreach (var operation in Commands)
        {
            Assert.Single(files, path => Path.GetFileName(path) == operation + "Command.cs");
            Assert.Single(files, path => Path.GetFileName(path) == operation + "Handler.cs");
            Assert.Single(files, path => Path.GetFileName(path) == operation + "Validator.cs");
        }

        foreach (var operation in Queries)
        {
            Assert.Single(files, path => Path.GetFileName(path) == operation + "Query.cs");
            Assert.Single(files, path => Path.GetFileName(path) == operation + "Handler.cs");
            Assert.Single(files, path => Path.GetFileName(path) == operation + "Validator.cs");
        }
    }

    [Fact]
    public void Catalog_source_does_not_depend_on_Dws_or_foreign_systems()
    {
        var sources = CatalogSources();
        Assert.NotEmpty(sources);
        var forbidden = new[]
        {
            "Modules.Dws", "Modules/Dws", "mg_dws_", "Diten.Platform", "Diten.AuthService",
            "Diten.PpmService", "WorkCenter", "PlatformSchemaManifest", "EnsureIndexes" + "Async"
        };

        foreach (var path in sources)
        {
            var source = File.ReadAllText(path);
            Assert.All(forbidden, token => Assert.DoesNotContain(token, source, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Catalog_persistence_is_owned_tenant_first_and_has_no_fallback_or_migration_path()
    {
        var persistence = CatalogSources("Persistence").Select(File.ReadAllText).ToArray();
        Assert.NotEmpty(persistence);
        var combined = string.Join('\n', persistence);

        Assert.Contains("TenantId", combined, StringComparison.Ordinal);
        Assert.Contains("IsDeleted", combined, StringComparison.Ordinal);
        Assert.Contains("StartTransaction", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("InMemory", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Compensat", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Migrate", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Seed", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(4, Regex.Matches(combined, "new\\(\"ux_pm_catalog_").Count);
        Assert.All(new[] { "ArchitectureCode", "DomainCode", "FamilyCode", "ProcessCode" },
            code => Assert.Contains(code, combined, StringComparison.Ordinal));
    }

    [Fact]
    public void Catalog_slice_does_not_modify_shared_composition_or_production_configuration()
    {
        var root = FindRoot();
        var changed = RunGit(root, "status", "--porcelain")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Length > 3 ? line[3..] : string.Empty)
            .Where(path => path.Contains("Diten.ManagementGovernanceService", StringComparison.Ordinal))
            .ToArray();

        Assert.DoesNotContain(changed, path => path.EndsWith("/Program.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(changed, path => path.EndsWith("/CustomBaseController.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(changed, path => path.EndsWith("/DependencyInjection.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(changed, path => path.Contains("appsettings", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(changed, path => path.Contains("launchSettings.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Catalog_permissions_are_exactly_the_closed_pack_inventory()
    {
        Assert.Equal(new[]
        {
            "management-governance.process-modeling.architectures.read",
            "management-governance.process-modeling.architectures.create",
            "management-governance.process-modeling.architectures.update",
            "management-governance.process-modeling.architectures.archive",
            "management-governance.process-modeling.definitions.read",
            "management-governance.process-modeling.definitions.create",
            "management-governance.process-modeling.definitions.update"
            ,"management-governance.process-modeling.definitions.archive"
        }, ProcessModelingPermissions.ExactPermissions.Take(8));

        var controller = File.ReadAllText(Path.Combine(
            FindRoot(),
            "services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Api/Controllers/ProcessModelingLocalTestController.cs"));
        Assert.Equal(14, Regex.Matches(controller, "Http(?:Get|Post|Put)\\(\"catalog/").Count);
        Assert.DoesNotContain("HasPermission", controller, StringComparison.Ordinal);
    }

    private static string CatalogDirectory(string project) => Path.Combine(
        FindRoot(), "services/Diten.ManagementGovernanceService/src",
        $"Diten.ManagementGovernanceService.{project}", "Modules/ProcessModeling/Catalog");

    private static IReadOnlyList<string> CatalogSources(string? project = null)
    {
        var projects = project is null ? new[] { "Application", "Persistence" } : new[] { project };
        return projects.SelectMany(name =>
        {
            var directory = CatalogDirectory(name);
            return Directory.Exists(directory)
                ? Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
                : [];
        }).ToArray();
    }

    private static string RunGit(string root, params string[] arguments)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo("git")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return output;
    }

    private static string FindRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !File.Exists(Path.Combine(cursor.FullName, "AGENTS.md"))) cursor = cursor.Parent;
        return cursor?.FullName ?? throw new InvalidOperationException("repo_root_not_found");
    }
}
