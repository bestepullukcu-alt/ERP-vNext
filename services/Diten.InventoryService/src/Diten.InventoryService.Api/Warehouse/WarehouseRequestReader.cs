using System.Text.Json;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Application.Features.WarehouseLocationRoles;
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
}
