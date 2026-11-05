using System.Text.Json;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Application.Features.OutboundShipments;
using Diten.InventoryService.Application.Features.PutawayTasks;
using Diten.InventoryService.Application.Features.WarehouseLocationRoles;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Enums;

namespace Diten.InventoryService.Api.Warehouse;

/// <summary>
/// Reads the WMS-BUNDLE request bodies from raw JSON (the RECALL / FEFO reader rules, own copy so the modules stay apart): the body is a JSON
/// object; only the members of its schema are accepted; a scope member (<c>tenantId</c> / <c>legalEntityId</c>) is refused (pack §4: 400);
/// strings are strings; ids are canonical UUIDs; a role is its exact name; a storage-condition code is any string (whether it is one of the
/// contract's codes is the handler's rule — 400 <c>WMS_STORAGE_CONDITION_INVALID</c>). A violation is a contract 400 <c>INVALID_REQUEST</c>
/// before any handler runs.
/// </summary>
public static class WarehouseRequestReader
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

        public bool Has(string name) => Root.TryGetProperty(name, out _);

        public string? String(string name) => Str(Root, name);

        public Guid Uuid(string name) => Id(Root, name) ?? throw new FormatException($"{name} is required and must be a canonical UUID.");

        public Guid? OptionalUuid(string name) => Id(Root, name);

        /// <summary>An enum member by its exact name; absent / null → <paramref name="fallback"/>.</summary>
        public T EnumOr<T>(string name, T fallback) where T : struct, System.Enum => OptionalEnum<T>(name) ?? fallback;

        public T? OptionalEnum<T>(string name) where T : struct, System.Enum
        {
            var raw = Str(Root, name);
            if (raw is null) return null;
            return System.Enum.TryParse<T>(raw, false, out var value) && System.Enum.IsDefined(value) && raw == value.ToString()
                ? value
                : throw new FormatException($"{name} must be one of {string.Join(", ", System.Enum.GetNames<T>())}.");
        }

        /// <summary>createPutawayTask — the source bucket: <c>candidateKey</c> or (warehouseId, fromLocationId, item, lotId?, stockStatus).</summary>
        public TaskSourceInput Source()
        {
            if (String("candidateKey") is { } key)
            {
                if (Has("warehouseId") || Has("fromLocationId") || Has("item") || Has("lotId") || Has("stockStatus"))
                    throw new FormatException("Send either candidateKey or the source members, not both.");
                var parts = key.Split('|');
                if (parts.Length != 6 || !WarehouseSupport.TryParseId(parts[0], out var w) || !WarehouseSupport.TryParseId(parts[1], out var l)
                    || !WarehouseSupport.TryParseId(parts[2], out var sku) || !WireNames.TryParseSkuLevel(parts[3], out var level)
                    || !WireNames.TryParseStockStatus(parts[5], out var status))
                    throw new FormatException("candidateKey is not a putaway candidate key.");
                return new TaskSourceInput(w, l, null, sku, level, parts[4].Length == 0 ? null : parts[4], status);
            }

            if (!Root.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object) throw new FormatException("item is required and must be an object.");
            foreach (var p in item.EnumerateObject())
            {
                if (p.Name is not ("itemId" or "skuId" or "skuLevel")) throw new FormatException($"Unknown member 'item.{p.Name}'.");
            }

            var levelText = Str(item, "skuLevel");
            if (!WireNames.TryParseSkuLevel(levelText, out var skuLevel)) throw new FormatException("item.skuLevel must be Gsku, Lsku, FinishedGood or Item.");
            var statusText = String("stockStatus") ?? throw new FormatException("stockStatus is required.");
            if (!WireNames.TryParseStockStatus(statusText, out var stockStatus)) throw new FormatException("stockStatus must be a stock status (AVAILABLE, QUARANTINE …).");
            return new TaskSourceInput(Uuid("warehouseId"), Uuid("fromLocationId"),
                Id(item, "itemId") ?? throw new FormatException("item.itemId is required and must be a canonical UUID."),
                Id(item, "skuId") ?? throw new FormatException("item.skuId is required and must be a canonical UUID."), skuLevel, String("lotId"), stockStatus);
        }

        /// <summary>A required array of role names (an empty array is allowed).</summary>
        public IReadOnlyList<WarehouseRole> Roles(string name)
            => Array(Root, name).Select(e => e.ValueKind == JsonValueKind.String && Enum.TryParse<WarehouseRole>(e.GetString(), false, out var role)
                                                && Enum.IsDefined(role) && e.GetString() == role.ToString()
                ? role
                : throw new FormatException($"{name}[] entries must be one of {string.Join(", ", Enum.GetNames<WarehouseRole>())}.")).ToList();

        /// <summary>A required array of strings (an empty array is allowed).</summary>
        public IReadOnlyList<string> Strings(string name)
            => Array(Root, name).Select(e => e.ValueKind == JsonValueKind.String && e.GetString() is { } s
                ? s
                : throw new FormatException($"{name}[] entries must be strings.")).ToList();

        /// <summary>bulkDeleteLocationRoles — <c>items: [{ roleId, version }]</c>.</summary>
        public IReadOnlyList<BulkDeleteLocationRoleItem> BulkItems(string name)
            => Array(Root, name).Select(e =>
            {
                if (e.ValueKind != JsonValueKind.Object) throw new FormatException($"{name}[] entries must be objects.");
                foreach (var p in e.EnumerateObject())
                {
                    if (p.Name is not ("roleId" or "version")) throw new FormatException($"Unknown member '{name}[].{p.Name}'.");
                }

                var version = e.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) && n >= 1
                    ? n
                    : throw new FormatException($"{name}[].version is required and must be an integer ≥ 1.");
                return new BulkDeleteLocationRoleItem(Id(e, "roleId") ?? throw new FormatException($"{name}[].roleId is required and must be a canonical UUID."), version);
            }).ToList();

        /// <summary>bulkAssignPutawayTasks — <c>items: [{ taskId, version }]</c>.</summary>
        public IReadOnlyList<BulkAssignItem> AssignItems(string name)
            => Array(Root, name).Select(e =>
            {
                if (e.ValueKind != JsonValueKind.Object) throw new FormatException($"{name}[] entries must be objects.");
                foreach (var p in e.EnumerateObject())
                {
                    if (p.Name is not ("taskId" or "version")) throw new FormatException($"Unknown member '{name}[].{p.Name}'.");
                }

                var version = e.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) && n >= 1
                    ? n
                    : throw new FormatException($"{name}[].version is required and must be an integer ≥ 1.");
                return new BulkAssignItem(Id(e, "taskId") ?? throw new FormatException($"{name}[].taskId is required and must be a canonical UUID."), version);
            }).ToList();

        /// <summary>
        /// S2 <c>createOutboundShipment</c> / <c>previewOutboundAllocation</c> (WMS draft §11): <c>warehouseId</c>, <c>stagingLocationId?</c>,
        /// <c>orderRef?</c>, <c>shipTo</c> (frozen <c>Address</c> members), <c>note?</c>, <c>lines: [{ orderLineId, item { itemId, skuId?, skuLevel },
        /// uomId, quantity, lotOverride? { lotId, fefoBypassReason? } }]</c> — closed objects, decimal strings.
        /// </summary>
        public CreateOutboundShipmentInput Outbound()
        {
            var lines = Array(Root, "lines").Select(e =>
            {
                if (e.ValueKind != JsonValueKind.Object) throw new FormatException("lines[] entries must be objects.");
                Closed(e, "lines[]", "orderLineId", "item", "uomId", "quantity", "lotOverride");
                if (!e.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object) throw new FormatException("lines[].item is required and must be an object.");
                Closed(item, "lines[].item", "itemId", "skuId", "skuLevel");
                if (!WireNames.TryParseSkuLevel(Str(item, "skuLevel"), out var level)) throw new FormatException("lines[].item.skuLevel must be Gsku, Lsku, FinishedGood or Item.");
                OutboundLotOverrideInput? lot = null;
                if (e.TryGetProperty("lotOverride", out var o) && o.ValueKind != JsonValueKind.Null)
                {
                    if (o.ValueKind != JsonValueKind.Object) throw new FormatException("lines[].lotOverride must be an object.");
                    Closed(o, "lines[].lotOverride", "lotId", "fefoBypassReason");
                    lot = new OutboundLotOverrideInput(Str(o, "lotId"), Str(o, "fefoBypassReason"));
                }

                return new OutboundLineInput(Str(e, "orderLineId"),
                    Id(item, "itemId") ?? throw new FormatException("lines[].item.itemId is required and must be a canonical UUID."),
                    Id(item, "skuId"), level, Str(e, "uomId"), Str(e, "quantity"), lot);
            }).ToList();
            return new CreateOutboundShipmentInput(Uuid("warehouseId"), OptionalUuid("stagingLocationId"), String("orderRef"), Address("shipTo"), String("note"), lines);
        }

        /// <summary>S2 <c>updateOutboundShipment</c> — header members only; each one's presence is kept (absent = unchanged).</summary>
        public UpdateOutboundShipmentInput OutboundChange()
            => new(Has("orderRef"), String("orderRef"), Has("shipTo"), Address("shipTo"), Has("note"), String("note"));

        private OutboundAddressInput? Address(string name)
        {
            if (!Root.TryGetProperty(name, out var a) || a.ValueKind == JsonValueKind.Null) return null;
            if (a.ValueKind != JsonValueKind.Object) throw new FormatException($"{name} must be an object.");
            Closed(a, name, "name", "country", "city", "postalCode", "line1", "line2");
            return new OutboundAddressInput(Str(a, "name"), Str(a, "country"), Str(a, "city"), Str(a, "postalCode"), Str(a, "line1"), Str(a, "line2"));
        }

        private static void Closed(JsonElement e, string path, params string[] members)
        {
            foreach (var p in e.EnumerateObject())
            {
                if (!members.Contains(p.Name, StringComparer.Ordinal)) throw new FormatException($"Unknown member '{path}.{p.Name}'.");
            }
        }

        private static IEnumerable<JsonElement> Array(JsonElement e, string name)
            => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
                ? v.EnumerateArray().ToList()
                : throw new FormatException($"{name} is required and must be an array.");

        private static string? Str(JsonElement e, string name)
        {
            if (!e.TryGetProperty(name, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            return v.ValueKind == JsonValueKind.String ? v.GetString() : throw new FormatException($"{name} must be a string.");
        }

        private static Guid? Id(JsonElement e, string name)
        {
            var raw = Str(e, name);
            if (raw is null) return null;
            return WarehouseSupport.TryParseId(raw, out var id) ? id : throw new FormatException($"{name} must be a canonical UUID.");
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

    public static readonly HashSet<string> CreateRoleMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "locationId", "roles", "acceptedStorageConditions", "note" };

    /// <summary>updateLocationRole — the node (<c>locationId</c>) is not a member: it never changes.</summary>
    public static readonly HashSet<string> UpdateRoleMembers = new(StringComparer.Ordinal) { "idempotencyKey", "roles", "acceptedStorageConditions", "note" };

    public static readonly HashSet<string> BulkDeleteMembers = new(StringComparer.Ordinal) { "idempotencyKey", "items" };

    // ── putaway tasks (WMS draft §4) ──
    public static readonly HashSet<string> CreateTaskMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "candidateKey", "warehouseId", "fromLocationId", "item", "lotId", "stockStatus", "plannedQuantity", "toLocationId", "targetSource",
          "assigneeUserId", "priority", "note" };

    /// <summary>updatePutawayTask — the source bucket is not a member: it never changes (§8.15, 400).</summary>
    public static readonly HashSet<string> UpdateTaskMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "plannedQuantity", "toLocationId", "targetSource", "assigneeUserId", "priority", "note" };

    /// <summary>bulkAssignPutawayTasks (Q-13 A).</summary>
    public static readonly HashSet<string> BulkAssignMembers = new(StringComparer.Ordinal) { "idempotencyKey", "assigneeUserId", "items" };

    public static readonly HashSet<string> ConfirmMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "quantity", "toLocationId", "deviationReason", "channel", "scanRef" };

    public static readonly HashSet<string> KeyMembers = new(StringComparer.Ordinal) { "idempotencyKey" };

    // ── S2 outbound (WMS draft §11) ──
    public static readonly HashSet<string> CreateOutboundMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "warehouseId", "stagingLocationId", "orderRef", "shipTo", "note", "lines" };

    /// <summary>previewOutboundAllocation — the create body without a key (a preview writes nothing).</summary>
    public static readonly HashSet<string> PreviewOutboundMembers = new(StringComparer.Ordinal)
        { "warehouseId", "stagingLocationId", "orderRef", "shipTo", "note", "lines" };

    /// <summary>updateOutboundShipment — the header only; the lines / allocation are not members (400).</summary>
    public static readonly HashSet<string> UpdateOutboundMembers = new(StringComparer.Ordinal) { "idempotencyKey", "orderRef", "shipTo", "note" };

    public static readonly HashSet<string> ReasonMembers = new(StringComparer.Ordinal) { "idempotencyKey", "reason" };
}
