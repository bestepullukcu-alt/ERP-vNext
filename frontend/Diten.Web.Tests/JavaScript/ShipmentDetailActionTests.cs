using Xunit;

namespace Diten.Web.Tests.JavaScript;

public sealed class ShipmentDetailActionTests
{
    private readonly string _details = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/details.js");
    private readonly string _create = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/create.js");
    [Fact] public void LifecycleMatrixAndPermissionSplitAreExact() { Assert.Contains("Draft: ['Planned', 'Cancelled']", _details); Assert.Contains("Delivered: ['Closed']", _details); Assert.Contains("target === 'Cancelled' ? permissions.canCancel : permissions.canDispatch", _details); Assert.Contains("['Dispatched', 'InTransit'].includes", _details); }
    // Q371: Create keeps ONE Idempotency-Key per form instance (the contract's intent). The old pin asserted the defect —
    // a key re-minted whenever the payload changed, so a corrected retry after an unknown commit created a duplicate.
    [Fact] public void StableIntentPreservesPayloadAndKey() { Assert.Contains("previous?.body === body", _details); Assert.Contains("'Idempotency-Key': intent.key", _details); Assert.Contains("const intentKey = crypto.randomUUID();", _create); Assert.Contains("'Idempotency-Key': intentKey", _create); Assert.DoesNotContain("intent.payload !== serialized", _create); }
    [Fact] public void UsesSharedModalAndDoesNotUploadOrStoreSecrets() { Assert.Contains("window.showConfirm?.", _details); Assert.Contains("window.showToast?.", _details); Assert.DoesNotContain("Swal.fire", _details); Assert.DoesNotContain("window.confirm", _details); Assert.DoesNotContain("FormData", _details); Assert.DoesNotContain("localStorage", _details + _create); Assert.DoesNotContain(":5061", _details + _create); }
    [Fact] public void CorrelationAndLifecycleRootRemainSeparate() { Assert.Contains("data.lifecycleCorrelationId", _details); Assert.Contains("body?.error?.correlationId", _details); Assert.DoesNotContain("lifecycleCorrelationId ||", _details); }
    [Fact] public void MutationsUseOnlyValidatedAuthoritativeRoot() { Assert.Contains("const authoritativeRoot", _details); Assert.Contains("uuidPattern.test(shipment.lifecycleCorrelationId)", _details); Assert.Contains("if (!correlationId)", _details); Assert.Contains("'X-Correlation-Id': intent.correlationId", _details); Assert.DoesNotContain("'X-Correlation-Id': uuid() }, body: intent.body", _details); }
    [Fact] public void SafeNotFoundHidesShipmentSurfacesAndActions()
    {
        Assert.Contains("const renderSafeNotFound", _details);
        Assert.Contains("document.querySelector('#shipment-details .row.g-6')", _details);
        Assert.Contains("document.getElementById('shipmentActions')", _details);
        Assert.Contains("document.getElementById('offcanvasTransition')", _details);
        Assert.Contains("document.getElementById('offcanvasPod')", _details);
        Assert.Contains("element.hidden = !visible", _details);
        Assert.Contains("element.toggleAttribute('inert', !visible)", _details);
        Assert.Contains("document.getElementById('shipmentActions')?.replaceChildren()", _details);
        Assert.Contains("renderSafeNotFound(`${L.notFound}${correlation ? ` ${L.supportReference}: ${correlation}` : ''}`)", _details);
        Assert.DoesNotContain("window.location", _details);
    }
    [Fact] public void DetailLoadIgnoresLateAsyncResponses()
    {
        Assert.Contains("let loadVersion = 0", _details);
        Assert.Contains("const version = ++loadVersion", _details);
        Assert.Contains("if (version !== loadVersion) return", _details);
    }
    // 2026-10-18 (found while scoping G6): the adapter answers 400 without ?legalEntityId=, and the workspace sent it on no
    // call — so Details, transitions and POD could never succeed through the Web. Every workspace call is scoped now,
    // a missing scope fails closed before any request, and both ways into the workspace carry the scope.
    [Fact] public void EveryWorkspaceCallCarriesTheLegalEntityScopeAndAMissingScopeFailsClosed()
    {
        Assert.Contains("const legalEntityId = host?.dataset.legalEntityId || ''", _details);
        Assert.Contains("const scoped = (path) => `${path}?legalEntityId=${encodeURIComponent(legalEntityId)}`", _details);
        Assert.Contains("await fetch(scoped(`/SupplyChain/Shipments/api/${shipmentId}`), {", _details);
        Assert.Contains("await fetch(scoped(`/SupplyChain/Shipments/api/${shipmentId}/${kind}`), {", _details);
        Assert.DoesNotContain("fetch(`/SupplyChain/Shipments/api/${shipmentId}", _details);
        Assert.Contains("if (!uuidPattern.test(legalEntityId)) { hideSkeleton(); text('shipmentNumber', L.unavailable); showError(L.legalEntityRequired); return; }", _details);
        Assert.Contains("Details/${encodeURIComponent(id)}?legalEntityId=${encodeURIComponent(document.getElementById('filterLegalEntity')?.value || '')}", _index);
        Assert.Contains("Details/${encodeURIComponent(result.shipmentId)}?legalEntityId=${encodeURIComponent(company)}", _create);
        Assert.Contains("data-legal-entity-id=\"@ViewBag.LegalEntityId\"", _detailsView);
        Assert.Contains("ViewBag.LegalEntityId = LegalEntityScopeRequest.TryResolve(Request, out var legalEntityId)", _controller);
    }
    private readonly string _index = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/index.js");
    private readonly string _detailsView = Read("frontend/Diten.Web/Views/SupplyChain/Shipments/Details.cshtml");
    private readonly string _controller = Read("frontend/Diten.Web/Controllers/SupplyChainShipmentsController.cs");
    private static string Read(string path) => File.ReadAllText(Path.Combine(Root(), path));
    private static string Root() { var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent; return dir?.FullName ?? throw new DirectoryNotFoundException(); }
}
