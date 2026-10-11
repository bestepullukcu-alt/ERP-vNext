using System.Text.Json;
using System.Text.RegularExpressions;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// MOD-0290-FU04 S4 — validates an answer against the contract file itself (the JSON rendering of the pinned YAML). The
/// OpenAPI subset the contract uses: $ref, type (one or a list with 'null'), 3.0 <c>nullable</c>, anyOf, enum,
/// required, properties, items, minItems / maxItems, maxLength, pattern, format uuid / date-time. In strict mode a
/// property the schema does not declare is a failure too (unless <c>additionalProperties: true</c>), so a field the
/// server adds without the contract shows up here first.
/// </summary>
public sealed class ProductMasterContractSchema
{
    public const string FixtureDirectory = "Fixtures/ProductMasterBundle";
    public const string V11Json = "product-master-bundle.openapi.json";
    public const string V11Yaml = "product-master-bundle.openapi.yaml";
    public const string V10Json = "product-master-bundle.v1.0.openapi.json";
    public const string V10Yaml = "product-master-bundle.v1.0.openapi.yaml";

    private static readonly Lazy<ProductMasterContractSchema> LazyV11 = new(() => Load(V11Json));

    /// <summary>Item 5: the one status list every answer uses; Deactivated only on an item.</summary>
    public static readonly IReadOnlySet<string> PublishedStatuses =
        new HashSet<string>(StringComparer.Ordinal) { "Draft", "Active", "Deactivated", "Retired" };
    private static readonly Lazy<ProductMasterContractSchema> LazyV10 = new(() => Load(V10Json));

    public static ProductMasterContractSchema V11 => LazyV11.Value;
    public static ProductMasterContractSchema V10 => LazyV10.Value;

    private readonly JsonElement _root;

    private ProductMasterContractSchema(JsonElement root) => _root = root;

    public static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, FixtureDirectory, name);

    private static ProductMasterContractSchema Load(string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FixturePath(name)));
        return new(document.RootElement.Clone());
    }

    /// <summary>
    /// Item 5 over a whole answer: every lifecycleStatus is one of the four published values, and Deactivated appears
    /// only on an item row (a Global Product / GSKU / LSKU / Finished Good is never Deactivated; v1.0 Archived never).
    /// </summary>
    public static IReadOnlyList<string> StatusFailures(JsonElement body)
    {
        var failures = new List<string>();
        Walk(body, "$");
        return failures;

        void Walk(JsonElement node, string at)
        {
            if (node.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var element in node.EnumerateArray()) Walk(element, $"{at}[{index++}]");
                return;
            }
            if (node.ValueKind != JsonValueKind.Object) return;
            if (node.TryGetProperty("lifecycleStatus", out var status))
            {
                var value = status.GetString();
                if (value is null || !PublishedStatuses.Contains(value))
                    failures.Add($"{at}.lifecycleStatus '{value}' is not one of {string.Join("/", PublishedStatuses)}");
                var isItem = node.TryGetProperty("itemKind", out var kind)
                    ? kind.GetString() == "Item"
                    : node.TryGetProperty("skuLevel", out var level) && level.GetString() == "Item";
                if (value == "Deactivated" && !isItem) failures.Add($"{at}: a product row is Deactivated");
            }
            foreach (var property in node.EnumerateObject()) Walk(property.Value, $"{at}.{property.Name}");
        }
    }

    public JsonElement Root => _root;

    /// <summary>The schema of an operation's answer (by operationId and status), components/responses resolved.</summary>
    public JsonElement ResponseSchema(string operationId, int status)
    {
        foreach (var path in _root.GetProperty("paths").EnumerateObject())
        foreach (var operation in path.Value.EnumerateObject())
        {
            if (operation.Value.ValueKind != JsonValueKind.Object
                || !operation.Value.TryGetProperty("operationId", out var id) || id.GetString() != operationId) continue;
            var response = operation.Value.GetProperty("responses").GetProperty(status.ToString());
            if (response.TryGetProperty("$ref", out var reference)) response = Resolve(reference.GetString()!);
            return response.GetProperty("content").GetProperty("application/json").GetProperty("schema");
        }
        throw new InvalidOperationException($"No operation {operationId} / {status} in the contract.");
    }

    public IReadOnlyList<string> ValidateResponse(string operationId, int status, JsonElement body, bool strict = true)
    {
        var failures = new List<string>();
        Validate(ResponseSchema(operationId, status), body, "$", strict, failures);
        return failures;
    }

    public IReadOnlyList<string> ValidateAgainst(string schemaName, JsonElement body, bool strict = true)
    {
        var failures = new List<string>();
        Validate(Resolve("#/components/schemas/" + schemaName), body, "$", strict, failures);
        return failures;
    }

    public IReadOnlyList<string> EnumValues(string schemaName) =>
        Resolve("#/components/schemas/" + schemaName).GetProperty("enum").EnumerateArray()
            .Select(x => x.GetString()!).ToArray();

    private JsonElement Resolve(string reference)
    {
        if (!reference.StartsWith("#/", StringComparison.Ordinal))
            throw new InvalidOperationException("Only local references: " + reference);
        var node = _root;
        foreach (var part in reference[2..].Split('/')) node = node.GetProperty(part);
        return node;
    }

    private void Validate(JsonElement schema, JsonElement value, string at, bool strict, List<string> failures)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            Validate(Resolve(reference.GetString()!), value, at, strict, failures);
            return;
        }
        if (schema.TryGetProperty("anyOf", out var anyOf))
        {
            var matched = anyOf.EnumerateArray().Any(option =>
            {
                var trial = new List<string>();
                Validate(option, value, at, strict, trial);
                return trial.Count == 0;
            });
            if (!matched) failures.Add($"{at}: matches no anyOf option ({value.GetRawText()})");
            return;
        }

        var types = Types(schema);
        if (value.ValueKind == JsonValueKind.Null)
        {
            var nullable = schema.TryGetProperty("nullable", out var n) && n.ValueKind == JsonValueKind.True;
            if (!nullable && !types.Contains("null")) failures.Add($"{at}: null is not allowed");
            return;
        }
        if (types.Count > 0 && !types.Any(type => Is(type, value)))
        {
            failures.Add($"{at}: {value.ValueKind} is not {string.Join("|", types)}");
            return;
        }
        if (schema.TryGetProperty("enum", out var values)
            && !values.EnumerateArray().Any(x => x.ValueKind == value.ValueKind && x.GetRawText() == value.GetRawText()))
            failures.Add($"{at}: {value.GetRawText()} is not in the enum");

        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()!;
            if (schema.TryGetProperty("format", out var format))
            {
                if (format.GetString() == "uuid" && !Guid.TryParseExact(text, "D", out _))
                    failures.Add($"{at}: '{text}' is not a uuid");
                if (format.GetString() == "date-time" && !DateTimeOffset.TryParse(text, out _))
                    failures.Add($"{at}: '{text}' is not a date-time");
            }
            if (schema.TryGetProperty("pattern", out var pattern) && !Regex.IsMatch(text, pattern.GetString()!))
                failures.Add($"{at}: '{text}' does not match {pattern.GetString()}");
            if (schema.TryGetProperty("maxLength", out var maxLength) && text.Length > maxLength.GetInt32())
                failures.Add($"{at}: longer than {maxLength.GetInt32()}");
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            var count = value.GetArrayLength();
            if (schema.TryGetProperty("minItems", out var min) && count < min.GetInt32())
                failures.Add($"{at}: fewer than {min.GetInt32()} items");
            if (schema.TryGetProperty("maxItems", out var max) && count > max.GetInt32())
                failures.Add($"{at}: more than {max.GetInt32()} items");
            if (schema.TryGetProperty("items", out var items))
            {
                var index = 0;
                foreach (var element in value.EnumerateArray()) Validate(items, element, $"{at}[{index++}]", strict, failures);
            }
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            var properties = schema.TryGetProperty("properties", out var declared) ? declared : default;
            if (schema.TryGetProperty("required", out var required))
                foreach (var name in required.EnumerateArray().Select(x => x.GetString()!))
                    if (!value.TryGetProperty(name, out _)) failures.Add($"{at}: required '{name}' is missing");
            var open = schema.TryGetProperty("additionalProperties", out var additional)
                       && additional.ValueKind == JsonValueKind.True;
            foreach (var property in value.EnumerateObject())
            {
                if (properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty(property.Name, out var child))
                    Validate(child, property.Value, $"{at}.{property.Name}", strict, failures);
                else if (strict && !open)
                    failures.Add($"{at}: '{property.Name}' is not in the contract");
            }
        }
    }

    private static List<string> Types(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type)) return [];
        return type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Select(x => x.GetString()!).ToList()
            : [type.GetString()!];
    }

    private static bool Is(string type, JsonElement value) => type switch
    {
        "string" => value.ValueKind == JsonValueKind.String,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "number" => value.ValueKind == JsonValueKind.Number,
        "array" => value.ValueKind == JsonValueKind.Array,
        "object" => value.ValueKind == JsonValueKind.Object,
        "null" => value.ValueKind == JsonValueKind.Null,
        _ => false
    };
}
