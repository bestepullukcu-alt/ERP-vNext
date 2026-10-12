using System.Globalization;
using System.Text.Json.Serialization;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Application.Features.Locations;

/*
 * Wire models of LOCATION v1 (frozen reads) and v1.1 (owner operations, MOD-0173-FU02 §15). Unwrapped like the
 * INVENTORY-BUNDLE wire; camelCase; null members are omitted. No member exists without a counterpart in
 * location.openapi.yaml (the contract-validating test handler closes every schema). No quantity appears in any of
 * them (K-10). contractVersion stays "v1" — v1.1 is additive to the v1 major (ASSUMPTION-LOC1-07).
 */

/// <summary>Contract <c>LocationRef</c> (v1).</summary>
public class LocationRefDto
{
    public string LocationId { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? ParentId { get; init; }
    public string LegalEntityId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsStockable { get; init; }
    public string? OperatingMode { get; init; }
    public string? OrgUnitRef { get; init; }
    public string? CostCenterCode { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;

    /// <summary>Not on the wire body: the controller turns it into the <c>ETag: W/"n"</c> header.</summary>
    [JsonIgnore]
    public int? ETagVersion { get; init; }
}

/// <summary>Contract <c>LocationDetail</c> (v1.1) = <c>LocationRef</c> + version and who / when.</summary>
public sealed class LocationDetailDto : LocationRefDto
{
    public int Version { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}

/// <summary>listLocations 200 (v1).</summary>
public sealed class LocationListDto
{
    public IReadOnlyList<LocationRefDto> Items { get; init; } = [];
    public int Total { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

/// <summary>getLocationPath 200 (v1).</summary>
public sealed class LocationPathDto
{
    public string LocationId { get; init; } = string.Empty;
    public IReadOnlyList<LocationPathEntryDto> Path { get; init; } = [];
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

public sealed class LocationPathEntryDto
{
    public string Type { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
}

/// <summary>validateLocations 200 (v1). <c>isStockable</c> is omitted for a location that does not exist.</summary>
public sealed class LocationValidationResultDto
{
    public IReadOnlyList<LocationValidationEntryDto> Results { get; init; } = [];
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

public sealed class LocationValidationEntryDto
{
    public string LocationId { get; init; } = string.Empty;
    public bool Exists { get; init; }
    public bool? IsStockable { get; init; }
}

/// <summary>searchLocations 200 (v1.1).</summary>
public sealed class LocationSearchPageDto
{
    public IReadOnlyList<LocationRefDto> Items { get; init; } = [];
    public long Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

/// <summary>getLocationHistory 200 (v1.1), newest first.</summary>
public sealed class LocationHistoryPageDto
{
    public IReadOnlyList<LocationChangeDto> Items { get; init; } = [];
    public long Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

/// <summary>Contract <c>LocationChange</c> (v1.1).</summary>
public sealed class LocationChangeDto
{
    public string ChangeId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Field { get; init; }
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
    public string OccurredAt { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
}

/// <summary>getLocationStockPresence 200 (v1.1) — booleans only, never a quantity (K-10).</summary>
public sealed class LocationStockPresenceDto
{
    public string LocationId { get; init; } = string.Empty;
    public bool HasStock { get; init; }
    public bool SubtreeHasStock { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

/// <summary>
/// bulkCreateBins (v1.1): a dry run answers <c>toCreate</c>; a real run / replay answers <c>created</c>. Exactly one
/// of the two is on the wire.
/// </summary>
public sealed class BulkBinsResultDto
{
    public IReadOnlyList<string>? ToCreate { get; init; }
    public IReadOnlyList<LocationRefDto>? Created { get; init; }
    public IReadOnlyList<string> Skipped { get; init; } = [];
    public int Count { get; init; }
    public string ContractVersion { get; init; } = InventoryContract.Version;
}

/// <summary>A JSON member of a PATCH body: absent (unchanged) or present with a value, null included.</summary>
public readonly record struct Patch<T>(bool IsPresent, T? Value)
{
    public static Patch<T> Absent => default;

    public static Patch<T> Of(T? value) => new(true, value);
}

public static class LocationMapping
{
    public static LocationRefDto ToRef(Location l, bool withETag = false) => new()
    {
        LocationId = Id(l.Id),
        Code = l.Code,
        Name = l.Name,
        Type = l.Type.ToString(),
        ParentId = l.ParentId.HasValue ? Id(l.ParentId.Value) : null,
        LegalEntityId = l.LegalEntityId.HasValue ? Id(l.LegalEntityId.Value) : string.Empty,
        Status = l.Status.ToString(),
        IsStockable = l.IsStockable,
        OperatingMode = l.OperatingMode,
        OrgUnitRef = l.OrgUnitRef,
        CostCenterCode = l.CostCenterCode,
        ETagVersion = withETag ? l.Version : null
    };

    public static LocationDetailDto ToDetail(Location l) => new()
    {
        LocationId = Id(l.Id),
        Code = l.Code,
        Name = l.Name,
        Type = l.Type.ToString(),
        ParentId = l.ParentId.HasValue ? Id(l.ParentId.Value) : null,
        LegalEntityId = l.LegalEntityId.HasValue ? Id(l.LegalEntityId.Value) : string.Empty,
        Status = l.Status.ToString(),
        IsStockable = l.IsStockable,
        OperatingMode = l.OperatingMode,
        OrgUnitRef = l.OrgUnitRef,
        CostCenterCode = l.CostCenterCode,
        ETagVersion = l.Version,
        Version = l.Version,
        CreatedAt = Utc(l.CreatedAt),
        CreatedBy = Id(l.CreatedBy),
        UpdatedAt = l.UpdatedAt.HasValue ? Utc(l.UpdatedAt.Value) : null,
        UpdatedBy = l.UpdatedBy.HasValue ? Id(l.UpdatedBy.Value) : null
    };

    public static LocationChangeDto ToChange(LocationChangeEntry e) => new()
    {
        ChangeId = Id(e.Id),
        Action = e.Action.ToString(),
        Field = e.Field,
        OldValue = e.OldValue,
        NewValue = e.NewValue,
        ActorUserId = Id(e.ActorUserId),
        OccurredAt = Utc(e.OccurredAt),
        CorrelationId = Id(e.CorrelationId)
    };

    public static string Id(Guid id) => id.ToString("D");

    public static string Utc(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

    /// <summary><c>W/"n"</c>.</summary>
    public static string ETag(int version) => string.Create(CultureInfo.InvariantCulture, $"W/\"{version}\"");
}
