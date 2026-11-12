using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.InventoryService.Tests.TestInfrastructure;
using Json.Schema;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// MOD-0181 DL-5 — the Evidence Pack answers against the WMS-COUNT-BUNDLE DRAFT rows DL-5 made binding
/// (<c>docs/analysis/contracts/drafts/wms-count-bundle-v1-proposal.md</c> §1, §3.11, §4.4, §5.3), as JSON Schema 2020-12 in the test project — a
/// SEPARATE validator, because 1.0.0 (the frozen file <see cref="CycleCountContract"/> reads) has no DL-5 operation (CT adds them in 1.1.0).
/// Derived member by member: <c>CountEvidenceExport</c> (closed; uuid / date-time formats; the format, language and status enums; a 64-hex
/// sha256; NO JSON null), its page, the <c>Error</c> envelope with the DL-5 codes per operation and status, and the file answer (its media type and
/// the <c>X-Content-SHA256</c> header). A member, value or code the draft does not name turns the call red.
/// </summary>
public static class CountEvidenceDraftContract
{
    public const string Export = "CountEvidenceExport";
    public const string ExportPage = "CountEvidenceExportPage";
    public const string Error = "Error";
    public const string AuthError = "AuthError";

    private const string Defs = """
    {
      "Uuid": { "type": "string", "format": "uuid" },
      "DateTime": { "type": "string", "format": "date-time" },
      "ContractVersion": { "const": "v1" },
      "Language": { "enum": ["en", "tr", "fr", "es", "zh", "ar", "ru"] },
      "CountEvidenceExport": {
        "type": "object", "additionalProperties": false,
        "required": ["exportId", "countId", "countNumber", "format", "language", "requestedLanguage", "requestedBy", "requestedAt", "fileName", "sha256",
                     "sizeBytes", "status", "contractVersion"],
        "properties": {
          "exportId": { "$ref": "#/$defs/Uuid" }, "countId": { "$ref": "#/$defs/Uuid" },
          "countNumber": { "type": "string", "pattern": "^[A-Z]{2,5}-CNT-\\d{6,}$" },
          "format": { "enum": ["Pdf", "Csv"] },
          "language": { "$ref": "#/$defs/Language" }, "requestedLanguage": { "$ref": "#/$defs/Language" },
          "requestedBy": { "$ref": "#/$defs/Uuid" }, "requestedAt": { "$ref": "#/$defs/DateTime" },
          "fileName": { "type": "string", "pattern": "^count-evidence-[A-Z]{2,5}-CNT-\\d{6,}-(en|tr|fr|es|zh|ar|ru)-\\d{14}\\.(pdf|csv)$" },
          "sha256": { "type": "string", "pattern": "^[0-9a-f]{64}$" },
          "sizeBytes": { "type": "integer", "minimum": 1 },
          "status": { "enum": ["Ready", "Failed"] },
          "contractVersion": { "$ref": "#/$defs/ContractVersion" }
        }
      },
      "Error": {
        "type": "object", "additionalProperties": false, "required": ["error", "contractVersion"],
        "properties": {
          "error": {
            "type": "object", "additionalProperties": false, "required": ["code", "message", "correlationId"],
            "properties": {
              "code": { "type": "string", "pattern": "^[A-Z][A-Z0-9_]+$" }, "message": { "type": "string" },
              "details": { "type": "object" }, "correlationId": { "$ref": "#/$defs/Uuid" }
            }
          },
          "contractVersion": { "$ref": "#/$defs/ContractVersion" }
        }
      },
      "AuthError": {
        "type": "object", "additionalProperties": false, "required": ["error", "contractVersion"],
        "properties": {
          "error": {
            "type": "object", "additionalProperties": false,
            "properties": { "code": { "type": "string" }, "message": { "type": "string" }, "details": { "type": "object" }, "correlationId": { "$ref": "#/$defs/Uuid" } }
          },
          "contractVersion": { "$ref": "#/$defs/ContractVersion" }
        }
      }
    }
    """;

    private static readonly object Gate = new();
    private static readonly ConcurrentDictionary<string, JsonSchema> Compiled = new();

    private static readonly Lazy<JsonObject> AllDefs = new(() =>
    {
        var defs = JsonNode.Parse(Defs)!.AsObject();
        defs[ExportPage] = new JsonObject
        {
            ["type"] = "object", ["additionalProperties"] = false, ["required"] = new JsonArray("items", "total", "page", "pageSize", "contractVersion"),
            ["properties"] = new JsonObject
            {
                ["items"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["$ref"] = $"#/$defs/{Export}" } },
                ["total"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
                ["page"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 },
                ["pageSize"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 200 },
                ["contractVersion"] = new JsonObject { ["$ref"] = "#/$defs/ContractVersion" }
            }
        };
        return defs;
    });

    /// <summary>Draft §4.4: method + path → operation, its success schema and the error codes it may answer per status.</summary>
    internal sealed record Operation(string OperationId, string Method, Regex Path, IReadOnlyDictionary<int, string> Success,
        IReadOnlyDictionary<int, string[]> Errors);

    internal static readonly IReadOnlyList<Operation> Operations =
    [
        new("createCountEvidenceExport", "POST", new Regex("^/api/warehouse/cycle-counts/[^/]+/evidence-exports/?$"),
            new Dictionary<int, string> { [201] = Export, [200] = Export },
            new Dictionary<int, string[]>
            {
                [400] = ["INVALID_REQUEST", "IDEMPOTENCY_KEY_REUSED"], [404] = ["COUNT_NOT_FOUND"], [409] = ["COUNT_INVALID_STATE"]
            }),
        new("listCountEvidenceExports", "GET", new Regex("^/api/warehouse/count-evidence-exports/?$"),
            new Dictionary<int, string> { [200] = ExportPage },
            new Dictionary<int, string[]> { [400] = ["INVALID_REQUEST"] }),
        new("getCountEvidenceExportFile", "GET", new Regex("^/api/warehouse/count-evidence-exports/[^/]+/file/?$"),
            new Dictionary<int, string>(),
            new Dictionary<int, string[]> { [404] = ["COUNT_EVIDENCE_NOT_FOUND"], [500] = ["COUNT_EVIDENCE_INTEGRITY"] })
    ];

    /// <summary>How many answers were validated per operationId + status.</summary>
    public static readonly ConcurrentDictionary<string, int> Validated = new(StringComparer.Ordinal);

    internal static Operation? Match(string method, string path) => Operations.FirstOrDefault(o => o.Method == method && o.Path.IsMatch(path));

    private static JsonSchema Schema(string name)
        => Compiled.GetOrAdd(name, n => JsonSchema.FromText(new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = $"https://contracts.diten.test/wms-count-bundle-draft/dl5/{n}",
            ["$defs"] = AllDefs.Value.DeepClone(),
            ["$ref"] = $"#/$defs/{n}"
        }.ToJsonString()));

    /// <summary>The draft's verdict on one body (empty = valid).</summary>
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

        EvaluationResults result;
        lock (Gate)
        {
            result = Schema(schemaName).Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true });
        }

        return result.IsValid
            ? []
            : (result.Details ?? []).Where(d => d.Errors is { Count: > 0 })
                .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Key} — {e.Value}"))
                .DefaultIfEmpty("invalid (no detail)").ToList();
    }
}

/// <summary>
/// Every DL-5 answer an evidence test receives is checked against <see cref="CountEvidenceDraftContract"/>: a JSON body against the schema its
/// operation names for the status (an undeclared 2xx is a violation; a 4xx / 5xx must be the <c>Error</c> envelope with a code the operation lists —
/// 401 / 403 are the service-wide middleware's <c>AuthError</c>); the file answer by its media type, its <c>X-Content-SHA256</c> header and that the
/// header IS the SHA-256 of the bytes; the <c>X-Correlation-Id</c> header everywhere. A violation throws (the test turns red).
/// </summary>
public sealed class CountEvidenceContractHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var operation = CountEvidenceDraftContract.Match(request.Method.Method, request.RequestUri!.AbsolutePath);
        if (operation is null) return response;

        var status = (int)response.StatusCode;
        var key = $"{operation.OperationId} {status}";
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (operation.OperationId == "getCountEvidenceExportFile" && status == 200)
        {
            var type = response.Content.Headers.ContentType?.MediaType;
            if (type is not ("application/pdf" or "text/csv"))
                throw new ContractViolationException($"{key}: the file answer's media type is {type}, not application/pdf or text/csv.");
            if (!response.Headers.TryGetValues("X-Content-SHA256", out var declared)
                || declared.Single() != Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant())
                throw new ContractViolationException($"{key}: X-Content-SHA256 is missing or is not the SHA-256 of the bytes.");
        }
        else if (status != 401 || bytes.Length > 0)
        {
            var body = System.Text.Encoding.UTF8.GetString(bytes);
            string schema;
            if (operation.Success.TryGetValue(status, out var success)) schema = success;
            else if (status is >= 200 and < 300) throw new ContractViolationException($"{key}: the draft declares no {status} answer. Body: {body}");
            else if (status is 401 or 403) schema = CountEvidenceDraftContract.AuthError;
            else
            {
                schema = CountEvidenceDraftContract.Error;
                var code = System.Text.Json.Nodes.JsonNode.Parse(body)?["error"]?["code"]?.GetValue<string>();
                if (!operation.Errors.TryGetValue(status, out var codes) || !codes.Contains(code))
                    throw new ContractViolationException($"{key}: the draft does not list {code} for {status}. Body: {body}");
            }

            var errors = CountEvidenceDraftContract.Validate(schema, body);
            if (errors.Count > 0)
                throw new ContractViolationException($"{key} body does not match the DL-5 draft '{schema}':\n  {string.Join("\n  ", errors)}\nBody: {body}");
        }

        if (!response.Headers.TryGetValues("X-Correlation-Id", out var correlation) || !Guid.TryParse(correlation.Single(), out _))
            throw new ContractViolationException($"{key} carries no UUID X-Correlation-Id header.");

        CountEvidenceDraftContract.Validated.AddOrUpdate(key, 1, (_, n) => n + 1);
        var content = new ByteArrayContent(bytes);
        foreach (var header in response.Content.Headers) content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        response.Content = content;
        return response;
    }
}
