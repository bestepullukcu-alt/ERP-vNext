using System.Text.Json;
using Diten.InventoryService.Application.Features.Locations;
using Diten.InventoryService.Application.Features.Locations.Commands;

namespace Diten.InventoryService.Api.Contract;

/// <summary>
/// Reads the LOCATION v1.1 request bodies (MOD-0173-FU02 §15) from raw JSON so a wrongly typed or unknown member is a
/// contract 400, not a model-binding accident: every body is a JSON object; only the members of its schema are
/// accepted (a scope member — tenantId / legalEntityId — is refused with the MOD-0173 wording; <c>type</c> on a PATCH
/// is refused, ASSUMPTION-LOC-04); strings are strings, booleans booleans, integers integers. A PATCH keeps "absent"
/// apart from "null".
/// </summary>
public static class LocationRequestReader
{
    private static readonly string[] ScopeProperties = ["tenantId", "legalEntityId", "tenant_id", "legal_entity_id"];

    private static readonly HashSet<string> CreateMembers = new(StringComparer.Ordinal)
        { "idempotencyKey", "code", "name", "type", "parentId", "isStockable", "operatingMode", "orgUnitRef", "costCenterCode" };

    private static readonly HashSet<string> UpdateMembers = new(StringComparer.Ordinal)
        { "code", "name", "parentId", "isStockable", "operatingMode", "orgUnitRef", "costCenterCode" };

    private static readonly HashSet<string> StatusMembers = new(StringComparer.Ordinal) { "status" };

    private static readonly HashSet<string> BulkMembers = new(StringComparer.Ordinal)
    {
        "idempotencyKey", "aisleFrom", "aisleTo", "rackFrom", "rackTo", "levelFrom", "levelTo", "positionsPerLevel",
        "isStockable", "dryRun"
    };

    private static readonly HashSet<string> ValidateMembers = new(StringComparer.Ordinal) { "locationIds" };

    public static Task<(CreateLocationCommand? Command, string? Error)> ReadCreateAsync(Stream body, string? headerKey, CancellationToken ct)
        => ReadAsync(body, CreateMembers, ct, r => new CreateLocationCommand
        {
            HeaderIdempotencyKey = headerKey,
            IdempotencyKey = r.String("idempotencyKey"),
            Code = r.String("code"),
            Name = r.String("name"),
            Type = r.String("type"),
            ParentId = r.String("parentId"),
            IsStockable = r.Bool("isStockable"),
            OperatingMode = r.String("operatingMode"),
            OrgUnitRef = r.String("orgUnitRef"),
            CostCenterCode = r.String("costCenterCode")
        });

    public static Task<(UpdateLocationCommand? Command, string? Error)> ReadUpdateAsync(Stream body, string id, string? ifMatch, CancellationToken ct)
        => ReadAsync(body, UpdateMembers, ct, r => new UpdateLocationCommand
        {
            LocationId = id,
            IfMatch = ifMatch,
            Code = r.PatchString("code"),
            Name = r.PatchString("name"),
            ParentId = r.PatchString("parentId"),
            IsStockable = r.PatchBool("isStockable"),
            OperatingMode = r.PatchString("operatingMode"),
            OrgUnitRef = r.PatchString("orgUnitRef"),
            CostCenterCode = r.PatchString("costCenterCode")
        }, rejectTypeMember: true);

    public static Task<(ChangeLocationStatusCommand? Command, string? Error)> ReadStatusAsync(Stream body, string id, string? ifMatch, CancellationToken ct)
        => ReadAsync(body, StatusMembers, ct, r => new ChangeLocationStatusCommand(id, ifMatch, r.String("status")));

    public static Task<(BulkCreateBinsCommand? Command, string? Error)> ReadBulkAsync(Stream body, string id, string? headerKey, CancellationToken ct)
        => ReadAsync(body, BulkMembers, ct, r => new BulkCreateBinsCommand
        {
            LocationId = id,
            HeaderIdempotencyKey = headerKey,
            IdempotencyKey = r.String("idempotencyKey"),
            AisleFrom = r.String("aisleFrom"),
            AisleTo = r.String("aisleTo"),
            RackFrom = r.Int("rackFrom"),
            RackTo = r.Int("rackTo"),
            LevelFrom = r.Int("levelFrom"),
            LevelTo = r.Int("levelTo"),
            PositionsPerLevel = r.Int("positionsPerLevel"),
            IsStockable = r.Bool("isStockable"),
            DryRun = r.Bool("dryRun")
        });

    public static Task<(IReadOnlyList<string>? Ids, string? Error)> ReadValidateAsync(Stream body, CancellationToken ct)
        => ReadAsync(body, ValidateMembers, ct, r => r.StringArray("locationIds"));

    private static async Task<(T? Value, string? Error)> ReadAsync<T>(
        Stream body, HashSet<string> members, CancellationToken ct, Func<Reader, T?> build, bool rejectTypeMember = false)
        where T : class
    {
        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(body, cancellationToken: ct);
        }
        catch (JsonException)
        {
            return (null, "The request body must be a JSON object.");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (null, "The request body must be a JSON object.");
            }

            var names = root.EnumerateObject().Select(p => p.Name).ToList();
            if (names.Any(n => ScopeProperties.Contains(n, StringComparer.OrdinalIgnoreCase)))
            {
                return (null, "tenantId/legalEntityId must not be sent: scope is taken from the access token.");
            }

            if (rejectTypeMember && names.Contains("type", StringComparer.Ordinal))
            {
                return (null, "type cannot be changed: archive the location and create one of the new type.");
            }

            var unknown = names.Where(n => !members.Contains(n)).ToList();
            if (unknown.Count > 0)
            {
                return (null, $"Unknown member(s): {string.Join(", ", unknown)}.");
            }

            var reader = new Reader(root);
            var value = build(reader);
            return reader.Errors.Count > 0 ? (null, string.Join(" ", reader.Errors)) : (value, null);
        }
    }

    private sealed class Reader
    {
        private readonly JsonElement _root;

        public Reader(JsonElement root) => _root = root;

        public List<string> Errors { get; } = [];

        public string? String(string name)
            => Present(name, out var value) && value.ValueKind != JsonValueKind.Null ? AsString(name, value) : null;

        public bool? Bool(string name)
            => Present(name, out var value) && value.ValueKind != JsonValueKind.Null ? AsBool(name, value) : null;

        public int? Int(string name)
        {
            if (!Present(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
            Errors.Add($"{name} must be an integer.");
            return null;
        }

        public Patch<string> PatchString(string name)
            => Present(name, out var value)
                ? Patch<string>.Of(value.ValueKind == JsonValueKind.Null ? null : AsString(name, value))
                : Patch<string>.Absent;

        public Patch<bool?> PatchBool(string name)
            => Present(name, out var value)
                ? Patch<bool?>.Of(value.ValueKind == JsonValueKind.Null ? null : AsBool(name, value))
                : Patch<bool?>.Absent;

        public IReadOnlyList<string>? StringArray(string name)
        {
            if (!Present(name, out var value) || value.ValueKind != JsonValueKind.Array)
            {
                Errors.Add($"{name} is required and must be an array of strings.");
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

        private bool Present(string name, out JsonElement value) => _root.TryGetProperty(name, out value);

        private string? AsString(string name, JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.String) return value.GetString();
            Errors.Add($"{name} must be a string.");
            return null;
        }

        private bool? AsBool(string name, JsonElement value)
        {
            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.GetBoolean();
            Errors.Add($"{name} must be true or false.");
            return null;
        }
    }
}
