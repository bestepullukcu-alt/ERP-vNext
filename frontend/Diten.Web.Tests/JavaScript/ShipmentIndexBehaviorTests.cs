using Xunit;

namespace Diten.Web.Tests.JavaScript;

public sealed class ShipmentIndexBehaviorTests
{
    private readonly string _script = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/index.js");
    [Fact] public void UsesOnlySameOriginFrozenListAdapter() { Assert.Contains("const endpoint = '/SupplyChain/Shipments/api';", _script); Assert.DoesNotContain(":5061", _script); Assert.DoesNotContain(":5000", _script); Assert.DoesNotContain("Authorization", _script); Assert.Contains("pageSize", _script); Assert.Contains("sourceDocumentId", _script); }
    [Fact] public void UsesDataTablesV2WithoutSavedState() { Assert.Contains("new DataTable", _script); Assert.Contains("serverSide: true", _script); Assert.Contains("stateSave: false", _script); Assert.Contains("window.DtDefaults", _script); }
    [Fact] public void OmitsUnsupportedMutationSurfaces() { Assert.DoesNotContain("method: 'DELETE'", _script); Assert.DoesNotContain("method: 'PUT'", _script); Assert.DoesNotContain("method: 'PATCH'", _script); Assert.DoesNotContain("/bulk", _script); Assert.DoesNotContain("/upload", _script); }
    private static string Read(string path) => File.ReadAllText(Path.Combine(Root(), path));
    private static string Root() { var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent; return dir?.FullName ?? throw new DirectoryNotFoundException(); }
}
