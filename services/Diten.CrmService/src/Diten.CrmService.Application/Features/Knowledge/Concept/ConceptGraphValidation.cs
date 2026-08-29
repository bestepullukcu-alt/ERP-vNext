using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Knowledge.Concept;

/// <summary>
/// MOD-0162 FU03 structural validation. Every rule returns a 400 message or null. All vocabulary (status / chain-status /
/// relationship-type / direction / external-ref-type / link-role) is validated IN-DOMAIN against the <c>Concept*</c>
/// constants — it never fails open on an unpublished MOD-0048 set (V19). Existence / archived / cross-subject / cycle
/// checks need repository access and live in the handlers. Shared code / name / effective-window rules are reused from
/// the FU02 <see cref="KnowledgeValidation"/>.
/// </summary>
public static class ConceptGraphValidation
{
    public static string? ValidateConceptStatus(string? status)
        => string.IsNullOrWhiteSpace(status) || ConceptStatuses.IsValid(status)
            ? null
            : $"Status must be one of: {string.Join(", ", ConceptStatuses.All)}. " +
              "A concept row is never hard-deleted; closing it is the archive endpoint.";

    public static string? ValidateChainStatus(string? status)
        => string.IsNullOrWhiteSpace(status) || ConceptChainStatuses.IsValid(status)
            ? null
            : $"Status must be one of: {string.Join(", ", ConceptChainStatuses.All)}.";

    public static string? ValidateRelationshipType(string? type)
        => ConceptRelationshipTypes.IsValid(type)
            ? null
            : $"RelationshipType is required and must be one of: {string.Join(", ", ConceptRelationshipTypes.All)}.";

    public static string? ValidateDirection(string? direction)
        => string.IsNullOrWhiteSpace(direction) || ConceptDirections.IsValid(direction)
            ? null
            : $"Direction must be one of: {string.Join(", ", ConceptDirections.All)}.";

    public static string? ValidateLinkRole(string? role)
        => string.IsNullOrWhiteSpace(role) || ConceptLinkRoles.IsValid(role)
            ? null
            : $"LinkRole must be one of: {string.Join(", ", ConceptLinkRoles.All)}.";

    /// <summary>ExternalRef is a paired field: both parts are supplied together or neither is, and the type — when
    /// supplied — must be in the in-domain set. The node never owns the master; this is provenance only.</summary>
    public static string? ValidateExternalRef(string? externalRefType, string? externalRefId)
    {
        var hasType = !string.IsNullOrWhiteSpace(externalRefType);
        var hasId = !string.IsNullOrWhiteSpace(externalRefId);

        if (hasType != hasId)
        {
            return "ExternalRefType and ExternalRefId must be supplied together (or both omitted).";
        }

        if (hasType && !ConceptExternalRefTypes.IsValid(externalRefType))
        {
            return $"ExternalRefType must be one of: {string.Join(", ", ConceptExternalRefTypes.All)}.";
        }

        return null;
    }

    /// <summary>OrderedConceptTypes: at least two type ids, none empty, and no type repeated (v1; recursion is F7).
    /// Same-subject membership needs repo access and is checked in the handler.</summary>
    public static string? ValidateOrderedTypesShape(IReadOnlyList<Guid>? orderedConceptTypes)
    {
        if (orderedConceptTypes is null || orderedConceptTypes.Count < 2)
        {
            return "OrderedConceptTypes must contain at least two concept type ids.";
        }

        if (orderedConceptTypes.Any(id => id == Guid.Empty))
        {
            return "OrderedConceptTypes cannot contain an empty concept type id.";
        }

        if (orderedConceptTypes.Distinct().Count() != orderedConceptTypes.Count)
        {
            return "OrderedConceptTypes cannot contain the same concept type twice (v1; recursion is a later follow-up).";
        }

        return null;
    }
}
