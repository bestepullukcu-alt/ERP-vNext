using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Application.Features.Quarantine.Commands;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Api.Quarantine;

/// <summary>
/// Reads the QUARANTINE-BUNDLE request bodies from raw JSON (the TRACE / LOCATION reader rules): the body is a JSON object;
/// only the members of its schema are accepted; a scope member (<c>tenantId</c> / <c>legalEntityId</c>) is refused; strings
/// are strings, numbers numbers, booleans booleans; ids are canonical UUIDs. A violation is a contract 400
/// (<c>INVALID_REQUEST</c>) before any handler runs — never a model-binding accident.
/// </summary>
public static class QuarantineRequestReader
{
    private static readonly string[] ScopeProperties = ["tenantId", "legalEntityId", "tenant_id", "legal_entity_id"];

    public sealed record ReadResult<T>(T? Value, string? Error) where T : class
    {
        public static ReadResult<T> Fail(string error) => new(null, error);
    }

    public sealed class FormatException(string message) : Exception(message);

    /// <summary>One parsed body with typed, refusing accessors.</summary>
    public sealed class Body(JsonElement root)
    {
        public JsonElement Root { get; } = root;

        public bool Has(string name) => Root.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null;

        public string? String(string name)
        {
            if (!Root.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            return v.ValueKind == JsonValueKind.String ? v.GetString() : throw new FormatException($"{name} must be a string.");
        }

        public bool? Bool(string name)
        {
            if (!Root.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            return v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : throw new FormatException($"{name} must be a boolean.");
        }

        public int? Int(string name)
        {
            if (!Root.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : throw new FormatException($"{name} must be an integer.");
        }

        public static decimal Decimal(JsonElement v, string name)
            => v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) && InventoryQuantity.Scale(d) <= InventoryQuantity.MaxFractionDigits
                ? d
                : throw new FormatException($"{name} must be a number with at most {InventoryQuantity.MaxFractionDigits} fraction digits.");

        public Guid Uuid(string name)
            => QuarantineSupport.TryParseId(String(name), out var id) ? id : throw new FormatException($"{name} must be a canonical UUID.");

        public Guid? OptionalUuid(string name)
            => String(name) is null ? null : Uuid(name);

        public DateOnly? Date(string name)
        {
            var raw = String(name);
            if (raw is null) return null;
            return DateOnly.TryParseExact(raw, QuarantineSupport.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                ? day
                : throw new FormatException($"{name} must be a date yyyy-MM-dd.");
        }

        public IReadOnlyList<JsonElement>? Array(string name)
        {
            if (!Root.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            return v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().ToList() : throw new FormatException($"{name} must be an array.");
        }

        public IReadOnlyDictionary<string, string>? StringMap(string name)
        {
            if (!Root.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            if (v.ValueKind != JsonValueKind.Object) throw new FormatException($"{name} must be an object.");
            return v.EnumerateObject().ToDictionary(p => p.Name,
                p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : throw new FormatException($"{name}.{p.Name} must be a string."),
                StringComparer.Ordinal);
        }

        public T Enum<T>(string name) where T : struct, Enum
            => String(name) is { } raw && System.Enum.TryParse<T>(raw, false, out var value) && System.Enum.IsDefined(value) && !int.TryParse(raw, out _)
                ? value
                : throw new FormatException($"{name} must be one of {string.Join(", ", System.Enum.GetNames<T>())}.");
    }

    public static async Task<ReadResult<T>> ReadAsync<T>(
        Stream stream, IReadOnlySet<string> allowed, Func<Body, T> map, CancellationToken ct) where T : class
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
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return ReadResult<T>.Fail("The body must be a JSON object.");
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (ScopeProperties.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                    return ReadResult<T>.Fail($"{property.Name} is never accepted from a body; the scope comes from the token and the company selector.");
                if (!allowed.Contains(property.Name))
                    return ReadResult<T>.Fail($"Unknown member '{property.Name}'.");
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

    // ── bodies ────────────────────────────────────────────────────────────────────────────────────────────

    private static readonly HashSet<string> BucketMembers = new(StringComparer.Ordinal)
        { "itemId", "skuId", "skuLevel", "warehouseId", "locationId", "quantity", "lotId" };

    public static readonly HashSet<string> QuarantineMembers = new(StringComparer.Ordinal)
        { "mode", "lotId", "buckets", "reason", "signatureId", "idempotencyKey" };

    public static readonly HashSet<string> BlockMembers = new(StringComparer.Ordinal)
        { "buckets", "blockReasonCode", "note", "reason", "signatureId", "idempotencyKey" };

    public static readonly HashSet<string> ReviewMembers = new(StringComparer.Ordinal) { "note", "idempotencyKey" };

    public static readonly HashSet<string> ReviewCompleteMembers = new(StringComparer.Ordinal) { "reason", "idempotencyKey" };

    public static readonly HashSet<string> DecisionMembers = new(StringComparer.Ordinal)
        { "kind", "targetId", "lines", "blockReasonCode", "reason", "coaChecked", "newRetestDate", "disposition", "idempotencyKey" };

    private static readonly HashSet<string> LineMembers = new(StringComparer.Ordinal) { "lineNo", "decision", "quantity" };

    private static readonly HashSet<string> DispositionMembers = new(StringComparer.Ordinal)
        { "kind", "quantity", "supplierId", "returnDocumentNo", "destructionMethod", "destructionDate" };

    public static readonly HashSet<string> SubmitMembers = new(StringComparer.Ordinal) { "signatureId", "idempotencyKey" };

    public static readonly HashSet<string> SettingsMembers = new(StringComparer.Ordinal)
        { "waitTargetDays", "fourEyes", "approverCandidates", "sampleLocations", "lotReleaseRequiresApproval", "reconcileAfterSeconds" };

    public static readonly HashSet<string> BlockReasonMembers = new(StringComparer.Ordinal) { "code", "names", "isActive" };

    public static readonly HashSet<string> TestOutcomeMembers = new(StringComparer.Ordinal) { "outcome", "approverUserId" };

    public static IReadOnlyList<BucketInput>? Buckets(Body body)
        => body.Array("buckets")?.Select((element, i) =>
        {
            if (element.ValueKind != JsonValueKind.Object) throw new FormatException($"buckets[{i}] must be an object.");
            var unknown = element.EnumerateObject().Select(p => p.Name).FirstOrDefault(n => !BucketMembers.Contains(n));
            if (unknown is not null) throw new FormatException($"Unknown member 'buckets[{i}].{unknown}'.");
            var b = new Body(element);
            if (!WireNames.TryParseSkuLevel(b.String("skuLevel"), out var level)) throw new FormatException($"buckets[{i}].skuLevel must be Gsku, Lsku or FinishedGood.");
            if (!b.Root.TryGetProperty("quantity", out var q)) throw new FormatException($"buckets[{i}].quantity is required.");
            return new BucketInput(b.Uuid("itemId"), b.Uuid("skuId"), level, b.String("warehouseId") ?? "", b.String("locationId") ?? "",
                Body.Decimal(q, $"buckets[{i}].quantity"), b.String("lotId"));
        }).ToList();

    public static IReadOnlyList<RequestedLine> Lines(Body body)
        => (body.Array("lines") ?? []).Select((element, i) =>
        {
            if (element.ValueKind != JsonValueKind.Object) throw new FormatException($"lines[{i}] must be an object.");
            var unknown = element.EnumerateObject().Select(p => p.Name).FirstOrDefault(n => !LineMembers.Contains(n));
            if (unknown is not null) throw new FormatException($"Unknown member 'lines[{i}].{unknown}'.");
            var l = new Body(element);
            if (!l.Root.TryGetProperty("quantity", out var q)) throw new FormatException($"lines[{i}].quantity is required.");
            return new RequestedLine(l.Int("lineNo") ?? throw new FormatException($"lines[{i}].lineNo is required."), l.Enum<LineDecision>("decision"),
                Body.Decimal(q, $"lines[{i}].quantity"));
        }).ToList();

    public static RequestedDisposition? Disposition(Body body)
    {
        if (!body.Root.TryGetProperty("disposition", out var element) || element.ValueKind == JsonValueKind.Null) return null;
        if (element.ValueKind != JsonValueKind.Object) throw new FormatException("disposition must be an object.");
        var unknown = element.EnumerateObject().Select(p => p.Name).FirstOrDefault(n => !DispositionMembers.Contains(n));
        if (unknown is not null) throw new FormatException($"Unknown member 'disposition.{unknown}'.");
        var d = new Body(element);
        if (!d.Root.TryGetProperty("quantity", out var q)) throw new FormatException("disposition.quantity is required.");
        return new RequestedDisposition(d.Enum<DispositionKind>("kind"), Body.Decimal(q, "disposition.quantity"), d.OptionalUuid("supplierId"),
            d.String("returnDocumentNo"), d.String("destructionMethod"), d.Date("destructionDate"));
    }

    public static IReadOnlyList<string>? StringList(Body body, string name)
        => body.Array(name)?.Select((e, i) => e.ValueKind == JsonValueKind.String ? e.GetString()! : throw new FormatException($"{name}[{i}] must be a string.")).ToList();
}
