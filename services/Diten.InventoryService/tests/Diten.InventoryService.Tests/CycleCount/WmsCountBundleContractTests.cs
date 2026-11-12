using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.InventoryService.Api.Controllers.CycleCounting;
using Diten.InventoryService.Api.CycleCounting;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;
using ServiceContract = Diten.InventoryService.Application.Features.CycleCounting.CycleCountContract;
using Dom = Diten.InventoryService.Domain.CycleCounting.Enums;
using Ledger = Diten.InventoryService.Domain.Enums;
using PostingKind = Diten.InventoryService.Application.Features.CycleCounting.Services.CycleCountPostingKind;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// MOD-0181 CT freeze step (the MOD-0178 §19.7 pattern) — the frozen <c>wms-count-bundle.openapi.yaml</c> itself, measured against the production
/// code (not a copy of it):
/// <list type="bullet">
/// <item>the header: WMS-COUNT-BUNDLE 1.0.0 FROZEN, owner MOD-0181 / Diten.InventoryService, source pack r8;</item>
/// <item>the operations are exactly the routes the cycle-count controllers serve, each with the action's <c>[HasPermission]</c> key, and the
/// writes are the service's <c>RequestOp</c> operationIds; the DL-5 Evidence Pack operations are not in 1.0.0;</item>
/// <item>the error catalogue is exactly <c>CycleCountErrorCodes</c>; every other code an operation lists is an <c>InventoryErrorCodes</c> code;</item>
/// <item>the request bodies are the members the service's request reader accepts;</item>
/// <item>JSON null is admitted nowhere (rule 4).</item>
/// </list>
/// </summary>
public sealed class WmsCountBundleContractTests
{
    private static JsonObject File => CycleCountContract.Yaml;

    private static IEnumerable<JsonObject> OperationNodes
        => File["paths"]!.AsObject().SelectMany(p => p.Value!.AsObject().Where(m => m.Value is JsonObject o && o["operationId"] is not null)
            .Select(m => m.Value!.AsObject()));

    // DL-5 (Evidence Pack) is coded against the proposal §4.4 rows and joins the contract only at the 1.1.0 freeze (CT step, additive). Until then
    // the 1.0.0 equalities below leave out exactly these named DL-5 parts of the code — nothing else — and the 1.1.0 freeze removes this set.
    private static readonly string[] Dl5OperationIds = ["createCountEvidenceExport", "listCountEvidenceExports", "getCountEvidenceExportFile"];
    private const string Dl5Controller = "CountEvidenceExportsController";
    private static readonly string[] Dl5Permissions = ["cycle-count.evidence.export", "cycle-count.evidence.read"];
    private const string Dl5ErrorCodePrefix = "COUNT_EVIDENCE_";

    private static string[] Strings(JsonNode? list) => list?.AsArray().Select(v => v!.GetValue<string>()).ToArray() ?? [];

    private static string[] StringConstants(Type type)
        => type.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!).ToArray();

    [Fact]
    public void The_contract_is_WMS_COUNT_BUNDLE_1_0_0_frozen_owned_by_MOD_0181()
    {
        var info = File["info"]!;
        Assert.Equal("3.1.0", File["openapi"]!.GetValue<string>());
        Assert.Equal("WMS-COUNT-BUNDLE", info["title"]!.GetValue<string>());
        Assert.Equal("1.0.0", info["version"]!.GetValue<string>());
        Assert.Equal("FROZEN", info["x-status"]!.GetValue<string>());
        Assert.Equal("MOD-0181", info["x-owner"]!.GetValue<string>());
        Assert.Equal(ServiceContract.ModuleId, info["x-owner"]!.GetValue<string>());
        Assert.Equal("Diten.InventoryService", info["x-owner-service"]!.GetValue<string>());
        Assert.Equal("control-tower", info["x-authored-by"]!.GetValue<string>());
        Assert.Equal("docs/analysis/contracts/drafts/wms-count-bundle-v1-proposal.md", info["x-source-proposal"]!.GetValue<string>());
        Assert.Contains(" r8 (sha256 f943ab48c0bcdcab512cc1fd21dd80e0710649b29bb520fb90bae2f840eaa856)", info["x-source-pack"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(CycleCountContract.BasePath, File["servers"]![0]!["url"]!.GetValue<string>());
        var changelog = Strings(info["x-changelog"]);
        Assert.Single(changelog);
        Assert.StartsWith("1.0.0 — CT freeze of DL-1…DL-4", changelog[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// The operations are exactly the routes the cycle-count controllers serve (method + path, ids as <c>{}</c>), each with the controller action's
    /// <c>[HasPermission]</c> key as its <c>x-permission</c>; together they use every key the service enforces.
    /// </summary>
    [Fact]
    public void Every_served_cycle_count_route_is_an_operation_with_its_permission_key()
    {
        string Normal(string path) => Regex.Replace(Regex.Replace(path, @"\{[^}]+\}", "{}"), "/+$", string.Empty);
        var contract = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (path, item) in File["paths"]!.AsObject())
        foreach (var (method, operation) in item!.AsObject())
            contract[$"{method.ToUpperInvariant()} {Normal(CycleCountContract.BasePath + path)}"] = operation!["x-permission"]?.GetValue<string>();

        var served = new Dictionary<string, string>(StringComparer.Ordinal);
        var dl5Served = new Dictionary<string, string>(StringComparer.Ordinal);
        var controllers = typeof(CycleCountsController).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(CycleCountsController).Namespace && typeof(ControllerBase).IsAssignableFrom(t));
        foreach (var controller in controllers)
        {
            var into = controller.Name == Dl5Controller ? dl5Served : served;
            var prefix = "/" + controller.GetCustomAttribute<RouteAttribute>()!.Template;
            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            foreach (var verb in action.GetCustomAttributes<HttpMethodAttribute>())
            foreach (var method in verb.HttpMethods)
            {
                var permission = action.GetCustomAttribute<HasPermissionAttribute>()!.Policy!["Permission:".Length..];
                into[$"{method} {Normal(prefix + (string.IsNullOrEmpty(verb.Template) ? string.Empty : "/" + verb.Template))}"] = permission;
            }
        }

        Assert.Equal(served.Keys.Order(StringComparer.Ordinal), contract.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(13 + 9, served.Count); // CycleCountsController 13 (DL-1 9, DL-2 3, DL-4 1) + CountVariancesController 9 (DL-3)
        foreach (var (route, permission) in served) Assert.Equal(permission, contract[route]);
        Assert.All(dl5Served, r => Assert.Contains(r.Value, Dl5Permissions)); // the DL-5 controller (not yet in 1.0.0) enforces only DL-5 keys
        Assert.True(dl5Served.Count is 0 or 3, $"the DL-5 controller serves {dl5Served.Count} routes; proposal §4.4 has 3");
        Assert.Empty(served.Values.Intersect(Dl5Permissions));
        Assert.Equal(CycleCountPermissions.Implemented.Except(Dl5Permissions).Order(StringComparer.Ordinal),
            served.Values.Distinct().Order(StringComparer.Ordinal));
    }

    /// <summary>The write operationIds are the service's own <c>RequestOp</c> names (the key events store them, rule 5) — no more, no fewer.</summary>
    [Fact]
    public void The_write_operationIds_are_the_service_RequestOp_names()
    {
        var writes = File["paths"]!.AsObject().SelectMany(p => p.Value!.AsObject())
            .Where(m => m.Key is "post" or "put").Select(m => m.Value!["operationId"]!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();
        var service = typeof(ServiceContract).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.Name.StartsWith("Op", StringComparison.Ordinal)).Select(f => (string)f.GetRawConstantValue()!)
            .Except(Dl5OperationIds).Order(StringComparer.Ordinal).ToArray(); // DL-5 operationIds join at 1.1.0
        Assert.Equal(service, writes);
        Assert.Equal(14, writes.Length);
        Assert.Equal(["updateCycleCountSettings"], File["paths"]!.AsObject().SelectMany(p => p.Value!.AsObject())
            .Where(m => m.Key == "put").Select(m => m.Value!["operationId"]!.GetValue<string>()).ToArray()); // rule 8: the one PUT
        Assert.DoesNotContain(File["paths"]!.AsObject().SelectMany(p => p.Value!.AsObject()), m => m.Key is "delete" or "patch");
    }

    /// <summary>1.0.0 = DL-1…DL-4: the DL-5 Evidence Pack rows of proposal §4.4 are named as not in this version and appear nowhere in the paths.</summary>
    [Fact]
    public void The_DL5_evidence_operations_are_not_in_1_0_0()
    {
        Assert.DoesNotContain(File["paths"]!.AsObject(), p => p.Key.Contains("evidence", StringComparison.OrdinalIgnoreCase));
        var deferred = Strings(File["info"]!["x-not-in-this-version"]!["DL-5"]);
        Assert.Equal(Dl5OperationIds, deferred);
        var operationIds = OperationNodes.Select(o => o["operationId"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        Assert.All(deferred, id => Assert.DoesNotContain(id, operationIds));
        var slices = File["info"]!["x-implemented-by-slice"]!.AsObject().SelectMany(s => Strings(s.Value)).Order(StringComparer.Ordinal);
        Assert.Equal(operationIds.Order(StringComparer.Ordinal), slices);
    }

    /// <summary>
    /// The contract's own error catalogue is exactly <c>CycleCountErrorCodes</c>; every code an operation lists is a catalogue code or a named
    /// reused <c>InventoryErrorCodes</c> code; every catalogue code is written by at least one operation.
    /// </summary>
    [Fact]
    public void The_error_catalogue_is_exactly_CycleCountErrorCodes()
    {
        var catalogue = File["components"]!["x-error-codes"]!.AsObject().Select(c => c.Key).Order(StringComparer.Ordinal).ToArray();
        Assert.Empty(catalogue.Where(c => c.StartsWith(Dl5ErrorCodePrefix, StringComparison.Ordinal)));
        Assert.Equal(StringConstants(typeof(CycleCountErrorCodes)).Where(c => !c.StartsWith(Dl5ErrorCodePrefix, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal), catalogue); // DL-5 codes (COUNT_EVIDENCE_*) join at 1.1.0
        Assert.Equal(25, catalogue.Length);

        var reused = Strings(File["components"]!["x-reused-error-codes"]);
        Assert.All(reused, code => Assert.Contains(code, StringConstants(typeof(InventoryErrorCodes))));
        Assert.Empty(reused.Intersect(catalogue));

        var listed = OperationNodes.SelectMany(o => Strings(o["x-error-codes"])).ToHashSet(StringComparer.Ordinal);
        Assert.All(listed, code => Assert.True(catalogue.Contains(code) || reused.Contains(code), $"{code} is listed by an operation but is neither catalogued nor reused"));
        Assert.All(catalogue, code => Assert.Contains(code, listed));
        Assert.All(File["components"]!["x-error-codes"]!.AsObject(), c =>
            Assert.Matches("^(400|403|404|409|422|503)$", c.Value!["status"]!.GetValue<string>()));
    }

    /// <summary>Every request body is the member set the service's request reader accepts (an unknown member is a 400 there).</summary>
    [Fact]
    public void The_request_bodies_are_the_members_the_request_reader_accepts()
    {
        var schemas = File["components"]!["schemas"]!.AsObject();
        string[] Members(string schema) => schemas[schema]!["properties"]!.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(CycleCountRequestReader.FormMembers.Order(StringComparer.Ordinal), Members("CycleCountFormRequest"));
        Assert.Equal(CycleCountRequestReader.BulkDeleteMembers.Order(StringComparer.Ordinal), Members("BulkDeleteCycleCountsRequest"));
        Assert.Equal(CycleCountRequestReader.KeyMembers.Order(StringComparer.Ordinal), Members("KeyRequest"));
        Assert.Equal(CycleCountRequestReader.ReasonMembers.Order(StringComparer.Ordinal), Members("ReasonRequest"));
        Assert.Equal(CycleCountRequestReader.EntryMembers.Order(StringComparer.Ordinal), Members("RecordCycleCountLineRequest"));
        Assert.Equal(CycleCountRequestReader.FoundLineMembers.Order(StringComparer.Ordinal), Members("AddCycleCountLineRequest"));
        Assert.Equal(CycleCountRequestReader.RecountMembers.Order(StringComparer.Ordinal), Members("RequestCountRecountRequest"));
        Assert.Equal(CycleCountRequestReader.ResolveMembers.Order(StringComparer.Ordinal), Members("ResolveCountAdjustmentIntegrityRequest"));
        Assert.Equal(CycleCountRequestReader.SettingsMembers.Order(StringComparer.Ordinal), Members("UpdateCycleCountSettingsRequest"));
    }

    /// <summary>
    /// Rule 4: no schema of the file admits JSON null (no "null" type, no nullable) — including the dashboard's accuracyPercent /
    /// manualCountMovements, which the proposal §3.10 words as "null" and the service leaves out. The validator refuses a null member.
    /// </summary>
    [Fact]
    public void JSON_null_is_admitted_nowhere()
    {
        var nullable = new List<string>();
        void Scan(JsonNode? node, string at)
        {
            switch (node)
            {
                case JsonObject o:
                    if (o["type"] is JsonArray types && types.Any(t => t is null || t.GetValue<string>() == "null")) nullable.Add(at);
                    if (o["type"] is JsonValue type && type.GetValue<string>() == "null") nullable.Add(at);
                    if (o["enum"] is JsonArray values && values.Any(v => v is null || v.GetValue<string>() is "null" or "")) nullable.Add(at);
                    if (o["nullable"] is not null) nullable.Add(at);
                    foreach (var (k, v) in o) Scan(v, $"{at}.{k}");
                    break;
                case JsonArray a:
                    for (var i = 0; i < a.Count; i++) Scan(a[i], $"{at}[{i}]");
                    break;
            }
        }

        Scan(File["components"]!["schemas"], "schemas");
        Scan(File["paths"], "paths");
        Assert.Empty(nullable);

        const string id = "6f1c2c5e-0d7b-4b0e-9f6a-1c2d3e4f5a6b";
        JsonObject Count() => new()
        {
            ["countId"] = id, ["number"] = "GMG-CNT-000001", ["warehouseId"] = id, ["countType"] = "Cycle", ["blind"] = false,
            ["scopeLocationIds"] = new JsonArray(id), ["scopeSkuIds"] = new JsonArray(), ["plannedDate"] = "2026-11-12", ["note"] = "x",
            ["status"] = "Draft", ["scopeBinIds"] = new JsonArray(), ["lineCount"] = 0, ["createdBy"] = id, ["createdAt"] = "2026-11-12T10:00:00.000Z",
            ["version"] = 1, ["contractVersion"] = "v1"
        };
        var schema = ContractSchemasName("getCycleCount", 200);
        Assert.Empty(CycleCountContract.Validate(schema, Count().ToJsonString()));
        var withNull = Count();
        withNull["note"] = null;
        Assert.NotEmpty(CycleCountContract.Validate(schema, withNull.ToJsonString()));
        var extra = Count();
        extra["snapshotTransactionIds"] = new JsonArray(id); // internal evidence, not a wire member
        Assert.NotEmpty(CycleCountContract.Validate(schema, extra.ToJsonString()));

        JsonObject Dashboard() => new()
        {
            ["periodDays"] = 31, ["periodStart"] = "2026-10-12T00:00:00.000Z", ["openCounts"] = 0, ["awaitingApprovalLines"] = 0,
            ["refusedAdjustments"] = 0, ["countedLines"] = 0, ["exactLines"] = 0, ["contractVersion"] = "v1"
        };
        var dashboard = ContractSchemasName("getCycleCountDashboard", 200);
        Assert.Empty(CycleCountContract.Validate(dashboard, Dashboard().ToJsonString()));
        var nullAccuracy = Dashboard();
        nullAccuracy["accuracyPercent"] = null;
        Assert.NotEmpty(CycleCountContract.Validate(dashboard, nullAccuracy.ToJsonString()));
    }

    /// <summary>
    /// Every <c>enum:</c> list of the frozen file (paths and schemas) is mapped to the production value set it stands for and is EQUAL to it in
    /// both directions — a value removed from the file or added to the code (or the other way round) turns this red. A new enum list in the
    /// file without a mapping here is red too. Wire spellings go through the service's own conversion (<c>WireNames</c>) where it has one.
    /// <para>Named exception (no enum in the code): <c>CycleCountAdjustment.integrityReason</c> — the service writes the two string literals
    /// <c>"KeyReused"</c> / <c>"HolderReversed"</c> (<c>CycleCountPostingEngine</c>), chosen by the integrity kinds of
    /// <c>CycleCountPostingKind</c>; the set is pinned to the names of those two kinds.</para>
    /// </summary>
    [Fact]
    public void Every_enum_list_equals_the_production_value_set_both_ways()
    {
        static string[] Names<T>() where T : struct, Enum => Enum.GetNames<T>();
        var countMovements = Enum.GetValues<Ledger.MovementType>().Where(Diten.InventoryService.Domain.CycleCounting.Rules.CycleCountDecisionRules.IsCountType)
            .Select(Diten.InventoryService.Domain.Rules.WireNames.ToWire).ToArray();
        var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["paths./count-variances.get.parameters[0].schema"] = [ServiceContract.SourceCount, ServiceContract.SourceManual, ServiceContract.SourceAll],
            ["schemas.SkuLevel"] = Enum.GetValues<Ledger.SkuLevel>().Select(Diten.InventoryService.Domain.Rules.WireNames.ToWire).ToArray(),
            ["schemas.CycleCountType"] = Names<Dom.CycleCountType>(),
            ["schemas.CycleCountStatus"] = Names<Dom.CycleCountStatus>(),
            ["schemas.CycleCountLineOrigin"] = Names<Dom.CycleCountLineOrigin>(),
            ["schemas.CycleCountLineStatus"] = Names<Dom.CycleCountLineStatus>(),
            ["schemas.CycleCountDecision.properties.kind"] = Names<Dom.CycleCountDecisionKind>(),
            ["schemas.CycleCountDecision.properties.source"] = Names<Dom.CycleCountDecisionSource>(),
            ["schemas.CycleCountAdjustment.properties.movementType"] = countMovements,
            ["schemas.CycleCountAdjustment.properties.status"] = Names<Dom.CycleCountAdjustmentStatus>(),
            ["schemas.CycleCountAdjustment.properties.integrityReason"] = [nameof(PostingKind.KeyReused), nameof(PostingKind.HolderReversed)], // named exception
            ["schemas.CycleCountIntegrityResolution.properties.kind"] = Names<Dom.CycleCountIntegrityDecision>(),
            ["schemas.CountManualMovement.properties.movementType"] = countMovements,
            ["schemas.CycleCountEventKind"] = Names<Dom.CycleCountEventKind>(),
            ["schemas.BulkDeleteCycleCountsResult.properties.results.items.properties.outcome"] =
                [ServiceContract.RowDeleted, ServiceContract.RowConflict, ServiceContract.RowNotFound, ServiceContract.RowInvalidState],
            ["schemas.ResolveCountAdjustmentIntegrityRequest.properties.decision"] = Names<Dom.CycleCountIntegrityDecision>()
        };

        var found = new Dictionary<string, string[]>(StringComparer.Ordinal);
        void Scan(JsonNode? node, string at)
        {
            switch (node)
            {
                case JsonObject o:
                    if (o["enum"] is JsonArray values) found[at] = values.Select(v => v!.GetValue<string>()).ToArray();
                    foreach (var (k, v) in o) Scan(v, at.Length == 0 ? k : $"{at}.{k}");
                    break;
                case JsonArray a:
                    for (var i = 0; i < a.Count; i++) Scan(a[i], $"{at}[{i}]");
                    break;
            }
        }

        Scan(File["paths"], "paths");
        Scan(File["components"]!["schemas"], "schemas");
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), found.Keys.Order(StringComparer.Ordinal));
        foreach (var (at, values) in found)
        {
            Assert.True(values.Distinct(StringComparer.Ordinal).Count() == values.Length, $"{at}: repeated value");
            Assert.True(expected[at].ToHashSet(StringComparer.Ordinal).SetEquals(values),
                $"{at}: file [{string.Join(", ", values)}] != code [{string.Join(", ", expected[at])}]");
        }
    }

    private static string ContractSchemasName(string operationId, int status) => TestInfrastructure.ContractSchemas.WmsResponse(operationId, status);
}
