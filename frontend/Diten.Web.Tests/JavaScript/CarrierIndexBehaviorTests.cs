namespace Diten.Web.Tests.JavaScript;

using Xunit;

public sealed class CarrierIndexBehaviorTests
{
    private readonly string _script = File.ReadAllText(Path.Combine(
        FindRepoRoot(), "frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Carriers/index.js"));
    private readonly string _localizationScript = File.ReadAllText(Path.Combine(
        FindRepoRoot(), "frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Carriers/index.l10n.js"));

    [Fact]
    public void UsesSameOriginProxyAndNeverBuildsBrowserBearerOrServicePortRequest()
    {
        Assert.Contains("const endpoint = '/SupplyChain/Carriers/api';", _script);
        Assert.DoesNotContain(":5061", _script);
        Assert.DoesNotContain("document.cookie", _script);
        Assert.DoesNotContain("Authorization", _script);
        Assert.DoesNotContain(":5000", _script);
    }

    [Fact]
    public void EmitsOnlyListCreateAndStatusOperations()
    {
        Assert.Contains("fetch(buildListUrl()", _script);
        // R-2 (SHIPMENT-BUNDLE 3.2.0): create still targets the create endpoint, now wrapped so the required
        // legalEntityId reaches the adapter — every call carries the company, not just the filtered list.
        Assert.Contains("fetch(withScope(endpoint)", _script);
        Assert.Contains("legalEntityId=${encodeURIComponent(legalEntityScope)}", _script);
        Assert.Contains("'/SupplyChain/api/legal-entities'", _script);
        Assert.Contains("withScope(`${endpoint}/${encodeURIComponent(carrierId)}/status`)", _script);
        Assert.DoesNotContain("method: 'DELETE'", _script);
        Assert.DoesNotContain("method: 'PATCH'", _script);
        Assert.DoesNotContain("method: 'PUT'", _script);
        Assert.DoesNotContain("/bulk", _script);
        Assert.DoesNotContain("/import", _script);
    }

    [Fact]
    // R-4a (MODULE-RECIPE 3.1/8.3; MOD-0184 §32.3 as applied, Q403): this test pinned a key re-minted per payload — the
    // defect R-2 measured creating two records from one intent. It now pins one key per opened form or status panel, and a
    // 409 IDEMPOTENCY_KEY_REUSED that blocks the form instead of minting a fresh key.
    public void KeepsOneIntentPerOpenedFormAndUsesSharedModalPrimitives()
    {
        Assert.Contains("const newIntent = () => ({ key: createUuid(), blocked: false });", _script);
        Assert.Contains("createIntent = newIntent();", _script);
        Assert.Contains("statusIntents.set(row.carrierId, newIntent());", _script);
        Assert.Contains("if (failure.code === 'IDEMPOTENCY_KEY_REUSED') createIntent.blocked = true;", _script);
        Assert.Contains("if (failure.code === 'IDEMPOTENCY_KEY_REUSED') intent.blocked = true;", _script);
        Assert.DoesNotContain("signature === signature", _script);
        Assert.DoesNotContain("createIntent = null;\n                if", _script);
        Assert.Contains("statusIntents.set(carrierId, intent)", _script);
        Assert.Contains("'Idempotency-Key': intent.key", _script);
        Assert.Contains("window.showConfirm?.(L.StatusChangeConfirm", _script);
        Assert.Contains("window.showToast?.", _script);
        Assert.DoesNotContain("Swal.fire", _script);
        Assert.DoesNotContain("window.alert", _script);
        Assert.DoesNotContain("window.confirm", _script);
    }

    [Fact]
    public void AppliesIndependentCreateAndStatusPermissions()
    {
        Assert.Contains("permissions.canCreate ? L.AddNewCarrier", _script);
        Assert.Contains("permissions.canChangeStatus && row.status !== 'Retired'", _script);
        Assert.Contains("availableStatusTargets", _script);
        Assert.Contains("['Suspended', 'Retired']", _script);
        Assert.Contains("['Active', 'Retired']", _script);
    }

    [Fact]
    public void LocalizationWarningsUsePresenceAndNonEmptyValueInsteadOfComparingValueToKey()
    {
        Assert.Contains("Object.prototype.hasOwnProperty.call(dictionary, key)", _localizationScript);
        Assert.Contains("dictionary[key].trim().length > 0", _localizationScript);
        Assert.DoesNotContain("dictionary[key] === key", _localizationScript);
    }

    // R-4a (MODULE-RECIPE 3.6, 3.11, 4.1; Returns draft UI-PM-03): measured live in the rescued code — a committed create
    // reported as "outcome unavailable" because ajax.reload() threw on an async ajax option; no skeleton while loading; a
    // failed load left an empty table under a toast worded as a write failure.
    [Fact]
    public void ListStatesAndReloadAreSafe()
    {
        Assert.Contains("ajax: (data, callback) => { void loadRows(data, callback); },", _script);
        Assert.DoesNotContain("ajax: loadRows,", _script);
        Assert.Contains("document.getElementById('carriersSkeleton')", _script);
        Assert.Contains("const showLoadFailure = (correlation) =>", _script);
        Assert.Contains("if (response.status === 403) { showDenied(); callback({ data: [] }); return; }", _script);
        Assert.Contains("L.ListUnavailable", _script);
        Assert.DoesNotContain("window.showToast?.(L.PersistenceUnavailable, 'error');\n            callback({ data: [] });", _script);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
