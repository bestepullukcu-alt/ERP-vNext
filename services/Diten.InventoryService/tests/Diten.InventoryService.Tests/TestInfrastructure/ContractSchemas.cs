using System.Collections;
using System.Text.Json.Nodes;
using Json.Schema;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// Validates response bodies against the contract files themselves — the FROZEN <c>inventory-bundle.openapi.yaml</c>
/// (AC15) and, since MOD-0173-FU02, <c>location.openapi.yaml</c> v1.1 (FU02 AC-15). Each OpenAPI 3.1 document is
/// turned into JSON Schema 2020-12:
/// <list type="bullet">
/// <item><c>components.schemas</c> become <c>$defs</c> (refs rewritten accordingly); inline 200 bodies declared under
/// <c>paths</c> are lifted by name.</item>
/// <item>LOCATION v1 declares <c>getLocationPath</c> / <c>validateLocations</c> by EXAMPLE only (no schema, and the
/// frozen v1 blocks may not be edited): their schema is inferred from that example — every member and type the example
/// shows, nothing else (closed, see below).</item>
/// <item><c>nullable: true</c> (an OpenAPI 3.0 keyword JSON Schema does not know) becomes <c>type: [T, "null"]</c>.</item>
/// <item>STRICTER than the contract on one point, deliberately: every object schema that declares
/// <c>properties</c> but no <c>additionalProperties</c> is closed (<c>false</c>). The service must not put a field on
/// the wire that the contract does not name — this is how "no invented field" is checked, not assumed.</item>
/// <item>Formats (<c>uuid</c>, <c>date-time</c>, <c>date</c>) are enforced, not just annotated.</item>
/// </list>
/// </summary>
public static class ContractSchemas
{
    public const string MovementResponse = "MovementResponse";
    public const string AvailabilityView = "AvailabilityView";
    public const string BalancePage = "BalancePage";
    public const string TransactionPage = "TransactionPage";
    public const string Error = "Error";

    /// <summary>INVENTORY-BUNDLE 1.1.0 (WP-0173-PM2) <c>getStockPresence</c>.</summary>
    public const string StockPresence = "StockPresence";
    public const string ReservationResponse = "ReservationResponse";

    // LOCATION (v1 frozen + v1.1 owner).
    public const string LocationRef = "LocationRef";
    public const string LocationList = "LocationList";
    public const string LocationPath = "LocationPath";
    public const string LocationValidation = "LocationValidation";
    public const string LocationDetail = "LocationDetail";
    public const string LocationSearchPage = "LocationSearchPage";
    public const string LocationHistoryPage = "LocationHistoryPage";
    public const string LocationStockPresence = "LocationStockPresence";
    public const string BulkBinsResult = "BulkBinsResult";
    public const string BulkBins200 = "BulkBins200";

    private static readonly Contract Inventory = new("inventory-bundle.openapi.yaml", "inventory-bundle", doc => new Dictionary<string, JsonNode>
    {
        [BalancePage] = doc["paths"]!["/balance"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!.DeepClone(),
        [TransactionPage] = doc["paths"]!["/transactions"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!.DeepClone()
    });

    private static readonly Contract Location = new("location.openapi.yaml", "location", doc => new Dictionary<string, JsonNode>
    {
        [LocationList] = doc["paths"]!["/"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!.DeepClone(),
        [LocationPath] = InferFromExample(doc["paths"]!["/{id}/path"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["example"]!),
        [LocationValidation] = InferFromExample(doc["paths"]!["/validate"]!["post"]!["responses"]!["200"]!["content"]!["application/json"]!["example"]!),
        [BulkBins200] = doc["paths"]!["/{id}/bins/bulk"]!["post"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!.DeepClone()
    });

    private static readonly object Gate = new();

    public static string ContractPath => RepoPaths.Contract("inventory-bundle.openapi.yaml");

    public static string LocationContractPath => RepoPaths.Contract("location.openapi.yaml");

    /// <summary>Returns the validation errors of <paramref name="body"/> against the named INVENTORY-BUNDLE schema (empty = valid).</summary>
    public static IReadOnlyList<string> Validate(string schemaName, string body) => Inventory.Validate(schemaName, body);

    /// <summary>Returns the validation errors of <paramref name="body"/> against the named LOCATION schema (empty = valid).</summary>
    public static IReadOnlyList<string> ValidateLocation(string schemaName, string body) => Location.Validate(schemaName, body);

    // TRACE-BUNDLE (MOD-0174, v1 frozen + v1.1 — WP-0174-P1). listLots declares its 200 body inline (v1): lifted by name.
    public const string LotRef = "LotRef";
    public const string LotList = "LotList";
    public const string LotDetail = "LotDetail";
    public const string LotHistoryPage = "LotHistoryPage";
    public const string SerialRef = "SerialRef";
    public const string SerialPage = "SerialPage";
    public const string Genealogy = "Genealogy";
    public const string GenealogyLink = "GenealogyLink";
    public const string ValidateResponse = "ValidateResponse";
    public const string Gs1CheckResult = "Gs1CheckResult";
    public const string PendingApproval = "PendingApproval";

    private static readonly Contract Trace = new("trace.openapi.yaml", "trace", doc => new Dictionary<string, JsonNode>
    {
        [LotList] = doc["paths"]!["/lots"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!.DeepClone()
    });

    public static string TraceContractPath => RepoPaths.Contract("trace.openapi.yaml");

    /// <summary>Returns the validation errors of <paramref name="body"/> against the named TRACE schema (empty = valid).</summary>
    public static IReadOnlyList<string> ValidateTrace(string schemaName, string body) => Trace.Validate(schemaName, body);

    // QUARANTINE-BUNDLE (MOD-0175, frozen 1.0.0 by WP-0175-Q1). The arrays some operations return are lifted by name.
    public const string HoldRef = "HoldRef";
    public const string HoldDetail = "HoldDetail";
    public const string HoldList = "HoldList";
    public const string HoldRefArray = "HoldRefArray";
    public const string Decision = "Decision";
    public const string BlockedStockRef = "BlockedStockRef";
    public const string BlockedStockDetail = "BlockedStockDetail";
    public const string BlockedList = "BlockedList";
    public const string BlockedRefArray = "BlockedRefArray";
    public const string Settings = "Settings";
    public const string BlockReason = "BlockReason";
    public const string BlockReasonArray = "BlockReasonArray";
    public const string WorkflowTemplateResult = "WorkflowTemplateResult";
    public const string SignableSubject = "SignableSubject";
    public const string RetestItemArray = "RetestItemArray";

    private static readonly Contract Quarantine = new("quarantine.openapi.yaml", "quarantine", doc => new Dictionary<string, JsonNode>
    {
        [HoldRefArray] = doc["paths"]!["/holds"]!["post"]!["responses"]!["201"]!["content"]!["application/json"]!["schema"]!.DeepClone(),
        [BlockedRefArray] = doc["paths"]!["/blocked"]!["post"]!["responses"]!["201"]!["content"]!["application/json"]!["schema"]!.DeepClone(),
        [BlockReasonArray] = doc["paths"]!["/block-reasons"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!.DeepClone(),
        [RetestItemArray] = doc["paths"]!["/retest"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!.DeepClone()
    });

    public static string QuarantineContractPath => RepoPaths.Contract("quarantine.openapi.yaml");

    /// <summary>Returns the validation errors of <paramref name="body"/> against the named QUARANTINE schema (empty = valid).</summary>
    public static IReadOnlyList<string> ValidateQuarantine(string schemaName, string body) => Quarantine.Validate(schemaName, body);

    private sealed class Contract
    {
        private readonly string _file;
        private readonly string _name;
        private readonly Func<JsonObject, IDictionary<string, JsonNode>> _lifted;
        private readonly Lazy<JsonObject> _defs;
        private readonly Dictionary<string, JsonSchema> _compiled = new(StringComparer.Ordinal);

        public Contract(string file, string name, Func<JsonObject, IDictionary<string, JsonNode>> lifted)
        {
            _file = file;
            _name = name;
            _lifted = lifted;
            _defs = new Lazy<JsonObject>(BuildDefs);
        }

        public IReadOnlyList<string> Validate(string schemaName, string body)
        {
            JsonNode? instance;
            try
            {
                instance = JsonNode.Parse(body);
            }
            catch (Exception ex)
            {
                return [$"body is not JSON: {ex.Message}"];
            }

            // Serialised on purpose: with the API test classes running in parallel, concurrent Evaluate calls on the same
            // compiled schema were measured to let an invalid body through (a boolean sent as "false" passed once in a
            // full run, never in isolation). A validator that can say "valid" by accident is worse than a slow one.
            EvaluationResults result;
            lock (Gate)
            {
                result = Schema(schemaName).Evaluate(instance, new EvaluationOptions
                {
                    OutputFormat = OutputFormat.List,
                    RequireFormatValidation = true
                });
            }

            if (result.IsValid)
            {
                return [];
            }

            return (result.Details ?? [])
                .Where(d => d.Errors is { Count: > 0 })
                .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Key} — {e.Value}"))
                .DefaultIfEmpty("invalid (no detail)")
                .ToList();
        }

        private JsonSchema Schema(string name)
        {
            lock (Gate)
            {
                if (_compiled.TryGetValue(name, out var cached)) return cached;
                var root = new JsonObject
                {
                    ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
                    ["$id"] = $"https://contracts.diten.test/{_name}/v1/{name}",
                    ["$defs"] = _defs.Value.DeepClone(),
                    ["$ref"] = $"#/$defs/{name}"
                };
                var schema = JsonSchema.FromText(root.ToJsonString());
                _compiled[name] = schema;
                return schema;
            }
        }

        private JsonObject BuildDefs()
        {
            var yaml = new DeserializerBuilder()
                .WithAttemptingUnquotedStringTypeDeserialization()
                .Build()
                .Deserialize<object>(File.ReadAllText(RepoPaths.Contract(_file)));
            var doc = (JsonObject)ToNode(yaml)!;

            var defs = new JsonObject();
            foreach (var (key, value) in doc["components"]!["schemas"]!.AsObject())
            {
                defs[key] = value!.DeepClone();
            }

            foreach (var (key, value) in _lifted(doc))
            {
                defs[key] = value;
            }

            foreach (var (_, schema) in defs)
            {
                Normalise(schema!);
            }

            return defs;
        }
    }

    /// <summary>
    /// The schema an example shows: objects list exactly the members present (across all array elements), arrays take
    /// the merged element shape, scalars their JSON type. No member is required (an example shows one instance).
    /// </summary>
    private static JsonNode InferFromExample(JsonNode example)
    {
        switch (example)
        {
            case JsonObject obj:
            {
                var properties = new JsonObject();
                foreach (var (key, value) in obj)
                {
                    properties[key] = value is null ? new JsonObject { ["type"] = "null" } : InferFromExample(value);
                }

                return new JsonObject { ["type"] = "object", ["properties"] = properties };
            }
            case JsonArray array:
            {
                JsonObject? merged = null;
                foreach (var item in array.Where(i => i is not null))
                {
                    var shape = (JsonObject)InferFromExample(item!);
                    merged = merged is null ? shape : Merge(merged, shape);
                }

                return new JsonObject { ["type"] = "array", ["items"] = merged ?? new JsonObject() };
            }
            case JsonValue value when value.TryGetValue<bool>(out _):
                return new JsonObject { ["type"] = "boolean" };
            case JsonValue value when value.TryGetValue<string>(out _):
                return new JsonObject { ["type"] = "string" };
            default:
                return new JsonObject { ["type"] = "number" };
        }
    }

    private static JsonObject Merge(JsonObject a, JsonObject b)
    {
        if (a["properties"] is JsonObject pa && b["properties"] is JsonObject pb)
        {
            foreach (var (key, value) in pb)
            {
                if (!pa.ContainsKey(key)) pa[key] = value!.DeepClone();
            }
        }

        return a;
    }

    private static void Normalise(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj["$ref"] is JsonValue refValue && refValue.GetValue<string>() is { } target
                    && target.StartsWith("#/components/schemas/", StringComparison.Ordinal))
                {
                    obj["$ref"] = "#/$defs/" + target["#/components/schemas/".Length..];
                }

                if (obj["nullable"] is JsonValue nullable && nullable.GetValue<bool>())
                {
                    obj.Remove("nullable");
                    if (obj["type"] is JsonValue type)
                    {
                        obj["type"] = new JsonArray(type.GetValue<string>(), "null");
                    }
                }

                if (obj["properties"] is JsonObject && !obj.ContainsKey("additionalProperties"))
                {
                    obj["additionalProperties"] = false;
                }

                foreach (var child in obj.Select(p => p.Value).Where(v => v is not null).ToList())
                {
                    Normalise(child!);
                }

                break;
            case JsonArray array:
                foreach (var child in array.Where(v => v is not null).ToList())
                {
                    Normalise(child!);
                }

                break;
        }
    }

    private static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        IDictionary map => new JsonObject(map.Keys.Cast<object>()
            .Select(k => KeyValuePair.Create(k.ToString()!, ToNode(map[k])))),
        string s => JsonValue.Create(s),
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        // YamlDotNet's unquoted-type inference returns the SMALLEST fitting integer type (minimum: 1 → byte).
        byte or sbyte or short or ushort or uint => JsonValue.Create(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture)),
        ulong u => JsonValue.Create(u),
        double d => JsonValue.Create(d),
        float f => JsonValue.Create(f),
        decimal m => JsonValue.Create(m),
        IEnumerable list => new JsonArray(list.Cast<object?>().Select(ToNode).ToArray()),
        _ => JsonValue.Create(value.ToString())
    };
}
