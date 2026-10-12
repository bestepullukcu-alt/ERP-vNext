using Xunit;

namespace Diten.Web.Tests.JavaScript;

// MOD-0185 Loads script pins (R-4b). Each assertion names a defect a module already paid for (MODULE-RECIPE): source pins
// prove the code shape only; the behaviour itself was measured live in the R-4b record.
public sealed class LoadIndexBehaviorTests
{
    private readonly string _script = Read("frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Loads/index.js");

    [Fact]
    public void OneIntentPerOpenedForm_KeyAndRootCorrelationTogether()
    {
        Assert.Contains("const newIntent = () => ({ key: createUuid(), correlation: createUuid(), blocked: false });", _script);
        Assert.Contains("createIntent = newIntent();", _script);
        Assert.Contains("'X-Correlation-Id': intent.correlation", _script);
        Assert.Contains("'Idempotency-Key': intent.key", _script);
        Assert.Contains("if (code === 'IDEMPOTENCY_KEY_REUSED' || code === 'CORRELATION_ROOT_MISMATCH') intent.blocked = true;", _script);
        Assert.DoesNotContain("bodyText !==", _script);
        Assert.DoesNotContain("signature ===", _script);
    }

    [Fact]
    public void ReloadIsSafeAndListStatesAreDistinct()
    {
        // R-2 (SHIPMENT-BUNDLE 3.2.0): the company is a required scope on every call. Pinned here so the
        // wiring cannot be removed silently — an unpinned scope is how the adapter and the page drift apart.
        Assert.Contains("query.set('legalEntityId', legalEntityScope)", _script);
        Assert.Contains("fetch(withScope(endpoint, createdScope)", _script);
        Assert.Contains("'/SupplyChain/api/legal-entities'", _script);
        Assert.Contains("if (!await loadLegalEntities()) return;", _script);
        Assert.Contains("ajax: (data, callback) => { void loadRows(data, callback); },", _script);
        Assert.Contains("document.getElementById('loadsSkeleton')", _script);
        Assert.Contains("if (response.status === 403) { showDenied(); callback({ data: [] }); return; }", _script);
        Assert.Contains("withReference(L.ListUnavailable, reference)", _script);
        Assert.DoesNotContain("window.showToast?.(L.ErrPersistenceUnavailable", _script);
    }

    [Fact]
    public void LocalWallClockInUtcOnTheWire()
    {
        Assert.Contains("const parseLocalInput = (raw) =>", _script);
        Assert.Contains("plannedDepartAt: departAt ? departAt.toISOString() : ''", _script);
        Assert.Contains("L.PlannedDepartAtInvalid", _script);
    }

    [Fact]
    public void NoTransitionSurfaceAndSharedPrimitivesOnly()
    {
        Assert.DoesNotContain("/transition", _script);
        Assert.DoesNotContain("Swal.fire", _script);
        Assert.DoesNotContain("window.confirm", _script);
        Assert.DoesNotContain("localStorage", _script);
        Assert.DoesNotContain(":5061", _script);
    }

    private static string Read(string path) => File.ReadAllText(Path.Combine(Root(), path));
    private static string Root() { var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent; return dir?.FullName ?? throw new DirectoryNotFoundException(); }
}
