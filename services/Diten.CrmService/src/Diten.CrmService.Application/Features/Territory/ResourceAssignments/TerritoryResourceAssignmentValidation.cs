using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Territory.ResourceAssignments;

public sealed record TerritoryResourceValidationError(string Message, int StatusCode);

/// <summary>Everything a validated assignment needs, resolved from the published vocabulary.</summary>
public sealed record TerritoryResourceResolution(
    TerritoryPositionRef Position,
    string CoverageScope,
    string Status,
    string AssignmentSource,
    List<TerritoryBusinessScope> BusinessScopes,
    bool IsPrimary,
    IReadOnlyList<string> Warnings);

/// <summary>
/// FU04 validation. Every rule is resolved from MOD-0048 published values and their metadata — the code asks the
/// vocabulary "does this coverage scope require a territory id / a business scope? may this role be primary?"
/// instead of hardcoding the answer. A missing set, a missing value or missing metadata is a controlled failure
/// (fail-closed); nothing is silently defaulted.
/// </summary>
public static class TerritoryResourceAssignmentValidation
{
    public const string DefaultStatus = "proposed";
    public const string ActiveStatus = "active";
    public const string EndedStatus = "ended";
    public const string DefaultSource = "manual";

    public static async Task<(TerritoryResourceResolution? Resolution, TerritoryResourceValidationError? Error)> ResolveAsync(
        ITerritoryReferenceValidator references,
        TerritoryModel model,
        TerritoryNode? node,
        Guid? territoryId,
        Guid? positionId,
        string positionCode,
        string? positionTitle,
        string? positionType,
        string? positionSourceSystem,
        string? coverageScopeInput,
        IReadOnlyList<string>? businessUnitScopeCodes,
        bool isPrimary,
        string? assignmentSourceInput,
        string? changeReason,
        DateTimeOffset validFrom,
        DateTimeOffset? validTo,
        CancellationToken cancellationToken,
        bool operational = false)
    {
        var warnings = new List<string>();

        // ---------------- position (replaces the former MOD-0048 role) ----------------
        positionCode = positionCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(positionCode))
        {
            return (null, new TerritoryResourceValidationError("PositionCode is required.", 400));
        }

        var resolvedPositionTitle = positionTitle?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedPositionTitle))
        {
            return (null, new TerritoryResourceValidationError("PositionTitle is required.", 400));
        }

        var resolvedPositionType = string.IsNullOrWhiteSpace(positionType) ? "person-position" : positionType.Trim();
        var resolvedSourceSystem = string.IsNullOrWhiteSpace(positionSourceSystem) ? "snapshot" : positionSourceSystem.Trim();
        var hasPolicy = TerritoryPositionPolicy.TryResolve(positionCode, out var positionPolicy);
        if (operational && !hasPolicy)
        {
            return (null, new TerritoryResourceValidationError(
                $"Position '{positionCode}' has no verified operational territory policy.", 409));
        }

        if (!hasPolicy)
        {
            warnings.Add($"Position '{positionCode}' uses a planning-only, unverified snapshot.");
        }

        // ---------------- coverage scope (now required — the role default fallback is gone) ----------------
        var coverageScope = coverageScopeInput?.Trim();
        if (string.IsNullOrWhiteSpace(coverageScope))
        {
            return (null, new TerritoryResourceValidationError("CoverageScope is required.", 400));
        }

        var scopeStatus = await references.ValidateValueAsync(TerritoryReferenceSets.TerritoryCoverageScope, coverageScope, cancellationToken);
        if (scopeStatus != ReferenceValidationStatus.Valid)
        {
            return (null, new TerritoryResourceValidationError(Message(TerritoryReferenceSets.TerritoryCoverageScope, coverageScope, scopeStatus), 400));
        }

        var scopeMeta = await references.GetValueMetadataAsync(TerritoryReferenceSets.TerritoryCoverageScope, coverageScope, cancellationToken);
        if (scopeMeta is null
            || !ReferenceMetadata.TryGetBool(scopeMeta, TerritoryAssignmentMetadataKeys.RequiresTerritoryId, out var requiresTerritory)
            || !ReferenceMetadata.TryGetBool(scopeMeta, TerritoryAssignmentMetadataKeys.AllowsTerritoryId, out var allowsTerritory)
            || !ReferenceMetadata.TryGetBool(scopeMeta, TerritoryAssignmentMetadataKeys.RequiresBusinessScope, out var requiresBusinessScope)
            || !ReferenceMetadata.TryGetBool(scopeMeta, TerritoryAssignmentMetadataKeys.AllowsBusinessScope, out var allowsBusinessScope))
        {
            return (null, new TerritoryResourceValidationError(
                $"Coverage scope '{coverageScope}' is missing required metadata; cannot validate the assignment.", 400));
        }

        // ---------------- territory id ↔ coverage scope consistency (pack §10 / §20) ----------------
        if (requiresTerritory && territoryId is null)
        {
            return (null, new TerritoryResourceValidationError(
                $"Coverage scope '{coverageScope}' requires a territory node.", 400));
        }

        if (!allowsTerritory && territoryId is not null)
        {
            return (null, new TerritoryResourceValidationError(
                $"Coverage scope '{coverageScope}' does not allow a territory node; leave territoryId empty.", 400));
        }

        if (territoryId is not null && node is null)
        {
            return (null, new TerritoryResourceValidationError("Territory node not found in this model.", 404));
        }

        if (hasPolicy)
        {
            if (positionPolicy.TerritoryRequired && node is null)
            {
                return (null, new TerritoryResourceValidationError(
                    $"Position '{positionCode}' requires a territory node.", 409));
            }

            if (!positionPolicy.TerritoryRequired && positionPolicy.AllowedLevels.Count == 0 && node is not null)
            {
                return (null, new TerritoryResourceValidationError(
                    $"Position '{positionCode}' is business-unit/model-wide and does not allow a territory node.", 409));
            }

            if (node is not null && positionPolicy.AllowedLevels.Count > 0
                && !positionPolicy.AllowedLevels.Contains(node.TerritoryLevel))
            {
                return (null, new TerritoryResourceValidationError(
                    $"Position '{positionCode}' requires node level: {string.Join(", ", positionPolicy.AllowedLevels)}.", 409));
            }
        }

        // ---------------- business scopes ----------------
        var scopeCodes = Normalize(businessUnitScopeCodes);

        if (!allowsBusinessScope && scopeCodes.Count > 0)
        {
            return (null, new TerritoryResourceValidationError(
                $"Coverage scope '{coverageScope}' does not allow business unit scopes.", 400));
        }

        if (requiresBusinessScope && scopeCodes.Count == 0)
        {
            return (null, new TerritoryResourceValidationError(
                $"Coverage scope '{coverageScope}' requires at least one business unit scope.", 400));
        }

        // An assignment may never widen the model's own business scope (FU02A contract).
        var modelScopes = model.BusinessScopes
            .Where(s => string.Equals(s.ScopeType, TerritoryReferenceSets.BusinessUnitScopeType, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.ScopeCode.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (scopeCodes.Count > 0)
        {
            if (modelScopes.Count == 0)
            {
                return (null, new TerritoryResourceValidationError(
                    "This territory model has no business unit scope, so an assignment cannot declare one.", 400));
            }

            var outside = scopeCodes.Where(c => !modelScopes.Contains(c)).ToList();
            if (outside.Count > 0)
            {
                return (null, new TerritoryResourceValidationError(
                    $"Business unit scope(s) outside the territory model scope: {string.Join(", ", outside)}.", 400));
            }
        }

        // ---------------- primary ----------------
        // The former role-metadata canBePrimary gate is gone with the role: any position may hold a primary
        // assignment. Exclusivity is still enforced by the conflict engine (one primary per node/position/scope).

        // ---------------- source + change reason ----------------
        var source = string.IsNullOrWhiteSpace(assignmentSourceInput) ? DefaultSource : assignmentSourceInput.Trim();
        var sourceStatus = await references.ValidateValueAsync(TerritoryReferenceSets.TerritoryAssignmentSource, source, cancellationToken);
        if (sourceStatus != ReferenceValidationStatus.Valid)
        {
            return (null, new TerritoryResourceValidationError(Message(TerritoryReferenceSets.TerritoryAssignmentSource, source, sourceStatus), 400));
        }

        var sourceMeta = await references.GetValueMetadataAsync(TerritoryReferenceSets.TerritoryAssignmentSource, source, cancellationToken);
        if (ReferenceMetadata.TryGetBool(sourceMeta, TerritoryAssignmentMetadataKeys.RequiresReason, out var needsReason)
            && needsReason && string.IsNullOrWhiteSpace(changeReason))
        {
            return (null, new TerritoryResourceValidationError(
                $"Assignment source '{source}' requires a change reason.", 400));
        }

        // ---------------- status ----------------
        var statusCheck = await references.ValidateValueAsync(TerritoryReferenceSets.TerritoryAssignmentStatus, DefaultStatus, cancellationToken);
        if (statusCheck != ReferenceValidationStatus.Valid)
        {
            return (null, new TerritoryResourceValidationError(Message(TerritoryReferenceSets.TerritoryAssignmentStatus, DefaultStatus, statusCheck), 400));
        }

        // ---------------- dates ----------------
        if (validTo is { } end2 && end2 < validFrom)
        {
            return (null, new TerritoryResourceValidationError("ValidTo must be greater than or equal to ValidFrom.", 400));
        }

        if (validFrom < model.EffectiveFrom)
        {
            return (null, new TerritoryResourceValidationError("Assignment ValidFrom is outside the territory model window.", 400));
        }

        if (model.EffectiveTo is { } modelEnd && (validTo is null || validTo > modelEnd))
        {
            return (null, new TerritoryResourceValidationError("Assignment ValidTo is outside the territory model window.", 400));
        }

        var businessScopes = scopeCodes
            .Select(c => new TerritoryBusinessScope { ScopeType = TerritoryReferenceSets.BusinessUnitScopeType, ScopeCode = c })
            .ToList();

        var position = new TerritoryPositionRef
        {
            PositionId = positionId is null || positionId == Guid.Empty ? null : positionId,
            PositionCode = positionCode,
            PositionTitle = resolvedPositionTitle,
            PositionType = resolvedPositionType,
            SourceSystem = resolvedSourceSystem,
            ValidationMode = hasPolicy ? "policy-validated" : "snapshot",
            PolicySource = hasPolicy ? TerritoryPositionPolicy.BuiltInSource : TerritoryPositionPolicy.UnverifiedSource
        };

        return (new TerritoryResourceResolution(
            position, coverageScope, operational ? ActiveStatus : DefaultStatus, source, businessScopes, isPrimary, warnings), null);
    }

    /// <summary>Trim, drop blanks, de-duplicate case-insensitively, preserve order.</summary>
    public static List<string> Normalize(IReadOnlyList<string>? codes)
    {
        if (codes is null)
        {
            return new List<string>();
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var raw in codes)
        {
            var value = raw?.Trim();
            if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static string Message(string setCode, string value, ReferenceValidationStatus status)
        => status switch
        {
            ReferenceValidationStatus.SetMissing =>
                $"Required reference set '{setCode}' is not published yet (MOD-0048 authoring pending).",
            _ => $"'{value}' is not a published value of reference set '{setCode}'."
        };
}
