using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Application.Features.Recall;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Api.Recall;

/// <summary>
/// Reads the RECALL-BUNDLE request bodies from raw JSON (the FEFO / QUARANTINE reader rules, own copy so the modules stay apart): the body
/// is a JSON object; only the members of its schema are accepted; a scope member (<c>tenantId</c> / <c>legalEntityId</c>) is refused; strings
/// are strings, numbers numbers; ids are canonical UUIDs; enums are their exact names. A violation is a contract 400
/// <c>INVALID_REQUEST</c> before any handler runs.
/// </summary>
public static class RecallRequestReader
{
    private static readonly string[] ScopeProperties = ["tenantId", "legalEntityId", "tenant_id", "legal_entity_id"];

    public sealed record ReadResult<T>(T? Value, string? Error) where T : class
    {
        public static ReadResult<T> Fail(string error) => new(null, error);
    }

    public sealed class FormatException(string message) : Exception(message);

    public sealed class Body(JsonElement root)
    {
        public JsonElement Root { get; } = root;

        public string? String(string name) => Str(Root, name);

        public int Int(string name)
            => Root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)
                ? i
                : throw new FormatException($"{name} is required and must be an integer.");

        /// <summary>R5: an optional integer (absent / null → null).</summary>
        public int? OptionalInt(string name)
            => !Root.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null ? null
                : v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : throw new FormatException($"{name} must be an integer.");

        /// <summary>R5: an optional boolean (absent / null → null).</summary>
        public bool? OptionalBool(string name)
            => !Root.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null ? null
                : v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : throw new FormatException($"{name} must be true or false.");

        public decimal Decimal(string name) => OptionalDecimal(Root, name) ?? throw new FormatException($"{name} is required and must be a number.");

        public Guid Uuid(string name) => Id(Root, name) ?? throw new FormatException($"{name} is required and must be a canonical UUID.");

        public Guid? OptionalUuid(string name) => Id(Root, name);

        public T Enum<T>(string name) where T : struct, Enum => EnumOf<T>(Root, name);

        public DateOnly Date(string name)
        {
            var raw = String(name) ?? throw new FormatException($"{name} is required (yyyy-MM-dd).");
            return DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                ? day
                : throw new FormatException($"{name} must be a date yyyy-MM-dd.");
        }

        public Domain.Enums.SkuLevel SkuLevel(string name)
            => WireNames.TryParseSkuLevel(String(name), out var level) ? level : throw new FormatException($"{name} must be Gsku, Lsku, FinishedGood or Item.");

        /// <summary>An object of numbers with exactly the keys "1", "2", "3" (checked by the handler for ranges).</summary>
        public IReadOnlyDictionary<string, decimal> Targets(string name)
        {
            if (!Root.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Object) throw new FormatException($"{name} is required and must be an object.");
            var map = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var p in v.EnumerateObject())
            {
                if (!RecallSettingsRules.Classes.Contains(p.Name)) throw new FormatException($"{name}.{p.Name} is not a recall class (1, 2, 3).");
                map[p.Name] = p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetDecimal(out var d) ? d : throw new FormatException($"{name}.{p.Name} must be a number.");
            }

            return map;
        }

        /// <summary>addScopeItems — the array of hand-entered items.</summary>
        public IReadOnlyList<RecallScopeItemInput> ScopeItems(string name)
        {
            if (!Root.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array) throw new FormatException($"{name} is required and must be an array.");
            var list = new List<RecallScopeItemInput>();
            foreach (var e in v.EnumerateArray())
            {
                Members(e, ItemMembers, name);
                StockStatus? status = null;
                if (Str(e, "stockStatus") is { } raw)
                {
                    status = WireNames.TryParseStockStatus(raw, out var s) ? s : throw new FormatException($"{name}[].stockStatus must be a stock status (AVAILABLE, QUARANTINE …).");
                }

                list.Add(new RecallScopeItemInput(EnumOf<RecallScopeItemKind>(e, "kind"), Id(e, "lotId") ?? throw new FormatException($"{name}[].lotId is required."),
                    Str(e, "parentKey"), Id(e, "warehouseId"), Id(e, "locationId"), status, Str(e, "sourceDocumentId"), Str(e, "customerRef"),
                    OptionalDecimal(e, "quantity"), Str(e, "baseUomId")));
            }

            return list;
        }

        /// <summary>markScopeItems — the array of marks.</summary>
        /// <summary>R3: an array of 1+ non-empty strings (node keys).</summary>
        public IReadOnlyList<string> Strings(string name)
        {
            if (!Root.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array) throw new FormatException($"{name} is required and must be an array.");
            return v.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String && e.GetString() is { Length: > 0 } s
                ? s
                : throw new FormatException($"{name}[] entries must be non-empty strings.")).ToList();
        }

        /// <summary>R3: marks are optional in reviseScope.</summary>
        public IReadOnlyList<RecallScopeMarkInput> OptionalMarks(string name) => Root.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? Marks(name) : [];

        public IReadOnlyList<RecallScopeMarkInput> Marks(string name)
        {
            if (!Root.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array) throw new FormatException($"{name} is required and must be an array.");
            var list = new List<RecallScopeMarkInput>();
            foreach (var e in v.EnumerateArray())
            {
                Members(e, MarkMembers, name);
                var included = e.TryGetProperty("included", out var inc) && inc.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? inc.GetBoolean()
                    : throw new FormatException($"{name}[].included is required and must be a boolean.");
                list.Add(new RecallScopeMarkInput(Str(e, "nodeKey") ?? throw new FormatException($"{name}[].nodeKey is required."), included, Str(e, "exclusionReason")));
            }

            return list;
        }

        private static void Members(JsonElement e, IReadOnlySet<string> allowed, string name)
        {
            if (e.ValueKind != JsonValueKind.Object) throw new FormatException($"{name}[] entries must be objects.");
            foreach (var p in e.EnumerateObject())
            {
                if (!allowed.Contains(p.Name)) throw new FormatException($"Unknown member '{name}[].{p.Name}'.");
            }
        }

        private static string? Str(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            return v.ValueKind == JsonValueKind.String ? v.GetString() : throw new FormatException($"{name} must be a string.");
        }

        private static Guid? Id(JsonElement e, string name)
        {
            var raw = Str(e, name);
            if (raw is null) return null;
            return RecallSupport.TryParseId(raw, out var id) ? id : throw new FormatException($"{name} must be a canonical UUID.");
        }

        private static decimal? OptionalDecimal(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            return v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) && InventoryQuantity.Scale(d) <= InventoryQuantity.MaxFractionDigits
                ? d
                : throw new FormatException($"{name} must be a number with at most {InventoryQuantity.MaxFractionDigits} fraction digits.");
        }

        private static T EnumOf<T>(JsonElement e, string name) where T : struct, Enum
            => RecallWire.TryParseName<T>(Str(e, name), out var value)
                ? value
                : throw new FormatException($"{name} must be one of {string.Join(", ", System.Enum.GetNames<T>())}.");
    }

    public static async Task<ReadResult<T>> ReadAsync<T>(Stream stream, IReadOnlySet<string> allowed, Func<Body, T> map, CancellationToken ct) where T : class
    {
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (JsonException)
        {
            return ReadResult<T>.Fail("The body is not valid JSON.");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object) return ReadResult<T>.Fail("The body must be a JSON object.");
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (ScopeProperties.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                    return ReadResult<T>.Fail($"{property.Name} is never accepted from a body; the scope comes from the token and the company selector.");
                if (!allowed.Contains(property.Name)) return ReadResult<T>.Fail($"Unknown member '{property.Name}'.");
            }

            try
            {
                return new ReadResult<T>(map(new Body(document.RootElement.Clone())), null);
            }
            catch (FormatException ex)
            {
                return ReadResult<T>.Fail(ex.Message);
            }
        }
    }

    private static readonly HashSet<string> ItemMembers = new(StringComparer.Ordinal)
        { "kind", "lotId", "parentKey", "warehouseId", "locationId", "stockStatus", "sourceDocumentId", "customerRef", "quantity", "baseUomId" };

    private static readonly HashSet<string> MarkMembers = new(StringComparer.Ordinal) { "nodeKey", "included", "exclusionReason" };

    public static readonly HashSet<string> SettingsMembers = new(StringComparer.Ordinal)
        { "drillTargetHours", "drillFrequencyWarningDays", "effectivenessTargetPercent", "traceMaxDepth", "traceMaxNodes", "reason", "signatureId", "idempotencyKey" };

    /// <summary>R4 prepareSettingsChange — the settings body without a signature (RECALL 1.3.0).</summary>
    public static readonly HashSet<string> PrepareSettingsMembers = new(StringComparer.Ordinal)
        { "drillTargetHours", "drillFrequencyWarningDays", "effectivenessTargetPercent", "traceMaxDepth", "traceMaxNodes", "reason", "idempotencyKey" };

    public static readonly HashSet<string> OpenMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "rootLotId", "direction", "class", "level", "reason", "reasonReference" };

    public static readonly HashSet<string> AddItemsMembers = new(StringComparer.Ordinal) { "idempotencyKey", "items" };

    public static readonly HashSet<string> MarksMembers = new(StringComparer.Ordinal) { "idempotencyKey", "marks" };

    public static readonly HashSet<string> NoteMembers = new(StringComparer.Ordinal) { "idempotencyKey", "note" };

    public static readonly HashSet<string> ReasonMembers = new(StringComparer.Ordinal) { "idempotencyKey", "reason" };

    public static readonly HashSet<string> KeyMembers = new(StringComparer.Ordinal) { "idempotencyKey" };

    /// <summary>WP-0177-R7b createEvidenceExport (RECALL 1.7.0).</summary>
    public static readonly HashSet<string> EvidenceExportMembers = new(StringComparer.Ordinal) { "idempotencyKey", "caseId", "runId", "from", "to", "format" };

    public static readonly HashSet<string> PrepareMembers = new(StringComparer.Ordinal) { "idempotencyKey", "action", "note", "summary", "openQuantityAcceptance" };

    public static readonly HashSet<string> SignedNoteMembers = new(StringComparer.Ordinal) { "idempotencyKey", "note", "signatureId" };

    public static readonly HashSet<string> CloseMembers = new(StringComparer.Ordinal) { "idempotencyKey", "summary", "openQuantityAcceptance", "signatureId" };

    public static readonly HashSet<string> NotificationMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "customerRef", "notifiedOn", "channel", "note", "supersedesId" };

    public static readonly HashSet<string> ConsumptionMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "customerRef", "skuId", "skuLevel", "quantity", "declaredOn", "evidence", "supersedesId" };

    public static readonly HashSet<string> SegregationMembers = new(StringComparer.Ordinal) { "idempotencyKey", "nodeKey", "locationId", "quantity", "note" };

    // R3
    public static readonly HashSet<string> RequestQuarantineMembers = new(StringComparer.Ordinal) { "idempotencyKey", "nodeKeys" };

    public static readonly HashSet<string> LinkReturnMembers = new(StringComparer.Ordinal) { "idempotencyKey", "transactionId", "customerRef", "returnReference" };

    public static readonly HashSet<string> ReviseMembers = new(StringComparer.Ordinal) { "idempotencyKey", "addNodeKeys", "marks", "reason" };

    // R5 drills — no time member anywhere: the server clock stamps start and end (pack §8.10)
    public static readonly HashSet<string> DrillPlanMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "name", "frequency", "customDays", "scopeKind", "nextDueOn", "ownerUserId", "active" };

    public static readonly HashSet<string> StartDrillMembers = new(StringComparer.Ordinal) { "idempotencyKey", "planId", "selectionMode", "lotId" };

    public static readonly HashSet<string> DrillNoteMembers = new(StringComparer.Ordinal) { "idempotencyKey", "resultNote" };

    public static readonly HashSet<string> DrillResultMembers = new(StringComparer.Ordinal) { "idempotencyKey", "resultNote", "signatureId" };
}
