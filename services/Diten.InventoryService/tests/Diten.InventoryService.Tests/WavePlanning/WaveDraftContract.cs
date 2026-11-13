using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.InventoryService.Tests.TestInfrastructure;
using Json.Schema;

namespace Diten.InventoryService.Tests.WavePlanning;

/// <summary>
/// Pack AC-1 — the WMS-WAVE-BUNDLE v1 DRAFT (<c>docs/analysis/contracts/drafts/wms-wave-bundle-v1-proposal.md</c>) turned into JSON Schema
/// 2020-12 in the test project (the MOD-0181 <c>CycleCountDraftContract</c> pattern; the draft file itself is not changed by tests). Derived
/// member by member from draft §1 (unwrapped success body, the <c>Error</c> envelope, NO JSON null, uuid / date-time formats), §3.1
/// <c>Wave</c>, §3.2 <c>WaveOrder</c>, §3.3 <c>WaveEvent</c>, §3.4 <c>WaveWarehouseSettings</c>, §3.5 <c>WaveCandidate</c>, §3.6
/// <c>WaveOrdersResult</c>, §3.7 <c>WaveLoad</c> and §3.8 pages. Every object is CLOSED and every enum is the draft's list: a member or a value
/// the draft does not name — or a required member that is missing — turns the call red.
/// </summary>
public static class WaveDraftContract
{
    public const string DraftPath = "docs/analysis/contracts/drafts/wms-wave-bundle-v1-proposal.md";

    public const string Error = "Error";
    public const string AuthError = "AuthError";
    public const string Wave = "Wave";
    public const string WavePage = "WavePage";
    public const string OrderPage = "WaveOrderPage";
    public const string EventPage = "WaveEventPage";
    public const string CandidatePage = "WaveCandidatePage";
    public const string Suggestions = "WaveSuggestions";
    public const string OrdersResult = "WaveOrdersResult";
    public const string Load = "WaveLoad";
    public const string Settings = "WaveWarehouseSettings";

    private const string Defs = """
    {
      "Uuid": { "type": "string", "format": "uuid" },
      "Text": { "type": "string", "minLength": 1 },
      "DateTime": { "type": "string", "format": "date-time", "pattern": "Z$" },
      "LocalDateTime": { "type": "string", "pattern": "^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}$" },
      "Count": { "type": "integer", "minimum": 0 },
      "ContractVersion": { "const": "v1" },
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
      "Wave": {
        "type": "object", "additionalProperties": false,
        "required": ["waveId", "number", "warehouseId", "warehouseCode", "waveType", "cutoffLocal", "cutoffAtUtc", "timeZoneId", "status", "orderCount",
                     "lineCount", "plannedTaskCount", "createdBy", "createdAt", "version", "contractVersion"],
        "properties": {
          "waveId": { "$ref": "#/$defs/Uuid" }, "number": { "type": "string", "pattern": "^[A-Z]{2,5}-WAV-\\d{6,}$" },
          "warehouseId": { "$ref": "#/$defs/Uuid" }, "warehouseCode": { "$ref": "#/$defs/Text" },
          "waveType": { "enum": ["Cargo", "Customer", "Region", "Manual"] },
          "cutoffLocal": { "$ref": "#/$defs/LocalDateTime" }, "cutoffAtUtc": { "$ref": "#/$defs/DateTime" }, "timeZoneId": { "$ref": "#/$defs/Text" },
          "cutoffAmbiguousResolved": { "const": true },
          "shipToFilter": { "type": "string", "minLength": 1, "maxLength": 100 },
          "maxOrders": { "type": "integer", "minimum": 1, "maximum": 1000 }, "maxLines": { "type": "integer", "minimum": 1, "maximum": 20000 },
          "responsibleUserId": { "$ref": "#/$defs/Uuid" }, "note": { "type": "string", "minLength": 1, "maxLength": 500 },
          "status": { "enum": ["Draft", "Releasing", "Released", "Picking", "Completed", "PartiallyCompleted", "Cancelled"] },
          "orderCount": { "$ref": "#/$defs/Count" }, "lineCount": { "$ref": "#/$defs/Count" }, "plannedTaskCount": { "$ref": "#/$defs/Count" },
          "limitWarnings": {
            "type": "array", "minItems": 1,
            "items": {
              "type": "object", "additionalProperties": false, "required": ["kind", "limit", "actual"],
              "properties": { "kind": { "enum": ["MaxOrders", "MaxLines"] }, "limit": { "type": "integer", "minimum": 1 }, "actual": { "type": "integer", "minimum": 1 } }
            }
          },
          "createdBy": { "$ref": "#/$defs/Uuid" }, "createdAt": { "$ref": "#/$defs/DateTime" }, "updatedAt": { "$ref": "#/$defs/DateTime" },
          "releasedBy": { "$ref": "#/$defs/Uuid" }, "releasedAt": { "$ref": "#/$defs/DateTime" },
          "cancelledBy": { "$ref": "#/$defs/Uuid" }, "cancelledAt": { "$ref": "#/$defs/DateTime" }, "cancelReason": { "$ref": "#/$defs/Text" },
          "completedAt": { "$ref": "#/$defs/DateTime" },
          "version": { "type": "integer", "minimum": 1 }, "contractVersion": { "$ref": "#/$defs/ContractVersion" }
        }
      },
      "WaveOrder": {
        "type": "object", "additionalProperties": false,
        "required": ["waveId", "outboundId", "outboundNumber", "lineCount", "allocationCount", "plannedTaskCount", "state", "addedBy", "addedAt", "addedVia", "version"],
        "properties": {
          "waveId": { "$ref": "#/$defs/Uuid" }, "outboundId": { "$ref": "#/$defs/Uuid" }, "outboundNumber": { "$ref": "#/$defs/Text" },
          "lineCount": { "$ref": "#/$defs/Count" }, "allocationCount": { "$ref": "#/$defs/Count" }, "plannedTaskCount": { "$ref": "#/$defs/Count" },
          "state": { "enum": ["Planned", "Issued", "Withdrawn", "Excluded", "Removed", "OrderCancelled"] },
          "addedBy": { "$ref": "#/$defs/Uuid" }, "addedAt": { "$ref": "#/$defs/DateTime" }, "addedVia": { "enum": ["Manual", "Suggestion"] },
          "closedBy": { "$ref": "#/$defs/Uuid" }, "closedAt": { "$ref": "#/$defs/DateTime" }, "closeReason": { "$ref": "#/$defs/Text" },
          "version": { "type": "integer", "minimum": 1 }
        }
      },
      "WaveEvent": {
        "type": "object", "additionalProperties": false, "required": ["eventId", "waveId", "seq", "kind", "atUtc", "data"],
        "oneOf": [ { "required": ["actorUserId"] }, { "required": ["system"] } ],
        "properties": {
          "eventId": { "$ref": "#/$defs/Uuid" }, "waveId": { "$ref": "#/$defs/Uuid" }, "seq": { "type": "integer", "minimum": 1 },
          "kind": { "enum": ["Created", "Updated", "OrdersAdded", "OrdersRemoved", "Cancelled", "ReleaseStarted", "Released", "ReleaseAborted", "OrderExcluded",
                             "PickingStarted", "RemainingRecalled", "OrderWithdrawn", "OrderMoved", "OrderCancelledUpstream", "Completed", "PartiallyCompleted",
                             "ExceptionRaised", "ExceptionResolved", "ExceptionIgnored", "ExceptionCleared"] },
          "actorUserId": { "$ref": "#/$defs/Uuid" }, "system": { "const": true }, "atUtc": { "$ref": "#/$defs/DateTime" },
          "data": { "type": "object", "additionalProperties": { "type": "string" } }
        }
      },
      "WaveWarehouseSettings": {
        "type": "object", "additionalProperties": false,
        "required": ["warehouseId", "waveManaged", "riskMinutesBeforeCutoff", "riskProgressPercent", "pendingTaskMinutes", "deferredOrderCount",
                     "releasingOrderCount", "stuckOrders", "version", "contractVersion"],
        "properties": {
          "warehouseId": { "$ref": "#/$defs/Uuid" }, "waveManaged": { "type": "boolean" }, "timeZoneId": { "$ref": "#/$defs/Text" },
          "riskMinutesBeforeCutoff": { "type": "integer", "minimum": 1, "maximum": 1440 },
          "riskProgressPercent": { "type": "integer", "minimum": 1, "maximum": 100 },
          "pendingTaskMinutes": { "type": "integer", "minimum": 1, "maximum": 1440 },
          "deferredOrderCount": { "$ref": "#/$defs/Count" }, "releasingOrderCount": { "$ref": "#/$defs/Count" },
          "stuckOrders": {
            "type": "array",
            "items": {
              "type": "object", "additionalProperties": false, "required": ["outboundId", "outboundNumber", "code", "firstFailedAtUtc"],
              "properties": {
                "outboundId": { "$ref": "#/$defs/Uuid" }, "outboundNumber": { "$ref": "#/$defs/Text" },
                "code": { "enum": ["OutboundUnreadable", "PlanInconsistent", "Unexpected", "RepeatedConnectionError"] },
                "firstFailedAtUtc": { "$ref": "#/$defs/DateTime" }
              }
            }
          },
          "version": { "type": "integer", "minimum": 0 }, "contractVersion": { "$ref": "#/$defs/ContractVersion" }
        }
      },
      "WaveCandidate": {
        "type": "object", "additionalProperties": false,
        "required": ["outboundId", "outboundNumber", "shipTo", "lineCount", "allocationCount", "plannedTaskCount", "createdAt", "eligible"],
        "properties": {
          "outboundId": { "$ref": "#/$defs/Uuid" }, "outboundNumber": { "$ref": "#/$defs/Text" }, "orderRef": { "$ref": "#/$defs/Text" },
          "shipTo": {
            "type": "object", "additionalProperties": false, "required": ["name", "country"],
            "properties": { "name": { "type": "string" }, "country": { "type": "string" }, "city": { "$ref": "#/$defs/Text" }, "postalCode": { "$ref": "#/$defs/Text" } }
          },
          "lineCount": { "$ref": "#/$defs/Count" }, "allocationCount": { "$ref": "#/$defs/Count" }, "plannedTaskCount": { "$ref": "#/$defs/Count" },
          "createdAt": { "$ref": "#/$defs/DateTime" }, "eligible": { "type": "boolean" }, "ineligibleReason": { "const": "AllocationPending" }
        }
      },
      "WaveSuggestions": {
        "type": "object", "additionalProperties": false, "required": ["items", "limitedBy", "contractVersion"],
        "properties": {
          "items": { "type": "array", "items": { "$ref": "#/$defs/WaveCandidate" } }, "limitedBy": { "enum": ["MaxOrders", "MaxLines", "Default"] },
          "contractVersion": { "$ref": "#/$defs/ContractVersion" }
        }
      },
      "WaveOrdersResult": {
        "type": "object", "additionalProperties": false, "required": ["wave", "orders", "contractVersion"],
        "properties": {
          "wave": { "$ref": "#/$defs/Wave" },
          "orders": {
            "type": "array",
            "items": {
              "type": "object", "additionalProperties": false, "required": ["outboundId", "outcome"],
              "properties": {
                "outboundId": { "$ref": "#/$defs/Uuid" }, "outcome": { "enum": ["Added", "Removed", "NotMember"] }, "order": { "$ref": "#/$defs/WaveOrder" }
              }
            }
          },
          "contractVersion": { "$ref": "#/$defs/ContractVersion" }
        }
      },
      "WaveLoad": {
        "type": "object", "additionalProperties": false, "required": ["zones", "pickers", "contractVersion"],
        "properties": {
          "zones": {
            "type": "array",
            "items": {
              "type": "object", "additionalProperties": false, "required": ["zoneKey", "plannedTaskCount", "orderCount"],
              "properties": {
                "zoneKey": { "type": "string" }, "aisle": { "$ref": "#/$defs/Text" }, "plannedTaskCount": { "$ref": "#/$defs/Count" },
                "orderCount": { "$ref": "#/$defs/Count" }
              }
            }
          },
          "pickers": {
            "type": "array",
            "items": {
              "type": "object", "additionalProperties": false, "required": ["userId", "taskCount"],
              "properties": { "userId": { "$ref": "#/$defs/Uuid" }, "taskCount": { "$ref": "#/$defs/Count" } }
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
        defs[WavePage] = Page(Wave);
        defs[OrderPage] = Page("WaveOrder");
        defs[EventPage] = Page("WaveEvent");
        defs[CandidatePage] = Page("WaveCandidate");
        return defs;
    });

    /// <summary>How many answers were validated per operation + status.</summary>
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
            ["$id"] = $"https://contracts.diten.test/wms-wave-bundle-draft/v1/{n}",
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

    internal sealed record Operation(string Method, string Template, string OperationId, Regex Path, string Success);

    private static Operation Op(string method, string template, string operationId, string success)
        => new(method, template, operationId,
            new Regex("^/api/warehouse/" + Regex.Replace(Regex.Escape(template), @"\\\{[a-zA-Z]+}", "[^/]+") + "/?$", RegexOptions.Compiled), success);

    /// <summary>Draft §4.1 — the 14 DL-1 operations (fixed segments first so a path parameter never shadows them).</summary>
    internal static readonly Operation[] Operations =
    [
        Op("GET", "waves", "listWaves", WavePage),
        Op("POST", "waves", "createWave", Wave),
        Op("GET", "waves/candidates", "listWaveCandidates", CandidatePage),
        Op("GET", "waves/settings/{warehouseId}", "getWaveWarehouseSettings", Settings),
        Op("POST", "waves/settings/{warehouseId}/update", "updateWaveWarehouseSettings", Settings),
        Op("GET", "waves/{waveId}", "getWave", Wave),
        Op("POST", "waves/{waveId}/update", "updateWave", Wave),
        Op("GET", "waves/{waveId}/orders", "listWaveOrders", OrderPage),
        Op("POST", "waves/{waveId}/orders", "addWaveOrders", OrdersResult),
        Op("POST", "waves/{waveId}/orders/remove", "removeWaveOrders", OrdersResult),
        Op("GET", "waves/{waveId}/suggestions", "listWaveSuggestions", Suggestions),
        Op("POST", "waves/{waveId}/cancel", "cancelWave", Wave),
        Op("GET", "waves/{waveId}/load", "getWaveLoad", Load),
        Op("GET", "waves/{waveId}/events", "listWaveEvents", EventPage)
    ];

    /// <summary>The operation rows of the draft FILE: (method, path, operationId, x-permission, slice table) — read, never hand-copied.</summary>
    internal static IReadOnlyList<(string Method, string Path, string OperationId, string Permission, bool Dl1)> DraftRows()
    {
        var text = File.ReadAllText(System.IO.Path.Combine(RepoPaths.Root, DraftPath.Replace('/', System.IO.Path.DirectorySeparatorChar)));
        var binding = text.IndexOf("### 4.1", StringComparison.Ordinal);
        var preliminary = text.IndexOf("### 4.2", StringComparison.Ordinal);
        var end = text.IndexOf("## 5.", StringComparison.Ordinal);
        return Regex.Matches(text, @"^\| `(GET|POST|PUT|DELETE|PATCH) (/[^`]+)` \| `([A-Za-z]+)` \| `([a-z.\-]+)` \|", RegexOptions.Multiline)
            .Where(m => m.Index > binding && m.Index < end)
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, m.Index < preliminary)).ToList();
    }
}

/// <summary>
/// Every <c>/api/warehouse/waves*</c> answer a wave test receives is checked against <see cref="WaveDraftContract"/>: the success body against
/// its operation's schema, every other status against <c>Error</c>, plus the <c>X-Correlation-Id</c> header. A violation throws (the test turns
/// red).
/// </summary>
public sealed class WaveDraftContractHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var path = request.RequestUri!.AbsolutePath;
        var operation = WaveDraftContract.Operations.FirstOrDefault(o => o.Method == request.Method.Method && o.Path.IsMatch(path));
        if (operation is null) return response;

        var status = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        // 401 / 403 / 500 come from the service-wide auth and exception middleware, before / around any 0180 code (no error code there)
        var schema = status is 200 or 201 ? operation.Success : status is 401 or 403 or 500 ? WaveDraftContract.AuthError : WaveDraftContract.Error;
        var key = operation.OperationId;
        if (status != 401 || body.Length > 0)
        {
            var errors = WaveDraftContract.Validate(schema, body);
            if (errors.Count > 0)
                throw new ContractViolationException($"{key} {status} body does not match the WMS-WAVE-BUNDLE draft '{schema}':\n  {string.Join("\n  ", errors)}\nBody: {body}");
        }

        if (!response.Headers.TryGetValues("X-Correlation-Id", out var correlation) || !Guid.TryParse(correlation.Single(), out _))
            throw new ContractViolationException($"{key} {status} carries no UUID X-Correlation-Id header.");

        WaveDraftContract.Validated.AddOrUpdate($"{key} {status}", 1, (_, n) => n + 1);
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
