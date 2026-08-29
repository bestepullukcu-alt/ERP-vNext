using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Territory.ResourceAssignments;

/// <summary>
/// The single deterministic definition of "is this assignment CURRENT at <c>effectiveAt</c>?" (pack §22.3 current
/// responsibility contract). Extracted so FU04A's current-responsibility query and FU04B's plan-vs-current engine
/// cannot drift into two different answers — pack §22.4 D-FU04B-3 forbids a second current definition.
///
/// <para>The predicate is unchanged from FU04A: active status, effective window covers the instant, not soft-deleted.
/// Model-status gating stays with the CALLER: FU04A requires an active model, FU04B relaxes it only for the archived
/// read-only historical comparison and flags that view as historical (D-FU04B-6).</para>
/// </summary>
public static class TerritoryCurrentResponsibilityPolicy
{
    public static bool IsCurrent(TerritoryResourceAssignment assignment, DateTimeOffset effectiveAt)
        => !assignment.IsDeleted
           && string.Equals(assignment.Status, TerritoryResourceAssignmentValidation.ActiveStatus, StringComparison.OrdinalIgnoreCase)
           && assignment.ValidFrom <= effectiveAt
           && (assignment.ValidTo is null || assignment.ValidTo >= effectiveAt);

    /// <summary>Normalized position code — the canonical match key. RoleCode is never used (pack §22.4).</summary>
    public static string NormalizePosition(string? positionCode)
        => (positionCode ?? string.Empty).Trim().ToUpperInvariant();

    public static string NormalizeScope(string? scopeCode)
        => (scopeCode ?? string.Empty).Trim().ToUpperInvariant();
}
