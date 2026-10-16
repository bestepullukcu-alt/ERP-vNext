using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.InventoryService.Api.Controllers.Recall;
using Diten.InventoryService.Api.Recall;
using Diten.InventoryService.Application.Features.Recall;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Infrastructure.Middleware;
using Diten.InventoryService.Tests.Architecture;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Recall;

/// <summary>WP-0177-R1 — the pure rules (pack §4.14, §8.3, §8.6, §8.7; CT-R1, CT-R6) measured on the production functions.</summary>
public sealed class RecallRulesTests
{
    private static RecallCase Case(RecallCaseStatus status, Guid? submitter = null, Guid? completer = null, bool completed = false) => new()
    {
        Status = status,
        ScopeSubmittedBy = submitter,
        EffectivenessCompletedBy = completer,
        Effectiveness = completed ? new RecallEffectivenessResult() : null
    };

    [Fact]
    public void The_defaults_are_the_owner_decision_KR_G()
    {
        var d = RecallSettingsRules.Defaults();
        Assert.Equal(4, d.DrillTargetHours);
        Assert.Equal(180, d.DrillFrequencyWarningDays);
        Assert.Equal(100m, d.EffectivenessTargetPercent["1"]);
        Assert.Equal(95m, d.EffectivenessTargetPercent["2"]);
        Assert.Equal(90m, d.EffectivenessTargetPercent["3"]);
        Assert.Equal(0, d.Version);
        Assert.Null(RecallSettingsRules.Validate(d));
        Assert.Equal(95m, RecallSettingsRules.TargetFor(new RecallSettings(), 2));
    }

    [Fact]
    public void Changes_lists_each_changed_field_once_and_ignores_trailing_zeros()
    {
        var before = RecallSettingsRules.Defaults();
        var after = before.Copy();
        after.DrillFrequencyWarningDays = 120;
        after.EffectivenessTargetPercent["2"] = 95.0m;
        after.EffectivenessTargetPercent["3"] = 85m;
        Assert.Equal(new[] { ("drillFrequencyWarningDays", "180", "120"), ("effectivenessTargetPercent.3", "90", "85") }, RecallSettingsRules.Changes(before, after));
        Assert.Equal(95m, before.EffectivenessTargetPercent["2"]); // Copy is deep
    }

    [Theory]
    [InlineData(RecallCaseStatus.Draft, true, true, false, false, true, false)]
    [InlineData(RecallCaseStatus.PendingScopeApproval, false, false, true, true, true, false)]
    [InlineData(RecallCaseStatus.Active, false, false, false, false, false, true)]
    [InlineData(RecallCaseStatus.EffectivenessCheck, false, false, false, false, false, false)]
    [InlineData(RecallCaseStatus.Closed, false, false, false, false, false, false)]
    [InlineData(RecallCaseStatus.Cancelled, false, false, false, false, false, false)]
    public void The_state_machine_matrix_pack_8_7(RecallCaseStatus status, bool edit, bool submit, bool @return, bool approve, bool cancel, bool start)
    {
        Assert.Equal(edit, RecallCaseRules.CanEditScope(status));
        Assert.Equal(submit, RecallCaseRules.CanSubmit(status));
        Assert.Equal(@return, RecallCaseRules.CanReturn(status));
        Assert.Equal(approve, RecallCaseRules.CanApprove(status));
        Assert.Equal(cancel, RecallCaseRules.CanCancel(status));
        Assert.Equal(start, RecallCaseRules.CanStartEffectiveness(status));
        Assert.Equal(status is RecallCaseStatus.Active or RecallCaseStatus.EffectivenessCheck, RecallCaseRules.IsUnderRecall(status));
        Assert.Equal(status is RecallCaseStatus.Closed or RecallCaseStatus.Cancelled, RecallCaseRules.IsTerminal(status));
    }

    [Fact]
    public void Completion_precedes_closure()
    {
        Assert.True(RecallCaseRules.CanCompleteEffectiveness(Case(RecallCaseStatus.EffectivenessCheck)));
        Assert.False(RecallCaseRules.CanClose(Case(RecallCaseStatus.EffectivenessCheck)));
        Assert.False(RecallCaseRules.CanCompleteEffectiveness(Case(RecallCaseStatus.EffectivenessCheck, completed: true)));
        Assert.True(RecallCaseRules.CanClose(Case(RecallCaseStatus.EffectivenessCheck, completed: true)));
        Assert.False(RecallCaseRules.CanClose(Case(RecallCaseStatus.Active, completed: true)));
    }

    [Fact]
    public void Four_eyes_both_ways_CT_R1()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        Assert.Equal(RecallErrorCodes.SodViolation, RecallCaseRules.SeparationOfDuties(Case(RecallCaseStatus.PendingScopeApproval, submitter: a), RecallPendingActionKind.ApproveScope, a)?.Code);
        Assert.Null(RecallCaseRules.SeparationOfDuties(Case(RecallCaseStatus.PendingScopeApproval, submitter: a), RecallPendingActionKind.ApproveScope, b));
        Assert.Equal(RecallErrorCodes.SodViolation, RecallCaseRules.SeparationOfDuties(Case(RecallCaseStatus.EffectivenessCheck, completer: a, completed: true), RecallPendingActionKind.Close, a)?.Code);
        Assert.Null(RecallCaseRules.SeparationOfDuties(Case(RecallCaseStatus.EffectivenessCheck, completer: a, completed: true), RecallPendingActionKind.Close, b));
        Assert.Null(RecallCaseRules.SeparationOfDuties(Case(RecallCaseStatus.EffectivenessCheck, submitter: a), RecallPendingActionKind.CompleteEffectiveness, a));
        Assert.Equal(409, RecallCaseRules.SeparationOfDuties(Case(RecallCaseStatus.PendingScopeApproval, submitter: a), RecallPendingActionKind.ApproveScope, a)!.Status);
    }

    private static RecallScopeItem Item(string key, string? parent, bool included = true, RecallScopeItemKind kind = RecallScopeItemKind.Lot, decimal? quantity = null)
        => new() { NodeKey = key, ParentKey = parent, Kind = kind, Included = included, LotId = "lot", SkuId = Guid.Empty, Quantity = quantity, ExclusionReason = included ? null : "gerekce on karakter" };

    [Fact]
    public void An_excluded_ancestor_excludes_the_subtree_CT_R6()
    {
        var items = new[]
        {
            Item("Lot:root", null), Item("Lot:child", "Lot:root", included: false), Item("Stock:child", "Lot:child", kind: RecallScopeItemKind.Stock),
            Item("Lot:grand", "Lot:child"), Item("Stock:root", "Lot:root", kind: RecallScopeItemKind.Stock)
        };
        Assert.Equal(["Lot:child", "Lot:grand", "Stock:child"], RecallScopeRules.Excluded(items).Select(i => i.NodeKey).OrderBy(k => k, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void The_scope_seal_is_order_independent_and_moves_with_any_member()
    {
        var a = new[] { Item("Lot:root", null), Item("Stock:x", "Lot:root", kind: RecallScopeItemKind.Stock, quantity: 450m) };
        var b = new[] { Item("Stock:x", "Lot:root", kind: RecallScopeItemKind.Stock, quantity: 450.000m), Item("Lot:root", null) };
        Assert.Equal(RecallScopeRules.Sha256(a, 1), RecallScopeRules.Sha256(b, 1));
        Assert.Matches("^[0-9a-f]{64}$", RecallScopeRules.Sha256(a, 1));
        Assert.NotEqual(RecallScopeRules.Sha256(a, 1), RecallScopeRules.Sha256(a, 2));
        var c = new[] { Item("Lot:root", null), Item("Stock:x", "Lot:root", kind: RecallScopeItemKind.Stock, quantity: 400m) };
        Assert.NotEqual(RecallScopeRules.Sha256(a, 1), RecallScopeRules.Sha256(c, 1));
        var d = new[] { Item("Lot:root", null), Item("Stock:x", "Lot:root", included: false, kind: RecallScopeItemKind.Stock, quantity: 450m) };
        Assert.NotEqual(RecallScopeRules.Sha256(a, 1), RecallScopeRules.Sha256(d, 1));
        Assert.Contains("\"form\":\"rs-scope-v1\"", RecallScopeRules.Canonical(a, 1));
    }

    [Fact]
    public void Node_keys_are_kind_plus_ids()
    {
        Assert.Equal("Lot:L1", RecallScopeRules.NodeKey(RecallScopeItemKind.Lot, "L1"));
        Assert.Equal("Stock:L1:W:-:QUARANTINE", RecallScopeRules.NodeKey(RecallScopeItemKind.Stock, "L1", "W", null, "QUARANTINE"));
        Assert.Equal("Shipment:L1:SHP-1", RecallScopeRules.NodeKey(RecallScopeItemKind.Shipment, "L1", sourceDocumentId: "SHP-1"));
    }

    [Fact]
    public void Effectiveness_never_sums_units_and_never_goes_below_zero()
    {
        var sku = Guid.NewGuid();
        var result = RecallEffectivenessRule.Compute(2, 95m,
        [
            new("C1", sku, "EA", 300m, 0m, 0m, true), new("C1", sku, "EA", 0m, 0m, 280m, true), new("C2", sku, "EA", 200m, 0m, 0m, false),
            new("C2", sku, "EA", 0m, 0m, 250m, false), new("C3", sku, "BOX", 10m, 0m, 0m, true)
        ], DateTimeOffset.UtcNow);
        var c1 = result.Rows.Single(r => r.CustomerRef == "C1");
        Assert.Equal(20m, c1.Open);
        Assert.Equal(93.3m, c1.Percent);
        Assert.False(c1.Met);
        var c2 = result.Rows.Single(r => r.CustomerRef == "C2");
        Assert.Equal(0m, c2.Open); // over-declared: open never negative
        Assert.True(c2.Met);
        Assert.Equal(2, result.Totals.Count); // EA and BOX stay apart
        var ea = result.Totals.Single(t => t.BaseUomId == "EA");
        Assert.Equal(500m, ea.Shipped);
        Assert.False(ea.Notified);
        Assert.True(RecallEffectivenessRule.HasOpenQuantity(result));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("kisa", false)]
    [InlineData("  on karakter  ", true)]
    public void Reason_is_10_to_1000_after_trim(string? reason, bool ok) => Assert.Equal(ok, RecallCaseRules.Reason(reason) is null);
}

/// <summary>
/// WP-0177-R1 item 6 (+ R2: 1.1.0, additive) — <c>recall.openapi.yaml</c> = the CT-accepted proposal + pack §19.7, measured against the PROPOSAL FILE and the
/// production controller: every proposal operation is in the yaml; the R1 controller serves exactly the R1 operations (path, method,
/// permission) and the rest are listed as not yet implemented; every owned error code is declared; SoD / scope-changed where §19.7 says.
/// </summary>
public sealed class RecallContractTests
{
    private static readonly Lazy<JsonObject> Yaml = new(() =>
    {
        var raw = new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(ContractSchemas.RecallContractPath));
        return (JsonObject)JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(raw))!;
    });

    private static readonly Lazy<string> Proposal = new(() => File.ReadAllText(RepoPaths.Contract("recall-bundle-v1-proposal.md")));

    private static readonly string[] Methods = ["get", "post", "put", "patch", "delete"];

    private static IEnumerable<(string Path, string Method, JsonObject Op)> Operations()
        => Yaml.Value["paths"]!.AsObject().SelectMany(p => Methods.Where(m => p.Value![m] is not null).Select(m => (p.Key, m, p.Value![m]!.AsObject())));

    private static List<string> Strings(JsonNode? node) => node!.AsArray().Select(v => v!.GetValue<string>()).ToList();

    [Fact]
    public void The_contract_is_frozen_1_7_0_owned_by_MOD_0177_with_the_CT_additions()
    {
        var info = Yaml.Value["info"]!;
        Assert.Equal("1.7.0", info["version"]!.GetValue<string>());
        Assert.Equal("FROZEN", info["x-status"]!.GetValue<string>());
        Assert.Equal("MOD-0177", info["x-owner"]!.GetValue<string>());
        Assert.Equal("/api/recall", Yaml.Value["servers"]![0]!["url"]!.GetValue<string>());
        var changelog = string.Join(" ", Strings(info["x-changelog"]));
        Assert.All(new[] { "CT-R1", "RECALL_SOD_VIOLATION", "RECALL_SCOPE_CHANGED", "CT-R5", "CT-R6", "CT-R7", "CT-R16", "CT-R17", "CT-R18" },
            t => Assert.Contains(t, changelog, StringComparison.Ordinal));
        // R2 (1.1.0): the trace seal form and the reconciliations R2-1…R2-7
        Assert.All(new[] { "1.1.0", "rt-v1", "R2-1", "R2-4", "R2-7", "SnapshotRef" }, t => Assert.Contains(t, changelog, StringComparison.Ordinal));
        // R3 (1.2.0): KR-B / KR-C / KR-H / CT-R5 and the reconciliations R3-1…R3-4
        Assert.All(new[] { "1.2.0", "WP-0177-R3", "LOT_UNDER_RECALL", "pendingRevision", "R3-1", "R3-4", "never cancelled" }, t => Assert.Contains(t, changelog, StringComparison.Ordinal));
        // R4 (1.3.0): the subject provider, prepareSettingsChange, F3-A and the reconciliations R4-1 / R4-2
        Assert.All(new[] { "1.3.0", "WP-0177-R4", "F3-A", "rc-v1", "rs-v1", "RECALL_SUBJECT_NOT_FOUND", "R4-1", "R4-2" }, t => Assert.Contains(t, changelog, StringComparison.Ordinal));
        // R5 (1.4.0): the drills (KR-G), dr-v1, CT-R17 and the reconciliations R5-1…R5-5
        Assert.All(new[] { "1.4.0", "WP-0177-R5", "dr-v1", "RECALL_DRILL_NOT_FOUND", "frequencyWarning", "R5-1", "R5-5" }, t => Assert.Contains(t, changelog, StringComparison.Ordinal));
        // R6 (1.5.0): the panel, the union read of Vakalar (CT decision A) and D-25
        Assert.All(new[] { "1.5.0", "WP-0177-R6", "getDashboard", "drillRows", "DrillCaseRow", "drillStatus", "D-25" }, t => Assert.Contains(t, changelog, StringComparison.Ordinal));
        // R7a (1.6.0): the case detail reads, D-26 / D-27 (CT decision A)
        Assert.All(new[] { "1.6.0", "WP-0177-R7a", "getCaseCustomers", "getCaseEffectiveness", "CaseCustomerShipment", "D-20", "D-26", "D-27" }, t => Assert.Contains(t, changelog, StringComparison.Ordinal));
        // R7b (1.7.0): the Kanıt Paketi (SHA-256 of the produced bytes) and D-28 (MOD-0175 owner step, read only)
        Assert.All(new[] { "1.7.0", "WP-0177-R7b", "previewEvidence", "createEvidenceExport", "PRODUCED BYTES", "FU-0176-PDF-L10N", "D-28", "holdNumber",
            "RECALL_EVIDENCE_EXPORT_NOT_FOUND" }, t => Assert.Contains(t, changelog, StringComparison.Ordinal));
        Assert.Equal(RecallPermissions.All.OrderBy(k => k), Strings(Yaml.Value["x-permissions"]!["owned"]).OrderBy(k => k));
        Assert.Equal("APPROVE", Yaml.Value["x-signable-actions"]!["defaultMeaningCode"]!.GetValue<string>());
        var settingsAction = Yaml.Value["x-signable-actions"]!["actions"]!.AsArray().Single(a => a!["actionCode"]!.GetValue<string>() == "recall.settings.update");
        Assert.Equal("false", settingsAction!["defaultRequired"]!.ToString()); // CT-R16 (YamlDotNet reads scalars as text)
    }

    [Fact]
    public void Every_proposal_operation_is_in_the_contract_plus_addScopeItems()
    {
        var proposed = Regex.Matches(Proposal.Value, @"^\| 4\.\d+ \| `(?<op>[a-zA-Z]+)` \|", RegexOptions.Multiline).Select(m => m.Groups["op"].Value).ToList();
        Assert.Equal(49, proposed.Count);
        var frozen = Operations().Select(o => o.Op["operationId"]!.GetValue<string>()).ToList();
        Assert.Equal(frozen.Count, frozen.Distinct().Count());
        Assert.All(proposed, op => Assert.Contains(op, frozen));
        Assert.Equal(["addScopeItems"], frozen.Except(proposed).ToList()); // reconciliation R-2
    }

    [Fact]
    public void The_controller_serves_exactly_the_R1_to_R7b_operations_with_their_permission()
    {
        var served = typeof(RecallController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Concat(typeof(Diten.InventoryService.Api.Controllers.Recall.RecallDrillsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)) // R5
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>().SelectMany(a => a.HttpMethods.Select(v =>
                (Path: "/" + a.Template, Method: v.ToLowerInvariant(), Permission: m.GetCustomAttribute<HasPermissionAttribute>()!.Policy!["Permission:".Length..]))))
            .ToList();
        Assert.Equal(49, served.Count); // R1 22 + R2 runTrace, exportTrace, getCaseScopeDiff + R3 six + R4 prepareSettingsChange + R5 ten drill operations + R6 getDashboard + R7a getCaseCustomers, getCaseEffectiveness + R7b the four evidence operations

        var byRoute = Operations().ToDictionary(o => (o.Path, o.Method));
        var r1 = Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["R1"]).Concat(Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["R2"]))
            .Concat(Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["R3"])).Concat(Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["R4"]))
            .Concat(Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["R5"])).Concat(Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["R6"])).Concat(Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["R7a"])).Concat(Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["R7b"])).ToList();
        Assert.Equal(["previewEvidence", "listEvidenceExports", "getEvidenceFile", "createEvidenceExport"], Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["R7b"]));
        Assert.Empty(Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["not-yet-implemented"])); // every operation of the contract is served
        Assert.Equal(["getCaseCustomers", "getCaseEffectiveness"], Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["R7a"]));
        Assert.Equal(["getDashboard"], Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["R6"]));
        Assert.Equal(["listDrillPlans", "createDrillPlan", "updateDrillPlan", "listDrillRuns", "startDrill", "getDrillRun", "completeDrillStep", "prepareDrillResult",
            "signDrillResult", "cancelDrill"], Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["R5"]));
        Assert.Equal("api/recall", typeof(Diten.InventoryService.Api.Controllers.Recall.RecallDrillsController).GetCustomAttribute<Microsoft.AspNetCore.Mvc.RouteAttribute>()!.Template);
        Assert.Equal(["prepareSettingsChange", "getSignableSubject"], Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["R4"]));
        Assert.Equal(["getCaseStock", "requestQuarantine", "retryQuarantineRequest", "getCaseReturns", "linkReturn", "reviseScope"],
            Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["R3"]));
        Assert.Equal(["runTrace", "exportTrace", "getCaseScopeDiff"], Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["R2"]));
        foreach (var (path, method, permission) in served)
        {
            Assert.True(byRoute.TryGetValue((path, method), out var op), $"{method} {path} is not in recall.openapi.yaml");
            Assert.Equal(permission, op.Op["x-permission"]!.GetValue<string>());
            Assert.Contains(op.Op["operationId"]!.GetValue<string>(), r1);
        }

        // R4: getSignableSubject is served by the subject-provider controller (the MOD-0175 / MOD-0176 pattern — 404 for every refusal, no [HasPermission])
        var provider = typeof(Diten.InventoryService.Api.Controllers.Recall.RecallESignSubjectsController);
        Assert.Equal("api/recall/esign-subjects", provider.GetCustomAttribute<Microsoft.AspNetCore.Mvc.RouteAttribute>()!.Template);
        Assert.Equal("/esign-subjects/{subjectType}/{subjectId}", "/esign-subjects/" + provider.GetMethod("GetSignableSubject")!.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpGetAttribute>()!.Template);
        Assert.Equal("getSignableSubject", byRoute[("/esign-subjects/{subjectType}/{subjectId}", "get")].Op["operationId"]!.GetValue<string>());
        Assert.Equal(r1.Count, served.Count + 1);
        var notYet = Strings(Yaml.Value["info"]!["x-implemented-by-slice"]!["not-yet-implemented"]);
        Assert.Equal(Operations().Select(o => o.Op["operationId"]!.GetValue<string>()).Except(r1).OrderBy(x => x), notYet.OrderBy(x => x));
        Assert.Equal("api/recall", typeof(RecallController).GetCustomAttribute<Microsoft.AspNetCore.Mvc.RouteAttribute>()!.Template);
    }

    [Fact]
    public void Every_owned_error_code_is_declared_and_the_CT_codes_sit_on_their_operations()
    {
        var declared = Yaml.Value["x-error-codes"]!.AsObject().Where(p => p.Key is not ("shared" or "owned-by-INVENTORY-1.4.0")).Select(p => p.Key)
            .Concat(Strings(Yaml.Value["x-error-codes"]!["shared"])).ToHashSet(StringComparer.Ordinal);
        var owned = typeof(RecallErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (string)f.GetValue(null)!).ToList();
        Assert.All(owned, code => Assert.Contains(code, declared));

        var proposed = Regex.Matches(Proposal.Value, @"^\| `(?<code>RECALL_[A-Z_]+)` \|", RegexOptions.Multiline).Select(m => m.Groups["code"].Value).ToList();
        Assert.True(proposed.Count >= 25);
        Assert.All(proposed, code => Assert.Contains(code, declared));

        var ops = Operations().ToDictionary(o => o.Op["operationId"]!.GetValue<string>());
        foreach (var op in new[] { "prepareCaseAction", "approveScope", "closeCase" })
        {
            Assert.Contains("RECALL_SOD_VIOLATION", Strings(ops[op].Op["x-error-codes"]));
        }

        Assert.Contains("RECALL_SCOPE_CHANGED", Strings(ops["approveScope"].Op["x-error-codes"]));
        Assert.Contains("RECALL_SCOPE_CHANGED", Strings(ops["prepareCaseAction"].Op["x-error-codes"]));
        Assert.Equal("409", Yaml.Value["x-error-codes"]!["RECALL_SOD_VIOLATION"]!.ToString());
        Assert.Equal(["LOT_UNDER_RECALL"], Strings(Yaml.Value["x-error-codes"]!["owned-by-INVENTORY-1.4.0"]));
    }
}

/// <summary>WP-0177-R1 — structure: scope, insert-only records, no signature record, keys on every action, page-less manifest.</summary>
public sealed class RecallArchitectureTests
{
    [Fact]
    public void The_recall_base_path_is_scoped_by_the_tenant_and_company_middleware()
        => Assert.Contains(RecallContract.BasePath, InventoryScopeResolutionMiddleware.ScopedBasePaths);

    [Fact]
    public void Every_recall_action_carries_a_permission_in_the_recall_namespace_and_the_manifest_declares_exactly_the_enforced_keys()
    {
        var actions = typeof(RecallController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Concat(typeof(Diten.InventoryService.Api.Controllers.Recall.RecallDrillsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)).ToList(); // + R5 drills
        Assert.NotEmpty(actions);
        Assert.All(actions, a => Assert.StartsWith("Permission:recall.", a.GetCustomAttribute<HasPermissionAttribute>()?.Policy ?? "", StringComparison.Ordinal));
        Assert.All(RecallPermissions.R1, k => Assert.Contains(k, RecallPermissions.All));

        // completeness (FU-RSV-01 rule): every key the recall controller enforces — by attribute or by the per-action check of
        // prepareCaseAction — reaches the catalog through ITS manifest, and the manifest declares nothing else (R1: API permissions only)
        var enforced = actions.Select(a => a.GetCustomAttribute<HasPermissionAttribute>()!.Policy!["Permission:".Length..]).ToHashSet(StringComparer.Ordinal);
        Assert.Superset(new HashSet<string>([RecallPermissions.CasesApprove, RecallPermissions.EffectivenessComplete, RecallPermissions.CasesClose]), enforced);
        var manifest = new RecallReadinessManifestProvider().GetManifest();
        Assert.Equal("RECALL-READINESS", manifest.ModuleCode);
        Assert.Equal(177, manifest.SortOrder);
        // WP-0177-R6: the first three pages (pack §11.1) carry their keys; every other enforced key stays an API permission
        Assert.Equal(
            [("RECALLDASHBOARD", "/Recall", RecallPermissions.DashboardRead), ("RECALLTRACE", "/Recall/Trace", RecallPermissions.TraceRun),
             ("RECALLCASES", "/Recall/Cases", RecallPermissions.CasesRead), ("RECALLCASEDETAILS", "/Recall/Cases/Details/{id}", RecallPermissions.CasesRead),
             ("RECALLDRILLS", "/Recall/Drills", RecallPermissions.DrillsRead), ("RECALLEVIDENCE", "/Recall/Evidence", RecallPermissions.EvidenceExport),
             ("RECALLSETTINGS", "/Recall/Settings", RecallPermissions.SettingsRead)],
            manifest.Pages.Select(p => (p.PageCode, p.RoutePath, p.RequiredPermission)).ToList());
        Assert.All(manifest.Pages.Where(p => p.PageCode != "RECALLCASEDETAILS"), p => Assert.True(p.IsNavigationVisible));
        // WP-0177-R7a: the case detail is a child page of Vakalar (not in the menu) with one action per pack §11.3 button
        var details = manifest.Pages.Single(p => p.PageCode == "RECALLCASEDETAILS");
        Assert.False(details.IsNavigationVisible);
        Assert.Equal("RECALLCASES", details.ParentPageCode);
        Assert.Equal(
            [("RECALL_SCOPE_MARK", RecallPermissions.CasesUpdate), ("RECALL_SCOPE_SUBMIT", RecallPermissions.CasesUpdate), ("RECALL_SCOPE_RETURN", RecallPermissions.CasesApprove),
             ("RECALL_SCOPE_APPROVE", RecallPermissions.CasesApprove), ("RECALL_SCOPE_REVISE", RecallPermissions.CasesUpdate),
             ("RECALL_QUARANTINE_REQUEST", RecallPermissions.QuarantineRequestsCreate), ("RECALL_SEGREGATION_CONFIRM", RecallPermissions.SegregationsConfirm),
             ("RECALL_CUSTOMER_NOTIFY", RecallPermissions.NotificationsCreate), ("RECALL_CONSUMPTION_DECLARE", RecallPermissions.ConsumptionsCreate),
             ("RECALL_RETURN_LINK", RecallPermissions.ReturnsLink), ("RECALL_EFFECTIVENESS_START", RecallPermissions.EffectivenessComplete),
             ("RECALL_EFFECTIVENESS_COMPLETE", RecallPermissions.EffectivenessComplete), ("RECALL_CASE_CLOSE", RecallPermissions.CasesClose),
             ("RECALL_CASE_CANCEL", RecallPermissions.CasesCancel)],
            details.Actions.Select(a => (a.ActionCode, a.PermissionKey)).ToList());
        Assert.Equal([("RECALL_TRACE_EXPORT", RecallPermissions.TraceExport), ("RECALL_CASE_OPEN", RecallPermissions.CasesCreate)],
            manifest.Pages.Single(p => p.PageCode == "RECALLTRACE").Actions.Select(a => (a.ActionCode, a.PermissionKey)).ToList());
        var declared = manifest.Pages.Select(p => p.RequiredPermission).Concat(manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey))
            .Concat((manifest.ApiPermissions ?? []).Select(p => p.PermissionKey)).ToList();
        // an API permission is declared once and never also by a page / action (several buttons may share one key — R7a)
        var onPages = manifest.Pages.Select(p => p.RequiredPermission).Concat(manifest.Pages.SelectMany(p => p.Actions).Select(a => a.PermissionKey)).ToHashSet(StringComparer.Ordinal);
        var api = (manifest.ApiPermissions ?? []).Select(p => p.PermissionKey).ToList();
        Assert.Equal(api.Count, api.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(api, onPages.Contains);
        declared = declared.Distinct(StringComparer.Ordinal).ToList();
        Assert.Equal(enforced.OrderBy(k => k, StringComparer.Ordinal), declared.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(RecallPermissions.Implemented.OrderBy(k => k, StringComparer.Ordinal), enforced.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(RecallPermissions.R1Only.Concat([RecallPermissions.TraceRun, RecallPermissions.TraceExport, RecallPermissions.QuarantineRequestsCreate, RecallPermissions.ReturnsLink,
                RecallPermissions.DrillsRead, RecallPermissions.DrillsManage, RecallPermissions.DrillsRun, RecallPermissions.DashboardRead, RecallPermissions.EvidenceExport])
                .OrderBy(k => k, StringComparer.Ordinal),
            RecallPermissions.Implemented.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Same(RecallPermissions.Implemented, RecallPermissions.R1); // the shared namespace test pins R1 (PARALEL KURAL) — an alias, not a copy
        Assert.All(manifest.ApiPermissions!, p => Assert.Equal("Tenant", p.Scope));
        // WP-0177-R7b: every recall key now has its page / button except the API-only lot status (pack §14.2)
        Assert.Equal([RecallPermissions.LotsStatusRead], manifest.ApiPermissions!.Select(p => p.PermissionKey).ToList());
        Assert.Equal(
            [("RECALL_DRILL_PLAN_SAVE", RecallPermissions.DrillsManage), ("RECALL_DRILL_RUN", RecallPermissions.DrillsRun), ("RECALL_DRILL_SIGN", RecallPermissions.DrillsRun)],
            manifest.Pages.Single(p => p.PageCode == "RECALLDRILLS").Actions.Select(a => (a.ActionCode, a.PermissionKey)).ToList());
        Assert.Equal([("RECALL_EVIDENCE_EXPORT", RecallPermissions.EvidenceExport)],
            manifest.Pages.Single(p => p.PageCode == "RECALLEVIDENCE").Actions.Select(a => (a.ActionCode, a.PermissionKey)).ToList());
        Assert.Equal([("RECALL_SETTINGS_SAVE", RecallPermissions.SettingsManage)],
            manifest.Pages.Single(p => p.PageCode == "RECALLSETTINGS").Actions.Select(a => (a.ActionCode, a.PermissionKey)).ToList());
    }

    [Fact]
    public void History_events_records_and_audit_rows_are_insert_only()
    {
        var mutating = typeof(IRecallWriteSession).GetMethods().Select(m => m.Name)
            .Where(n => Regex.IsMatch(n, "Update|Replace|Delete|Remove|Save|Upsert|Release")).OrderBy(n => n).ToList();
        // the mutable heads: case (version-guarded), settings (version-guarded), a Draft scope mark, the lot-block release at closure
        // R3: + the quarantine request head (status / attempts / holds, version-guarded); return links stay insert-only
        Assert.Equal(["ReleaseLotBlocksAsync", "ReplaceCaseAsync", "ReplaceQuarantineRequestAsync", "ReplaceScopeItemAsync", "SaveSettingsAsync"], mutating);

        foreach (var file in SourceScan.Production("Diten.InventoryService.Persistence").Where(f => f.RelativePath.Contains("Recall", StringComparison.Ordinal)))
        {
            Assert.DoesNotMatch(@"_(history|events|audit|notifications|consumptions|segregations)\.(Update|Replace|Delete|FindOneAndUpdate|FindOneAndReplace|FindOneAndDelete|BulkWrite)", file.Body);
            Assert.DoesNotMatch(@"\.(DeleteOne|DeleteMany)Async", file.Body);
        }
    }

    [Fact]
    public async Task Recall_keeps_no_signature_record_and_ships_a_fail_closed_gate()
    {
        var entities = typeof(EntityBase).Assembly.GetTypes()
            .Where(t => typeof(EntityBase).IsAssignableFrom(t) && t.Namespace?.Contains(".Recall", StringComparison.Ordinal) == true).ToList();
        Assert.Equal(19, entities.Count); // R1: settings, history, case, event, scope item, lot block, notification, consumption, segregation, audit; R2: trace snapshot; R3: quarantine request, return link; R4: prepared settings change; R5: drill plan, drill run, drill event; R7b: evidence export, evidence file
        Assert.DoesNotContain(entities, t => t.Name.Contains("Signature", StringComparison.Ordinal));
        Assert.Contains(typeof(IRecallSignature), typeof(FailClosedRecallSignature).GetInterfaces());
        Assert.False(await new FailClosedRecallSignature().VerifyAsync(new RecallSignatureVerification("s", "recall.scope.approve", "APPROVE", Guid.NewGuid(),
            "recall.case", Guid.NewGuid(), DateTimeOffset.UtcNow)));
    }

    /// <summary>
    /// WP-0177-R2 narrows the R1 rule "no sourceModule": the trace READS it only to identify a node — the MVP-6 shipment (== MOD-0183), the
    /// production document of a produced lot (the inputs read) — and carries it on the movement reference. No handler, service or rule file
    /// names it, so no exemption or authorization can hang on it; no recall type posts a movement.
    /// </summary>
    private static readonly string[] TraceReadFiles =
    [
        "Features/Recall/Ports/IRecallTraceSource.cs", "Features/Recall/RecallModels.cs", "Features/Recall/Trace/RecallTraceEngine.cs",
        "Features/Recall/Trace/RecallTraceService.cs", "Recall/RecallTraceSource.cs"
    ];

    [Fact]
    public void No_recall_type_decides_on_sourceModule_or_posts_a_movement()
    {
        foreach (var file in SourceScan.Production("Diten.InventoryService.Application").Concat(SourceScan.Production("Diten.InventoryService.Persistence"))
                     .Where(f => f.RelativePath.Contains("Recall", StringComparison.Ordinal)))
        {
            if (!TraceReadFiles.Any(t => file.RelativePath.EndsWith(t, StringComparison.Ordinal)))
            {
                Assert.DoesNotContain("SourceModule", file.Body, StringComparison.Ordinal);
            }

            Assert.DoesNotContain("PostAsync", file.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("InternalMovementPostingService", file.Body, StringComparison.Ordinal);
        }

        var engine = SourceScan.Production("Diten.InventoryService.Application").Single(f => f.RelativePath.EndsWith("Trace/RecallTraceEngine.cs", StringComparison.Ordinal));
        foreach (var line in engine.Body.Split('\n').Where(l => l.Contains("SourceModule", StringComparison.Ordinal) && !l.TrimStart().StartsWith("//", StringComparison.Ordinal)))
        {
            Assert.True(line.Contains("SourceModule: TraceRules.ShipmentModule", StringComparison.Ordinal)
                        || line.Contains("TransactionsOfDocumentAsync(source.SourceModule, source.SourceDocumentId", StringComparison.Ordinal)
                        || line.Contains("t.SourceModule, t.SourceDocumentId)", StringComparison.Ordinal),
                $"the trace uses sourceModule beyond identifying a node: {line.Trim()}");
        }
    }
}
