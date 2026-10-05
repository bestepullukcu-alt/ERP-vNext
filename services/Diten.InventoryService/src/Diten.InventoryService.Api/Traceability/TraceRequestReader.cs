using System.Text.Json;
using Diten.InventoryService.Application.Features.Locations;
using Diten.InventoryService.Application.Features.Traceability;
using Diten.InventoryService.Application.Features.Traceability.Commands;
using Diten.InventoryService.Application.Features.Traceability.Queries;

namespace Diten.InventoryService.Api.Traceability;

/// <summary>
/// Reads the TRACE request bodies (v1 + v1.1) from raw JSON so a wrongly typed or unknown member is a contract 400, not a
/// model-binding accident (same reader rules as LOCATION, MOD-0173-FU02): every body is a JSON object; only the members
/// of its schema are accepted; a scope member (tenantId / legalEntityId) is refused; strings are strings, booleans
/// booleans. On <c>updateLot</c> a key member (lot number, item, SKU, status) is refused with
/// <c>LOT_KEY_IMMUTABLE</c> — the owner rule "Lot No and SKU are locked after save, Draft too" — and "absent" stays apart
/// from "null".
/// </summary>
public static class TraceRequestReader
{
    private static readonly string[] ScopeProperties = ["tenantId", "legalEntityId", "tenant_id", "legal_entity_id"];

    private static readonly HashSet<string> CreateMembers = new(StringComparer.Ordinal)
    {
        "idempotencyKey", "itemId", "lotNumber", "manufacturerLot", "supplierLot", "manufacturingDate", "expiryDate",
        "retestDate", "countryOfOrigin", "skuId", "skuLevel", "coaReference"
    };

    private static readonly HashSet<string> UpdateMembers = new(StringComparer.Ordinal)
        { "manufacturerLot", "supplierLot", "manufacturingDate", "expiryDate", "retestDate", "countryOfOrigin", "coaReference", "reason" };

    private static readonly HashSet<string> ImmutableMembers = new(StringComparer.Ordinal)
        { "lotNumber", "itemId", "skuId", "skuLevel", "status", "lotId" };

    private static readonly HashSet<string> StatusMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "toStatus", "reason", "acknowledgeImpact" };

    private static readonly HashSet<string> GenealogyMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "parentLotId", "childLotId", "transformationReference", "sourceModule", "quantityConsumed", "uomId" };

    private static readonly HashSet<string> ValidateMembers = new(StringComparer.Ordinal) { "lotIds", "serialIds" };

    private static readonly HashSet<string> Gs1Members = new(StringComparer.Ordinal) { "code", "skuId", "skuLevel", "lotId" };

    public sealed record ReadResult<T>(T? Value, string? ErrorCode, string? Error) where T : class;

    public static Task<ReadResult<CreateLotCommand>> ReadCreateAsync(Stream body, string? headerKey, CancellationToken ct)
        => ReadAsync(body, CreateMembers, null, ct, r => new CreateLotCommand
        {
            HeaderIdempotencyKey = headerKey,
            IdempotencyKey = r.String("idempotencyKey"),
            ItemId = r.String("itemId"),
            LotNumber = r.String("lotNumber"),
            ManufacturerLot = r.String("manufacturerLot"),
            SupplierLot = r.String("supplierLot"),
            ManufacturingDate = r.String("manufacturingDate"),
            ExpiryDate = r.String("expiryDate"),
            RetestDate = r.String("retestDate"),
            CountryOfOrigin = r.String("countryOfOrigin"),
            SkuId = r.String("skuId"),
            SkuLevel = r.String("skuLevel"),
            CoaReference = r.String("coaReference")
        });

    public static Task<ReadResult<UpdateLotCommand>> ReadUpdateAsync(
        Stream body, string lotId, string? ifMatch, bool canUpdateDraft, bool canUpdateReleased, CancellationToken ct)
        => ReadAsync(body, UpdateMembers, ImmutableMembers, ct, r => new UpdateLotCommand
        {
            LotId = lotId,
            IfMatch = ifMatch,
            CanUpdateDraft = canUpdateDraft,
            CanUpdateReleased = canUpdateReleased,
            ManufacturerLot = r.PatchString("manufacturerLot"),
            SupplierLot = r.PatchString("supplierLot"),
            ManufacturingDate = r.PatchString("manufacturingDate"),
            ExpiryDate = r.PatchString("expiryDate"),
            RetestDate = r.PatchString("retestDate"),
            CountryOfOrigin = r.PatchString("countryOfOrigin"),
            CoaReference = r.PatchString("coaReference"),
            Reason = r.String("reason")
        });

    public static Task<ReadResult<ChangeLotStatusCommand>> ReadStatusAsync(Stream body, string lotId, string? ifMatch, string? headerKey, CancellationToken ct)
        => ReadAsync(body, StatusMembers, null, ct, r => new ChangeLotStatusCommand
        {
            LotId = lotId,
            IfMatch = ifMatch,
            HeaderIdempotencyKey = headerKey,
            IdempotencyKey = r.String("idempotencyKey"),
            ToStatus = r.String("toStatus"),
            Reason = r.String("reason"),
            AcknowledgeImpact = r.Bool("acknowledgeImpact")
        });

    public static Task<ReadResult<LinkGenealogyCommand>> ReadGenealogyAsync(Stream body, string lotId, string? headerKey, CancellationToken ct)
        => ReadAsync(body, GenealogyMembers, null, ct, r => new LinkGenealogyCommand
        {
            LotId = lotId,
            HeaderIdempotencyKey = headerKey,
            IdempotencyKey = r.String("idempotencyKey"),
            ParentLotId = r.String("parentLotId"),
            ChildLotId = r.String("childLotId"),
            TransformationReference = r.String("transformationReference"),
            SourceModule = r.String("sourceModule"),
            QuantityConsumed = r.String("quantityConsumed"),
            UomId = r.String("uomId")
        });

    public static Task<ReadResult<ValidateTraceQuery>> ReadValidateAsync(Stream body, CancellationToken ct)
        => ReadAsync(body, ValidateMembers, null, ct, r =>
        {
            var lots = r.OptionalStringArray("lotIds");
            var serials = r.OptionalStringArray("serialIds");
            return lots is null || serials is null ? null : new ValidateTraceQuery(lots, serials);
        });

    public static Task<ReadResult<CheckGs1CodeQuery>> ReadGs1Async(Stream body, CancellationToken ct)
        => ReadAsync(body, Gs1Members, null, ct, r => new CheckGs1CodeQuery(r.String("code"), r.String("skuId"), r.String("skuLevel"), r.String("lotId")));

    private static async Task<ReadResult<T>> ReadAsync<T>(
        Stream body, HashSet<string> members, HashSet<string>? immutable, CancellationToken ct, Func<Reader, T?> build)
        where T : class
    {
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(body, cancellationToken: ct);
        }
        catch (JsonException)
        {
            return Invalid<T>("The request body must be a JSON object.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Invalid<T>("The request body must be a JSON object.");

            var names = root.EnumerateObject().Select(p => p.Name).ToList();
            if (names.Any(n => ScopeProperties.Contains(n, StringComparer.OrdinalIgnoreCase)))
                return Invalid<T>("tenantId/legalEntityId must not be sent: scope is taken from the access token.");

            if (immutable is not null && names.Where(immutable.Contains).ToList() is { Count: > 0 } locked)
            {
                return new ReadResult<T>(null, TraceErrorCodes.LotKeyImmutable,
                    $"{string.Join(", ", locked)} cannot be changed after the lot is saved (status changes go through POST …/status).");
            }

            var unknown = names.Where(n => !members.Contains(n)).ToList();
            if (unknown.Count > 0) return Invalid<T>($"Unknown member(s): {string.Join(", ", unknown)}.");

            var reader = new Reader(root);
            var value = build(reader);
            return reader.Errors.Count > 0 || value is null
                ? Invalid<T>(reader.Errors.Count > 0 ? string.Join(" ", reader.Errors) : "The request body is malformed.")
                : new ReadResult<T>(value, null, null);
        }
    }

    private static ReadResult<T> Invalid<T>(string message) where T : class
        => new(null, TraceErrorCodes.InvalidRequest, message);

    private sealed class Reader
    {
        private readonly JsonElement _root;

        public Reader(JsonElement root) => _root = root;

        public List<string> Errors { get; } = [];

        public string? String(string name)
            => _root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? AsString(name, value) : null;

        public bool? Bool(string name)
        {
            if (!_root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.GetBoolean();
            Errors.Add($"{name} must be true or false.");
            return null;
        }

        public Patch<string> PatchString(string name)
            => _root.TryGetProperty(name, out var value)
                ? Patch<string>.Of(value.ValueKind == JsonValueKind.Null ? null : AsString(name, value))
                : Patch<string>.Absent;

        /// <summary>Absent / null = empty list; otherwise an array of strings.</summary>
        public IReadOnlyList<string>? OptionalStringArray(string name)
        {
            if (!_root.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return [];
            if (value.ValueKind != JsonValueKind.Array)
            {
                Errors.Add($"{name} must be an array of strings.");
                return null;
            }

            var items = new List<string>();
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    Errors.Add($"{name} must contain strings only.");
                    return null;
                }

                items.Add(item.GetString()!);
            }

            return items;
        }

        private string? AsString(string name, JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.String) return value.GetString();
            Errors.Add($"{name} must be a string.");
            return null;
        }
    }
}
