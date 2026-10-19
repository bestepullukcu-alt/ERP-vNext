using System.Collections;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.Web.Tests.Traceability;
using Json.Schema;
using YamlDotNet.Serialization;

namespace Diten.Web.Tests.Contracts;

/// <summary>
/// WP-MVP1-FU-CLEAN1 (FU-WEB-CONTRACT-FAKES, O-LIVE1-1) — binds the OLDER fake gateways (their <c>On(prefix, status, body)</c>) to the contract
/// files, the way <see cref="ContractGateway"/> binds the newer ones: a 2xx body a fake registers must be a valid 2xx response of an operation of
/// the contract that owns the path (its <c>servers</c> base: /api/inventory, /api/locations, /api/trace, /api/quarantine, /api/product-master,
/// /api/expiry, /api/recall), compiled with the same rules as <see cref="WebContractSchemas"/> (OpenAPI → JSON Schema 2020-12, nullable, CLOSED objects,
/// formats). A route the contract has no operation for is a violation too. Paths of services that have NO contract file in
/// docs/analysis/contracts (AuthService users, Platform organisation units, the legacy /api/gskus · /api/global-products) are named in
/// <see cref="Unbound"/> with the reason — never silently skipped.
/// </summary>
public static class ContractRoutes
{
    private static readonly (string Base, string File)[] Contracts =
    [
        ("/api/inventory", "inventory-bundle.openapi.yaml"),
        ("/api/locations", "location.openapi.yaml"),
        ("/api/trace", "trace.openapi.yaml"),
        ("/api/quarantine", "quarantine.openapi.yaml"),
        ("/api/product-master", "product-master-bundle.openapi.yaml"),
        ("/api/expiry", "fefo.openapi.yaml"),
        ("/api/recall", "recall.openapi.yaml") // WP-MVP1-FU-CLEAN2 (FU-WEB-CONTRACT-FAKES-RECALL)
    ];

    /// <summary>Path bases with no contract file in this repository (the reason is the record).</summary>
    public static readonly IReadOnlyDictionary<string, string> Unbound = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["/api/users"] = "AuthService — no users contract file in docs/analysis/contracts",
        ["/api/platform/organization-units"] = "Platform organisation units — no contract file in docs/analysis/contracts",
        ["/api/gskus"] = "legacy MDM route — not part of product-master-bundle",
        ["/api/global-products"] = "legacy MDM route — not part of product-master-bundle"
    };

    private static readonly object Gate = new();
    private static readonly Dictionary<string, (List<Operation> Ops, JsonObject Defs)> Loaded = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, JsonSchema> Compiled = new(StringComparer.Ordinal);

    private sealed record Operation(string Method, string Template, string Status, string DefName, JsonNode? Example = null);

    /// <summary>Throws <see cref="ContractViolationException"/> when a 2xx fake body is not a response of the owning contract.</summary>
    public static void Require(string prefix, int status, string body)
    {
        var errors = Check(prefix, status, body);
        if (errors.Count > 0) throw new ContractViolationException(OwnerOf(prefix)?.File ?? "(no contract)", prefix, errors);
    }

    /// <summary>
    /// A DELIBERATE off-contract body (a negative test of the Web's own guard, e.g. an unknown enum value): it must break the contract, and
    /// ONLY by the named violations — any other mismatch, or a body the contract accepts, is a violation again.
    /// </summary>
    public static void RequireOff(string prefix, int status, string body, params string[] allowed) => RequireOff(prefix, null, status, body, allowed);

    /// <summary>As <see cref="RequireOff(string, int, string, string[])"/>, measured against <paramref name="method"/>'s operations only.</summary>
    public static void RequireOff(string prefix, string? method, int status, string body, params string[] allowed)
    {
        var errors = Check(prefix, status, body, method);
        var file = OwnerOf(prefix)?.File ?? "(no contract)";
        if (errors.Count == 0) throw new ContractViolationException(file, prefix, ["registered as off-contract, but the contract accepts the body"]);
        var other = errors.Where(e => !allowed.Any(a => e.Contains(a, StringComparison.Ordinal))).ToList();
        if (other.Count > 0) throw new ContractViolationException(file, prefix, other);
    }

    /// <summary>The violations of one registration (empty = bound and valid, or a non-2xx, or an <see cref="Unbound"/> base).</summary>
    /// <para>WP-MVP1-FU-CLEAN2: <paramref name="method"/> (when given) keeps only that method's operations — a POST answer is never measured
    /// against the GET of the same path.</para>
    public static IReadOnlyList<string> Check(string prefix, int status, string body, string? method = null)
    {
        if (status is < 200 or >= 300) return [];
        var path = prefix.Split('?')[0];
        if (Unbound.Keys.Any(b => path.StartsWith(b, StringComparison.Ordinal))) return [];
        var owner = OwnerOf(prefix);
        if (owner is null) return [$"{path}: no contract owns this path (add it to ContractRoutes or to Unbound with a reason)"];
        var (ops, _) = Load(owner.Value.File);
        var rel = path.Length > owner.Value.Base.Length ? path[owner.Value.Base.Length..] : "/";
        var candidates = Candidates(ops, rel);
        if (method is not null) candidates = candidates.Where(c => string.Equals(c.Method, method, StringComparison.OrdinalIgnoreCase)).ToList();
        if (candidates.Count == 0) return [$"{path}: {owner.Value.File} has no operation for {rel}"];
        if (string.IsNullOrWhiteSpace(body)) return candidates.Any(c => c.DefName.Length == 0 && c.Example is null) ? [] : [$"{path}: empty body, the contract answers a JSON body"];
        JsonNode? instance;
        try
        {
            instance = JsonNode.Parse(body);
        }
        catch (Exception ex)
        {
            return [$"{path}: body is not JSON: {ex.Message}"];
        }

        var failures = new List<string>();
        // an operation the contract documents by an EXAMPLE only (no schema): the fake may carry no member the example does not name
        foreach (var op in candidates.Where(c => c.DefName.Length == 0 && c.Example is not null))
        {
            var extra = ExtraMembers(op.Example!, instance, string.Empty).ToList();
            if (extra.Count == 0) return [];
            failures.AddRange(extra.Select(e => $"{op.Method.ToUpperInvariant()} {op.Template} {op.Status} (example): {e} is not in the contract example"));
        }

        foreach (var op in candidates.Where(c => c.DefName.Length > 0))
        {
            var result = Evaluate(owner.Value.File, op.DefName, instance);
            if (result.IsValid) return [];
            failures.AddRange((result.Details ?? []).Where(d => d.Errors is { Count: > 0 })
                .SelectMany(d => d.Errors!.Select(e => $"{op.Method.ToUpperInvariant()} {op.Template} {op.Status} {d.InstanceLocation}: {e.Key} — {e.Value}")).Take(6));
        }

        return failures.Count > 0 ? failures : [$"{path}: no 2xx JSON response schema"];
    }

    private static (string Base, string File)? OwnerOf(string prefix)
    {
        var path = prefix.Split('?')[0];
        foreach (var c in Contracts.OrderByDescending(c => c.Base.Length))
            if (path == c.Base || path.StartsWith(c.Base + "/", StringComparison.Ordinal)) return c;
        return null;
    }

    /// <summary>
    /// The operations a fake prefix stands for: the templates that match it segment by segment ({x} = any segment); a prefix ending in "/"
    /// (an item route the fake answers for any id) stands for the templates one segment deeper.
    /// </summary>
    private static List<Operation> Candidates(List<Operation> ops, string rel)
    {
        var deeper = rel.EndsWith('/') && rel.Length > 1;
        var segments = rel.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        bool Matches(string template, int extra)
        {
            var t = template.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (t.Length != segments.Length + extra) return false;
            for (var i = 0; i < segments.Length; i++)
                if (!(t[i].StartsWith('{') || string.Equals(t[i], segments[i], StringComparison.Ordinal) || Regex.IsMatch(segments[i], @"^[0-9a-fA-F-]{8,}$") && t[i].StartsWith('{')))
                    return false;
            return extra == 0 || t[^1].StartsWith('{');
        }

        var exact = ops.Where(o => Matches(o.Template, deeper ? 1 : 0)).ToList();
        // a prefix WITHOUT the trailing slash that names no operation answers the item route below it ("/products" for "/products/{itemId}")
        return exact.Count > 0 || deeper ? exact : ops.Where(o => Matches(o.Template, 1)).ToList();
    }

    private static EvaluationResults Evaluate(string file, string def, JsonNode? instance)
    {
        lock (Gate)
        {
            var key = file + "#" + def;
            if (!Compiled.TryGetValue(key, out var schema))
            {
                var root = new JsonObject
                {
                    ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
                    ["$id"] = $"https://contracts.diten.test/web-routes/{file}/{Uri.EscapeDataString(def)}",
                    ["$defs"] = Load(file).Defs.DeepClone(),
                    ["$ref"] = $"#/$defs/{def}"
                };
                schema = JsonSchema.FromText(root.ToJsonString());
                Compiled[key] = schema;
            }

            return schema.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true });
        }
    }

    private static (List<Operation> Ops, JsonObject Defs) Load(string file)
    {
        lock (Gate)
        {
            if (Loaded.TryGetValue(file, out var done)) return done;
            var yaml = new DeserializerBuilder().WithAttemptingUnquotedStringTypeDeserialization().Build()
                .Deserialize<object>(File.ReadAllText(Path.Combine(TraceViewRenderer.RepoRoot(), "docs", "analysis", "contracts", file)));
            var doc = (JsonObject)ToNode(yaml)!;
            var defs = new JsonObject();
            if (doc["components"]?["schemas"] is JsonObject schemas)
                foreach (var (key, value) in schemas) defs[key] = value!.DeepClone();
            var ops = new List<Operation>();
            var index = 0;
            foreach (var (template, item) in doc["paths"]!.AsObject())
            {
                foreach (var (method, op) in item!.AsObject().Where(p => p.Key is "get" or "post" or "put" or "patch" or "delete"))
                {
                    if (op?["responses"] is not JsonObject responses) continue;
                    foreach (var (status, response) in responses.Where(r => r.Key.StartsWith('2')))
                    {
                        var json = Resolve(doc, response)?["content"]?["application/json"];
                        var schema = json?["schema"];
                        if (schema is null)
                        {
                            ops.Add(new Operation(method, template, status, string.Empty, json?["example"]?.DeepClone()));
                            continue;
                        }

                        var name = $"op{index++}";
                        defs[name] = schema.DeepClone();
                        ops.Add(new Operation(method, template, status, name));
                    }
                }
            }

            foreach (var (_, schema) in defs) Normalise(schema!);
            Loaded[file] = (ops, defs);
            return (ops, defs);
        }
    }

    /// <summary>The members of <paramref name="actual"/> (objects, first array element) the <paramref name="example"/> does not name.</summary>
    private static IEnumerable<string> ExtraMembers(JsonNode example, JsonNode? actual, string at)
    {
        switch (example, actual)
        {
            case (JsonObject e, JsonObject a):
                foreach (var (key, value) in a)
                {
                    if (!e.ContainsKey(key)) yield return $"{at}/{key}";
                    else if (e[key] is { } inner && value is not null) foreach (var x in ExtraMembers(inner, value, $"{at}/{key}")) yield return x;
                }

                break;
            case (JsonArray e, JsonArray a) when e.Count > 0:
                foreach (var (item, i) in a.Select((v, i) => (v, i))) foreach (var x in ExtraMembers(e[0]!, item, $"{at}/{i}")) yield return x;
                break;
        }
    }

    /// <summary>A response given as <c>$ref: '#/components/responses/X'</c> resolved to X.</summary>
    private static JsonNode? Resolve(JsonObject doc, JsonNode? response)
        => response?["$ref"] is JsonValue r && r.GetValue<string>() is { } target && target.StartsWith("#/components/responses/", StringComparison.Ordinal)
            ? doc["components"]?["responses"]?[target["#/components/responses/".Length..]]
            : response;

    private static void Normalise(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj["$ref"] is JsonValue refValue && refValue.GetValue<string>() is { } target && target.StartsWith("#/components/schemas/", StringComparison.Ordinal))
                    obj["$ref"] = "#/$defs/" + target["#/components/schemas/".Length..];
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
        byte or sbyte or short or ushort or uint => JsonValue.Create(Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture)),
        ulong u => JsonValue.Create(u),
        double d => JsonValue.Create(d),
        float f => JsonValue.Create(f),
        decimal m => JsonValue.Create(m),
        IEnumerable list => new JsonArray(list.Cast<object?>().Select(ToNode).ToArray()),
        _ => JsonValue.Create(value.ToString())
    };
}
