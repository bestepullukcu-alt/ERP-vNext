using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Diten.FpaService.ArchitectureTests.Topology;

public sealed class FpaPrimaryTopologyTests
{
    private static readonly string Root=FindRoot();

    [Fact] public void Exact_project_reference_graph_and_shared_module_isolation()
    {
        AssertRefs("Domain");AssertRefs("Application","Diten.FpaService.Domain");AssertRefs("Persistence","Diten.FpaService.Application","Diten.FpaService.Domain");AssertRefs("Infrastructure","Diten.FpaService.Application");AssertRefs("Api","Diten.FpaService.Application","Diten.FpaService.Persistence","Diten.FpaService.Infrastructure","Diten.Platform.Common");
        var source=Path.Combine(Root,"services","Diten.FpaService","src");var all=ReadSources(source);
        foreach(var forbidden in new[]{"Diten.EnterpriseStrategyService","Diten.PpmService","DecisionIntelligence","MongoClient","RabbitMQ","MassTransit","WebApplication.CreateBuilder",".Run("})Assert.DoesNotContain(forbidden,all,StringComparison.Ordinal);
        AssertModuleIsolation(source);

        var fixture=Path.Combine(Path.GetTempPath(),$"fpa-topology-{Guid.NewGuid():N}");try
        {
            var budgeting=Path.Combine(fixture,"Diten.FpaService.Domain","Modules","Budgeting");var scenario=Path.Combine(fixture,"Diten.FpaService.Domain","Modules","ScenarioPlanning");Directory.CreateDirectory(budgeting);Directory.CreateDirectory(scenario);
            File.WriteAllText(Path.Combine(budgeting,"Budget.cs"),"namespace Diten.FpaService.Domain.Modules.Budgeting; public sealed class Budget { }");File.WriteAllText(Path.Combine(scenario,"Scenario.cs"),"namespace Diten.FpaService.Domain.Modules.ScenarioPlanning; public sealed class Scenario { }");
            AssertModuleIsolation(fixture); // Co-hosting is valid; the module name itself is not forbidden.
            File.WriteAllText(Path.Combine(budgeting,"Illegal.cs"),"using Diten.FpaService.Persistence.Modules.ScenarioPlanning; namespace Diten.FpaService.Domain.Modules.Budgeting;");
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(()=>AssertModuleIsolation(fixture));
        }finally{if(Directory.Exists(fixture))Directory.Delete(fixture,true);}
    }

    [Fact] public void No_integration_project_or_runtime_configuration_exists()
    {var service=Path.Combine(Root,"services","Diten.FpaService");Assert.Empty(Directory.GetFileSystemEntries(service,"*IntegrationTests*",SearchOption.AllDirectories));Assert.Empty(Directory.GetFileSystemEntries(service,"appsettings*.json",SearchOption.AllDirectories));Assert.Empty(Directory.GetFileSystemEntries(service,"launchSettings.json",SearchOption.AllDirectories));}

    [Fact] public void Solution_and_test_project_graph_are_exact()
    {
        var service=Path.Combine(Root,"services","Diten.FpaService");var solution=File.ReadAllLines(Path.Combine(service,"Diten.FpaService.sln")).Where(x=>x.StartsWith("Project(",StringComparison.Ordinal)).Select(x=>x.Split('=')[1].Split(',')[0].Trim().Trim('"')).ToArray();
        Assert.Equal(new[]{"Diten.FpaService.Domain","Diten.FpaService.Application","Diten.FpaService.Persistence","Diten.FpaService.Infrastructure","Diten.FpaService.Api","Diten.FpaService.UnitTests","Diten.FpaService.ArchitectureTests","Diten.FpaService.ContractTests"},solution);
        AssertTestProject("UnitTests",["Diten.FpaService.Api","Diten.FpaService.Application","Diten.FpaService.Domain","Diten.FpaService.Persistence"]);AssertTestProject("ArchitectureTests",["Diten.FpaService.Api","Diten.FpaService.Application","Diten.FpaService.Domain","Diten.FpaService.Infrastructure","Diten.FpaService.Persistence"]);AssertTestProject("ContractTests",["Diten.FpaService.Application","Diten.FpaService.Domain","Diten.FpaService.Persistence"]);
    }

    private static void AssertModuleIsolation(string sourceRoot)
    {
        foreach(var file in SourceFiles(sourceRoot))
        {
            var normalized=file.Replace('\\','/');var text=File.ReadAllText(file);
            if(normalized.Contains("/Modules/Budgeting/",StringComparison.Ordinal))Assert.DoesNotMatch(new Regex(@"(?:Domain|Persistence)\.Modules\.ScenarioPlanning|Modules\.ScenarioPlanning\.(?:Domain|Persistence)|ScenarioPlanning(?:Repository|PersistenceOwnershipManifest|DependencyInjection|Session|Transaction)",RegexOptions.CultureInvariant),text);
            if(normalized.Contains("/Modules/ScenarioPlanning/",StringComparison.Ordinal))Assert.DoesNotMatch(new Regex(@"(?:Domain|Persistence)\.Modules\.Budgeting|Modules\.Budgeting\.(?:Domain|Persistence)|Budgeting(?:Repository|PersistenceOwnershipManifest|DependencyInjection|Session|Transaction)",RegexOptions.CultureInvariant),text);
        }
    }
    private static IEnumerable<string> SourceFiles(string root)=>Directory.Exists(root)?Directory.GetFiles(root,"*.cs",SearchOption.AllDirectories).Where(x=>!x.Contains("/obj/")&&!x.Contains("/bin/")):[];
    private static string ReadSources(string root)=>string.Join('\n',SourceFiles(root).Select(File.ReadAllText));
    private static void AssertRefs(string project,params string[] expected){var path=Path.Combine(Root,"services","Diten.FpaService","src",$"Diten.FpaService.{project}",$"Diten.FpaService.{project}.csproj");var actual=XDocument.Load(path).Descendants("ProjectReference").Select(x=>Path.GetFileNameWithoutExtension((string)x.Attribute("Include")!)).Order().ToArray();Assert.Equal(expected.Order().ToArray(),actual);}
    private static void AssertTestProject(string project,string[] expected){var path=Path.Combine(Root,"services","Diten.FpaService","tests",$"Diten.FpaService.{project}",$"Diten.FpaService.{project}.csproj");var xml=XDocument.Load(path);Assert.Equal(expected.Order(),xml.Descendants("ProjectReference").Select(x=>Path.GetFileNameWithoutExtension((string)x.Attribute("Include")!)).Order());Assert.Equal(new[]{"Microsoft.NET.Test.Sdk","xunit","xunit.runner.visualstudio"},xml.Descendants("PackageReference").Select(x=>(string)x.Attribute("Include")!).Order());if(project=="ContractTests"){var content=File.ReadAllText(path);Assert.DoesNotContain("execution/domains",content,StringComparison.Ordinal);Assert.Contains("Fixtures/Budgeting/**",content,StringComparison.Ordinal);}}
    private static string FindRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"AGENTS.md")))d=d.Parent;return d?.FullName??throw new InvalidOperationException("repo root");}
}
