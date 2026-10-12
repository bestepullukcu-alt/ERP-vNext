using System.Text.Json;

namespace Diten.Web.Models.Inventory;

// ── LOCATION v1 / v1.1 wire shapes (location.openapi.yaml 1.1.0) — read as the Gateway returns them ─────────────

public sealed class LocationRefDto
{
    public string? LocationId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? ParentId { get; set; }
    public string? LegalEntityId { get; set; }
    public string? Status { get; set; }
    public bool? IsStockable { get; set; }
    public string? OperatingMode { get; set; }
    public string? OrgUnitRef { get; set; }
    public string? CostCenterCode { get; set; }
}

public sealed class LocationSearchPageDto
{
    public List<LocationRefDto>? Items { get; set; }
    public int? Total { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public string? ContractVersion { get; set; }
}

public sealed class LocationPathStepDto
{
    public string? Type { get; set; }
    public string? Id { get; set; }
    public string? Code { get; set; }
}

public sealed class LocationPathDto
{
    public string? LocationId { get; set; }
    public List<LocationPathStepDto>? Path { get; set; }
}

public sealed class LocationChangeDto
{
    public string? ChangeId { get; set; }
    public string? Action { get; set; }
    public string? Field { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? ActorUserId { get; set; }
    public string? OccurredAt { get; set; }
    public string? CorrelationId { get; set; }
}

public sealed class LocationHistoryPageDto
{
    public List<LocationChangeDto>? Items { get; set; }
    public int? Total { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}

public sealed class LocationStockPresenceDto
{
    public string? LocationId { get; set; }
    public bool? HasStock { get; set; }
    public bool? SubtreeHasStock { get; set; }
}

public sealed class BulkBinsPreviewDto
{
    public List<string>? ToCreate { get; set; }
    public List<string>? Skipped { get; set; }
    public int? Count { get; set; }
}

public sealed class BulkBinsResultDto
{
    public List<LocationRefDto>? Created { get; set; }
    public List<string>? Skipped { get; set; }
    public int? Count { get; set; }
}

// ── what the screens draw ────────────────────────────────────────────────────────────────────────────────────

/// <summary>One L-2 row. No quantity anywhere on a location screen (K-10).</summary>
public sealed class LocationRowViewModel
{
    public string? Id { get; init; }
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? Type { get; init; }
    public string? ParentId { get; init; }
    public string? ParentPath { get; init; }
    public string? Status { get; init; }
    public bool? IsStockable { get; init; }
    public string? OperatingMode { get; init; }
}

/// <summary>One tree node (L-1). Children are loaded on expand; a Bin never has any (K-01).</summary>
public sealed class LocationTreeNodeViewModel
{
    public string? Id { get; init; }
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? Type { get; init; }
    public string? ParentId { get; init; }
    public string? Status { get; init; }
    public bool? IsStockable { get; init; }
    public bool CanHaveChildren => Type is not null && Type != "Bin";
}

/// <summary>One org-unit option (Platform OrganizationUnit, same company, Active, not archived — pack §7, LOC-11).</summary>
public sealed record OrgUnitOption(string Value, string Text);

// ── what the browser posts (same-origin, anti-forgery) ────────────────────────────────────────────────────────

/// <summary>L-3 create. The company is not a field (K-03/K-14): the service takes the scope company.</summary>
public sealed class LocationCreateRequestViewModel
{
    public string? IdempotencyKey { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? ParentId { get; set; }
    public bool? IsStockable { get; set; }
    public string? OperatingMode { get; set; }
    public string? OrgUnitRef { get; set; }
    public string? CostCenterCode { get; set; }
}

/// <summary>
/// L-3 edit: the members the reader CHANGED (absent = unchanged, null = cleared — LOCATION v1.1 PATCH semantics),
/// so the body is kept as JSON and only the contract's members are forwarded.
/// </summary>
public sealed class LocationEditRequestViewModel
{
    public string? IfMatch { get; set; }
    public JsonElement Changes { get; set; }
}

public sealed class LocationStatusRequestViewModel
{
    public string? Status { get; set; }
    public string? IfMatch { get; set; }
}

/// <summary>L-4 bulk bins (K-12). One key per opened dialog: the preview does not consume it, the real run does.</summary>
public sealed class BulkBinsRequestViewModel
{
    public string? IdempotencyKey { get; set; }
    public string? AisleFrom { get; set; }
    public string? AisleTo { get; set; }
    public int? RackFrom { get; set; }
    public int? RackTo { get; set; }
    public int? LevelFrom { get; set; }
    public int? LevelTo { get; set; }
    public int? PositionsPerLevel { get; set; }
    public bool? IsStockable { get; set; }
    public bool DryRun { get; set; }
}
