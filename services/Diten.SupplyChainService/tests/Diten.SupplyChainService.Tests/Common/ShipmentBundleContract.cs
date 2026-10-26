using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace Diten.SupplyChainService.Tests.Common;

/// <summary>
/// Validates JSON against the FROZEN shipment-bundle contract (docs/analysis/contracts/shipment-bundle.openapi.yaml,
/// OpenAPI 3.1 / JSON Schema 2020-12), read from the repository at test time — never a checked-in copy, so a contract
/// change is measured, not mirrored. Covers exactly the keywords the contract uses: $ref (with siblings), type (incl.
/// ["x","null"]), format uuid/date-time/date, required, properties, additionalProperties (bool or schema), items, minItems,
/// const, enum, pattern, minLength/maxLength (code points), minimum/maximum, allOf, anyOf, oneOf. Annotations (description, example(s), …) are ignored. §16 evidence audit 2026-10-19.
/// </summary>
public static class ShipmentBundleContract
{
    public const string RelativePath = "docs/analysis/contracts/shipment-bundle.openapi.yaml";
    private static readonly Lazy<JsonObject> Document = new(Load);
    public static JsonObject Root => Document.Value;

    /// <summary>Errors for a response body of <paramref name="method"/> <paramref name="pathTemplate"/> with
    /// <paramref name="status"/> (paths as the contract writes them, e.g. "/shipments/{shipmentId}").</summary>
    public static IReadOnlyList<string> ValidateResponse(string pathTemplate, string method, int status, JsonNode? body)
    {
        var operation = Root["paths"]?[pathTemplate]?[method.ToLowerInvariant()] as JsonObject
                        ?? throw new InvalidOperationException($"No operation {method} {pathTemplate} in the contract.");
        var responses = operation["responses"] as JsonObject ?? throw new InvalidOperationException("No responses.");
        var response = responses[status.ToString(CultureInfo.InvariantCulture)] as JsonObject;
        if (response is null) return [$"{method} {pathTemplate}: status {status} is not a published response"];
        response = Deref(response);
        var schema = response["content"]?["application/json"]?["schema"];
        if (schema is null) return body is null ? [] : [$"{method} {pathTemplate} {status}: contract publishes no JSON body"];
        var errors = new List<string>();
        Validate(schema, body, "$", errors);
        return errors;
    }

    public static IReadOnlyList<string> Validate(JsonNode schema, JsonNode? value)
    {
        var errors = new List<string>();
        Validate(schema, value, "$", errors);
        return errors;
    }

    private static JsonObject Deref(JsonObject node)
    {
        while (node["$ref"] is JsonValue r) node = (JsonObject)Pointer(r.GetValue<string>());
        return node;
    }

    public static JsonNode Pointer(string reference)
    {
        if (!reference.StartsWith("#/", StringComparison.Ordinal)) throw new InvalidOperationException($"External $ref {reference}");
        JsonNode? node = Root;
        foreach (var raw in reference[2..].Split('/'))
            node = node?[raw.Replace("~1", "/").Replace("~0", "~")];
        return node ?? throw new InvalidOperationException($"Unresolved $ref {reference}");
    }

    private static void Validate(JsonNode schemaNode, JsonNode? value, string at, List<string> errors)
    {
        if (schemaNode is JsonValue b && b.TryGetValue<bool>(out var allow)) { if (!allow) errors.Add($"{at}: not allowed"); return; }
        if (schemaNode is not JsonObject schema) return;
        if (schema["$ref"] is JsonValue reference) Validate(Pointer(reference.GetValue<string>()), value, at, errors);
        if (schema["allOf"] is JsonArray all) foreach (var s in all) Validate(s!, value, at, errors);
        if (schema["anyOf"] is JsonArray any && !any.Any(s => Validate(s!, value).Count == 0)) errors.Add($"{at}: matches none of anyOf");
        if (schema["oneOf"] is JsonArray one)
        {
            var passing = one.Count(s => Validate(s!, value).Count == 0);
            if (passing != 1) errors.Add($"{at}: matches {passing} of oneOf (exactly 1 required)");
        }
        if (schema["type"] is JsonNode type)
        {
            var types = type is JsonArray a ? a.Select(t => t!.GetValue<string>()).ToArray() : [type.GetValue<string>()];
            if (!types.Any(t => Is(t, value))) { errors.Add($"{at}: expected {string.Join("|", types)}, got {Kind(value)}"); return; }
        }
        if (schema["const"] is JsonNode constant && !JsonNode.DeepEquals(constant, value)) errors.Add($"{at}: expected const {constant.ToJsonString()}");
        if (schema["enum"] is JsonArray choices && !choices.Any(c => JsonNode.DeepEquals(c, value))) errors.Add($"{at}: {value?.ToJsonString()} not in enum");
        if (value is JsonValue v && v.TryGetValue<string>(out var text))
        {
            var length = text.EnumerateRunes().Count();
            if (schema["minLength"] is JsonValue minL && length < minL.GetValue<long>()) errors.Add($"{at}: shorter than {minL}");
            if (schema["maxLength"] is JsonValue maxL && length > maxL.GetValue<long>()) errors.Add($"{at}: longer than {maxL}");
            if (schema["pattern"] is JsonValue p && !Regex.IsMatch(text, p.GetValue<string>())) errors.Add($"{at}: '{text}' does not match {p}");
            switch (schema["format"]?.GetValue<string>())
            {
                case "uuid" when !Guid.TryParseExact(text, "D", out _): errors.Add($"{at}: '{text}' is not a uuid"); break;
                case "date-time" when !(text.Contains('T') && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)): errors.Add($"{at}: '{text}' is not a date-time"); break;
                case "date" when !DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _): errors.Add($"{at}: '{text}' is not a date"); break;
            }
        }
        if (value is JsonObject obj)
        {
            var properties = schema["properties"] as JsonObject;
            if (schema["required"] is JsonArray required)
                foreach (var name in required.Select(r => r!.GetValue<string>()))
                    if (!obj.ContainsKey(name)) errors.Add($"{at}: missing required '{name}'");
            foreach (var (name, child) in obj)
            {
                if (properties?[name] is JsonNode propertySchema) Validate(propertySchema, child, $"{at}.{name}", errors);
                else if (schema["additionalProperties"] is JsonNode extra) Validate(extra, child, $"{at}.{name}", errors);
            }
        }
        if (value is JsonValue number && !number.TryGetValue<string>(out _) && number.TryGetValue<double>(out var x))
        {
            if (schema["minimum"] is JsonValue min && x < min.GetValue<double>()) errors.Add($"{at}: below minimum {min}");
            if (schema["maximum"] is JsonValue max && x > max.GetValue<double>()) errors.Add($"{at}: above maximum {max}");
        }
        if (value is JsonArray sized && schema["minItems"] is JsonValue minItems && sized.Count < minItems.GetValue<long>()) errors.Add($"{at}: fewer than {minItems} items");
        if (value is JsonArray array && schema["items"] is JsonNode items)
            for (var i = 0; i < array.Count; i++) Validate(items, array[i], $"{at}[{i}]", errors);
    }

    private static bool Is(string type, JsonNode? value) => type switch
    {
        "null" => value is null,
        "object" => value is JsonObject,
        "array" => value is JsonArray,
        "string" => value is JsonValue s && s.TryGetValue<string>(out _),
        "boolean" => value is JsonValue b && b.TryGetValue<bool>(out _),
        "integer" => value is JsonValue i && (i.TryGetValue<long>(out _) || (i.TryGetValue<double>(out var d) && Math.Floor(d) == d)),
        "number" => value is JsonValue n && (n.TryGetValue<double>(out _) || n.TryGetValue<long>(out _) || n.TryGetValue<decimal>(out _)),
        _ => false
    };

    private static string Kind(JsonNode? value) => value switch
    {
        null => "null", JsonObject => "object", JsonArray => "array",
        JsonValue v when v.TryGetValue<string>(out _) => "string", JsonValue v when v.TryGetValue<bool>(out _) => "boolean",
        _ => "number"
    };

    // ── YAML → JSON with the YAML core schema: quoted scalars are strings; plain scalars become null/bool/number when they
    //    spell one, else strings. (YamlDotNet's object deserializer would turn every scalar into a string.)
    private static JsonObject Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        var path = Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("repo root"), RelativePath);
        var stream = new YamlStream();
        using (var reader = new StreamReader(path)) stream.Load(reader);
        return (JsonObject)Convert(stream.Documents[0].RootNode)!;
    }

    private static readonly Regex Integer = new(@"^[-+]?[0-9]+$"), Float = new(@"^[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?$");

    private static JsonNode? Convert(YamlNode node) => node switch
    {
        YamlMappingNode map => new JsonObject(map.Children.Select(kv => KeyValuePair.Create(((YamlScalarNode)kv.Key).Value!, Convert(kv.Value)))),
        YamlSequenceNode seq => new JsonArray(seq.Children.Select(Convert).ToArray()),
        YamlScalarNode s when s.Style is YamlDotNet.Core.ScalarStyle.Plain => Plain(s.Value ?? string.Empty),
        YamlScalarNode s => JsonValue.Create(s.Value ?? string.Empty),
        _ => null
    };

    private static JsonNode? Plain(string text) => text switch
    {
        "" or "~" or "null" or "Null" or "NULL" => null,
        "true" or "True" or "TRUE" => JsonValue.Create(true),
        "false" or "False" or "FALSE" => JsonValue.Create(false),
        _ when Integer.IsMatch(text) && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l) => JsonValue.Create(l),
        _ when Float.IsMatch(text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => JsonValue.Create(d),
        _ => JsonValue.Create(text)
    };
}
