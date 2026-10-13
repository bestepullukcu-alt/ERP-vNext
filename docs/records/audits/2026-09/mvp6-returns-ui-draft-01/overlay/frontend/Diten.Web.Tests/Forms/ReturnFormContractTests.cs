using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Models.SupplyChain.Returns;
using Xunit;

namespace Diten.Web.Tests.Forms;

// MOD-0186 Returns — view-model/validator mapping and form/view contract (pack §32.2, §32.5–§32.7, §32.9;
// RU-04, RU-05, RU-08, RU-09, RU-18…RU-20, RU-22). DRAFT overlay — not built, not run.
public sealed class ReturnFormContractTests
{
    private const string ViewDir = "frontend/Diten.Web/Views/SupplyChain/Returns";
    private const string ResxDir = "frontend/Diten.Web/Resources/Views/SupplyChain/Returns";
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // The 21 published Returns codes (shipment-bundle.openapi.yaml /returns operations), one resx key each.
    private static readonly string[] ErrorKeys =
    [
        "ErrInvalidRequest", "ErrReturnNotFound", "ErrShipmentNotFound", "ErrShipmentLineNotFound", "ErrInvalidReturnTransition",
        "ErrDispositionRequired", "ErrInvalidReturnQuantity", "ErrReturnQuantityExceeded", "ErrReturnUomMismatch",
        "ErrDuplicateReturnLine", "ErrShipmentNotReturnable", "ErrCorrelationRootMismatch", "ErrIdempotencyKeyReused",
        "ErrReturnSourceChanged", "ErrReturnShipmentRootInvalid", "ErrDependencyResponseInvalid", "ErrReturnShipmentRootUnavailable",
        "ErrReferenceStateUnavailable", "ErrDependencyUnavailable", "ErrPersistenceUnavailable", "ErrInternalError"
    ];

    // ── view-model mapping ──────────────────────────────────────────────────────────────────────────────────────

    [Theory] // mirror of ReturnPermissions.ForTarget (ReturnPermissions.cs:7-16)
    [InlineData("Authorized", "supplychain.returns.authorize")]
    [InlineData("Rejected", "supplychain.returns.authorize")]
    [InlineData("InTransit", "supplychain.returns.transit")]
    [InlineData("Cancelled", "supplychain.returns.cancel")]
    [InlineData("Received", "supplychain.returns.receive")]
    [InlineData("Dispositioned", "supplychain.returns.disposition")]
    [InlineData("Closed", "supplychain.returns.close")]
    [InlineData("Requested", null)]
    [InlineData("Unknown", null)]
    public void Target_key_map_mirrors_the_backend(string target, string? key) =>
        Assert.Equal(key, ReturnUiPermissions.ForTarget(target));

    [Fact] // ReturnLifecycle.cs:4-9; InTransit→Cancelled does not exist
    public void Lifecycle_arrows_mirror_the_backend_exactly()
    {
        var arrows = ReturnUiLifecycle.Transitions
            .SelectMany(pair => pair.Value.Select(target => $"{pair.Key}->{target}"))
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[]
        {
            "Authorized->Cancelled", "Authorized->InTransit", "Dispositioned->Closed", "InTransit->Received",
            "Received->Dispositioned", "Requested->Authorized", "Requested->Rejected"
        }, arrows);
        Assert.DoesNotContain("InTransit->Cancelled", arrows);
        Assert.Equal(ReturnUiLifecycle.Statuses.OrderBy(x => x), ReturnUiLifecycle.Transitions.Keys.OrderBy(x => x));
        Assert.Equal(new[] { "Delivered", "Closed" }, ReturnUiLifecycle.EligibleShipmentStatuses);
    }

    [Fact] // G-SHIPREAD + the .transition conjunction (pack §33 open gap 3)
    public void Page_permissions_require_shipments_read_and_transition_for_every_mutation()
    {
        var keys = new HashSet<string>
        {
            ReturnUiPermissions.Create, ReturnUiPermissions.Transition, ReturnUiPermissions.Authorize, ReturnUiPermissions.Transit,
            ReturnUiPermissions.Cancel, ReturnUiPermissions.Receive, ReturnUiPermissions.Disposition, ReturnUiPermissions.Close
        };
        Assert.Equal(new ReturnPagePermissions(false, false, false, false, false, false, false), ReturnPagePermissions.From(keys.Contains));

        keys.Add(ReturnUiPermissions.ShipmentRead);
        Assert.Equal(new ReturnPagePermissions(true, true, true, true, true, true, true), ReturnPagePermissions.From(keys.Contains));

        keys.Remove(ReturnUiPermissions.Transition);
        var createOnly = ReturnPagePermissions.From(keys.Contains);
        Assert.Equal(new ReturnPagePermissions(true, false, false, false, false, false, false), createOnly);
        Assert.False(createOnly.CanTransitionAny);
    }

    [Theory]
    [InlineData("{\"shipmentId\":\"44444444-4444-4444-8444-444444444444\"}", true)]
    [InlineData("{\"shipmentId\":\"44444444-4444-4444-8444-444444444444\",\"reasonCode\":\"\",\"lines\":[]}", true)] // lines are the backend's job
    [InlineData("{\"shipmentId\":44}", false)]
    [InlineData("{\"shipmentId\":\"not-a-uuid\"}", false)]
    [InlineData("[]", false)]
    [InlineData("", false)]
    [InlineData("{", false)]
    public void Create_reader_extracts_only_the_routing_value(string body, bool ok) =>
        Assert.Equal(ok, ReturnRequestReader.TryReadCreateShipmentId(body, out _));

    [Theory]
    [InlineData("{\"targetStatus\":\"Received\",\"occurredAt\":\"x\"}", true, "Received")]
    [InlineData("{\"targetStatus\":\"\"}", true, "")]
    [InlineData("{\"targetStatus\":null}", false, "")]
    [InlineData("{\"occurredAt\":\"2026-09-26T10:00:00Z\"}", false, "")]
    public void Transition_reader_extracts_only_the_target(string body, bool ok, string target)
    {
        Assert.Equal(ok, ReturnRequestReader.TryReadTransitionTarget(body, out var read));
        Assert.Equal(target, read);
    }

    [Theory] // returns-semantics-v3.0.0.md line 8: Missing/null503; malformed502
    [InlineData("{\"lifecycleCorrelationId\":\"55555555-5555-4555-8555-555555555555\"}", ReturnRootState.Present)]
    [InlineData("{\"lifecycleCorrelationId\":\"00000000-0000-0000-0000-000000000000\"}", ReturnRootState.Present)] // nil is not "missing"
    [InlineData("{\"lifecycleCorrelationId\":null}", ReturnRootState.Unavailable)]
    [InlineData("{\"lifecycleCorrelationId\":\"\"}", ReturnRootState.Unavailable)]
    [InlineData("{}", ReturnRootState.Unavailable)]
    [InlineData("{\"lifecycleCorrelationId\":\"root\"}", ReturnRootState.Invalid)]
    [InlineData("{\"lifecycleCorrelationId\":7}", ReturnRootState.Invalid)]
    public void Root_resolution_follows_the_annex(string json, ReturnRootState expected)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(expected, ReturnRootResolution.From(document.RootElement).State);
    }

    [Fact]
    public void Shipment_projection_carries_number_status_and_three_line_fields_only()
    {
        Assert.Equal(new[] { "ShipmentNumber", "Status", "Lines" },
            typeof(ReturnShipmentProjection).GetProperties().Select(p => p.Name).ToArray());
        Assert.Equal(new[] { "LineNumber", "Quantity", "UomId" },
            typeof(ReturnShipmentLineProjection).GetProperties().Select(p => p.Name).ToArray());
        using var document = JsonDocument.Parse("{\"shipmentNumber\":\"S-1\",\"status\":\"Delivered\",\"lines\":[{\"lineNumber\":\"1\",\"itemId\":\"x\",\"quantity\":\"2.000\",\"uomId\":\"EA\"}],\"lifecycleCorrelationId\":\"55555555-5555-4555-8555-555555555555\"}");
        var projection = ReturnShipmentProjection.From(document.RootElement);
        Assert.Equal("S-1", projection.ShipmentNumber);
        Assert.Equal("Delivered", projection.Status);
        Assert.Equal(new ReturnShipmentLineProjection("1", "2.000", "EA"), Assert.Single(projection.Lines));
    }

    // ── create form (6 schema fields, no client tightening) ─────────────────────────────────────────────────────

    [Fact]
    public void Create_form_has_the_six_contract_fields_and_no_scope_fields()
    {
        var form = Read($"{ViewDir}/_CreateEditOffcanvas.cshtml");
        foreach (var id in new[] { "returnShipmentId", "returnLinesBody", "returnReasonCode", "returnEvidenceList", "returnLineRowTemplate" })
        {
            Assert.Equal(1, Count(form, $"id=\"{id}\""));
        }

        // lines[].shipmentLineNumber / quantity / uomId live in the line-row template (one row per resolved source line).
        foreach (var cls in new[] { "js-line-select", "js-line-number", "js-line-shipped", "js-line-uom", "js-line-quantity" })
        {
            Assert.Equal(1, Count(form, cls));
        }

        Assert.Equal(1, Count(form, "class=\"form-control return-evidence-input\""));
        foreach (var forbidden in new[] { "TenantId", "LegalEntityId", "tenantId", "legalEntityId", "lifecycleCorrelationId", "idempotency", "remaining" })
        {
            Assert.DoesNotContain(forbidden, form);
        }
    }

    [Fact]
    public void Create_form_adds_no_client_tightening()
    {
        var form = Read($"{ViewDir}/_CreateEditOffcanvas.cshtml");
        Assert.DoesNotMatch(new Regex(@"\srequired(\s|=|/|>)"), form);
        Assert.DoesNotContain("maxlength", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pattern=", form, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("type=\"number\"", form);
        Assert.Contains("inputmode=\"decimal\"", form);
    }

    [Fact]
    public void Transition_surface_has_the_published_inputs_only()
    {
        var index = Read($"{ViewDir}/Index.cshtml");
        foreach (var id in new[] { "transitionTarget", "transitionOccurredAt", "transitionDispositionCode", "transitionInventoryReference" })
        {
            Assert.Equal(1, Count(index, $"id=\"{id}\""));
        }

        Assert.Contains("id=\"transitionDispositionGroup\"", index);
        Assert.Contains("id=\"transitionReceivedNote\"", index);
        Assert.DoesNotMatch(new Regex(@"\srequired(\s|=|/|>)"), index);
    }

    // ── views: shell, UAS-001, partial paths ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_returns_view_states_the_tenant_shell_and_has_no_inline_handler()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root(), ViewDir), "*.cshtml"))
        {
            var view = File.ReadAllText(file);
            Assert.Contains("Layout = \"_LayoutTenantShell\";", view);
            Assert.DoesNotMatch(new Regex(@"\son[a-z]+\s*=", RegexOptions.IgnoreCase), view);
            Assert.DoesNotContain("/Platform", view);
        }
    }

    [Fact]
    public void Uas_gate_precedes_every_protected_surface()
    {
        var index = Read($"{ViewDir}/Index.cshtml");
        var gate = index.IndexOf("<partial name=\"_AccessDenied\"", StringComparison.Ordinal);
        Assert.True(gate >= 0);
        Assert.True(index.IndexOf("return;", StringComparison.Ordinal) > gate);
        foreach (var surface in new[] { "~/Views/SupplyChain/Returns/_Filter.cshtml", "~/Views/SupplyChain/Returns/_DataTable.cshtml", "<h5" })
        {
            Assert.True(index.IndexOf(surface, StringComparison.Ordinal) > gate, surface);
        }

        Assert.Contains("Perms.Has(ReturnUiPermissions.Read)", index);
    }

    [Fact]
    public void Index_uses_explicit_partial_paths_and_has_no_bulk_or_details_page()
    {
        var index = Read($"{ViewDir}/Index.cshtml");
        foreach (var partial in new[] { "_Filter.cshtml", "_DataTable.cshtml", "_DetailsQuickView.cshtml", "_CreateEditOffcanvas.cshtml", "_IndexL10n.cshtml" })
        {
            Assert.Contains($"~/Views/SupplyChain/Returns/{partial}", index);
        }

        Assert.DoesNotContain("_BulkActionBar", index);
        Assert.False(File.Exists(Path.Combine(Root(), ViewDir, "Details.cshtml")));
        var table = Read($"{ViewDir}/_DataTable.cshtml");
        Assert.DoesNotContain("dt-checkboxes", table);
        Assert.Equal(1, Count(table, "data-dt-standard=\"v2\""));
        Assert.DoesNotContain("Loading", table);
        Assert.DoesNotContain("Edit", Read($"{ViewDir}/_DetailsQuickView.cshtml"));
    }

    // ── L10n: 7/7 parity, no empty, no echo, no English copy ─────────────────────────────────────────────────────

    [Fact]
    public void Seven_resources_have_identical_non_empty_non_echo_keys()
    {
        var dictionaries = Languages.ToDictionary(language => language, language => Resx($"{ResxDir}/ReturnsIndex.{language}.resx"));
        var english = dictionaries["en"];
        foreach (var (language, rows) in dictionaries)
        {
            Assert.Equal(english.Keys.Order(StringComparer.Ordinal), rows.Keys.Order(StringComparer.Ordinal));
            Assert.All(rows, row =>
            {
                Assert.False(string.IsNullOrWhiteSpace(row.Value), $"{language}: empty {row.Key}");
                Assert.NotEqual(row.Key, row.Value.Trim());
                Assert.DoesNotMatch(new Regex(@"TODO|TBD|\[\[|\]\]|lorem", RegexOptions.IgnoreCase), row.Value);
            });
        }

        // Identical-to-English values are allowed only where the word is genuinely the same in that language.
        var sameWordAllowList = new HashSet<string>(StringComparer.Ordinal) { "fr:ActionsHeader" };
        foreach (var (language, rows) in dictionaries.Where(d => d.Key != "en"))
        {
            foreach (var (key, value) in rows)
            {
                if (value == english[key])
                {
                    Assert.Contains($"{language}:{key}", sameWordAllowList);
                }
            }
        }

        Assert.All(dictionaries["ar"].Values, value => Assert.Matches(new Regex(@"[؀-ۿ]"), value));
    }

    [Fact]
    public void Mandatory_keys_and_all_twenty_one_error_codes_are_localized()
    {
        var english = Resx($"{ResxDir}/ReturnsIndex.en.resx");
        foreach (var key in new[] { "ReturnsTitle", "PageDescription", "AddNewReturns", "RmaNumber", "ShipmentId", "ActionsHeader" }.Concat(ErrorKeys))
        {
            Assert.True(english.ContainsKey(key), key);
        }

        Assert.Equal(21, english.Keys.Count(key => key.StartsWith("Err", StringComparison.Ordinal)));
        // One identical safe-not-found text for the Return and the Shipment 404 (pack §32.8).
        Assert.Equal(english["ErrReturnNotFound"], english["ErrShipmentNotFound"]);
    }

    [Fact]
    public void Received_wording_is_a_manual_assertion()
    {
        var english = Resx($"{ResxDir}/ReturnsIndex.en.resx");
        Assert.Equal("Received (manual assertion)", english["TargetReceivedLabel"]);
        Assert.Equal("Receive (manual assertion)", english["ActionReceive"]);
        Assert.Contains("not a warehouse receipt", english["ReceivedManualNote"]);
        Assert.Contains("not the remaining quantity", english["ReturnLinesHelp"]);
    }

    [Fact]
    public void L10n_bridge_exposes_every_module_key_once()
    {
        var bridge = Read($"{ViewDir}/_IndexL10n.cshtml");
        var bridged = Regex.Matches(bridge, @"(?<!Shared)Localizer\[""(?<key>[^""]+)""\]").Select(m => m.Groups["key"].Value).ToArray();
        Assert.Equal(bridged.Length, bridged.Distinct(StringComparer.Ordinal).Count());
        Assert.True(Resx($"{ResxDir}/ReturnsIndex.en.resx").Keys.ToHashSet(StringComparer.Ordinal).SetEquals(bridged));
    }

    private static Dictionary<string, string> Resx(string path) =>
        XDocument.Load(Path.Combine(Root(), path)).Root!.Elements("data")
            .ToDictionary(e => e.Attribute("name")!.Value, e => e.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static string Read(string path) => File.ReadAllText(Path.Combine(Root(), path));
    private static int Count(string text, string token) => text.Split(token, StringSplitOptions.None).Length - 1;
    private static string Root() { var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent; return dir?.FullName ?? throw new DirectoryNotFoundException(); }
}
