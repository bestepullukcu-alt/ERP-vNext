using System.Xml.Linq;

namespace Diten.FpaService.ArchitectureTests.Topology;

public sealed class FpaPrimaryTopologyTests
{
    private static readonly string Root = FindRoot();
    [Fact] public void Exact_project_reference_graph_and_no_forbidden_dependency()
    {
        AssertRefs("Domain");
        AssertRefs("Application", "Diten.FpaService.Domain");
        AssertRefs("Persistence", "Diten.FpaService.Application", "Diten.FpaService.Domain");
        AssertRefs("Infrastructure", "Diten.FpaService.Application");
        AssertRefs("Api", "Diten.FpaService.Application", "Diten.FpaService.Persistence", "Diten.FpaService.Infrastructure");
        var all = string.Join('\n', Directory.GetFiles(Path.Combine(Root,"services","Diten.FpaService","src"), "*", SearchOption.AllDirectories).Where(x => !x.Contains("/obj/") && !x.Contains("/bin/")).Select(File.ReadAllText));
        foreach (var forbidden in new[]{"Diten.EnterpriseStrategyService","Diten.PpmService","ScenarioPlanning","DecisionIntelligence","MongoClient","RabbitMQ","MassTransit","WebApplication.CreateBuilder",".Run("}) Assert.DoesNotContain(forbidden, all, StringComparison.Ordinal);
    }
    [Fact] public void No_integration_project_or_runtime_configuration_exists()
    {
        var service = Path.Combine(Root,"services","Diten.FpaService");
        Assert.Empty(Directory.GetFileSystemEntries(service,"*IntegrationTests*",SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFileSystemEntries(service,"appsettings*.json",SearchOption.AllDirectories));
        Assert.Empty(Directory.GetFileSystemEntries(service,"launchSettings.json",SearchOption.AllDirectories));
    }
    [Fact] public void Solution_and_test_project_graph_are_exact()
    {
        var service=Path.Combine(Root,"services","Diten.FpaService");var solution=File.ReadAllLines(Path.Combine(service,"Diten.FpaService.sln")).Where(x=>x.StartsWith("Project(",StringComparison.Ordinal)).Select(x=>x.Split('=')[1].Split(',')[0].Trim().Trim('"')).ToArray();
        Assert.Equal(new[]{"Diten.FpaService.Domain","Diten.FpaService.Application","Diten.FpaService.Persistence","Diten.FpaService.Infrastructure","Diten.FpaService.Api","Diten.FpaService.UnitTests","Diten.FpaService.ArchitectureTests","Diten.FpaService.ContractTests"},solution);
        AssertTestProject("UnitTests",new[]{"Diten.FpaService.Application","Diten.FpaService.Domain","Diten.FpaService.Persistence"});AssertTestProject("ArchitectureTests",new[]{"Diten.FpaService.Api","Diten.FpaService.Application","Diten.FpaService.Domain","Diten.FpaService.Infrastructure","Diten.FpaService.Persistence"});AssertTestProject("ContractTests",new[]{"Diten.FpaService.Application","Diten.FpaService.Domain","Diten.FpaService.Persistence"});
    }
    private static void AssertRefs(string project, params string[] expected)
    {
        var path=Path.Combine(Root,"services","Diten.FpaService","src",$"Diten.FpaService.{project}",$"Diten.FpaService.{project}.csproj");
        var actual=XDocument.Load(path).Descendants("ProjectReference").Select(x=>Path.GetFileNameWithoutExtension((string)x.Attribute("Include")!)).Order().ToArray();
        Assert.Equal(expected.Order().ToArray(),actual);
    }
    private static void AssertTestProject(string project,string[] expected){var path=Path.Combine(Root,"services","Diten.FpaService","tests",$"Diten.FpaService.{project}",$"Diten.FpaService.{project}.csproj");var xml=XDocument.Load(path);Assert.Equal(expected.Order(),xml.Descendants("ProjectReference").Select(x=>Path.GetFileNameWithoutExtension((string)x.Attribute("Include")!)).Order());Assert.Equal(new[]{"Microsoft.NET.Test.Sdk","xunit","xunit.runner.visualstudio"},xml.Descendants("PackageReference").Select(x=>(string)x.Attribute("Include")!).Order());}
    private static string FindRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"AGENTS.md")))d=d.Parent;return d?.FullName??throw new InvalidOperationException("repo root");}
}
