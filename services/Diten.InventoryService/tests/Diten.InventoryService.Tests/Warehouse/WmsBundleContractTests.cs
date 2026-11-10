using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Infrastructure.Authorization;
using Diten.InventoryService.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 CT freeze step (pack §19.7, AC-60) — the frozen <c>wms-bundle.openapi.yaml</c> itself:
/// <list type="bullet">
/// <item>its outbound part is WAREHOUSE-OUTBOUND 1.0.0 (Q-06 superset rule): the 1.0.0 file (kept as the fixture
/// <c>wms-bundle-v1.0.0.openapi.yaml</c>) path-for-path and schema-for-schema EQUAL to <c>warehouse-outbound.openapi.yaml</c>; the current
/// file equal but for the proposal §2.3 optional members (its single list) and the S2 methods on the two frozen path items;</item>
/// <item>1.1.0 is additive over 1.0.0 (nothing of 1.0.0 removed or changed but the named widenings);</item>
/// <item>the operations are the routes the warehouse controllers serve, with their permission keys;</item>
/// <item>JSON null is admitted at exactly the two named §8.23 members (rule 4 exception) and nowhere else in the WMS part.</item>
/// </list>
/// </summary>
public sealed class WmsBundleContractTests
{
    private static string FixtureV10Path
        => Path.Combine(RepoPaths.ServiceRoot, "tests", "Diten.InventoryService.Tests", "Contract", "Fixtures", "wms-bundle-v1.0.0.openapi.yaml");

    /// <summary>The 1.0.0 file as frozen by the CT step (commit 3adf141da), LF line ends — the 1.1.0 comparison base.</summary>
    private const string FrozenV10Sha256 = "3d60be9bd2427a689a05545a65c1032503f727110750acd5f5b788d038056a2c";

    /// <summary>The schemas, responses and paths WAREHOUSE-OUTBOUND 1.0.0 defines (copied unchanged into WMS-BUNDLE).</summary>
    private static readonly string[] OutboundSchemas =
        ["Decimal", "SkuLevel", "OutboundStatus", "Address", "OutboundLine", "Package", "OutboundShipment", "OutboundShipmentReadyEvent", "Error"];

    /// <summary>Proposal §2.3 — the single list of the 1.1.0 optional members (Package.contents and SkuLevel Item are NOT in it) + §7 eventId.</summary>
    private static readonly (string Schema, string[] Members)[] Section23 =
    [
        ("OutboundShipment", ["outboundNumber", "legalEntityId"]),
        ("OutboundLine", ["lotId", "fromWarehouseId", "fromLocationId", "stockStatus", "reservationId", "fefoBypassReason"]),
        ("OutboundShipmentReadyEvent", ["eventId", "tenantId", "legalEntityId", "outboundNumber"])
    ];

    private static JsonObject Load(string path)
    {
        var raw = new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path));
        return (JsonObject)JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(raw))!;
    }

    private static JsonObject Current => Load(ContractSchemas.WmsContractPath);

    private static JsonObject Outbound => Load(RepoPaths.Contract("warehouse-outbound.openapi.yaml"));

    private static string Sha256(string path)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(path).Replace("\r\n", "\n"))))
            .ToLowerInvariant();

    [Fact]
    public void The_contract_is_WMS_BUNDLE_1_1_0_frozen_owned_by_MOD_0178()
    {
        var file = Current;
        var info = file["info"]!;
        Assert.Equal("WMS-BUNDLE", info["title"]!.GetValue<string>());
        Assert.Equal("1.1.0", info["version"]!.GetValue<string>());
        Assert.Equal("FROZEN", info["x-status"]!.GetValue<string>());
        Assert.Equal("MOD-0178", info["x-owner"]!.GetValue<string>());
        Assert.Equal("Diten.InventoryService", info["x-owner-service"]!.GetValue<string>());
        Assert.Equal("control-tower", info["x-authored-by"]!.GetValue<string>());
        Assert.Equal("3.1.0", file["openapi"]!.GetValue<string>());
        Assert.Equal(WarehouseContract.BasePath, file["servers"]![0]!["url"]!.GetValue<string>());
        var changelog = info["x-changelog"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
        Assert.Equal(2, changelog.Count);
        Assert.StartsWith("1.0.0 — CT freeze of S1", changelog[0], StringComparison.Ordinal);
        Assert.StartsWith("1.1.0 — additive", changelog[1], StringComparison.Ordinal);
    }

    [Fact]
    public void The_v1_0_fixture_is_the_1_0_0_file_byte_for_byte()
    {
        Assert.Equal(FrozenV10Sha256, Sha256(FixtureV10Path));
        var fixture = Load(FixtureV10Path);
        Assert.Equal("1.0.0", fixture["info"]!["version"]!.GetValue<string>());
        Assert.Equal("FROZEN", fixture["info"]!["x-status"]!.GetValue<string>());
    }

    /// <summary>
    /// AC-60 / Q-06 "uyum testi": the outbound part of WMS-BUNDLE 1.0.0 is WAREHOUSE-OUTBOUND 1.0.0 path-for-path (every method, parameter, response,
    /// status code, example) and schema-for-schema (every property, type, enum value, required list) — compared as data.
    /// </summary>
    [Fact]
    public void The_1_0_0_outbound_part_equals_WAREHOUSE_OUTBOUND_1_0_0()
    {
        var outbound = Outbound;
        var wms = Load(FixtureV10Path);
        Assert.Equal("1.0.0", outbound["info"]!["version"]!.GetValue<string>());
        Assert.Equal(["/outbound-shipments", "/outbound-shipments/{outboundId}"], outbound["paths"]!.AsObject().Select(p => p.Key).ToArray());
        foreach (var (path, item) in outbound["paths"]!.AsObject())
            Assert.True(JsonNode.DeepEquals(item, wms["paths"]![path]), $"path {path} differs from WAREHOUSE-OUTBOUND 1.0.0");
        Assert.Equal(OutboundSchemas, outbound["components"]!["schemas"]!.AsObject().Select(s => s.Key).ToArray());
        foreach (var (name, schema) in outbound["components"]!["schemas"]!.AsObject())
            Assert.True(JsonNode.DeepEquals(schema, wms["components"]!["schemas"]![name]), $"schema {name} differs from WAREHOUSE-OUTBOUND 1.0.0");
        foreach (var (name, response) in outbound["components"]!["responses"]!.AsObject())
            Assert.True(JsonNode.DeepEquals(response, wms["components"]!["responses"]![name]), $"response {name} differs from WAREHOUSE-OUTBOUND 1.0.0");
        Assert.True(JsonNode.DeepEquals(outbound["components"]!["securitySchemes"], wms["components"]!["securitySchemes"]));
        Assert.True(JsonNode.DeepEquals(outbound["servers"], wms["servers"]));
        Assert.True(JsonNode.DeepEquals(outbound["security"], wms["security"]));
    }

    /// <summary>
    /// The current file's outbound part: WAREHOUSE-OUTBOUND 1.0.0 + exactly the proposal §2.3 optional members (none of them required;
    /// <c>Package</c> and <c>SkuLevel</c> untouched — <c>contents</c> / <c>Item</c> are "next contract version"), the event's correlationId a UUID,
    /// the list example's outboundId a UUID, and the S2 methods (POST on the list path, PUT on the detail path). Any other changed path,
    /// parameter, property, type, enum value, required list or status code turns this red.
    /// </summary>
    [Fact]
    public void The_current_outbound_part_is_WAREHOUSE_OUTBOUND_1_0_0_plus_the_section_2_3_members_only()
    {
        var outbound = Outbound;
        var wms = Current;
        foreach (var (path, item) in outbound["paths"]!.AsObject())
        {
            var mine = (JsonObject)wms["paths"]![path]!.DeepClone();
            Assert.Equal(path == "/outbound-shipments" ? ["get", "post"] : ["get", "put"], mine.Select(m => m.Key).ToArray());
            mine.Remove(path == "/outbound-shipments" ? "post" : "put");
            var example = mine["get"]!["responses"]!["200"]!["content"]!["application/json"]!.AsObject();
            if (path == "/outbound-shipments")
            {
                Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", example["example"]!["items"]![0]!["outboundId"]!.GetValue<string>());
                example.Remove("example");
                ((JsonObject)item!["get"]!["responses"]!["200"]!["content"]!["application/json"]!).Remove("example");
            }

            Assert.True(JsonNode.DeepEquals(item, mine), $"path {path} differs from WAREHOUSE-OUTBOUND 1.0.0 beyond the 1.1.0 additions");
        }

        var schemas = (JsonObject)wms["components"]!["schemas"]!.DeepClone();
        foreach (var (schema, members) in Section23)
        {
            var properties = schemas[schema]!["properties"]!.AsObject();
            Assert.All(members, m => Assert.True(properties.ContainsKey(m), $"{schema}.{m} (§2.3) missing"));
            Assert.All(members, m => Assert.DoesNotContain(m, schemas[schema]!["required"]!.AsArray().Select(r => r!.GetValue<string>())));
            foreach (var m in members) properties.Remove(m);
        }

        Assert.Equal("uuid", schemas["OutboundShipmentReadyEvent"]!["properties"]!["correlationId"]!["format"]!.GetValue<string>());
        schemas["OutboundShipmentReadyEvent"]!["properties"]!["correlationId"]!.AsObject().Remove("format");
        foreach (var (name, schema) in outbound["components"]!["schemas"]!.AsObject())
            Assert.True(JsonNode.DeepEquals(schema, schemas[name]), $"schema {name} differs from WAREHOUSE-OUTBOUND 1.0.0 beyond §2.3");
        Assert.True(JsonNode.DeepEquals(outbound["components"]!["responses"]!["UnknownOutbound"], wms["components"]!["responses"]!["UnknownOutbound"]));
    }

    /// <summary>
    /// 1.1.0 is additive over the 1.0.0 fixture: every member of 1.0.0 is still there with the same value. Allowed: new keys (paths, methods,
    /// schemas, properties, responses, extensions) except a NEW constraint keyword on an existing schema; enum lists and x- lists only grow at
    /// their end; descriptions / summaries / examples are free text. Named exceptions (pick kind, 1.1.0): the task number and postingKey
    /// patterns admit PCK / wms:pck; OutboundShipmentReadyEvent.correlationId gains format uuid (§2.3).
    /// </summary>
    [Fact]
    public void The_1_1_0_change_is_additive_over_1_0_0()
    {
        var before = Load(FixtureV10Path);
        var after = Current;
        var allowed = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["$.components.schemas.WarehouseTaskMembers.properties.number.pattern"] = "^[A-Z]{2,5}-(PUT|PCK)-\\d{6,}$",
            ["$.components.schemas.TaskConfirmation.properties.postingKey.pattern"] = "^wms:(put|pck):[0-9a-f-]{36}:[1-9][0-9]*$",
            ["$.components.schemas.OutboundShipmentReadyEvent.properties.correlationId.format"] = "uuid",
            ["$.info.version"] = "1.1.0"
        };
        var problems = new List<string>();
        Compare(before, after, "$", allowed, problems);
        Assert.True(problems.Count == 0, string.Join("\n", problems));
        Assert.Equal(1, after["info"]!["x-changelog"]!.AsArray().Count - before["info"]!["x-changelog"]!.AsArray().Count);
    }

    private static readonly HashSet<string> Constraints = new(StringComparer.Ordinal)
    {
        "type", "format", "pattern", "enum", "const", "required", "minimum", "maximum", "minLength", "maxLength", "minItems", "maxItems",
        "additionalProperties", "unevaluatedProperties", "$ref", "items", "nullable"
    };

    private static void Compare(JsonNode? before, JsonNode? after, string at, IReadOnlyDictionary<string, string> allowed, List<string> problems)
    {
        var key = at[(at.LastIndexOf('.') + 1)..];
        if (allowed.TryGetValue(at, out var expected))
        {
            if (after?.GetValue<string>() != expected) problems.Add($"{at}: expected the named change to '{expected}'");
            return;
        }

        if (key is "description" or "summary" or "example") return;
        switch (before)
        {
            case JsonObject b when after is JsonObject a:
                foreach (var (name, value) in b) Compare(value, a[name], $"{at}.{name}", allowed, problems);
                foreach (var (name, _) in a.Where(p => !b.ContainsKey(p.Key)))
                {
                    if (Constraints.Contains(name) && !allowed.ContainsKey($"{at}.{name}")) problems.Add($"{at}: new constraint '{name}' on an existing schema");
                    else if (allowed.ContainsKey($"{at}.{name}")) Compare(null, a[name], $"{at}.{name}", allowed, problems);
                }

                break;
            case JsonArray b when after is JsonArray a:
                var growing = key == "enum" || key.StartsWith("x-", StringComparison.Ordinal);
                if (growing ? a.Count < b.Count : a.Count != b.Count) problems.Add($"{at}: list changed ({b.Count} -> {a.Count})");
                for (var i = 0; i < Math.Min(a.Count, b.Count); i++) Compare(b[i], a[i], $"{at}[{i}]", allowed, problems);
                break;
            default:
                if (!JsonNode.DeepEquals(before, after)) problems.Add($"{at}: {before?.ToJsonString() ?? "absent"} -> {after?.ToJsonString() ?? "removed"}");
                break;
        }
    }

    /// <summary>
    /// The operations are exactly the routes the warehouse controllers serve (method + path, ids as <c>{}</c>), each with the controller action's
    /// <c>[HasPermission]</c> key as its <c>x-permission</c> (the two frozen GETs: the outbound read key, not named in the frozen copy).
    /// </summary>
    [Fact]
    public void Every_served_warehouse_route_is_an_operation_with_its_permission_key()
    {
        string Normal(string path) => Regex.Replace(Regex.Replace(path, @"\{[^}]+\}", "{}"), "/+$", string.Empty);
        var contract = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (path, item) in Current["paths"]!.AsObject())
        foreach (var (method, operation) in item!.AsObject())
            contract[$"{method.ToUpperInvariant()} {Normal(WarehouseContract.BasePath + path)}"] = operation!["x-permission"]?.GetValue<string>();

        var served = new Dictionary<string, string>(StringComparer.Ordinal);
        var controllers = typeof(Diten.InventoryService.Api.Controllers.Warehouse.PutawayTasksController).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(Diten.InventoryService.Api.Controllers.Warehouse.PutawayTasksController).Namespace && typeof(ControllerBase).IsAssignableFrom(t));
        foreach (var controller in controllers)
        {
            var prefix = "/" + controller.GetCustomAttribute<RouteAttribute>()!.Template;
            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            foreach (var verb in action.GetCustomAttributes<HttpMethodAttribute>())
            foreach (var method in verb.HttpMethods)
            {
                var permission = action.GetCustomAttribute<HasPermissionAttribute>()!.Policy!["Permission:".Length..];
                served[$"{method} {Normal(prefix + (string.IsNullOrEmpty(verb.Template) ? string.Empty : "/" + verb.Template))}"] = permission;
            }
        }

        Assert.Equal(served.Keys.Order(StringComparer.Ordinal), contract.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(20 + 2 + 26, served.Count); // S1 20 (§4) + the frozen 2 + §11's 26 (S2 23 + S3 3)
        foreach (var (route, permission) in served)
        {
            if (contract[route] is null) Assert.Equal(WarehousePermissions.OutboundShipmentsRead, permission); // the two frozen GETs
            else Assert.Equal(permission, contract[route]);
        }
    }

    /// <summary>
    /// Rule 4 and its one named exception (pack §8.23): across the WMS part of the file JSON null is admitted only by
    /// <c>WarehouseExceptionDetails.stagingDifference</c> and <c>.differenceDeferredBy</c> (the copied WAREHOUSE-OUTBOUND schemas keep their
    /// 1.0.0 <c>nullable</c> as frozen). The validator admits null there and refuses it on any other member of the same details object.
    /// </summary>
    [Fact]
    public void JSON_null_is_admitted_only_at_the_two_named_ShortDispatchUnresolved_members()
    {
        var schemas = Current["components"]!["schemas"]!.AsObject();
        var nullable = new List<string>();
        void Scan(JsonNode? node, string at)
        {
            switch (node)
            {
                case JsonObject o:
                    if (o["type"] is JsonArray types && types.Any(t => t is null || t.GetValue<string>() == "null")) nullable.Add(at);
                    if (o["enum"] is JsonArray values && values.Any(v => v is null || v.GetValue<string>() is "null" or "")) nullable.Add(at);
                    if (o["nullable"] is not null) nullable.Add(at);
                    foreach (var (k, v) in o) Scan(v, $"{at}.{k}");
                    break;
                case JsonArray a:
                    for (var i = 0; i < a.Count; i++) Scan(a[i], $"{at}[{i}]");
                    break;
            }
        }

        foreach (var (name, schema) in schemas.Where(s => !OutboundSchemas.Contains(s.Key))) Scan(schema, name);
        Assert.Equal(["StagingDifferenceDeferral", "WarehouseExceptionDetails.properties.stagingDifference"], nullable.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("#/components/schemas/StagingDifferenceDeferral",
            schemas["WarehouseExceptionDetails"]!["properties"]!["differenceDeferredBy"]!["$ref"]!.GetValue<string>());
        Assert.Contains("NAMED EXCEPTION TO RULE 4", Current["info"]!["description"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Contains("NAMED EXCEPTION TO RULE 4", schemas["WarehouseExceptionDetails"]!["description"]!.GetValue<string>(), StringComparison.Ordinal);

        const string id = "6f1c2c5e-0d7b-4b0e-9f6a-1c2d3e4f5a6b";
        JsonObject Page(JsonObject details) => new()
        {
            ["items"] = new JsonArray(new JsonObject
            {
                ["exceptionId"] = $"ShortDispatchUnresolved:{id}:{id}", ["kind"] = "ShortDispatchUnresolved", ["outboundId"] = id, ["warehouseId"] = id,
                ["details"] = details, ["openedAtUtc"] = "2026-11-02T10:00:00Z", ["status"] = "Open"
            }),
            ["total"] = 1, ["page"] = 1, ["pageSize"] = 25, ["contractVersion"] = "v1"
        };
        JsonObject Details() => new()
        {
            ["outboundNumber"] = "GMG-OUT-000001", ["skuId"] = id, ["expected"] = "30.000", ["issued"] = "25.000", ["reason"] = "LotMissing",
            ["stagingLocationId"] = id, ["stagingDifference"] = null, ["differenceDeferredBy"] = "PendingConfirmation"
        };

        Assert.Empty(WmsContract.Validate(WmsContract.ExceptionPage, Page(Details()).ToJsonString()));
        var computed = Details();
        computed["stagingDifference"] = new JsonArray(new JsonObject { ["lotId"] = "L1", ["onHand"] = "5.000", ["bound"] = "0.000", ["difference"] = "5.000" });
        computed["differenceDeferredBy"] = null;
        Assert.Empty(WmsContract.Validate(WmsContract.ExceptionPage, Page(computed).ToJsonString()));
        var third = Details();
        third["lotId"] = null;
        Assert.NotEmpty(WmsContract.Validate(WmsContract.ExceptionPage, Page(third).ToJsonString()));
        var extra = Details();
        extra["note"] = "x";
        Assert.NotEmpty(WmsContract.Validate(WmsContract.ExceptionPage, Page(extra).ToJsonString()));
        var badDeferral = Details();
        badDeferral["differenceDeferredBy"] = "Later";
        Assert.NotEmpty(WmsContract.Validate(WmsContract.ExceptionPage, Page(badDeferral).ToJsonString()));
    }

    /// <summary>
    /// The WMS error catalogue names only codes the service defines (WarehouseErrorCodes), every WMS_* code the proposal §5.1 lists, and leaves out
    /// the codes the service writes that neither the proposal nor the pack names — WMS_PACKAGE_NOT_FOUND (404 of the package paths) and
    /// WMS_ASSIGNEE_DIRECTORY_UNAVAILABLE (503 of the assignee check): open owner decision at the freeze (CT report), not frozen.
    /// </summary>
    [Fact]
    public void The_error_catalogue_is_the_proposal_s_codes_and_the_service_defines_each()
    {
        var catalogue = Current["components"]!["x-error-codes"]!.AsObject().Select(c => c.Key).Where(k => !k.StartsWith("reused", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        Assert.All(catalogue, code => Assert.Contains(code, WarehouseErrorCodes.All));
        Assert.Equal([WarehouseErrorCodes.PackageNotFound], WarehouseErrorCodes.All.Except(catalogue).ToArray());
        Assert.DoesNotContain(WarehouseErrorCodes.AssigneeDirectoryUnavailable, catalogue); // not in WarehouseErrorCodes.All either
    }
}
