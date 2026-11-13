using System.Text.Json;
using Diten.InventoryService.Application.Features.WavePlanning.Services;
using Diten.InventoryService.Application.Features.Waves;

namespace Diten.InventoryService.Api.WavePlanning;

/// <summary>
/// Reads the WMS-WAVE-BUNDLE request bodies from raw JSON (the MOD-0181 <c>CycleCountRequestReader</c> rules, own copy so the modules stay
/// apart): the body is a JSON object; only the members of its schema are accepted (a server member such as <c>number</c>, <c>status</c>,
/// <c>cutoffAtUtc</c>, <c>gateSeq</c> is an unknown member → 400, AC-10); a scope member (<c>tenantId</c> / <c>legalEntityId</c>) is refused
/// (pack §4, AC-4); strings are strings; ids are canonical UUIDs; a JSON null is the member's absence (draft §1 rule 4). A violation is a
/// contract 400 <c>INVALID_REQUEST</c> before any handler runs.
/// </summary>
public static class WaveRequestReader
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

        public int? OptionalInt(string name)
        {
            if (!root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
            return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : throw new FormatException($"{name} must be a whole number.");
        }

        public int Int(string name) => OptionalInt(name) ?? throw new FormatException($"{name} is required and must be a whole number.");

        public IReadOnlyList<Guid> Uuids(string name)
        {
            if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) throw new FormatException($"{name} is required and must be an array of canonical UUIDs.");
            return value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String && WaveSupport.TryParseId(e.GetString(), out var id)
                ? id
                : throw new FormatException($"{name} must be an array of canonical UUIDs.")).ToList();
        }

        /// <summary>The Draft form (draft §4.1 create / update).</summary>
        public WaveFormInput Form() => new(Uuid("warehouseId"), String("waveType") ?? throw new FormatException("waveType is required."),
            String("cutoffLocal") ?? throw new FormatException("cutoffLocal is required."), String("shipToFilter"), OptionalInt("maxOrders"), OptionalInt("maxLines"),
            OptionalUuid("responsibleUserId"), String("note"));

        /// <summary>The warehouse setting (draft §4.1 <c>updateWaveWarehouseSettings</c>): the whole setting, every threshold named.</summary>
        public WaveSettingsInput Settings() => new(Bool("waveManaged"), String("timeZoneId"), Int("riskMinutesBeforeCutoff"), Int("riskProgressPercent"),
            Int("pendingTaskMinutes"));
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
        return WaveSupport.TryParseId(raw, out var id) ? id : throw new FormatException($"{name} must be a canonical UUID.");
    }

    // ── members (draft §4.1) ──
    public static readonly HashSet<string> CreateMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "warehouseId", "waveType", "cutoffLocal", "shipToFilter", "maxOrders", "maxLines", "responsibleUserId", "note" };

    public static readonly HashSet<string> UpdateMembers = new(CreateMembers, StringComparer.Ordinal) { "reason" };

    public static readonly HashSet<string> AddOrdersMembers = new(StringComparer.Ordinal) { "idempotencyKey", "outboundIds", "via", "reason" };

    public static readonly HashSet<string> RemoveOrdersMembers = new(StringComparer.Ordinal) { "idempotencyKey", "outboundIds", "reason" };

    public static readonly HashSet<string> ReasonMembers = new(StringComparer.Ordinal) { "idempotencyKey", "reason" };

    public static readonly HashSet<string> SettingsMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "waveManaged", "timeZoneId", "riskMinutesBeforeCutoff", "riskProgressPercent", "pendingTaskMinutes", "reason" };
}
