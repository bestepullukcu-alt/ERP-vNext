using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.PlannedVisit.Contract;

/// <summary>
/// MOD-0155 FU01 contract surface: feature flags + in-domain vocabulary + supported filters + limits + error codes +
/// permissions + limitations. Published so a contract-driven UI needs no hardcoded vocabulary, no hardcoded ceiling and
/// no hardcoded outcome name — a hardcoded list is a second source of truth, and it drifts silently.
/// </summary>
public sealed record PlannedVisitContractDto(
    string ModuleId,
    string ModuleName,
    string Service,
    string RuntimeScope,
    Guid TenantId,
    bool IsReady,
    PlannedVisitFeatureFlags Features,
    PlannedVisitVocabularyDto Vocabularies,
    PlannedVisitSupportedFilters SupportedFilters,
    PlannedVisitContractLimits Limits,
    IReadOnlyList<string> ErrorCodes,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations);

/// <summary>The in-domain vocabulary exactly as the runtime enforces it (D2). Every dropdown is fed from here.</summary>
public sealed record PlannedVisitVocabularyDto(
    IReadOnlyList<string> TargetTypes,
    IReadOnlyList<string> Purposes,
    IReadOnlyList<string> VisitTypes,
    IReadOnlyList<string> Statuses,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> ResourceTypes,
    IReadOnlyList<string> SelectionModes,
    IReadOnlyList<string> ContentSources)
{
    public static PlannedVisitVocabularyDto Current => new(
        PlannedVisitTargetType.All,
        PlannedVisitPurpose.All,
        PlannedVisitType.All,
        PlannedVisitStatus.All,
        PlannedVisitSource.All,
        PlannedVisitResourceTypes.All,
        PlannedVisitSelectionMode.All,
        PlannedVisitContentSource.All);
}

/// <summary>Which list filters the runtime honours. A filter that is not here is not silently ignored.</summary>
public sealed record PlannedVisitSupportedFilters(IReadOnlyList<string> List)
{
    public static PlannedVisitSupportedFilters Current => new(new[]
    {
        "plannedDateFrom", "plannedDateTo", "resourceId", "targetType", "targetId",
        "planStatus", "visitPurpose", "territoryNodeId", "campaignId", "includeArchived"
    });
}

/// <summary>Published ceilings, so the editor enforces the same numbers the runtime does.</summary>
public sealed record PlannedVisitContractLimits(
    int MaxVisitCodeLength,
    int MaxResourceIdLength,
    int MaxObjectiveLength,
    int MaxNotesLength,
    int MaxCancellationReasonLength,
    int MinDurationMinutes,
    int MaxDurationMinutes)
{
    public static PlannedVisitContractLimits Current => new(
        PlannedVisitLimits.MaxVisitCodeLength,
        PlannedVisitLimits.MaxResourceIdLength,
        PlannedVisitLimits.MaxObjectiveLength,
        PlannedVisitLimits.MaxNotesLength,
        PlannedVisitLimits.MaxCancellationReasonLength,
        PlannedVisitLimits.MinDurationMinutes,
        PlannedVisitLimits.MaxDurationMinutes);
}
