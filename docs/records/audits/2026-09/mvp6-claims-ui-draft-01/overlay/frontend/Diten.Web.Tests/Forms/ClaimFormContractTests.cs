using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Diten.Web.Models.SupplyChain.Claims;
using Xunit;

namespace Diten.Web.Tests.Forms;

// MOD-0187 Claims — view-model/validator mapping and form/view contract (pack §32.2, §32.6, §32.7, §32.9;
// CU-06, CU-09, CU-11, CU-12, CU-24). DRAFT overlay — not built, not run.
public sealed class ClaimFormContractTests
{
    private const string ViewDir = "frontend/Diten.Web/Views/SupplyChain/Claims";
    private const string ResxDir = "frontend/Diten.Web/Resources/Views/SupplyChain/Claims";
    private static readonly string[] Languages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // ── view-model mapping ──────────────────────────────────────────────────────────────────────────────────────

    [Theory] // mirror of ClaimModels.cs:45 ClaimWire.Permission
    [InlineData("Open", "supplychain.claims.create")]
    [InlineData("Investigating", "supplychain.claims.investigate")]
    [InlineData("Withdrawn", "supplychain.claims.investigate")]
    [InlineData("Approved", "supplychain.claims.decide")]
    [InlineData("Rejected", "supplychain.claims.decide")]
    [InlineData("Closed", "supplychain.claims.decide")]
    [InlineData("Settled", "supplychain.claims.settle")]
    [InlineData("Unknown", "supplychain.claims.decide")]
    public void Target_key_map_mirrors_the_backend(string target, string key) =>
        Assert.Equal(key, ClaimUiPermissions.ForTarget(target));

    [Fact] // ClaimLifecycle.cs:4-7
    public void Lifecycle_arrows_mirror_the_backend_exactly()
    {
        var arrows = ClaimUiLifecycle.Transitions
            .SelectMany(pair => pair.Value.Select(target => $"{pair.Key}->{target}"))
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[]
        {
            "Approved->Settled", "Investigating->Approved", "Investigating->Rejected", "Open->Investigating",
            "Open->Withdrawn", "Rejected->Closed", "Settled->Closed"
        }, arrows);
        Assert.Equal(ClaimUiLifecycle.Statuses.OrderBy(x => x), ClaimUiLifecycle.Transitions.Keys.OrderBy(x => x));
        Assert.Equal(new[] { "Dispatched", "InTransit", "Delivered", "Exception", "Closed" }, ClaimUiLifecycle.EligibleShipmentStatuses);
    }

    [Fact] // G-SHIPREAD: no mutation surface without supplychain.shipments.read
    public void Page_permissions_require_shipments_read_for_every_mutation()
    {
        var all = new HashSet<string> { ClaimUiPermissions.Create, ClaimUiPermissions.Investigate, ClaimUiPermissions.Decide, ClaimUiPermissions.Settle };
        var withoutShipmentRead = ClaimPagePermissions.From(all.Contains);
        Assert.Equal(new ClaimPagePermissions(false, false, false, false), withoutShipmentRead);

        all.Add(ClaimUiPermissions.ShipmentRead);
        Assert.Equal(new ClaimPagePermissions(true, true, true, true), ClaimPagePermissions.From(all.Contains));

        var settleOnly = ClaimPagePermissions.From(new HashSet<string> { ClaimUiPermissions.Settle, ClaimUiPermissions.ShipmentRead }.Contains);
        Assert.Equal(new ClaimPagePermissions(false, false, false, true), settleOnly);
    }

    [Theory]
    [InlineData("{\"shipmentId\":\"44444444-4444-4444-8444-444444444444\"}", true)]
    [InlineData("{\"shipmentId\":\"44444444-4444-4444-8444-444444444444\",\"reasonCode\":\"\",\"claimedAmount\":\"1e2\"}", true)] // amount is the backend's job
    [InlineData("{\"shipmentId\":44}", false)]
    [InlineData("{\"shipmentId\":\"not-a-uuid\"}", false)]
    [InlineData("[]", false)]
    [InlineData("", false)]
    [InlineData("{", false)]
    public void Create_reader_extracts_only_the_routing_value(string body, bool ok) =>
        Assert.Equal(ok, ClaimRequestReader.TryReadCreateShipmentId(body, out _));

    [Theory]
    [InlineData("{\"targetStatus\":\"Approved\",\"occurredAt\":\"x\"}", true, "Approved")]
    [InlineData("{\"targetStatus\":\"\"}", true, "")]
    [InlineData("{\"targetStatus\":null}", false, "")]
    [InlineData("{\"occurredAt\":\"2026-09-26T10:00:00Z\"}", false, "")]
    public void Transition_reader_extracts_only_the_target(string body, bool ok, string target)
    {
        Assert.Equal(ok, ClaimRequestReader.TryReadTransitionTarget(body, out var read));
        Assert.Equal(target, read);
    }

    [Theory] // claims-semantics-v3.0.0.md line 8
    [InlineData("{\"lifecycleCorrelationId\":\"55555555-5555-4555-8555-555555555555\"}", ClaimRootState.Present)]
    [InlineData("{\"lifecycleCorrelationId\":\"00000000-0000-0000-0000-000000000000\"}", ClaimRootState.Present)] // nil is not "missing"
    [InlineData("{\"lifecycleCorrelationId\":null}", ClaimRootState.Incomplete)]
    [InlineData("{\"lifecycleCorrelationId\":\"\"}", ClaimRootState.Incomplete)]
    [InlineData("{}", ClaimRootState.Incomplete)]
    [InlineData("{\"lifecycleCorrelationId\":\"root\"}", ClaimRootState.Invalid)]
    [InlineData("{\"lifecycleCorrelationId\":7}", ClaimRootState.Invalid)]
    public void Root_resolution_follows_the_annex(string json, ClaimRootState expected)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Equal(expected, ClaimRootResolution.From(document.RootElement).State);
    }

    [Fact]
    public void Shipment_projection_carries_three_fields_only()
    {
        Assert.Equal(new[] { "ShipmentNumber", "Status", "CarrierId" },
            typeof(ClaimShipmentProjection).GetProperties().Select(p => p.Name).ToArray());
        using var document = JsonDocument.Parse("{\"shipmentNumber\":\"S-1\",\"status\":\"Delivered\",\"carrierId\":null,\"lifecycleCorrelationId\":\"55555555-5555-4555-8555-555555555555\"}");
        Assert.Equal(new ClaimShipmentProjection("S-1", "Delivered", null), ClaimShipmentProjection.From(document.RootElement));
    }

    // ── create form (6 fields, no client tightening) ────────────────────────────────────────────────────────────

    [Fact]
    public void Create_form_has_exactly_the_six_contract_inputs_and_no_scope_fields()
    {
        var form = Read($"{ViewDir}/_CreateEditOffcanvas.cshtml");
        foreach (var id in new[] { "claimShipmentId", "claimLinkCarrier", "claimReasonCode", "claimClaimedAmount", "claimCurrency", "claimEvidenceList" })
        {
            Assert.Equal(1, Count(form, $"id=\"{id}\""));
        }

        Assert.Equal(1, Count(form, "class=\"form-control claim-evidence-input\""));
        foreach (var forbidden in new[] { "TenantId", "LegalEntityId", "tenantId", "legalEntityId", "lifecycleCorrelationId", "idempotency" })
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
        Assert.DoesNotContain("text-transform", form);
        Assert.Contains("inputmode=\"decimal\"", form);
        Assert.Contains("id=\"claimLinkCarrier\" class=\"form-check-input\" disabled", form);
    }

    [Fact]
    public void Transition_surface_has_the_published_inputs_only()
    {
        var index = Read($"{ViewDir}/Index.cshtml");
        foreach (var id in new[] { "transitionTarget", "transitionOccurredAt", "transitionApprovedAmount", "transitionResolutionCode", "transitionNote" })
        {
            Assert.Equal(1, Count(index, $"id=\"{id}\""));
        }

        Assert.Contains("id=\"transitionApprovedAmountGroup\"", index);
    }

    // ── views: shell, UAS-001, partial paths ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_claims_view_states_the_tenant_shell()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root(), ViewDir), "*.cshtml"))
        {
            Assert.Contains("Layout = \"_LayoutTenantShell\";", File.ReadAllText(file));
        }
    }

    [Fact]
    public void Uas_gate_precedes_every_protected_surface()
    {
        var index = Read($"{ViewDir}/Index.cshtml");
        var gate = index.IndexOf("<partial name=\"_AccessDenied\"", StringComparison.Ordinal);
        Assert.True(gate >= 0);
        Assert.True(index.IndexOf("return;", StringComparison.Ordinal) > gate);
        foreach (var surface in new[] { "~/Views/SupplyChain/Claims/_Filter.cshtml", "~/Views/SupplyChain/Claims/_DataTable.cshtml", "<h5" })
        {
            Assert.True(index.IndexOf(surface, StringComparison.Ordinal) > gate, surface);
        }

        Assert.Contains("Perms.Has(ClaimUiPermissions.Read)", index);
    }

    [Fact]
    public void Index_uses_explicit_partial_paths_and_has_no_bulk_or_details_page()
    {
        var index = Read($"{ViewDir}/Index.cshtml");
        foreach (var partial in new[] { "_Filter.cshtml", "_DataTable.cshtml", "_DetailsQuickView.cshtml", "_CreateEditOffcanvas.cshtml", "_IndexL10n.cshtml" })
        {
            Assert.Contains($"~/Views/SupplyChain/Claims/{partial}", index);
        }

        Assert.DoesNotContain("_BulkActionBar", index);
        Assert.False(File.Exists(Path.Combine(Root(), ViewDir, "Details.cshtml")));
        Assert.DoesNotContain("dt-checkboxes", Read($"{ViewDir}/_DataTable.cshtml"));
        Assert.DoesNotContain("Edit", Read($"{ViewDir}/_DetailsQuickView.cshtml"));
    }

    // ── L10n: 7/7 parity, no empty, no echo, no English copy ─────────────────────────────────────────────────────

    [Fact]
    public void Seven_resources_have_identical_non_empty_non_echo_keys()
    {
        var dictionaries = Languages.ToDictionary(language => language, language => Resx($"{ResxDir}/ClaimsIndex.{language}.resx"));
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
    }

    [Fact]
    public void Mandatory_keys_and_all_eighteen_error_codes_are_localized()
    {
        var english = Resx($"{ResxDir}/ClaimsIndex.en.resx");
        foreach (var key in new[]
                 {
                     "ClaimsTitle", "PageDescription", "AddNewClaims", "ClaimNumber", "ShipmentId", "ClaimedAmount", "CurrencyLabel", "ActionsHeader",
                     "ErrInvalidRequest", "ErrUnauthenticated", "ErrForbidden", "ErrClaimNotFound", "ErrUnsupportedMediaType",
                     "ErrClaimShipmentIneligible", "ErrClaimCarrierMismatch", "ErrClaimAmountInvalid", "ErrClaimApprovalAmountInvalid",
                     "ErrClaimApprovedAmountNotAllowed", "ErrInvalidClaimTransition", "ErrClaimCorrelationMismatch", "ErrIdempotencyKeyReused",
                     "ErrClaimReferenceInvalid", "ErrClaimReferenceIncomplete", "ErrClaimReferenceUnavailable", "ErrClaimStorageUnavailable",
                     "ErrInternalError"
                 })
        {
            Assert.True(english.ContainsKey(key), key);
        }

        Assert.Equal(18, english.Keys.Count(key => key.StartsWith("Err", StringComparison.Ordinal)));
    }

    [Fact]
    public void Settled_wording_never_implies_a_payment()
    {
        var english = Resx($"{ResxDir}/ClaimsIndex.en.resx");
        Assert.Equal("Settled (operational status, no payment posted)", english["TargetSettledLabel"]);
        Assert.Equal("Settle (no payment posted)", english["ActionSettle"]);
    }

    [Fact]
    public void L10n_bridge_exposes_every_module_key()
    {
        var bridge = Read($"{ViewDir}/_IndexL10n.cshtml");
        foreach (var key in Resx($"{ResxDir}/ClaimsIndex.en.resx").Keys)
        {
            Assert.Contains($"{key} = Localizer[\"{key}\"].Value", bridge);
        }
    }

    private static Dictionary<string, string> Resx(string path) =>
        XDocument.Load(Path.Combine(Root(), path)).Root!.Elements("data")
            .ToDictionary(e => e.Attribute("name")!.Value, e => e.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static string Read(string path) => File.ReadAllText(Path.Combine(Root(), path));
    private static int Count(string text, string token) => text.Split(token, StringSplitOptions.None).Length - 1;
    private static string Root() { var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent; return dir?.FullName ?? throw new DirectoryNotFoundException(); }
}
