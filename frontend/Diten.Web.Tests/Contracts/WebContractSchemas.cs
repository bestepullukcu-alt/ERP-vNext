using System.Collections;
using System.Text.Json.Nodes;
using Diten.Web.Tests.Traceability;
using Json.Schema;
using YamlDotNet.Serialization;

namespace Diten.Web.Tests.Contracts;

/// <summary>
/// WP-MVP1-FIX-LIVE1 — the Web's fake Gateway bodies measured against the CONTRACT FILES, not against what a test author believed the
/// service sends (the 2026-10-14 live finding: Expiry and Quarantine read <c>id</c> from LOCATION rows whose contract field is
/// <c>locationId</c>, and every Web test fed them <c>id</c>, so nothing went red). The InventoryService <c>ContractSchemas</c> method,
/// unchanged in substance:
/// <list type="bullet">
/// <item>each OpenAPI 3.1 document becomes JSON Schema 2020-12 (<c>components.schemas</c> → <c>$defs</c>; inline 200 bodies lifted by
/// name);</item>
/// <item><c>nullable: true</c> → <c>type: [T, "null"]</c>;</item>
/// <item>every object schema with <c>properties</c> and no <c>additionalProperties</c> is CLOSED — a fake body carrying a member the
/// contract does not name (<c>id</c> on a <c>LocationRef</c>) fails;</item>
/// <item>formats are enforced.</item>
/// </list>
/// </summary>
public static class WebContractSchemas
{
    // LOCATION (location.openapi.yaml)
    public const string LocationList = "LocationList";
    public const string LocationSearchPage = "LocationSearchPage";
    public const string LocationRef = "LocationRef";

    // TRACE (trace.openapi.yaml)
    public const string LotList = "LotList";

    // INVENTORY-BUNDLE (inventory-bundle.openapi.yaml)
    public const string BalancePage = "BalancePage";

    // FEFO-BUNDLE (fefo.openapi.yaml)
    public const string FefoOrder = "FefoOrder";

    public static readonly ContractFile Location = new("location.openapi.yaml", doc => new Dictionary<string, JsonNode>
    {
        [LocationList] = doc["paths"]!["/"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!.DeepClone()
    });

    public static readonly ContractFile Trace = new("trace.openapi.yaml", doc => new Dictionary<string, JsonNode>
    {
        [LotList] = doc["paths"]!["/lots"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!.DeepClone()
    });

    public static readonly ContractFile Inventory = new("inventory-bundle.openapi.yaml", doc => new Dictionary<string, JsonNode>
    {
        [BalancePage] = doc["paths"]!["/balance"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!.DeepClone()
    });

    public static readonly ContractFile Fefo = new("fefo.openapi.yaml", _ => new Dictionary<string, JsonNode>());

    private static readonly object Gate = new();

    /// <summary>One contract file, compiled lazily; <see cref="Validate"/> answers the errors (empty = valid).</summary>
    public sealed class ContractFile
    {
        private readonly string _file;
        private readonly Func<JsonObject, IDictionary<string, JsonNode>> _lifted;
        private readonly Lazy<JsonObject> _defs;
        private readonly Dictionary<string, JsonSchema> _compiled = new(StringComparer.Ordinal);

        public ContractFile(string file, Func<JsonObject, IDictionary<string, JsonNode>> lifted)
        {
            _file = file;
            _lifted = lifted;
            _defs = new Lazy<JsonObject>(BuildDefs);
        }

        public string File => _file;

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

            EvaluationResults result;
            lock (Gate)
            {
                result = Schema(schemaName).Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true });
            }

            if (result.IsValid) return [];
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
                if (!_defs.Value.ContainsKey(name)) throw new ArgumentException($"{_file} has no schema {name}", nameof(name));
                var root = new JsonObject
                {
                    ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
                    ["$id"] = $"https://contracts.diten.test/web/{_file}/{name}",
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
            var path = Path.Combine(TraceViewRenderer.RepoRoot(), "docs", "analysis", "contracts", _file);
            var yaml = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build()
                .Deserialize<object>(System.IO.File.ReadAllText(path));
            var doc = (JsonObject)ToNode(yaml)!;
            var defs = new JsonObject();
            foreach (var (key, value) in doc["components"]!["schemas"]!.AsObject()) defs[key] = value!.DeepClone();
            foreach (var (key, value) in _lifted(doc)) defs[key] = value;
            foreach (var (_, schema) in defs) Normalise(schema!);
            return defs;
        }
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
                    if (obj["type"] is JsonValue type) obj["type"] = new JsonArray(type.GetValue<string>(), "null");
                }

                if (obj["properties"] is JsonObject && !obj.ContainsKey("additionalProperties")) obj["additionalProperties"] = false;
                foreach (var child in obj.Select(p => p.Value).Where(v => v is not null).ToList()) Normalise(child!);
                break;
            case JsonArray array:
                foreach (var child in array.Where(v => v is not null).ToList()) Normalise(child!);
                break;
        }
    }

    private static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        IDictionary map => new JsonObject(map.Keys.Cast<object>().Select(k => KeyValuePair.Create(k.ToString()!, ToNode(map[k])))),
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
