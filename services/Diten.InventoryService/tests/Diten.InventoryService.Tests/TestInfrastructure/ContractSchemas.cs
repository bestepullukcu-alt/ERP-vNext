using System.Collections;
using System.Text.Json.Nodes;
using Json.Schema;
using YamlDotNet.Serialization;

namespace Diten.InventoryService.Tests.TestInfrastructure;

/// <summary>
/// Validates response bodies against the FROZEN <c>inventory-bundle.openapi.yaml</c> itself (AC15). The OpenAPI 3.1
/// document is turned into JSON Schema 2020-12:
/// <list type="bullet">
/// <item><c>components.schemas</c> become <c>$defs</c> (refs rewritten accordingly); the inline 200 bodies of
/// <c>getBalance</c> / <c>getTransactions</c> are lifted from <c>paths</c>.</item>
/// <item><c>nullable: true</c> (an OpenAPI 3.0 keyword JSON Schema does not know) becomes <c>type: [T, "null"]</c>.</item>
/// <item>STRICTER than the contract on one point, deliberately: every object schema that declares
/// <c>properties</c> but no <c>additionalProperties</c> is closed (<c>false</c>). The service must not put a field on
/// the wire that the frozen contract does not name — this is how "no invented field" is checked, not assumed.</item>
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

    private static readonly Lazy<JsonObject> Defs = new(BuildDefs);
    private static readonly Dictionary<string, JsonSchema> Compiled = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    public static string ContractPath => RepoPaths.Contract("inventory-bundle.openapi.yaml");

    /// <summary>Returns the validation errors of <paramref name="body"/> against the named contract schema (empty = valid).</summary>
    public static IReadOnlyList<string> Validate(string schemaName, string body)
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

    private static JsonSchema Schema(string name)
    {
        lock (Gate)
        {
            if (Compiled.TryGetValue(name, out var cached)) return cached;
            var root = new JsonObject
            {
                ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
                ["$id"] = $"https://contracts.diten.test/inventory-bundle/v1/{name}",
                ["$defs"] = Defs.Value.DeepClone(),
                ["$ref"] = $"#/$defs/{name}"
            };
            var schema = JsonSchema.FromText(root.ToJsonString());
            Compiled[name] = schema;
            return schema;
        }
    }

    private static JsonObject BuildDefs()
    {
        var yaml = new DeserializerBuilder()
            .WithAttemptingUnquotedStringTypeDeserialization()
            .Build()
            .Deserialize<object>(File.ReadAllText(ContractPath));
        var doc = (JsonObject)ToNode(yaml)!;

        var defs = new JsonObject();
        foreach (var (key, value) in doc["components"]!["schemas"]!.AsObject())
        {
            defs[key] = value!.DeepClone();
        }

        defs[BalancePage] = doc["paths"]!["/balance"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!.DeepClone();
        defs[TransactionPage] = doc["paths"]!["/transactions"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!.DeepClone();

        foreach (var (_, schema) in defs)
        {
            Normalise(schema!);
        }

        return defs;
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
        double d => JsonValue.Create(d),
        float f => JsonValue.Create(f),
        decimal m => JsonValue.Create(m),
        IEnumerable list => new JsonArray(list.Cast<object?>().Select(ToNode).ToArray()),
        _ => JsonValue.Create(value.ToString())
    };
}
