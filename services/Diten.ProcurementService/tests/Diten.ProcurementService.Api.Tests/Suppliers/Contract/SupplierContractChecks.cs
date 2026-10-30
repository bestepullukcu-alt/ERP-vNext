using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Diten.ProcurementService.Api.Tests.Suppliers.Contract;

/// <summary>
/// MOD-0140 DL-1 (pack §16 / §17): the checks behind the SUPPLIER 1.2.0 → 1.3.0 contract tests. Every check returns the
/// list of violations (empty = holds), so the same check runs on the real file and on sabotaged copies of it — a check
/// that passes everything would pass the sabotage too (guard the guard).
/// </summary>
public static class SupplierContractChecks
{
    public const string ContractFileName = "supplier.openapi.yaml";
    public const string PackRelativePath = "execution/domains/procurement/module-packs/MOD-0140-supplier-onboarding.md";
    public const string Frozen120RelativePath =
        "services/Diten.ProcurementService/tests/Diten.ProcurementService.Api.Tests/Suppliers/Contract/Fixtures/supplier-v1.2.0.openapi.yaml";

    /// <summary>The nine 1.2.0 tenant operations plus the two 1.3.0 eligibility operations.</summary>
    public static readonly string[] TenantOperations =
    [
        "listSuppliers", "createSupplier", "getSupplier", "updateSupplier", "deleteSupplier", "bulkDeleteSuppliers",
        "validateSuppliers", "getOnboardingCase", "submitOnboardingCase", "listEligibleSuppliers", "getSupplierEligibility"
    ];

    /// <summary>The only two example violations 1.2.0 already had (pack §16): they may neither grow nor move.</summary>
    public static readonly string[] KnownExampleViolations =
    [
        "listSuppliers 200 /nextCursor",
        "validateSuppliers 200 /results/1/status"
    ];

    private static readonly object EvaluateGate = new();

    public static string RepoRoot
    {
        get
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
                current = current.Parent;
            }

            throw new InvalidOperationException("Repo root not found (no AGENTS.md above the test binary).");
        }
    }

    public static string Read(string relativePath) => File.ReadAllText(Path.Combine(RepoRoot, relativePath));

    /// <summary>The contract, found by name under the docs tree (as Inventory's <c>RepoPaths.Contract</c> does).</summary>
    public static string ReadContract()
    {
        var matches = Directory.EnumerateFiles(Path.Combine(RepoRoot, "docs"), ContractFileName, SearchOption.AllDirectories).ToList();
        return matches.Count == 1
            ? File.ReadAllText(matches[0])
            : throw new InvalidOperationException($"Expected exactly one '{ContractFileName}' under the docs tree, found {matches.Count}.");
    }

    // ------------------------------------------------------------------------------------------------- YAML → JSON
    /// <summary>
    /// YAML 1.2 core schema: a plain scalar is null / bool / number when it reads as one, any quoted or block scalar is a
    /// string. A duplicate key throws (one response per status code is then guaranteed by loading at all).
    /// </summary>
    public static JsonObject Parse(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return (JsonObject)ToNode(stream.Documents[0].RootNode)!;
    }

    private static JsonNode? ToNode(YamlNode node) => node switch
    {
        YamlMappingNode map => ToObject(map),
        YamlSequenceNode seq => new JsonArray(seq.Children.Select(ToNode).ToArray()),
        YamlScalarNode scalar => ToScalar(scalar),
        _ => throw new InvalidOperationException($"Unexpected YAML node {node.NodeType} at {node.Start}.")
    };

    private static JsonObject ToObject(YamlMappingNode map)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in map.Children)
        {
            var name = ((YamlScalarNode)key).Value!;
            if (obj.ContainsKey(name)) throw new InvalidOperationException($"Duplicate key '{name}' at {key.Start}.");
            obj[name] = ToNode(value);
        }

        return obj;
    }

    private static JsonNode? ToScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? string.Empty;
        if (scalar.Style != ScalarStyle.Plain) return JsonValue.Create(value);
        if (value is "" or "~" or "null" or "Null" or "NULL") return null;
        if (value is "true" or "True" or "TRUE") return JsonValue.Create(true);
        if (value is "false" or "False" or "FALSE") return JsonValue.Create(false);
        if (Regex.IsMatch(value, @"^[-+]?[0-9]+$") && long.TryParse(value, out var l)) return JsonValue.Create(l);
        if (Regex.IsMatch(value, @"^[-+]?(\.[0-9]+|[0-9]+(\.[0-9]*)?)([eE][-+]?[0-9]+)?$") && double.TryParse(value,
                System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
            return JsonValue.Create(d);
        return JsonValue.Create(value);
    }

    // ------------------------------------------------------------------------------------------- operations / refs
    public static IEnumerable<(string Path, string Method, JsonObject Operation)> Operations(JsonObject doc)
    {
        foreach (var (path, item) in doc["paths"]!.AsObject())
        foreach (var (method, op) in item!.AsObject())
            yield return (path, method, op!.AsObject());
    }

    public static JsonObject Operation(JsonObject doc, string operationId) =>
        Operations(doc).Single(o => (string?)o.Operation["operationId"] == operationId).Operation;

    public static JsonNode Resolve(JsonObject doc, JsonNode node)
    {
        while (node is JsonObject obj && obj["$ref"] is JsonValue r && ((string)r!).StartsWith("#/components/", StringComparison.Ordinal))
        {
            JsonNode current = doc;
            foreach (var part in ((string)r!)[2..].Split('/')) current = current[part]!;
            node = current;
        }

        return node;
    }

    // --------------------------------------------------------------------------- (whitelist) 1.2.0 → 1.3.0 differences
    /// <summary>
    /// Pack §16 fark raporu (Revizyon 4, F-3): every node of 1.2.0 is in 1.3.0 unchanged, except the closed list —
    /// <c>info.description</c> (1.2.0 value is a prefix), <c>info.version</c>, <c>info.x-producer-status</c> (FROZEN + date),
    /// <c>info.x-front-loaded-for</c> (+ MOD-0119, MOD-0289 at the end) and the <c>validateSuppliers</c> 200 example (rows
    /// gain the new members, every 1.2.0 member keeps its value). New keys are allowed only where adding is additive: a new
    /// path, a new component, a new property, a new response code of an existing operation, a new operation description.
    /// </summary>
    public static List<string> WhitelistViolations(JsonObject v120, JsonObject v130)
    {
        var violations = new List<string>();
        Walk(v120, v130, [], violations);
        return violations;
    }

    private static void Walk(JsonNode? old, JsonNode? now, List<string> path, List<string> violations)
    {
        var at = "/" + string.Join('/', path);
        switch (old)
        {
            case JsonObject oldObj:
                if (now is not JsonObject newObj)
                {
                    violations.Add($"{at}: mapping replaced by {now?.GetValueKind()}");
                    return;
                }

                foreach (var (key, value) in oldObj)
                {
                    if (!newObj.ContainsKey(key)) violations.Add($"{at}/{key}: removed");
                    else Walk(value, newObj[key], [.. path, key], violations);
                }

                foreach (var (key, _) in newObj)
                {
                    if (!oldObj.ContainsKey(key) && !AdditionAllowed(path, key)) violations.Add($"{at}/{key}: added where only 1.2.0 may stand");
                }

                return;
            case JsonArray oldArr:
                if (at == "/info/x-front-loaded-for")
                {
                    var expected = oldArr.Select(n => (string?)n).Concat(["MOD-0119", "MOD-0289"]).ToArray();
                    var actual = (now as JsonArray)?.Select(n => (string?)n).ToArray() ?? [];
                    if (!expected.SequenceEqual(actual)) violations.Add($"{at}: [{string.Join(", ", actual)}] is not 1.2.0 + MOD-0119, MOD-0289");
                    return;
                }

                if (at == "/paths//validate/post/responses/200/content/application/json/example/results")
                {
                    if (now is not JsonArray rows || rows.Count != oldArr.Count)
                    {
                        violations.Add($"{at}: row count changed");
                        return;
                    }

                    for (var i = 0; i < oldArr.Count; i++)
                    foreach (var (key, value) in oldArr[i]!.AsObject())
                    {
                        if (rows[i] is not JsonObject row || !row.ContainsKey(key) || !JsonNode.DeepEquals(value, row[key]))
                            violations.Add($"{at}/{i}/{key}: 1.2.0 value changed");
                    }

                    return;
                }

                if (!JsonNode.DeepEquals(oldArr, now)) violations.Add($"{at}: sequence changed");
                return;
            default:
                var oldText = old?.ToJsonString();
                var newText = now?.ToJsonString();
                if (oldText == newText) return;
                switch (at)
                {
                    case "/info/description" when now is JsonValue && old is JsonValue
                                                  && ((string)now!).StartsWith((string)old!, StringComparison.Ordinal):
                    case "/info/version" when (string?)old == "1.2.0" && (string?)now == "1.3.0":
                    case "/info/x-producer-status" when (string?)old == "FROZEN" && now is JsonValue
                                                        && Regex.IsMatch((string)now!, @"^FROZEN [0-9]{4}-[0-9]{2}-[0-9]{2}$"):
                        return;
                    default:
                        violations.Add($"{at}: {oldText} → {newText}");
                        return;
                }
        }
    }

    private static bool AdditionAllowed(List<string> parent, string key) =>
        parent.Count == 1 && parent[0] == "paths"                                                          // new path
        || parent.Count == 2 && parent[0] == "components" && parent[1] is "schemas" or "responses"        // new component
        || parent.Count > 0 && parent[^1] == "properties"                                                  // new property
        || parent.Count == 4 && parent[0] == "paths" && parent[3] == "responses" && Regex.IsMatch(key, "^[1-5][0-9]{2}$")
        || parent.Count == 3 && parent[0] == "paths" && key == "description";                             // operation note

    /// <summary>Pack §16: <c>info.description</c> of 1.2.0 is a prefix of 1.3.0's — :12 sentence included.</summary>
    public static List<string> DescriptionPrefixViolations(JsonObject v120, JsonObject v130)
    {
        var old = (string)v120["info"]!["description"]!;
        var now = (string)v130["info"]!["description"]!;
        return now.StartsWith(old, StringComparison.Ordinal) && now.Length > old.Length
            ? []
            : ["info.description: the 1.2.0 value is not a prefix of 1.3.0's (or nothing was appended)"];
    }

    // ----------------------------------------------------------------------------------------- (a) response mapping
    /// <summary>Pack §17 (a): every (operation, status) of 1.2.0 goes to the same response in 1.3.0 — <c>$ref</c> and target.</summary>
    public static List<string> ResponseMappingViolations(JsonObject v120, JsonObject v130)
    {
        var violations = new List<string>();
        foreach (var (_, _, op) in Operations(v120))
        {
            var id = (string)op["operationId"]!;
            var now = Operations(v130).Where(o => (string?)o.Operation["operationId"] == id).Select(o => o.Operation).SingleOrDefault();
            if (now is null)
            {
                violations.Add($"{id}: missing in 1.3.0");
                continue;
            }

            foreach (var (code, response) in op["responses"]!.AsObject())
            {
                var newResponse = now["responses"]?[code];
                if (id == "validateSuppliers" && code == "200") newResponse = WithoutValidateAdditions(newResponse, response!);
                if (!JsonNode.DeepEquals(response, newResponse))
                    violations.Add($"{id} {code}: response entry changed");
                else if (!JsonNode.DeepEquals(Resolve(v120, response!), Resolve(v130, newResponse!)))
                    violations.Add($"{id} {code}: referenced response object changed");
            }
        }

        return violations;
    }

    /// <summary>
    /// Pack §16: the inline <c>validateSuppliers</c> 200 is the one 1.2.0 response that changes — exactly the five row
    /// properties of §3.3 (3) and the example (whose 1.2.0 values the whitelist check keeps). Removing those two
    /// additions must give back the 1.2.0 response; anything else that differs stays a difference.
    /// </summary>
    private static JsonNode? WithoutValidateAdditions(JsonNode? response, JsonNode old)
    {
        if (response?.DeepClone() is not JsonObject copy) return response;
        if (copy["content"]?["application/json"]?["schema"]?["properties"]?["results"]?["items"]?["properties"] is JsonObject row)
        {
            foreach (var added in new[] { "supplierMasterId", "relationshipExists", "onboardingStatus", "purchasingEnabled", "eligibleForTransactions" })
                row.Remove(added);
        }

        if (copy["content"]?["application/json"] is JsonObject media && old["content"]?["application/json"]?["example"] is { } oldExample)
            media["example"] = oldExample.DeepClone();
        return copy;
    }

    // -------------------------------------------------------------------------------------------- (b) null spelling
    /// <summary>
    /// Pack §17 (b): the 1.3.0 nodes carry no <c>nullable</c> keyword, and null is admitted exactly where §3.1 says —
    /// measured by validating instances, not by reading the keyword.
    /// </summary>
    public static List<string> NullSpellingViolations(JsonObject v130)
    {
        var violations = new List<string>();
        var added = new (string Where, JsonNode? Node)[]
        {
            ("SupplierEligibility", v130["components"]?["schemas"]?["SupplierEligibility"]),
            ("validateSuppliers row", ValidateRowSchema(v130)),
            ("listEligibleSuppliers 200", EligibleEnvelopeSchema(v130))
        };
        foreach (var (where, node) in added)
        {
            if (node is null) violations.Add($"{where}: missing");
            else if (node.ToJsonString().Contains("\"nullable\"", StringComparison.Ordinal)) violations.Add($"{where}: carries 'nullable'");
        }

        const string eligible =
            """{"supplierId":"SUP-1","supplierMasterId":"0b6f3c2e-6d1a-4c8e-9a51-2f4e7d9b1a10","relationshipExists":true,"status":"Active","onboardingStatus":"Approved","purchasingEnabled":true,"eligibleForTransactions":true}""";
        var nullable = new[] { "supplierMasterId", "status", "onboardingStatus", "purchasingEnabled" };
        var required = new[] { "supplierId", "supplierMasterId", "relationshipExists", "status", "onboardingStatus", "purchasingEnabled", "eligibleForTransactions" };
        var schema = new JsonObject { ["$ref"] = "#/components/schemas/SupplierEligibility" };
        if (ValidationErrors(v130, schema, JsonNode.Parse(eligible)).Count > 0) violations.Add("SupplierEligibility: a valid instance is rejected");
        foreach (var field in required)
        {
            var withNull = JsonNode.Parse(eligible)!.AsObject();
            withNull[field] = null;
            var accepted = ValidationErrors(v130, schema, withNull).Count == 0;
            if (accepted != nullable.Contains(field))
                violations.Add($"SupplierEligibility.{field}: null {(accepted ? "accepted" : "rejected")}");

            var without = JsonNode.Parse(eligible)!.AsObject();
            without.Remove(field);
            if (ValidationErrors(v130, schema, without).Count == 0) violations.Add($"SupplierEligibility.{field}: not required");
        }

        var row = ValidateRowSchema(v130);
        if (row is not null)
        {
            foreach (var (field, admitsNull) in new[] { ("supplierMasterId", true), ("relationshipExists", false), ("onboardingStatus", true), ("purchasingEnabled", true), ("eligibleForTransactions", false) })
            {
                var accepted = ValidationErrors(v130, row, new JsonObject { [field] = null }).Count == 0;
                if (accepted != admitsNull) violations.Add($"validateSuppliers row.{field}: null {(accepted ? "accepted" : "rejected")}");
            }
        }

        var envelope = EligibleEnvelopeSchema(v130);
        if (envelope is not null)
        {
            if (ValidationErrors(v130, envelope, JsonNode.Parse("""{"items":[],"nextCursor":null,"contractVersion":"v1"}""")).Count > 0)
                violations.Add("listEligibleSuppliers 200: nextCursor null rejected");
            if (ValidationErrors(v130, envelope, JsonNode.Parse("""{"items":[],"contractVersion":"v1"}""")).Count == 0)
                violations.Add("listEligibleSuppliers 200: nextCursor not required");
        }

        return violations;
    }

    private static JsonNode? ValidateRowSchema(JsonObject doc) =>
        doc["paths"]?["/validate"]?["post"]?["responses"]?["200"]?["content"]?["application/json"]?["schema"]?["properties"]?["results"]?["items"];

    private static JsonNode? EligibleEnvelopeSchema(JsonObject doc) =>
        doc["paths"]?["/eligible"]?["get"]?["responses"]?["200"]?["content"]?["application/json"]?["schema"];

    // --------------------------------------------------------------------------------------- (c) example validation
    /// <summary>
    /// Pack §17 (c): every example in the file (request bodies, responses, named examples) against its schema as JSON
    /// Schema 2020-12, formats enforced; returns "operationId status instancePath" for each failing leaf.
    /// </summary>
    public static List<string> ExampleViolations(JsonObject doc)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (_, _, op) in Operations(doc))
        {
            var id = (string)op["operationId"]!;
            if (op["requestBody"]?["content"] is JsonObject requestContent)
            {
                foreach (var (_, media) in requestContent)
                {
                    if (media?["example"] is { } example) Add(found, $"{id} request", ValidationErrors(doc, media["schema"]!, example));
                }
            }

            foreach (var (code, response) in op["responses"]!.AsObject())
            {
                if (Resolve(doc, response!)["content"] is not JsonObject content) continue;
                foreach (var (_, media) in content)
                {
                    if (media?["example"] is { } example) Add(found, $"{id} {code}", ValidationErrors(doc, media["schema"]!, example));
                    if (media?["examples"] is JsonObject named)
                    {
                        foreach (var (name, ex) in named) Add(found, $"{id} {code} {name}", ValidationErrors(doc, media["schema"]!, ex!["value"]));
                    }
                }
            }
        }

        return [.. found];

        static void Add(SortedSet<string> set, string prefix, IEnumerable<string> locations)
        {
            foreach (var location in locations) set.Add($"{prefix} {location}");
        }
    }

    /// <summary>Failing instance locations (deepest only) of <paramref name="instance"/> against <paramref name="schema"/>.</summary>
    public static List<string> ValidationErrors(JsonObject doc, JsonNode schema, JsonNode? instance)
    {
        var defs = new JsonObject();
        foreach (var (name, s) in doc["components"]!["schemas"]!.AsObject()) defs[name] = RewriteRefs(s!.DeepClone());

        var root = RewriteRefs(schema.DeepClone()) as JsonObject ?? throw new InvalidOperationException("schema is not an object");
        root["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        root["$defs"] = defs;

        EvaluationResults result;
        lock (EvaluateGate)
        {
            result = JsonSchema.FromText(root.ToJsonString()).Evaluate(instance, new EvaluationOptions
            {
                OutputFormat = OutputFormat.Hierarchical,
                RequireFormatValidation = true
            });
        }

        if (result.IsValid) return [];

        // Hierarchical output: descend only through nodes that are themselves invalid — a failing branch under a oneOf /
        // anyOf that holds as a whole is not an error (the List output reports those branches too).
        var locations = new List<string>();
        Collect(result, locations);
        if (locations.Count == 0) locations.Add(result.InstanceLocation.ToString());
        locations = locations.Distinct(StringComparer.Ordinal).ToList();
        return locations.Where(l => !locations.Any(o => o != l && o.StartsWith(l + "/", StringComparison.Ordinal))).ToList();

        static void Collect(EvaluationResults node, List<string> into)
        {
            if (node.IsValid) return;
            if (node.Errors is { Count: > 0 }) into.Add(node.InstanceLocation.ToString());
            foreach (var child in node.Details) Collect(child, into);
        }
    }

    private static JsonNode? RewriteRefs(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (key == "$ref" && obj[key] is JsonValue v && ((string)v!).StartsWith("#/components/schemas/", StringComparison.Ordinal))
                        obj[key] = "#/$defs/" + ((string)v!)["#/components/schemas/".Length..];
                    else obj[key] = RewriteRefs(obj[key]?.DeepClone());
                }

                return obj;
            case JsonArray arr:
                return new JsonArray(arr.Select(n => RewriteRefs(n?.DeepClone())).ToArray());
            default:
                return node;
        }
    }

    // ---------------------------------------------------------------------------------------- (d) S-2 / S-6 surface
    /// <summary>
    /// Pack §17 (d): exactly one 403 per tenant operation (OnboardingWorkflowForbidden on submitOnboardingCase,
    /// LegalEntityLookupForbidden on the other ten), 404 and 503 everywhere, <c>Error.error.supplierId</c> optional,
    /// DuplicateSupplier as in 1.2.0, and 1.3.0 carries no relationship / tenant-wide tax-id path and no new security scheme.
    /// </summary>
    public static List<string> SurfaceViolations(JsonObject v120, JsonObject v130)
    {
        var violations = new List<string>();
        var ids = Operations(v130).Select(o => (string)o.Operation["operationId"]!).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var expectedIds = TenantOperations.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (!ids.SequenceEqual(expectedIds)) violations.Add($"operations: [{string.Join(", ", ids)}]");

        foreach (var id in TenantOperations)
        {
            var op = Operations(v130).Where(o => (string?)o.Operation["operationId"] == id).Select(o => o.Operation).SingleOrDefault();
            if (op is null) continue;
            var expected403 = id == "submitOnboardingCase" ? "OnboardingWorkflowForbidden" : "LegalEntityLookupForbidden";
            if ((string?)op["responses"]?["403"]?["$ref"] != $"#/components/responses/{expected403}") violations.Add($"{id}: 403 is not {expected403}");
            if (op["responses"]?["404"] is null) violations.Add($"{id}: no 404");
            if ((string?)op["responses"]?["503"]?["$ref"] != "#/components/responses/DependencyUnavailable") violations.Add($"{id}: 503 is not DependencyUnavailable");
        }

        if ((string?)Operation(v130, "submitOnboardingCase")["responses"]?["409"]?["$ref"] != "#/components/responses/WorkflowTemplateMissing")
            violations.Add("submitOnboardingCase: 409 is not WorkflowTemplateMissing");

        var error = v130["components"]?["schemas"]?["Error"];
        if (error?["required"] is not null || error?["properties"]?["error"]?["required"] is not null) violations.Add("Error: a required list was added");
        if ((string?)error?["properties"]?["error"]?["properties"]?["supplierId"]?["type"] != "string") violations.Add("Error.error.supplierId: missing or not a string");

        if (!JsonNode.DeepEquals(v120["components"]!["responses"]!["DuplicateSupplier"], v130["components"]?["responses"]?["DuplicateSupplier"]))
            violations.Add("DuplicateSupplier: changed");

        foreach (var (path, _) in v130["paths"]!.AsObject())
        {
            if (Regex.IsMatch(path, "relationship|tax|company|link", RegexOptions.IgnoreCase)) violations.Add($"{path}: relationship / tax-id path belongs to 1.4.0 (DL-3)");
        }

        var schemes = v130["components"]?["securitySchemes"]?.AsObject().Select(p => p.Key).ToArray() ?? [];
        if (!schemes.SequenceEqual(["bearerAuth"])) violations.Add($"securitySchemes: [{string.Join(", ", schemes)}] — the service token scheme belongs to 1.5.0 (DL-7)");
        return violations;
    }

    // --------------------------------------------------------------------------------------------- pack ↔ contract
    /// <summary>
    /// Pack §3.1 / §3.3: the contract is copied from the pack, not re-interpreted. <c>SupplierEligibility</c> equals the
    /// §3.1 block; every line of the §3.3 block (comments and the structural keys it only shows for context aside) is a
    /// line of the contract.
    /// </summary>
    public static List<string> PackTextViolations(string pack, string contractText, JsonObject v130)
    {
        var violations = new List<string>();
        var section31 = pack.IndexOf("### 3.1 ", StringComparison.Ordinal);
        var section33 = pack.IndexOf("### 3.3 ", StringComparison.Ordinal);
        var block31 = Regex.Matches(pack, "```yaml\n(.*?)```", RegexOptions.Singleline).First(m => m.Index > section31).Groups[1].Value;
        var block33 = Regex.Matches(pack, "```yaml\n(.*?)```", RegexOptions.Singleline).First(m => m.Index > section33).Groups[1].Value;

        var fromPack = Parse(block31)["SupplierEligibility"];
        if (!JsonNode.DeepEquals(fromPack, v130["components"]?["schemas"]?["SupplierEligibility"]))
            violations.Add("SupplierEligibility differs from pack §3.1");

        var lines = contractText.Split('\n').Select(l => l.TrimEnd('\r').TrimEnd()).ToHashSet(StringComparer.Ordinal);
        var structural = new HashSet<string>(StringComparer.Ordinal) { "paths:", "components:", "schemas:", "responses:", "Error:", "properties:", "error:" };
        foreach (var raw in block33.Split('\n'))
        {
            var line = Regex.Replace(raw, @"^(\s*(get|post|patch|delete):)\s+#.*$", "$1").TrimEnd();
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#') || structural.Contains(trimmed)) continue;
            if (trimmed.StartsWith("supplierId: { type: string, description: \"yalnız 409", StringComparison.Ordinal))
            {
                // Error.error is a flow mapping in 1.2.0 (:276); the property is added inside it, same text.
                if (!contractText.Contains(trimmed, StringComparison.Ordinal))
                    violations.Add("Error.error.supplierId text differs from pack §3.3 (5)");
                continue;
            }

            if (!lines.Contains(line)) violations.Add($"pack §3.3 line not in the contract: {trimmed[..Math.Min(90, trimmed.Length)]}");
        }

        return violations;
    }
}
