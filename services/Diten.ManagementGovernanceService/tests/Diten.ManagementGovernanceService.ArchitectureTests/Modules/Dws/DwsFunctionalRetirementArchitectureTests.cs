using Xunit;

namespace Diten.ManagementGovernanceService.ArchitectureTests.Modules.Dws;

public sealed class DwsFunctionalRetirementArchitectureTests
{
    private static readonly string[] RetiredTokens =
    [
        "DwsDispatchRequest",
        "DwsLocalResult",
        "IDwsLocalActionExecutor",
        "new BsonDocument(\"Value\"",
        "DwsMongoLocalActionExecutor"
    ];

    [Fact]
    public void Typed_functional_handlers_controller_and_persistence_do_not_reach_generic_smoke_dispatch()
    {
        var root = FindRoot();
        var files = Directory.GetFiles(
                Path.Combine(root, "services/Diten.ManagementGovernanceService/src"),
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path =>
                path.Contains("/Features/Dws/Handlers/", StringComparison.Ordinal)
                || path.EndsWith("/DwsStructuresController.cs", StringComparison.Ordinal)
                || path.EndsWith("/Modules/Dws/DwsFunctionalPorts.cs", StringComparison.Ordinal)
                || path.EndsWith("/Modules/Dws/DwsTypedPersistence.cs", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(files);
        foreach (var source in files.Select(File.ReadAllText))
            Assert.All(RetiredTokens, token => Assert.DoesNotContain(token, source, StringComparison.Ordinal));
    }

    [Fact]
    public void Historical_smoke_types_are_quarantined_outside_the_functional_call_graph()
    {
        var root = FindRoot();
        var historical = Path.Combine(
            root,
            "services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Application/Features/Dws/DwsLocalContracts.cs");
        var controller = Path.Combine(
            root,
            "services/Diten.ManagementGovernanceService/src/Diten.ManagementGovernanceService.Api/Controllers/DwsStructuresController.cs");

        Assert.Contains("DwsDispatchRequest", File.ReadAllText(historical), StringComparison.Ordinal);
        Assert.DoesNotContain("DwsDispatchRequest", File.ReadAllText(controller), StringComparison.Ordinal);
    }

    private static string FindRoot()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null && !File.Exists(Path.Combine(cursor.FullName, "AGENTS.md"))) cursor = cursor.Parent;
        return cursor?.FullName ?? throw new InvalidOperationException("repo_root_not_found");
    }
}
