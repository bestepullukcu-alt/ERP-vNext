using System.Text.Json;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts;

namespace Diten.InventoryService.Api.CycleCounting;

/// <summary>
/// Reads the WMS-COUNT-BUNDLE request bodies from raw JSON (the MOD-0178 <c>WarehouseRequestReader</c> rules, own copy so the modules stay apart):
/// the body is a JSON object; only the members of its schema are accepted (a server field such as <c>number</c> or <c>status</c> is an unknown
/// member → 400, AC-12); a scope member (<c>tenantId</c> / <c>legalEntityId</c>) is refused (pack §4, AC-6); strings are strings; ids are
/// canonical UUIDs; a JSON null is the member's absence (draft §1 rule 4). A violation is a contract 400 <c>INVALID_REQUEST</c> before any
/// handler runs.
/// </summary>
public static class CycleCountRequestReader
{
    private static readonly string[] ScopeProperties = ["tenantId", "legalEntityId", "tenant_id", "legal_entity_id"];

    public sealed record ReadResult<T>(T? Value, string? Error) where T : class
    {
        public static ReadResult<T> Fail(string error) => new(null, error);
    }

    public sealed class FormatException(string message) : Exception(message);

    public sealed class Body(JsonElement root)
    {
        public string? String(string name) => Str(root, name);

        public Guid Uuid(string name) => Id(root, name) ?? throw new FormatException($"{name} is required and must be a canonical UUID.");

        public Guid? OptionalUuid(string name) => Id(root, name);

        public bool Bool(string name)
        {
            if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) throw new FormatException($"{name} is required and must be true or false.");
            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new FormatException($"{name} must be true or false.")
            };
        }

        public IReadOnlyList<Guid> Uuids(string name)
        {
            if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return [];
            if (value.ValueKind != JsonValueKind.Array) throw new FormatException($"{name} must be an array of canonical UUIDs.");
            return value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String && CycleCountSupport.TryParseId(e.GetString(), out var id)
                ? id
                : throw new FormatException($"{name} must be an array of canonical UUIDs.")).ToList();
        }

        /// <summary>The Draft form (draft §4.1 create / update).</summary>
        public CycleCountFormInput Form() => new(Uuid("warehouseId"), String("countType") ?? throw new FormatException("countType is required."), Bool("blind"),
            Uuids("scopeLocationIds"), Uuids("scopeSkuIds"), String("plannedDate") ?? throw new FormatException("plannedDate is required."),
            OptionalUuid("assignedTo"), String("note"));

        /// <summary>bulkDeleteCycleCounts — <c>items: [{ countId, version }]</c>.</summary>
        public IReadOnlyList<CycleCountBulkItemInput> BulkItems()
        {
            if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) throw new FormatException("items is required and must be an array.");
            return items.EnumerateArray().Select(item =>
            {
                if (item.ValueKind != JsonValueKind.Object) throw new FormatException("Each item is an object { countId, version }.");
                foreach (var p in item.EnumerateObject())
                {
                    if (p.Name is not ("countId" or "version")) throw new FormatException($"Unknown member 'items.{p.Name}'.");
                }

                var id = Id(item, "countId") ?? throw new FormatException("items.countId is required and must be a canonical UUID.");
                if (!item.TryGetProperty("version", out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var version) || version < 1)
                    throw new FormatException("items.version is required and must be a positive integer.");
                return new CycleCountBulkItemInput(id, version);
            }).ToList();
        }
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

    private static string? Str(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : throw new FormatException($"{name} must be a string.");
    }

    private static Guid? Id(JsonElement element, string name)
    {
        var raw = Str(element, name);
        if (raw is null) return null;
        return CycleCountSupport.TryParseId(raw, out var id) ? id : throw new FormatException($"{name} must be a canonical UUID.");
    }

    // ── members (draft §4.1) ──
    public static readonly HashSet<string> FormMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "warehouseId", "countType", "blind", "scopeLocationIds", "scopeSkuIds", "plannedDate", "assignedTo", "note" };

    public static readonly HashSet<string> KeyMembers = new(StringComparer.Ordinal) { "idempotencyKey" };

    public static readonly HashSet<string> ReasonMembers = new(StringComparer.Ordinal) { "idempotencyKey", "reason" };

    public static readonly HashSet<string> BulkDeleteMembers = new(StringComparer.Ordinal) { "idempotencyKey", "reason", "items" };
}
