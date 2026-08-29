namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0162 FU03 — Concept graph in-domain (structural) vocabulary. Validated in the runtime against these constants,
/// never through MOD-0048, so authoring never fails open on an unpublished reference set. Surfaced on the concept-graph
/// contract so a UI needs no hardcoded list. MOD-0048 publish of concept-status / concept-relationship-type /
/// concept-chain-status is a separate operator follow-up (F-RD). The relationship-type set is the boundary FU01C §5
/// canonical (decision D3=A); the divergent authoring-template values were mapped away and are never accepted.
/// </summary>
public static class ConceptStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Active, Inactive, Archived };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Draft : value.Trim().ToLowerInvariant();
}

/// <summary>Concept chain template lifecycle. Wider than <see cref="ConceptStatuses"/> because a chain is versioned and
/// publishable (draft → review → approved → published → inactive → archived). Hard delete does not exist.</summary>
public static class ConceptChainStatuses
{
    public const string Draft = "draft";
    public const string Review = "review";
    public const string Approved = "approved";
    public const string Published = "published";
    public const string Inactive = "inactive";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Review, Approved, Published, Inactive, Archived };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Draft : value.Trim().ToLowerInvariant();
}

/// <summary>Directed relationship type. Boundary FU01C §5 canonical set — the ONLY valid vocabulary (decision D3=A).
/// The early authoring-template values (related-to / depends-on / targets / maps-to / replaces) were mapped to this set
/// and are never accepted; an unknown value is a 400 (fail-closed, in-domain).</summary>
public static class ConceptRelationshipTypes
{
    public const string LeadsTo = "leads-to";
    public const string Requires = "requires";
    public const string Addresses = "addresses";
    public const string Evidences = "evidences";
    public const string BelongsTo = "belongs-to";
    public const string Custom = "custom";

    public static readonly IReadOnlyList<string> All = new[] { LeadsTo, Requires, Addresses, Evidences, BelongsTo, Custom };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>Relationship direction. <c>outbound</c> is the default; <c>bidirectional</c> is an explicit declaration —
/// a reverse edge is never auto-derived.</summary>
public static class ConceptDirections
{
    public const string Outbound = "outbound";
    public const string Bidirectional = "bidirectional";

    public static readonly IReadOnlyList<string> All = new[] { Outbound, Bidirectional };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Outbound : value.Trim().ToLowerInvariant();
}

/// <summary>What a concept node points at in another system. The node is NEVER the SoR of that master; it carries an
/// explicit reference only, nothing is copied. Product target is the MDM Global Product (decision 2026-08-25):
/// brand/product were removed; the only product value is <c>global-product</c>.</summary>
public static class ConceptExternalRefTypes
{
    public const string GlobalProduct = "global-product";
    public const string Document = "document";
    public const string AudienceProfile = "audience-profile";
    public const string ReferenceDataValue = "reference-data-value";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        GlobalProduct, Document, AudienceProfile, ReferenceDataValue, Other
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>Role a piece of content plays against the concept it links to.</summary>
public static class ConceptLinkRoles
{
    public const string Primary = "primary";
    public const string Supporting = "supporting";
    public const string Evidence = "evidence";
    public const string ObjectionHandling = "objection-handling";

    public static readonly IReadOnlyList<string> All = new[] { Primary, Supporting, Evidence, ObjectionHandling };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Primary : value.Trim().ToLowerInvariant();
}

/// <summary>Canonical FU03 reason / outcome codes surfaced on write outcomes and on the contract. Nothing is silent.</summary>
public static class ConceptGraphReasonCodes
{
    public const string TypeCreated = "concept_type_created";
    public const string TypeUpdated = "concept_type_updated";
    public const string TypeArchived = "concept_type_archived";
    public const string TypeDuplicateCode = "concept_type_duplicate_code";

    public const string NodeCreated = "concept_node_created";
    public const string NodeUpdated = "concept_node_updated";
    public const string NodeArchived = "concept_node_archived";
    public const string NodeDuplicateCode = "concept_node_duplicate_code";
    public const string NodeSubjectTypeMismatch = "concept_node_subject_type_mismatch";

    public const string RelationshipCreated = "concept_relationship_created";
    public const string RelationshipUpdated = "concept_relationship_updated";
    public const string RelationshipArchived = "concept_relationship_archived";
    public const string RelationshipSelfLoop = "concept_relationship_self_loop";
    public const string RelationshipCrossSubject = "concept_relationship_cross_subject";
    public const string RelationshipCycle = "concept_relationship_cycle";
    public const string RelationshipDuplicateActive = "concept_relationship_duplicate_active";
    public const string RelationshipNonConforming = "concept_relationship_non_conforming";

    public const string ChainTemplateCreated = "concept_chain_template_created";
    public const string ChainTemplateUpdated = "concept_chain_template_updated";
    public const string ChainTemplateArchived = "concept_chain_template_archived";
    public const string ChainTemplateInvalidSequence = "concept_chain_template_invalid_sequence";
    public const string ChainTemplatePublishOverlap = "concept_chain_template_publish_overlap";

    public const string ContentLinkCreated = "content_concept_link_created";
    public const string ContentLinkArchived = "content_concept_link_archived";
    public const string ContentLinkRelationshipMismatch = "content_concept_link_relationship_mismatch";

    public const string ArchivedNoMutation = "concept_archived_no_mutation";
    public const string ReferenceArchived = "concept_reference_archived";
    public const string ContentConceptNodeUnresolved = "knowledge_content_concept_node_unresolved";
}
