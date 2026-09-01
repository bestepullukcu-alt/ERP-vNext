using System.Xml.Linq;
using System.Diagnostics;
using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.ProcessModeling;

public sealed class ProcessModelingArchitectureTests
{
    public static IEnumerable<object[]> IsolationMatrix => new[] { "DecisionRegistry", "Modules/Dws" }.SelectMany(s=>new[]{"project","type","namespace","DI","repository","collection-index","session-transaction","permission","audit-outbox","migration","domain-reference","persistence-reference"}.Select(d=>new object[]{s,d}));

    [Theory][MemberData(nameof(IsolationMatrix))]
    public void Exact_twelve_dimension_isolation_matrix_is_fail_closed(string sibling,string dimension)
    {
        var root=ServiceRoot();var name=sibling.Replace("Modules/",string.Empty,StringComparison.Ordinal);var token=sibling.Replace('/','.');
        var moduleFiles=Directory.GetFiles(Path.Combine(root,"src"),"*.cs",SearchOption.AllDirectories).Where(x=>x.Contains("ProcessModeling",StringComparison.Ordinal)).ToArray();
        var moduleText=string.Join('\n',moduleFiles.Select(File.ReadAllText));var projects=string.Join('\n',Directory.GetFiles(Path.Combine(root,"src"),"*.csproj",SearchOption.AllDirectories).Select(File.ReadAllText));
        var manifest=string.Join('\n',ProcessModelingPersistenceManifest.Collections.Concat(ProcessModelingPersistenceManifest.Indexes.Select(x=>x.Name)));
        void NoSibling(string text){Assert.DoesNotContain(token,text,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain(name,text,StringComparison.OrdinalIgnoreCase);}
        switch(dimension)
        {
            case "project": NoSibling(projects); break;
            case "type": NoSibling(string.Join('\n',moduleText.Split('\n').Where(x=>x.Contains("class ",StringComparison.Ordinal)||x.Contains("record ",StringComparison.Ordinal)||x.Contains("interface ",StringComparison.Ordinal)))); break;
            case "namespace": NoSibling(string.Join('\n',moduleText.Split('\n').Where(x=>x.Contains("namespace ",StringComparison.Ordinal)||x.Contains("using ",StringComparison.Ordinal)))); break;
            case "DI":
                // DWS owns the current shared composition. Core ProcessModeling may not register there.
                var composition = File.ReadAllText(Path.Combine(root,"src","Diten.ManagementGovernanceService.Infrastructure","DependencyInjection.cs"));
                Assert.Contains("AddDws", composition, StringComparison.Ordinal);
                Assert.DoesNotContain("ProcessModeling", composition, StringComparison.Ordinal);
                break;
            case "repository": NoSibling(string.Join('\n',moduleFiles.Where(x=>x.Contains("Persistence",StringComparison.Ordinal)).Select(File.ReadAllText))); break;
            case "collection-index": NoSibling(manifest); break;
            case "session-transaction": NoSibling(File.ReadAllText(Path.Combine(root,"src","Diten.ManagementGovernanceService.Persistence","Modules","ProcessModeling","ProcessModelingMongoPersistence.cs"))); break;
            case "permission": NoSibling(File.ReadAllText(Path.Combine(root,"src","Diten.ManagementGovernanceService.Application","Modules","ProcessModeling","ProcessModelingPermissions.cs"))); break;
            case "audit-outbox": NoSibling(manifest+moduleText); break;
            case "migration": Assert.DoesNotContain(moduleFiles,x=>x.Contains("Migration",StringComparison.OrdinalIgnoreCase)); break;
            case "domain-reference": NoSibling(string.Join('\n',moduleFiles.Where(x=>x.Contains(".Domain",StringComparison.Ordinal)).Select(File.ReadAllText))); break;
            case "persistence-reference": NoSibling(string.Join('\n',moduleFiles.Where(x=>x.Contains(".Persistence",StringComparison.Ordinal)).Select(File.ReadAllText))); break;
            default: throw new InvalidOperationException("unknown_isolation_dimension:"+dimension);
        }
    }

    [Fact] public void Immutable_DecisionRegistry_checkpoint_and_Dws_precohost_gate_are_bound()
    {
        const string commit="2d354a97bfbe09ed665e44dba8665181d2a56d78";var root=RepositoryRoot();var psi=new ProcessStartInfo("git",$"ls-tree -r --name-only {commit} -- services/Diten.ManagementGovernanceService") { WorkingDirectory=root,RedirectStandardOutput=true,UseShellExecute=false };using var p=Process.Start(psi)??throw new InvalidOperationException();var tree=p.StandardOutput.ReadToEnd();p.WaitForExit();Assert.Equal(0,p.ExitCode);
        foreach(var path in new[]{"Application/DecisionRegistry/","Domain/DecisionRegistry/","Persistence/DecisionRegistry/","Infrastructure/DependencyInjection.cs","ArchitectureTests/DecisionRegistry/","IntegrationTests/DecisionRegistry/","Tests/DecisionRegistry/"})Assert.Contains(path,tree,StringComparison.Ordinal);
        Assert.DoesNotContain("Modules/Dws",tree,StringComparison.Ordinal);var pack=File.ReadAllText(Path.Combine(root,"execution/domains/management-governance/module-packs/MOD-0355-business-process-architecture-modeling.md"));Assert.Contains("Modules/Dws",pack,StringComparison.Ordinal);Assert.Matches("(?is)before\\s+composition",pack);
    }
    [Fact] public void Shared_program_is_dws_owned_and_contains_no_process_modeling_activation()
    {
        var root = ServiceRoot();
        var projects = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories).Select(Path.GetFileName).Order().ToArray();
        Assert.Equal(8, projects.Length);
        var program = File.ReadAllText(Path.Combine(root,"src","Diten.ManagementGovernanceService.Api","Program.cs"));
        // DWS has separately authorized local-test hosting; this Core slice must not join that host.
        Assert.Contains("Dws", program, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessModeling", program, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(root,"appsettings*",SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFiles(root,"launchSettings.json",SearchOption.AllDirectories));
    }

    [Fact] public void Module_sources_do_not_reference_cohosted_sibling_implementations()
    {
        var root = ServiceRoot();
        foreach (var file in Directory.GetFiles(Path.Combine(root,"src"),"*.cs",SearchOption.AllDirectories).Where(x => x.Contains("ProcessModeling",StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("Modules.Dws", text, StringComparison.Ordinal);
            Assert.DoesNotContain(".DecisionRegistry", text, StringComparison.Ordinal);
            Assert.DoesNotContain("RabbitMQ", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("MassTransit", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact] public void Shared_projects_preserve_the_exact_current_dependency_allowlist()
    {
        var root = ServiceRoot();
        foreach (var path in Directory.GetFiles(Path.Combine(root,"src"),"*.csproj",SearchOption.AllDirectories))
        {
            var doc = XDocument.Load(path);
            var packages=doc.Descendants("PackageReference").Select(x=>(string?)x.Attribute("Include")).ToArray();
            string?[] expectedPackages = path switch
            {
                var p when p.Contains(".Application", StringComparison.Ordinal) => new[] { "MediatR", "Microsoft.Extensions.DependencyInjection.Abstractions", "Microsoft.Extensions.Logging.Abstractions" },
                var p when p.Contains(".Infrastructure", StringComparison.Ordinal) => new[] { "Microsoft.Extensions.DependencyInjection.Abstractions" },
                var p when p.Contains(".Persistence", StringComparison.Ordinal) => new[] { "Microsoft.Extensions.DependencyInjection.Abstractions", "MongoDB.Driver" },
                _ => Array.Empty<string?>()
            };
            Assert.Equal(expectedPackages, packages);
            foreach (var reference in doc.Descendants("ProjectReference").Select(x => (string?)x.Attribute("Include")))
                Assert.DoesNotContain("DecisionRegistry", reference ?? string.Empty, StringComparison.Ordinal);
        }
    }

    [Fact] public void Persistence_manifest_has_no_cross_module_or_platform_ownership()
    {
        var manifest = File.ReadAllText(Path.Combine(ServiceRoot(),"src","Diten.ManagementGovernanceService.Persistence","Modules","ProcessModeling","ProcessModelingPersistenceManifest.cs"));
        Assert.DoesNotContain("dws_", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("decision_registry", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("audit_outbox", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.All(ProcessModelingPersistenceManifest.Indexes, index => Assert.False(index.Ttl));
    }

    private static string ServiceRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !string.Equals(cursor.Name,"Diten.ManagementGovernanceService",StringComparison.Ordinal)) cursor = cursor.Parent;
        return cursor?.FullName ?? throw new InvalidOperationException("Service root not found.");
    }
    private static string RepositoryRoot(){var cursor=new DirectoryInfo(ServiceRoot());while(cursor.Parent is not null&&!Directory.Exists(Path.Combine(cursor.FullName,".git"))&&!File.Exists(Path.Combine(cursor.FullName,".git")))cursor=cursor.Parent;return cursor.FullName;}
}
