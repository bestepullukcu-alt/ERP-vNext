using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.Territory.ResourceAssignments;

/// <summary>
/// Metadata keys FU04 reads off the published MOD-0048 values. They are declared here (not the VALUES) so the
/// vocabulary stays the single source of truth: the code asks "does this coverage scope require a territory id?"
/// rather than hardcoding which scopes do.
/// </summary>
public static class TerritoryAssignmentMetadataKeys
{
    // territory-coverage-scope
    public const string RequiresTerritoryId = "requiresTerritoryId";
    public const string AllowsTerritoryId = "allowsTerritoryId";
    public const string RequiresBusinessScope = "requiresBusinessScope";
    public const string AllowsBusinessScope = "allowsBusinessScope";

    // territory-resource-role
    public const string DefaultCoverageScope = "defaultCoverageScope";
    public const string CanBePrimary = "canBePrimary";
    public const string IsSalesRole = "isSalesRole";
    public const string IsManagementRole = "isManagementRole";

    // territory-assignment-source
    public const string RequiresReason = "requiresReason";

    // territory-assignment-status
    public const string IsActiveLike = "isActiveLike";
    public const string AllowsMutation = "allowsMutation";
    public const string IsHistorical = "isHistorical";
}

public sealed record TerritoryResourceRefInput(
    string ResourceId,
    string? ResourceType,
    string DisplayName,
    string? Email);

public sealed record CreateTerritoryResourceAssignmentCommand(
    Guid ModelId,
    Guid? TerritoryId,
    TerritoryResourceRefInput? Resource,
    Guid? PositionId,
    string PositionCode,
    string? PositionName,
    string? CoverageScope,
    IReadOnlyList<string>? BusinessUnitScopeCodes,
    bool IsPrimary,
    string? AssignmentSource,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string? ChangeReason,
    string? CorrelationId,
    string? PositionType = null,
    string? PositionSourceSystem = null) : IRequest<Response<Guid>>;

public sealed record UpdateTerritoryResourceAssignmentCommand(
    Guid ModelId,
    Guid AssignmentId,
    Guid? TerritoryId,
    TerritoryResourceRefInput? Resource,
    Guid? PositionId,
    string PositionCode,
    string? PositionName,
    string? CoverageScope,
    IReadOnlyList<string>? BusinessUnitScopeCodes,
    bool IsPrimary,
    string? AssignmentSource,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string? ChangeReason,
    string? CorrelationId,
    string? PositionType = null,
    string? PositionSourceSystem = null) : IRequest<Response<bool>>;

/// <summary>Soft-deletes a not-yet-effective (<c>proposed</c>) assignment. An assignment that is already active must
/// be ENDED instead — see <see cref="EndTerritoryResourceAssignmentCommand"/> (pack §10: terminating is not deleting).</summary>
public sealed record SoftDeleteTerritoryResourceAssignmentCommand(
    Guid ModelId,
    Guid AssignmentId,
    string? Reason,
    string? CorrelationId) : IRequest<Response<bool>>;

/// <summary>Terminates an assignment: <c>Status=ended</c> + <c>ValidTo</c>. History is preserved; nothing is deleted.</summary>
public sealed record EndTerritoryResourceAssignmentCommand(
    Guid ModelId,
    Guid AssignmentId,
    DateTimeOffset? EndDate,
    string? Reason,
    string? CorrelationId) : IRequest<Response<bool>>;

public sealed record ReplaceTerritoryResourceAssignmentCommand(
    Guid ModelId,
    Guid AssignmentId,
    TerritoryResourceRefInput? Resource,
    Guid? PositionId,
    string PositionCode,
    string? PositionTitle,
    string? PositionType,
    string? PositionSourceSystem,
    DateTimeOffset EffectiveDate,
    string? Reason,
    string? CorrelationId) : IRequest<Response<Guid>>;

public sealed record TransferTerritoryResourceAssignmentCommand(
    Guid ModelId,
    Guid AssignmentId,
    Guid? TargetTerritoryId,
    string? CoverageScope,
    IReadOnlyList<string>? BusinessUnitScopeCodes,
    DateTimeOffset EffectiveDate,
    string? Reason,
    string? CorrelationId) : IRequest<Response<Guid>>;

/// <summary>Read-only conflict/warning report for a model's resource assignments (no mutation).</summary>
public sealed record ValidateTerritoryResourceConflictsCommand(Guid ModelId)
    : IRequest<Response<TerritoryResourceConflictReportDto>>;

public sealed record GetTerritoryResourceAssignmentListQuery(Guid ModelId)
    : IRequest<Response<TerritoryResourceAssignmentListDto>>;

public sealed record GetTerritoryResourceAssignmentByIdQuery(Guid ModelId, Guid AssignmentId)
    : IRequest<Response<TerritoryResourceAssignmentDto>>;

public sealed record GetCurrentTerritoryResourceResponsibilitiesQuery(
    Guid ModelId,
    Guid? TerritoryId,
    string? BusinessUnitScope,
    string? PositionCode,
    DateTimeOffset? EffectiveAt,
    bool PrimaryOnly = true) : IRequest<Response<IReadOnlyList<TerritoryResourceAssignmentDto>>>;

public sealed record GetTerritoryResourceAssignmentHistoryQuery(
    Guid ModelId,
    Guid? TerritoryId,
    string? ResourceId,
    string? PositionCode) : IRequest<Response<IReadOnlyList<TerritoryResourceAssignmentDto>>>;

public sealed record GetResourceTerritoryResponsibilitiesQuery(
    string ResourceId,
    DateTimeOffset? EffectiveAt,
    bool IncludeHistory = false) : IRequest<Response<IReadOnlyList<TerritoryResourceAssignmentDto>>>;

// ---------------------------------------------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------------------------------------------

public sealed record TerritoryResourceAssignmentListDto(
    Guid ModelId,
    string ModelStatus,
    bool IsEditable,
    int TotalCount,
    int ActiveCount,
    IReadOnlyList<string> ModelBusinessUnitScopes,
    IReadOnlyList<TerritoryResourceAssignmentDto> Items);

public sealed record TerritoryResourceAssignmentDto(
    Guid Id,
    Guid ModelId,
    Guid? TerritoryId,
    string? TerritoryCode,
    string? TerritoryName,
    string? TerritoryLevel,
    string ResourceId,
    string ResourceType,
    string ResourceDisplayName,
    string? ResourceEmail,
    Guid? PositionId,
    string PositionCode,
    string PositionTitle,
    string PositionType,
    string PositionSourceSystem,
    string CoverageScope,
    IReadOnlyList<string> BusinessUnitScopes,
    string Status,
    string AssignmentSource,
    bool IsPrimary,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    bool IsExpired,
    bool IsCurrent,
    bool IsPlanningOnly,
    string? ChangeReason,
    Guid? ReplacedAssignmentId,
    Guid? ReplacementAssignmentId,
    string? ReplacementReason,
    Guid? TransferFromAssignmentId,
    Guid? TransferToAssignmentId,
    string? TransferReason,
    string? PreviousPositionCode,
    string? NewPositionCode,
    string? CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record TerritoryResourceConflictReportDto(
    Guid ModelId,
    DateTimeOffset GeneratedAt,
    int AssignmentCount,
    int ConflictCount,
    int WarningCount,
    IReadOnlyList<TerritoryResourceConflictDto> Conflicts,
    IReadOnlyList<TerritoryResourceConflictDto> Warnings);

public sealed record TerritoryResourceConflictDto(
    string Kind,
    string Severity,
    string Message,
    IReadOnlyList<Guid> AssignmentIds,
    string? PositionCode,
    Guid? TerritoryId,
    string? TerritoryCode,
    IReadOnlyList<string> BusinessUnitScopes);

public static class TerritoryResourceAssignmentMapper
{
    public static TerritoryResourceAssignmentDto ToDto(
        TerritoryResourceAssignment a, TerritoryNode? node, DateTimeOffset now)
        => new(
            a.Id,
            a.ModelId,
            a.TerritoryId,
            node?.TerritoryCode,
            node?.Name,
            node?.TerritoryLevel,
            a.Resource.ResourceId,
            a.Resource.ResourceType,
            a.Resource.DisplayName,
            a.Resource.Email,
            a.Position.PositionId ?? a.PositionId,
            a.EffectivePositionCode,
            a.EffectivePositionTitle,
            a.Position.PositionType,
            a.Position.SourceSystem,
            a.CoverageScope,
            a.BusinessScopes.Select(s => s.ScopeCode).ToList(),
            a.Status,
            a.AssignmentSource,
            a.IsPrimary,
            a.ValidFrom,
            a.ValidTo,
            a.ValidTo is { } end && end < now,
            string.Equals(a.Status, TerritoryResourceAssignmentValidation.ActiveStatus, StringComparison.OrdinalIgnoreCase)
                && a.ValidFrom <= now && (a.ValidTo is null || a.ValidTo >= now),
            string.Equals(a.Status, TerritoryResourceAssignmentValidation.DefaultStatus, StringComparison.OrdinalIgnoreCase),
            a.ChangeReason,
            a.ReplacedAssignmentId,
            a.ReplacementAssignmentId,
            a.ReplacementReason,
            a.TransferFromAssignmentId,
            a.TransferToAssignmentId,
            a.TransferReason,
            a.PreviousPositionCode,
            a.NewPositionCode,
            a.CorrelationId,
            a.CreatedAt,
            a.UpdatedAt);
}
