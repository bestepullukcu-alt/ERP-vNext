using Xunit;

namespace Diten.Web.Tests.JavaScript;

public sealed class ShipmentIndexBehaviorTests
{
    private readonly string _script = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/index.js");
    // MOD-0183 r9 DL-4: the factory pages (start/length); the adapter turns them into queryShipments page/pageSize.
    [Fact] public void UsesOnlySameOriginFrozenListAdapter() { Assert.Contains("const endpoint = '/SupplyChain/Shipments/api';", _script); Assert.DoesNotContain(":5061", _script); Assert.DoesNotContain(":5000", _script); Assert.DoesNotContain("Authorization", _script); Assert.Contains("{ id: 'filterSourceDocumentId', key: 'sourceDocumentId', kind: 'single' }", _script); Assert.Contains("{ id: 'filterStatus', key: 'status', kind: 'single' }", _script); }
    // R-2 (SHIPMENT-BUNDLE 3.2.0): the company is a required scope on every call, not an optional filter. Pinned
    // on both surfaces so the wiring cannot be removed silently — the list and the Create page each resolve it.
    [Fact]
    public void CarriesTheCompanyScopeOnTheListAndOnCreate()
    {
        var create = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/create.js");
        Assert.Contains("new URLSearchParams({ legalEntityId: company() })", _script);
        Assert.Contains("dataset.legalEntityId", _script);
        Assert.Contains("url: `${endpoint}/export?legalEntityId=${encodeURIComponent(company())}`", _script);
        Assert.DoesNotContain("document.cookie", _script);
        Assert.Contains("'/SupplyChain/api/legal-entities'", create);
        // MOD-0183 r9 §24.1 (SB-1): the sender is the shared selector's company, read-only; it rides the query, never the body.
        Assert.Contains("${base}?legalEntityId=${encodeURIComponent(company())}", create);
        Assert.Contains("if (!company()) { showError(L.senderCompanyRequired); return; }", create);
        Assert.DoesNotContain("formLegalEntity", create);
    }

    // The shared selector, not a page of its own: the same cookie as the MVP-1 screens, and no list until a company is chosen.
    [Fact]
    public void UsesTheSharedCompanySelectorAndDrawsNoListWithoutACompany()
    {
        var index = Read("frontend/Diten.Web/Views/SupplyChain/Shipments/Index.cshtml");
        Assert.Contains("CompanySelection.ResolveAsync(", index);
        Assert.Contains("<partial name=\"~/Views/Inventory/Shared/_CompanySelector.cshtml\" model=\"company\" />", index);
        Assert.InRange(index.IndexOf("if (!company.Ready)", StringComparison.Ordinal), 0, index.IndexOf("id=\"shipmentsListSurface\"", StringComparison.Ordinal));
        Assert.Contains("data-legal-entity-id=\"@companyScope\"", index);
        Assert.DoesNotContain("filterLegalEntity", Read("frontend/Diten.Web/Views/SupplyChain/Shipments/_Filter.cshtml"));
    }

    [Fact]
    public void TheCreatePageOpensOnTheChosenCompany()
    {
        Assert.Contains("data-company-id=\"@company?.ToString(\"D\")\"", Read("frontend/Diten.Web/Views/SupplyChain/Shipments/_Form.cshtml"));
        Assert.Contains("String(row.legalEntityId ?? row.LegalEntityId ?? '') === company()", Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/create.js"));
    }

    // MOD-0183 r9 §24.3 (SB-9): the shared factory in server mode, declared alike in the pack, the table marker and here.
    [Fact] public void UsesTheSharedListFactoryInServerMode() { Assert.Contains("window.DitenDataTable.createList({", _script); Assert.Contains("dataMode: 'server'", _script); Assert.Contains("export: { mode: 'server'", _script); Assert.Contains("DataMode = \"server\"", Read("frontend/Diten.Web/Views/SupplyChain/Shipments/_DataTable.cshtml")); Assert.DoesNotContain("new DataTable(", _script); }
    [Fact] public void OmitsUnsupportedMutationSurfaces() { Assert.DoesNotContain("method: 'DELETE'", _script); Assert.DoesNotContain("method: 'PUT'", _script); Assert.DoesNotContain("method: 'PATCH'", _script); Assert.DoesNotContain("/bulk", _script); Assert.DoesNotContain("/upload", _script); }
    private static string Read(string path) => File.ReadAllText(Path.Combine(Root(), path));
    private static string Root() { var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent; return dir?.FullName ?? throw new DirectoryNotFoundException(); }
}
