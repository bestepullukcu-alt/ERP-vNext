using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.InventoryService.Tests.TestInfrastructure;
using Json.Schema;

namespace Diten.InventoryService.Tests.CycleCounting;

/// <summary>
/// Pack §17 "Sözleşme" — the WMS-COUNT-BUNDLE v1 DRAFT (<c>docs/analysis/contracts/drafts/wms-count-bundle-v1-proposal.md</c>) turned into JSON
/// Schema 2020-12 in the test project (the MOD-0178 <c>WmsDraftContract</c> pattern; the draft file itself is not changed by tests). Derived member
/// by member from draft §1 (unwrapped success body, the <c>Error</c> envelope, NO JSON null, decimal strings, uuid / date / date-time formats),
/// §3.1 <c>CycleCount</c>, §3.2 <c>CycleCountLine</c> (with the DL-2 members), §3.3 <c>CycleCountEvent</c>, §3.4 pages and §3.5
/// <c>BulkDeleteCycleCountsResult</c>, DL-3 §3.6–§3.9 (decision, correction, integrity resolution, Variance Trace rows, settings); operations §4.1
/// (DL-1), §4.2 (DL-2) and §4.3 (DL-3). Every
/// object is CLOSED and every enum is the draft's list: a member or a value the draft does not name turns the call red.
/// </summary>
public static class CycleCountDraftContract
{
    public const string Error = "Error";
    public const string AuthError = "AuthError";
    public const string Count = "CycleCount";
    public const string CountPage = "CycleCountPage";
    public const string LinePage = "CycleCountLinePage";
    public const string EventPage = "CycleCountEventPage";
    public const string BulkDeleteResult = "BulkDeleteCycleCountsResult";
    public const string Line = "CycleCountLine";
    public const string Variance = "CountVariance";
    public const string VariancePage = "CountVariancePage";
    public const string Settings = "CycleCountSettings";

    private const string Defs = """
    {
      "Uuid": { "type": "string", "format": "uuid" },
      "Text": { "type": "string", "minLength": 1 },
      "Date": { "type": "string", "pattern": "^\\d{4}-\\d{2}-\\d{2}$" },
      "DateTime": { "type": "string", "format": "date-time" },
      "Quantity": { "type": "string", "pattern": "^-?\\d+(\\.\\d{1,6})?$" },
      "ContractVersion": { "const": "v1" },
      "SkuLevel": { "enum": ["Gsku", "Lsku", "FinishedGood", "Item"] },
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
      },
      "CycleCount": {
        "type": "object", "additionalProperties": false,
        "required": ["countId", "number", "warehouseId", "countType", "blind", "scopeLocationIds", "scopeSkuIds", "plannedDate", "status", "scopeBinIds",
                     "lineCount", "createdBy", "createdAt", "version", "contractVersion"],
        "properties": {
          "countId": { "$ref": "#/$defs/Uuid" }, "number": { "type": "string", "pattern": "^[A-Z]{2,5}-CNT-\\d{6,}$" },
          "warehouseId": { "$ref": "#/$defs/Uuid" }, "countType": { "enum": ["Cycle", "Full"] }, "blind": { "type": "boolean" },
          "scopeLocationIds": { "type": "array", "items": { "$ref": "#/$defs/Uuid" } }, "scopeSkuIds": { "type": "array", "items": { "$ref": "#/$defs/Uuid" } },
          "plannedDate": { "$ref": "#/$defs/Date" }, "assignedTo": { "$ref": "#/$defs/Uuid" }, "note": { "$ref": "#/$defs/Text" },
          "status": { "enum": ["Draft", "Released", "Counting", "Submitted", "Closed", "Cancelled"] }, "snapshotAt": { "$ref": "#/$defs/DateTime" },
          "scopeBinIds": { "type": "array", "items": { "$ref": "#/$defs/Uuid" } }, "lineCount": { "type": "integer", "minimum": 0 },
          "recountOfId": { "$ref": "#/$defs/Uuid" }, "createdBy": { "$ref": "#/$defs/Uuid" }, "createdAt": { "$ref": "#/$defs/DateTime" },
          "updatedAt": { "$ref": "#/$defs/DateTime" }, "submittedBy": { "$ref": "#/$defs/Uuid" }, "submittedAt": { "$ref": "#/$defs/DateTime" },
          "cancelledBy": { "$ref": "#/$defs/Uuid" }, "cancelledAt": { "$ref": "#/$defs/DateTime" },
          "cancelReason": { "$ref": "#/$defs/Text" }, "version": { "type": "integer", "minimum": 1 }, "contractVersion": { "$ref": "#/$defs/ContractVersion" }
        }
      },
      "CycleCountLine": {
        "type": "object", "additionalProperties": false,
        "required": ["lineId", "countId", "warehouseId", "locationId", "item", "stockStatus", "baseUomId", "origin", "serialControlled", "status", "version"],
        "properties": {
          "lineId": { "$ref": "#/$defs/Uuid" }, "countId": { "$ref": "#/$defs/Uuid" }, "warehouseId": { "$ref": "#/$defs/Uuid" },
          "locationId": { "$ref": "#/$defs/Uuid" },
          "item": {
            "type": "object", "additionalProperties": false, "required": ["itemId", "skuId", "skuLevel"],
            "properties": { "itemId": { "$ref": "#/$defs/Uuid" }, "skuId": { "$ref": "#/$defs/Uuid" }, "skuLevel": { "$ref": "#/$defs/SkuLevel" } }
          },
          "lotId": { "$ref": "#/$defs/Text" }, "stockStatus": { "const": "AVAILABLE" }, "baseUomId": { "$ref": "#/$defs/Text" },
          "origin": { "enum": ["Snapshot", "Found", "Recount"] }, "serialControlled": { "type": "boolean" },
          "expectedQuantity": { "$ref": "#/$defs/Quantity" }, "countedQuantity": { "$ref": "#/$defs/Quantity" },
          "countedBy": { "$ref": "#/$defs/Uuid" }, "countedAt": { "$ref": "#/$defs/DateTime" },
          "movementsSinceSnapshot": { "$ref": "#/$defs/Quantity" }, "expectedAtCount": { "$ref": "#/$defs/Quantity" },
          "varianceQuantity": { "$ref": "#/$defs/Quantity" }, "movementDuringCount": { "type": "boolean" }, "snapshotDrift": { "type": "boolean" },
          "scanRef": { "type": "string", "minLength": 1, "maxLength": 200 },
          "recountOfLineId": { "$ref": "#/$defs/Uuid" }, "recountDepth": { "type": "integer", "minimum": 1 },
          "decision": { "$ref": "#/$defs/CycleCountDecision" }, "adjustment": { "$ref": "#/$defs/CycleCountAdjustment" },
          "integrityResolution": { "$ref": "#/$defs/CycleCountIntegrityResolution" },
          "status": { "enum": ["Open", "Counted", "NoVariance", "AwaitingApproval", "Approved", "Rejected", "Recounted"] },
          "version": { "type": "integer", "minimum": 1 }
        }
      },
      "CycleCountDecision": {
        "type": "object", "additionalProperties": false, "required": ["kind", "source", "at"],
        "properties": {
          "kind": { "enum": ["Approved", "Rejected", "Recount"] }, "source": { "enum": ["User", "Tolerance"] }, "by": { "$ref": "#/$defs/Uuid" },
          "at": { "$ref": "#/$defs/DateTime" }, "reason": { "$ref": "#/$defs/Text" }
        }
      },
      "CycleCountAdjustment": {
        "type": "object", "additionalProperties": false,
        "required": ["movementType", "idempotencyKey", "status", "transactionDate", "postingDate", "attempts", "firstAttemptAt", "lastAttemptAt"],
        "properties": {
          "movementType": { "enum": ["COUNT_GAIN", "COUNT_LOSS"] }, "quantity": { "$ref": "#/$defs/Quantity" },
          "idempotencyKey": { "type": "string", "pattern": "^cycle-count:[0-9a-f-]{36}$" },
          "status": { "enum": ["Pending", "Posted", "Refused", "IntegrityError"] }, "transactionId": { "$ref": "#/$defs/Uuid" },
          "transactionNumber": { "$ref": "#/$defs/Text" }, "refusalCode": { "type": "string", "pattern": "^[A-Z][A-Z0-9_]+$" },
          "refusalDetails": { "type": "object" }, "refusalStatus": { "type": "integer", "minimum": 400, "maximum": 499 },
          "integrityReason": { "enum": ["KeyReused", "HolderReversed"] }, "holderTransactionId": { "$ref": "#/$defs/Uuid" },
          "transactionDate": { "$ref": "#/$defs/DateTime" }, "postingDate": { "$ref": "#/$defs/DateTime" }, "attempts": { "type": "integer", "minimum": 1 },
          "firstAttemptAt": { "$ref": "#/$defs/DateTime" }, "lastAttemptAt": { "$ref": "#/$defs/DateTime" }
        }
      },
      "CycleCountIntegrityResolution": {
        "type": "object", "additionalProperties": false, "required": ["kind", "by", "at", "reason"],
        "properties": {
          "kind": { "enum": ["AcceptHolder", "Reject"] }, "by": { "$ref": "#/$defs/Uuid" }, "at": { "$ref": "#/$defs/DateTime" },
          "reason": { "$ref": "#/$defs/Text" }, "holderTransactionId": { "$ref": "#/$defs/Uuid" }
        }
      },
      "CountManualMovement": {
        "type": "object", "additionalProperties": false,
        "required": ["transactionId", "transactionNumber", "movementType", "item", "quantity", "uomId", "sourceModule", "sourceType", "createdBy", "createdAt",
                     "transactionDate", "postingDate", "sourceMismatch"],
        "properties": {
          "transactionId": { "$ref": "#/$defs/Uuid" }, "transactionNumber": { "$ref": "#/$defs/Text" }, "movementType": { "enum": ["COUNT_GAIN", "COUNT_LOSS"] },
          "warehouseId": { "$ref": "#/$defs/Uuid" }, "locationId": { "$ref": "#/$defs/Uuid" },
          "item": {
            "type": "object", "additionalProperties": false, "required": ["itemId", "skuId", "skuLevel"],
            "properties": { "itemId": { "$ref": "#/$defs/Uuid" }, "skuId": { "$ref": "#/$defs/Uuid" }, "skuLevel": { "$ref": "#/$defs/SkuLevel" } }
          },
          "lotId": { "$ref": "#/$defs/Text" }, "stockStatus": { "$ref": "#/$defs/Text" }, "quantity": { "$ref": "#/$defs/Quantity" },
          "uomId": { "$ref": "#/$defs/Text" }, "sourceModule": { "$ref": "#/$defs/Text" }, "sourceType": { "$ref": "#/$defs/Text" },
          "sourceDocumentId": { "$ref": "#/$defs/Text" }, "sourceLineId": { "$ref": "#/$defs/Text" }, "createdBy": { "$ref": "#/$defs/Uuid" },
          "createdAt": { "$ref": "#/$defs/DateTime" }, "transactionDate": { "$ref": "#/$defs/DateTime" }, "postingDate": { "$ref": "#/$defs/DateTime" },
          "sourceMismatch": { "type": "boolean" }
        }
      },
      "CountVariance": {
        "type": "object", "additionalProperties": false, "required": ["source", "contractVersion"],
        "properties": {
          "source": { "enum": ["Count", "Manual"] }, "countNumber": { "type": "string", "pattern": "^[A-Z]{2,5}-CNT-\\d{6,}$" },
          "line": { "$ref": "#/$defs/CycleCountLine" }, "ledgerTransactionId": { "$ref": "#/$defs/Uuid" },
          "ledgerTransactionNumber": { "$ref": "#/$defs/Text" }, "movement": { "$ref": "#/$defs/CountManualMovement" },
          "contractVersion": { "$ref": "#/$defs/ContractVersion" }
        },
        "oneOf": [
          { "properties": { "source": { "const": "Count" } }, "required": ["line"], "not": { "required": ["movement"] } },
          { "properties": { "source": { "const": "Manual" } }, "required": ["movement"], "not": { "required": ["line"] } }
        ]
      },
      "CycleCountSettings": {
        "type": "object", "additionalProperties": false,
        "required": ["toleranceAbsolute", "tolerancePercent", "maxRecounts", "maxLinesPerCount", "isDefault", "version", "contractVersion"],
        "properties": {
          "toleranceAbsolute": { "$ref": "#/$defs/Quantity" }, "tolerancePercent": { "$ref": "#/$defs/Quantity" },
          "maxRecounts": { "type": "integer", "minimum": 0, "maximum": 10 }, "maxLinesPerCount": { "type": "integer", "minimum": 1, "maximum": 5000 },
          "isDefault": { "type": "boolean" }, "updatedBy": { "$ref": "#/$defs/Uuid" }, "updatedAt": { "$ref": "#/$defs/DateTime" },
          "version": { "type": "integer", "minimum": 0 }, "contractVersion": { "$ref": "#/$defs/ContractVersion" }
        }
      },
      "CycleCountEvent": {
        "type": "object", "additionalProperties": false, "required": ["eventId", "countId", "seq", "kind", "atUtc", "data"],
        "properties": {
          "eventId": { "$ref": "#/$defs/Uuid" }, "countId": { "$ref": "#/$defs/Uuid" }, "lineId": { "$ref": "#/$defs/Uuid" },
          "seq": { "type": "integer", "minimum": 1 },
          "kind": { "enum": ["Created", "Updated", "Released", "Cancelled", "LineCounted", "LineAdded", "Submitted", "VarianceComputed", "Approved", "Rejected",
                             "RecountRequested", "AdjustmentPending", "AdjustmentPosted", "AdjustmentRefused", "AdjustmentIntegrityError", "IntegrityResolved", "Closed"] },
          "actorUserId": { "$ref": "#/$defs/Uuid" }, "atUtc": { "$ref": "#/$defs/DateTime" },
          "data": { "type": "object", "additionalProperties": { "type": "string" } }
        }
      },
      "BulkDeleteCycleCountsResult": {
        "type": "object", "additionalProperties": false, "required": ["results", "contractVersion"],
        "properties": {
          "results": {
            "type": "array",
            "items": {
              "type": "object", "additionalProperties": false, "required": ["countId", "outcome"],
              "properties": {
                "countId": { "$ref": "#/$defs/Uuid" }, "outcome": { "enum": ["Deleted", "Conflict", "NotFound", "InvalidState"] },
                "version": { "type": "integer", "minimum": 1 }
              }
            }
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
        defs[CountPage] = Page(Count);
        defs[LinePage] = Page("CycleCountLine");
        defs[EventPage] = Page("CycleCountEvent");
        defs[VariancePage] = Page(Variance);
        return defs;
    });

    /// <summary>The operations validated, with the schemas their answers must match.</summary>
    public static readonly ConcurrentDictionary<string, int> Validated = new();

    private static JsonObject Page(string item) => new()
    {
        ["type"] = "object", ["additionalProperties"] = false, ["required"] = new JsonArray("items", "total", "page", "pageSize", "contractVersion"),
        ["properties"] = new JsonObject
        {
            ["items"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["$ref"] = $"#/$defs/{item}" } },
            ["total"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
            ["page"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 },
            ["pageSize"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 200 },
            ["contractVersion"] = new JsonObject { ["$ref"] = "#/$defs/ContractVersion" }
        }
    };

    private static JsonSchema Schema(string name)
        => Compiled.GetOrAdd(name, n => JsonSchema.FromText(new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = $"https://contracts.diten.test/wms-count-bundle-draft/v1/{n}",
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

    internal sealed record Operation(string Method, string Template, Regex Path, string Success);

    private static Operation Op(string method, string template, string success)
        => new(method, template, new Regex("^/api/warehouse/" + Regex.Escape(template).Replace("\\{id}", "[^/]+") + "/?$", RegexOptions.Compiled), success);

    /// <summary>Draft §4.1 + §4.2 + §4.3 — the DL-1, DL-2 and DL-3 operations (literal segments first so <c>{id}</c> never shadows them).</summary>
    internal static readonly Operation[] Operations =
    [
        Op("GET", "cycle-counts", CountPage),
        Op("POST", "cycle-counts", Count),
        Op("POST", "cycle-counts/bulk-delete", BulkDeleteResult),
        Op("GET", "cycle-counts/{id}", Count),
        Op("POST", "cycle-counts/{id}/update", Count),
        Op("POST", "cycle-counts/{id}/release", Count),
        Op("POST", "cycle-counts/{id}/cancel", Count),
        Op("GET", "cycle-counts/{id}/lines", LinePage),
        Op("GET", "cycle-counts/{id}/events", EventPage),
        Op("POST", "cycle-counts/{id}/lines", Line),
        Op("POST", "cycle-counts/{id}/lines/{id}/count", Line),
        Op("POST", "cycle-counts/{id}/submit", Count),
        Op("POST", "cycle-counts/{id}/recount", Count),
        Op("GET", "count-variances", VariancePage),
        Op("GET", "count-variances/{id}", Variance),
        Op("POST", "count-variances/{id}/approve", Line),
        Op("POST", "count-variances/{id}/reject", Line),
        Op("POST", "count-variances/{id}/retry", Line),
        Op("POST", "count-variances/{id}/resolve-integrity", Line),
        Op("GET", "count-settings", Settings),
        Op("PUT", "count-settings", Settings)
    ];
}

/// <summary>
/// Every <c>/api/warehouse/cycle-counts*</c> answer a cycle-count test receives is checked against <see cref="CycleCountDraftContract"/>: the
/// success body against its operation's schema, every other status against <c>Error</c>, plus the <c>X-Correlation-Id</c> header. A violation
/// throws (the test turns red).
/// </summary>
public sealed class CycleCountDraftContractHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var path = request.RequestUri!.AbsolutePath;
        var operation = CycleCountDraftContract.Operations.FirstOrDefault(o => o.Method == request.Method.Method && o.Path.IsMatch(path));
        if (operation is null) return response;

        var status = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        // 401 / 403 / 500 come from the service-wide auth and exception middleware, before / around any 0181 code (no error code there)
        var schema = status is 200 or 201 ? operation.Success : status is 401 or 403 or 500 ? CycleCountDraftContract.AuthError : CycleCountDraftContract.Error;
        var key = $"{operation.Method} {operation.Template}";
        if (status != 401 || body.Length > 0)
        {
            var errors = CycleCountDraftContract.Validate(schema, body);
            if (errors.Count > 0)
                throw new ContractViolationException($"{key} {status} body does not match the WMS-COUNT-BUNDLE draft '{schema}':\n  {string.Join("\n  ", errors)}\nBody: {body}");
        }

        if (!response.Headers.TryGetValues("X-Correlation-Id", out var correlation) || !Guid.TryParse(correlation.Single(), out _))
            throw new ContractViolationException($"{key} {status} carries no UUID X-Correlation-Id header.");

        CycleCountDraftContract.Validated.AddOrUpdate($"{key} {status}", 1, (_, n) => n + 1);
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        foreach (var header in response.Content.Headers)
        {
            content.Headers.Remove(header.Key);
            content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        response.Content = content;
        return response;
    }
}
