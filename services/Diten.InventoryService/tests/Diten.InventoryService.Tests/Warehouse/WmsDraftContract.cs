using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.InventoryService.Tests.TestInfrastructure;
using Json.Schema;

namespace Diten.InventoryService.Tests.Warehouse;

/// <summary>
/// MOD-0178 AC-19 — the WMS-BUNDLE v1 DRAFT (<c>docs/analysis/contracts/drafts/wms-bundle-v1-proposal.md</c>) turned into JSON Schema 2020-12
/// in the test project (the draft file itself is not changed). Derived from the draft, member by member:
/// <list type="bullet">
/// <item>§1 rule 3 the unwrapped success body and the <c>Error</c> envelope; rule 4 NO JSON null anywhere (no schema allows <c>null</c>);
/// rule 10 decimal strings (≤ 6 fraction digits); rule 9 / §3 <c>date-time</c> and <c>uuid</c> formats are enforced.</item>
/// <item>§3.1 <c>WarehouseTask</c>, §3.2 <c>TaskConfirmation</c>, §3.3 <c>PutawayCandidate</c>, §3.4 <c>WarehouseTaskEvent</c>, §3.5 / §3.6
/// <c>WarehouseLocationRole</c> (+ <c>effectiveStorageConditions</c>), §3.8 pages, §3.9 <c>ConfirmPutawayTaskResult</c>, §4 bulk results.</item>
/// <item>Every object is CLOSED (<c>additionalProperties: false</c>) and every enum is the draft's list: a member or a value the draft does
/// not name turns the call red. Deliberate additions over the draft are listed in <see cref="Additions"/> (proposed for the freeze step,
/// pack §19.7 — DL-2 report).</item>
/// </list>
/// <see cref="WmsDraftContractHandler"/> validates every <c>/api/warehouse</c> answer the warehouse tests receive.
/// </summary>
public static class WmsDraftContract
{
    /// <summary>Members the service writes that the draft does not name — each one a proposal for the freeze (DL-2 report, open point).</summary>
    public static readonly IReadOnlyList<string> Additions =
    [
        "TaskConfirmation.error.reason — KeyReused / HolderReversed / StatusDiverged / OverPlanned (pack §8.3, AC-63 / AC-64 / AC-66 / AC-69)",
        "BulkAssignResult.contractVersion, BulkDeleteLocationRolesResult.contractVersion — the v1 marker every other S1 body carries (§3.1, §3.8)"
    ];

    public const string Error = "Error";

    /// <summary>
    /// 401 / 403 come from the service-wide auth middleware, before any 0178 code: §1 rule 3 names the INVENTORY <c>Error</c>, whose
    /// schema requires no member (<c>inventory-bundle.openapi.yaml:484-494</c>) — there <c>code</c> may be absent; everything else closed.
    /// </summary>
    public const string AuthError = "AuthError";
    public const string Task = "WarehouseTask";
    public const string ConfirmResult = "ConfirmPutawayTaskResult";
    public const string TaskPage = "WarehouseTaskPage";
    public const string CandidatePage = "PutawayCandidatePage";
    public const string EventPage = "WarehouseTaskEventPage";
    public const string BulkAssignResult = "BulkAssignResult";
    public const string Role = "WarehouseLocationRole";
    public const string RolePage = "WarehouseLocationRolePage";
    public const string BulkDeleteResult = "BulkDeleteLocationRolesResult";

    private const string Defs = """
    {
      "Uuid": { "type": "string", "format": "uuid" },
      "Text": { "type": "string" },
      "DateTime": { "type": "string", "format": "date-time" },
      "Quantity": { "type": "string", "pattern": "^-?\\d+(\\.\\d{1,6})?$" },
      "ContractVersion": { "const": "v1" },
      "SkuLevel": { "enum": ["Gsku", "Lsku", "FinishedGood", "Item"] },
      "StockStatus": { "enum": ["AVAILABLE", "QUARANTINE", "QUALITY_INSPECTION", "BLOCKED", "RESERVED", "IN_TRANSIT"] },
      "Item": {
        "type": "object", "additionalProperties": false, "required": ["itemId", "skuId", "skuLevel"],
        "properties": { "itemId": { "$ref": "#/$defs/Uuid" }, "skuId": { "$ref": "#/$defs/Uuid" }, "skuLevel": { "$ref": "#/$defs/SkuLevel" } }
      },
      "Person": {
        "type": "object", "additionalProperties": false, "required": ["userId"],
        "properties": { "userId": { "$ref": "#/$defs/Uuid" }, "name": { "$ref": "#/$defs/Text" } }
      },
      "Stamp": {
        "type": "object", "additionalProperties": false, "required": ["userId", "atUtc"],
        "properties": { "userId": { "$ref": "#/$defs/Uuid" }, "name": { "$ref": "#/$defs/Text" }, "atUtc": { "$ref": "#/$defs/DateTime" } }
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
            "type": "object", "additionalProperties": false, "required": ["message", "correlationId"],
            "properties": {
              "code": { "type": "string", "pattern": "^[A-Z][A-Z0-9_]+$" }, "message": { "type": "string" },
              "correlationId": { "$ref": "#/$defs/Uuid" }
            }
          },
          "contractVersion": { "$ref": "#/$defs/ContractVersion" }
        }
      },
      "SourceRef": {
        "type": "object", "additionalProperties": false, "required": ["sourceModule"],
        "properties": {
          "sourceModule": { "type": "string" }, "sourceType": { "type": "string" }, "sourceDocumentId": { "type": "string" },
          "sourceLineId": { "type": "string" }, "transactionId": { "type": "string" }
        }
      },
      "HolderMovement": {
        "type": "object", "additionalProperties": false,
        "required": ["transactionId", "transactionNumber", "sourceModule", "sourceType", "quantity", "uomId", "baseQuantity", "postingDate"],
        "properties": {
          "transactionId": { "type": "string" }, "transactionNumber": { "type": "string" }, "sourceModule": { "type": "string" },
          "sourceType": { "type": "string" }, "sourceDocumentId": { "type": "string" }, "sourceLineId": { "type": "string" },
          "quantity": { "$ref": "#/$defs/Quantity" }, "uomId": { "type": "string" }, "baseQuantity": { "$ref": "#/$defs/Quantity" },
          "fromWarehouseId": { "type": "string" }, "fromLocationId": { "type": "string" }, "toWarehouseId": { "type": "string" },
          "toLocationId": { "type": "string" }, "postingDate": { "$ref": "#/$defs/DateTime" }, "lotId": { "type": "string" },
          "serialIds": { "type": "array", "items": { "type": "string" } }, "fromStockStatus": { "$ref": "#/$defs/StockStatus" },
          "toStockStatus": { "$ref": "#/$defs/StockStatus" }, "reversedByTransactionId": { "type": "string" }
        }
      },
      "ConfirmationError": {
        "type": "object", "additionalProperties": false,
        "properties": {
          "code": { "type": "string" }, "message": { "type": "string" }, "details": { "type": "object" },
          "status": { "type": "integer", "minimum": 100, "maximum": 599 }, "reason": { "type": "string" }
        }
      },
      "TaskConfirmation": {
        "type": "object", "additionalProperties": false,
        "required": ["seq", "quantity", "toLocationId", "status", "postingKey", "transactionDate", "postingDate", "channel", "confirmedBy", "confirmedAtUtc", "attempts"],
        "properties": {
          "seq": { "type": "integer", "minimum": 1 }, "quantity": { "$ref": "#/$defs/Quantity" }, "toLocationId": { "type": "string" },
          "deviationReason": { "type": "string" }, "status": { "enum": ["Pending", "Posted", "Refused", "IntegrityError"] },
          "postingKey": { "type": "string", "pattern": "^wms:put:[0-9a-f-]{36}:[1-9][0-9]*$" },
          "transactionDate": { "$ref": "#/$defs/DateTime" }, "postingDate": { "$ref": "#/$defs/DateTime" },
          "serialIds": { "type": "array", "items": { "type": "string" } }, "transactionId": { "type": "string" },
          "transactionNumber": { "type": "string" }, "error": { "$ref": "#/$defs/ConfirmationError" },
          "resolution": { "enum": ["SupersededByNewConfirmation", "ReturnedToSource"] },
          "priorResolution": { "enum": ["SupersededByNewConfirmation", "ReturnedToSource"] },
          "resolutionReason": { "type": "string" }, "channel": { "enum": ["Ui", "Api", "External"] }, "scanRef": { "type": "string" },
          "confirmedBy": { "$ref": "#/$defs/Person" }, "confirmedAtUtc": { "$ref": "#/$defs/DateTime" },
          "attempts": { "type": "integer", "minimum": 1 }, "lastAttemptAtUtc": { "$ref": "#/$defs/DateTime" },
          "refusalSource": { "enum": ["PreCheck", "Ledger4xx", "Resolve"] }, "refusedOnRetry": { "type": "boolean" },
          "refusedAtUtc": { "$ref": "#/$defs/DateTime" }, "inFlightUntilUtc": { "$ref": "#/$defs/DateTime" },
          "integrityResolution": { "enum": ["ResolvedOwnMovement", "ReturnedToRefused"] }, "resolveReason": { "type": "string" },
          "holderMovement": { "$ref": "#/$defs/HolderMovement" }
        }
      },
      "TaskMembers": {
        "taskId": { "$ref": "#/$defs/Uuid" }, "number": { "type": "string", "pattern": "^[A-Z]{2,5}-PUT-\\d{6,}$" },
        "companyCodePrefix": { "type": "string", "pattern": "^[A-Z]{2,5}$" }, "warehouseCode": { "type": "string" },
        "kind": { "enum": ["Putaway"] }, "status": { "enum": ["Open", "InProgress", "Completed", "Cancelled"] },
        "origin": { "enum": ["Manual", "Receipt", "Production", "Cancellation", "External"] }, "sourceRef": { "$ref": "#/$defs/SourceRef" },
        "sourceSystem": { "type": "string" }, "externalRef": { "type": "string" }, "legalEntityId": { "$ref": "#/$defs/Uuid" },
        "warehouseId": { "type": "string" }, "item": { "$ref": "#/$defs/Item" }, "lotId": { "type": "string" }, "lotNumber": { "type": "string" },
        "stockStatus": { "$ref": "#/$defs/StockStatus" }, "fromLocationId": { "type": "string" }, "toLocationId": { "type": "string" },
        "suggestedToLocationId": { "type": "string" }, "targetSource": { "enum": ["Manual", "SuggestionApplied"] },
        "plannedQuantity": { "$ref": "#/$defs/Quantity" }, "confirmedQuantity": { "$ref": "#/$defs/Quantity" },
        "pendingQuantity": { "$ref": "#/$defs/Quantity" }, "remainingQuantity": { "$ref": "#/$defs/Quantity" }, "baseUomId": { "type": "string" },
        "assignee": { "$ref": "#/$defs/Person" }, "priority": { "enum": ["Low", "Normal", "High"] }, "note": { "type": "string" },
        "serialControlled": { "type": "boolean" }, "confirmations": { "type": "array", "items": { "$ref": "#/$defs/TaskConfirmation" } },
        "integrationFlag": { "enum": ["None", "PostingPending", "PostingPendingLong", "PostingRefused", "PostingIntegrityError"] },
        "allowedActions": { "type": "array", "items": { "$ref": "#/$defs/Action" } },
        "disabledActions": {
          "type": "array",
          "items": {
            "type": "object", "additionalProperties": false, "required": ["action", "reasonCode"],
            "properties": { "action": { "$ref": "#/$defs/Action" }, "reasonCode": { "type": "string" } }
          }
        },
        "shortCloseReason": { "type": "string" }, "cancelReason": { "type": "string" }, "created": { "$ref": "#/$defs/Stamp" },
        "updated": { "$ref": "#/$defs/Stamp" }, "version": { "type": "integer", "minimum": 1 }, "contractVersion": { "$ref": "#/$defs/ContractVersion" }
      },
      "Action": { "enum": ["Confirm", "Claim", "Update", "Cancel", "CloseShort", "Retry", "MarkReturned", "ResolveIntegrity"] },
      "PutawayCandidate": {
        "type": "object", "additionalProperties": false,
        "required": ["candidateKey", "warehouseId", "locationId", "item", "stockStatus", "onHand", "openTaskQuantity", "openQuantity", "baseUomId", "eligible"],
        "properties": {
          "candidateKey": { "type": "string", "pattern": "^[^|]+\\|[^|]+\\|[^|]+\\|[^|]+\\|[^|]*\\|[^|]+$" },
          "warehouseId": { "type": "string" }, "locationId": { "type": "string" }, "locationCode": { "type": "string" },
          "item": { "$ref": "#/$defs/Item" }, "lotId": { "type": "string" }, "lotNumber": { "type": "string" },
          "lotStatus": { "enum": ["Draft", "Released", "Blocked", "Expired"] }, "stockStatus": { "$ref": "#/$defs/StockStatus" },
          "onHand": { "$ref": "#/$defs/Quantity" }, "openTaskQuantity": { "$ref": "#/$defs/Quantity" }, "openQuantity": { "$ref": "#/$defs/Quantity" },
          "baseUomId": { "type": "string" }, "eligible": { "type": "boolean" },
          "notEligibleReason": { "enum": ["StockHeld", "LotBlocked", "LotExpired", "LotUnderRecall"] },
          "blockingDocument": {
            "type": "object", "additionalProperties": false, "required": ["kind", "id"],
            "properties": { "kind": { "enum": ["QuarantineHold", "BlockedStock", "RecallCase", "LotStatus"] }, "id": { "type": "string" }, "number": { "type": "string" } }
          },
          "storageConditions": { "type": "array", "items": { "type": "string" } }, "lastPostingDate": { "$ref": "#/$defs/DateTime" }
        }
      },
      "WarehouseTaskEvent": {
        "type": "object", "additionalProperties": false, "required": ["seq", "kind", "system", "atUtc", "data", "correlationId"],
        "properties": {
          "seq": { "type": "integer", "minimum": 1 },
          "kind": { "enum": ["Created", "Updated", "Assigned", "Claimed", "SuggestionApplied", "ConfirmationRecorded", "ConfirmationPosted", "ConfirmationRefused",
                             "ConfirmationRetried", "ConfirmationReconciled", "ConfirmationIntegrityError", "IntegrityResolved", "ConfirmationDeviation",
                             "ConfirmationSuperseded", "ConfirmationReturned", "ClosedShort", "Completed", "Cancelled"] },
          "actor": { "$ref": "#/$defs/Person" }, "system": { "type": "boolean" }, "atUtc": { "$ref": "#/$defs/DateTime" },
          "data": { "type": "object", "additionalProperties": { "type": "string" } }, "correlationId": { "$ref": "#/$defs/Uuid" }
        }
      },
      "EffectiveStorageConditions": {
        "type": "object", "additionalProperties": false, "required": ["codes"],
        "properties": {
          "codes": { "type": "array", "items": { "type": "string" } }, "inheritedFromLocationId": { "type": "string" },
          "inheritedFromLocationCode": { "type": "string" }
        }
      },
      "WarehouseLocationRole": {
        "type": "object", "additionalProperties": false,
        "required": ["roleId", "legalEntityId", "warehouseId", "locationId", "roles", "acceptedStorageConditions", "version", "contractVersion"],
        "properties": {
          "roleId": { "$ref": "#/$defs/Uuid" }, "legalEntityId": { "$ref": "#/$defs/Uuid" }, "warehouseId": { "type": "string" },
          "locationId": { "type": "string" }, "locationCode": { "type": "string" }, "locationType": { "enum": ["StorageLocation", "Zone", "Bin"] },
          "roles": { "type": "array", "items": { "enum": ["Receiving", "Staging"] } },
          "acceptedStorageConditions": { "type": "array", "items": { "enum": ["AMBIENT_15_25", "COOL_2_8", "FROZEN_BELOW_MINUS_20", "PROTECT_FROM_LIGHT", "PROTECT_FROM_MOISTURE"] } },
          "effectiveStorageConditions": { "$ref": "#/$defs/EffectiveStorageConditions" }, "note": { "type": "string" },
          "version": { "type": "integer", "minimum": 1 }, "contractVersion": { "$ref": "#/$defs/ContractVersion" }
        }
      }
    }
    """;

    private static readonly object Gate = new();
    private static readonly ConcurrentDictionary<string, JsonSchema> Compiled = new(StringComparer.Ordinal);
    private static readonly Lazy<JsonObject> AllDefs = new(BuildDefs);

    /// <summary>How many answers were validated per operation + status (AC-19 coverage).</summary>
    public static readonly ConcurrentDictionary<string, int> Validated = new(StringComparer.Ordinal);

    private static JsonObject BuildDefs()
    {
        var defs = (JsonObject)JsonNode.Parse(Defs)!;
        var members = (JsonObject)defs["TaskMembers"]!;
        defs.Remove("TaskMembers");
        JsonObject TaskSchema(bool withSeq)
        {
            var properties = (JsonObject)members.DeepClone();
            var required = new JsonArray("taskId", "number", "companyCodePrefix", "kind", "status", "origin", "legalEntityId", "warehouseId", "item",
                "stockStatus", "fromLocationId", "targetSource", "plannedQuantity", "confirmedQuantity", "pendingQuantity", "remainingQuantity", "baseUomId",
                "priority", "serialControlled", "confirmations", "integrationFlag", "allowedActions", "disabledActions", "created", "version", "contractVersion");
            if (withSeq)
            {
                properties["confirmationSeq"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 };
                required.Add("confirmationSeq");
            }

            return new JsonObject { ["type"] = "object", ["additionalProperties"] = false, ["required"] = required, ["properties"] = properties };
        }

        JsonObject Page(string item) => new()
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

        JsonObject Rows(string array, string idName, string outcomeName, params string[] outcomes) => new()
        {
            ["type"] = "object", ["additionalProperties"] = false, ["required"] = new JsonArray(array),
            ["properties"] = new JsonObject
            {
                [array] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object", ["additionalProperties"] = false, ["required"] = new JsonArray(idName, outcomeName),
                        ["properties"] = new JsonObject
                        {
                            [idName] = new JsonObject { ["type"] = "string" },
                            [outcomeName] = new JsonObject { ["enum"] = new JsonArray(outcomes.Select(o => (JsonNode)o).ToArray()) },
                            ["version"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 }
                        }
                    }
                },
                ["contractVersion"] = new JsonObject { ["$ref"] = "#/$defs/ContractVersion" } // addition — see Additions
            }
        };

        defs[Task] = TaskSchema(false);
        defs[ConfirmResult] = TaskSchema(true);
        defs[TaskPage] = Page(Task);
        defs[CandidatePage] = Page("PutawayCandidate");
        defs[EventPage] = Page("WarehouseTaskEvent");
        defs[RolePage] = Page(Role);
        defs[BulkAssignResult] = Rows("results", "taskId", "outcome", "Assigned", "Conflict", "NotFound", "InvalidTransition", "PendingConfirmation");
        defs[BulkDeleteResult] = Rows("items", "roleId", "result", "Deleted", "Conflict", "NotFound");
        return defs;
    }

    private static JsonSchema Schema(string name)
        => Compiled.GetOrAdd(name, n => JsonSchema.FromText(new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = $"https://contracts.diten.test/wms-bundle-draft/v1/{n}",
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
        lock (Gate) // the Inventory validator measured concurrent Evaluate calls letting an invalid body through — serialised the same way
        {
            result = Schema(schemaName).Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.List, RequireFormatValidation = true });
        }

        return result.IsValid
            ? []
            : (result.Details ?? []).Where(d => d.Errors is { Count: > 0 })
                .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Key} — {e.Value}"))
                .DefaultIfEmpty("invalid (no detail)").ToList();
    }

    internal sealed record Operation(string Method, string Template, Regex Path, Func<int, string> Success);

    private static Operation Op(string method, string template, Func<int, string> success)
        => new(method, template, new Regex("^/api/warehouse/" + Regex.Escape(template).Replace("\\{id}", "[^/]+") + "/?$", RegexOptions.Compiled), success);

    /// <summary>WMS draft §4 — the S1 operations (literal segments first so <c>{id}</c> never shadows them).</summary>
    internal static readonly Operation[] Operations =
    [
        Op("GET", "putaway-candidates", _ => CandidatePage),
        Op("GET", "putaway-tasks", _ => TaskPage),
        Op("POST", "putaway-tasks", _ => Task),
        Op("POST", "putaway-tasks/bulk-assign", _ => BulkAssignResult),
        Op("GET", "putaway-tasks/{id}", _ => Task),
        Op("PUT", "putaway-tasks/{id}", _ => Task),
        Op("GET", "putaway-tasks/{id}/events", _ => EventPage),
        Op("POST", "putaway-tasks/{id}/claim", _ => Task),
        Op("POST", "putaway-tasks/{id}/confirmations", _ => ConfirmResult),
        Op("POST", "putaway-tasks/{id}/confirmations/{id}/retry", _ => ConfirmResult),
        Op("POST", "putaway-tasks/{id}/confirmations/{id}/returned", _ => Task),
        Op("POST", "putaway-tasks/{id}/confirmations/{id}/resolve-integrity", _ => Task),
        Op("POST", "putaway-tasks/{id}/close-short", _ => Task),
        Op("POST", "putaway-tasks/{id}/cancel", _ => Task),
        Op("GET", "location-roles", _ => RolePage),
        Op("POST", "location-roles/bulk-delete", _ => BulkDeleteResult),
        Op("POST", "location-roles", _ => Role),
        Op("GET", "location-roles/{id}", _ => Role),
        Op("PUT", "location-roles/{id}", _ => Role),
        Op("DELETE", "location-roles/{id}", _ => Role)
    ];
}

/// <summary>
/// AC-19 — every <c>/api/warehouse</c> answer a warehouse test receives is checked against <see cref="WmsDraftContract"/>: the success body
/// against its operation's schema, every other status against <c>Error</c>, plus the <c>X-Correlation-Id</c> header. A violation throws
/// (the test turns red). The frozen WAREHOUSE-OUTBOUND GETs are measured by AC-18 against the frozen file's rules, not here.
/// </summary>
public sealed class WmsDraftContractHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var path = request.RequestUri!.AbsolutePath;
        var operation = WmsDraftContract.Operations.FirstOrDefault(o => o.Method == request.Method.Method && o.Path.IsMatch(path));
        if (operation is null) return response;

        var status = (int)response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var schema = status is 200 or 201 ? operation.Success(status) : status is 401 or 403 ? WmsDraftContract.AuthError : WmsDraftContract.Error;
        var errors = WmsDraftContract.Validate(schema, body);
        var key = $"{operation.Method} {operation.Template}";
        if (errors.Count > 0)
        {
            throw new ContractViolationException($"{key} {status} body does not match the WMS draft '{schema}':\n  {string.Join("\n  ", errors)}\nBody: {body}");
        }

        if (!response.Headers.TryGetValues("X-Correlation-Id", out var correlation) || !Guid.TryParse(correlation.Single(), out _))
        {
            throw new ContractViolationException($"{key} {status} carries no UUID X-Correlation-Id header.");
        }

        WmsDraftContract.Validated.AddOrUpdate($"{key} {status}", 1, (_, n) => n + 1);
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
