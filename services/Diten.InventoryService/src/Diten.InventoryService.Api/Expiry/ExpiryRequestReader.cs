using System.Text.Json;
using Diten.InventoryService.Application.Features.Expiry.Services;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Api.Expiry;

/// <summary>
/// Reads the FEFO-BUNDLE request bodies from raw JSON (the QUARANTINE / TRACE reader rules, own copy so the modules stay apart):
/// the body is a JSON object; only the members of its schema are accepted; a scope member (<c>tenantId</c> / <c>legalEntityId</c>) is
/// refused; strings are strings, numbers numbers; ids are canonical UUIDs; enums are their exact names. A violation is a contract
/// 400 <c>INVALID_REQUEST</c> before any handler runs.
/// </summary>
public static class ExpiryRequestReader
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

        public string? String(string name)
        {
            if (!Root.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            return v.ValueKind == JsonValueKind.String ? v.GetString() : throw new FormatException($"{name} must be a string.");
        }

        public int Int(string name)
            => Root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)
                ? i
                : throw new FormatException($"{name} is required and must be an integer.");

        public decimal? Decimal(string name)
        {
            if (!Root.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            return v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) && InventoryQuantity.Scale(d) <= InventoryQuantity.MaxFractionDigits
                ? d
                : throw new FormatException($"{name} must be a number with at most {InventoryQuantity.MaxFractionDigits} fraction digits.");
        }

        public Guid Uuid(string name)
            => ExpirySupport.TryParseId(String(name), out var id) ? id : throw new FormatException($"{name} must be a canonical UUID.");

        public Guid? OptionalUuid(string name) => String(name) is null ? null : Uuid(name);

        public T Enum<T>(string name) where T : struct, Enum
            => ExpiryWire.TryParseName<T>(String(name), out var value)
                ? value
                : throw new FormatException($"{name} must be one of {string.Join(", ", System.Enum.GetNames<T>())}.");

        public DateOnly? Date(string name)
        {
            var raw = String(name);
            if (raw is null) return null;
            return DateOnly.TryParseExact(raw, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var day)
                ? day
                : throw new FormatException($"{name} must be a date yyyy-MM-dd.");
        }

        public Domain.Enums.SkuLevel SkuLevel(string name)
            => WireNames.TryParseSkuLevel(String(name), out var level)
                ? level
                : throw new FormatException($"{name} must be Gsku, Lsku, FinishedGood or Item.");

        public Domain.Expiry.Enums.ExpiryExceptionType ExceptionType(string name)
            => ExpiryWire.TryParseType(String(name), out var type)
                ? type
                : throw new FormatException($"{name} must be one of {string.Join(", ", ExpiryWire.AllTypeCodes)}.");

        /// <summary>An object of integers with exactly the four exception type codes.</summary>
        public IReadOnlyDictionary<string, int> SlaHours(string name)
        {
            if (!Root.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Object) throw new FormatException($"{name} is required and must be an object.");
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var p in v.EnumerateObject())
            {
                if (!ExpiryWire.AllTypeCodes.Contains(p.Name)) throw new FormatException($"{name}.{p.Name} is not an exception type.");
                map[p.Name] = p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out var h) ? h : throw new FormatException($"{name}.{p.Name} must be an integer.");
            }

            return map;
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

    public static readonly HashSet<string> PolicyMembers = new(StringComparer.Ordinal)
    {
        "approachingDays", "criticalDays", "fefoOutboundMode", "belowMinRemainingMode", "dateOverrideMode", "slaHours", "expiredOnHandThresholdDays",
        "reason", "idempotencyKey"
    };

    public static readonly HashSet<string> OpenMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "type", "lotId", "warehouseId", "quantity", "description" };

    public static readonly HashSet<string> KeyMembers = new(StringComparer.Ordinal) { "idempotencyKey" };

    public static readonly HashSet<string> CommentMembers = new(StringComparer.Ordinal) { "idempotencyKey", "text" };

    public static readonly HashSet<string> PrepareMembers = new(StringComparer.Ordinal) { "idempotencyKey", "resolution", "reason" };

    public static readonly HashSet<string> ResolveMembers = new(StringComparer.Ordinal) { "idempotencyKey", "resolution", "reason", "signatureId" };

    public static readonly HashSet<string> CloseMembers = new(StringComparer.Ordinal) { "idempotencyKey", "note" };

    public static readonly HashSet<string> DateOverrideMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "itemId", "skuId", "skuLevel", "lotNumber", "manufacturingDate", "expiryDate", "retestDate", "reason" };
}
