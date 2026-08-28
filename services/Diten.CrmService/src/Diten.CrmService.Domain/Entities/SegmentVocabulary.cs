namespace Diten.CrmService.Domain.Entities;

// ---------------------------------------------------------------------------------------------------------------
// MOD-0167 FU02 in-domain vocabulary (D-VOCAB = A). Validated against these constants in the runtime, never through
// MOD-0048, so authoring never fails open on an unpublished reference set and never blocks on an operator task. An
// out-of-set value is a 400. Publishing the same values as MOD-0048 sets is a separate operator follow-up (F-RD).
// Kept beside Segment.cs as its own file so the aggregate file stays readable; it is the same vocabulary surface the
// pack places "in Domain/Entities/Segment.cs".
// ---------------------------------------------------------------------------------------------------------------

/// <summary>What kind of membership a segment expresses. A <c>dynamic</c> segment refuses manual rows on purpose:
/// otherwise the "dynamic" label lies and where a member came from can only be learned row by row.</summary>
public static class SegmentTypes
{
    public const string Static = "static";
    public const string Dynamic = "dynamic";
    public const string Hybrid = "hybrid";

    public static readonly IReadOnlyList<string> All = new[] { Static, Dynamic, Hybrid };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Dynamic : value.Trim().ToLowerInvariant();
}

/// <summary>What the segment groups. Immutable after create.</summary>
public static class SegmentSubjectTypes
{
    public const string Account = "account";
    public const string Contact = "contact";

    public static readonly IReadOnlyList<string> All = new[] { Account, Contact };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>Segment lifecycle. Hard delete does not exist; the only legal transitions are draft to active,
/// draft to archived and active to archived.</summary>
public static class SegmentStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Active, Archived };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Draft : value.Trim().ToLowerInvariant();
}

/// <summary>How the ROOT children of the criteria tree combine.</summary>
public static class SegmentMatchModes
{
    public const string All = "all";
    public const string Any = "any";

    public static readonly IReadOnlyList<string> AllValues = new[] { All, Any };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && AllValues.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? All : value.Trim().ToLowerInvariant();
}

public static class SegmentCriteriaNodeKinds
{
    public const string Group = "group";
    public const string Predicate = "predicate";

    public static readonly IReadOnlyList<string> All = new[] { Group, Predicate };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

public static class SegmentGroupOperators
{
    public const string And = "and";
    public const string Or = "or";
    public const string Not = "not";

    public static readonly IReadOnlyList<string> All = new[] { And, Or, Not };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>The closed operator set. Arity is enforced by the validator, never assumed from the caller.</summary>
public static class SegmentOperators
{
    public const string Eq = "eq";
    public const string Ne = "ne";
    public const string In = "in";
    public const string NotIn = "not-in";
    public const string Contains = "contains";
    public const string Gt = "gt";
    public const string Gte = "gte";
    public const string Lt = "lt";
    public const string Lte = "lte";
    public const string Between = "between";
    public const string IsNull = "is-null";
    public const string IsNotNull = "is-not-null";

    public static readonly IReadOnlyList<string> All =
        new[] { Eq, Ne, In, NotIn, Contains, Gt, Gte, Lt, Lte, Between, IsNull, IsNotNull };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    /// <summary>How many values the operator takes: (min, max). The in / not-in ceiling is
    /// <see cref="SegmentLimits.MaxValuesPerInOperator"/>.</summary>
    public static (int Min, int Max) Arity(string? op) => Normalize(op) switch
    {
        IsNull or IsNotNull => (0, 0),
        Between => (2, 2),
        In or NotIn => (1, SegmentLimits.MaxValuesPerInOperator),
        _ => (1, 1)
    };
}

public static class SegmentValueTypes
{
    public const string String = "string";
    public const string Number = "number";
    public const string Date = "date";
    public const string Bool = "bool";
    public const string Guid = "guid";

    public static readonly IReadOnlyList<string> All = new[] { String, Number, Date, Bool, Guid };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>The ONLY two membership modes a <see cref="TargetCustomer"/> may carry. There is deliberately no third
/// value: derived membership is never written down (D3 + D-TC).</summary>
public static class SegmentMembershipModes
{
    public const string ManualInclude = "manual-include";
    public const string ManualExclude = "manual-exclude";

    public static readonly IReadOnlyList<string> All = new[] { ManualInclude, ManualExclude };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>The verdict of a membership question. <c>unknown</c> is an ANSWER, not an error, and it is NEVER
/// <c>member</c> (MOD-0167-FU01 section 5, verbatim).</summary>
public static class SegmentMembershipVerdicts
{
    public const string Member = "member";
    public const string NotMember = "not-member";
    public const string Unknown = "unknown";

    public static readonly IReadOnlyList<string> All = new[] { Member, NotMember, Unknown };
}

/// <summary>Canonical reason codes. Every accepted AND every eliminated candidate carries at least one: silent
/// elimination is forbidden.</summary>
public static class SegmentReasonCodes
{
    public const string CriteriaMatched = "criteria_matched";
    public const string CriteriaNotMatched = "criteria_not_matched";
    public const string ManualInclude = "manual_include";
    public const string ManualExclude = "manual_exclude";
    public const string ConsentUnknown = "consent_unknown";
    public const string ConsentBlocked = "consent_blocked";
    public const string TerritoryCoverageUnavailable = "territory_coverage_unavailable";
    public const string ConceptProductNodeMissing = "concept_product_node_missing";
    public const string ConceptAffinityNoSpecialtyReached = "concept_affinity_no_specialty_reached";
    public const string ConceptAffinityNotMatched = "concept_affinity_not_matched";
    public const string ConceptSubjectSpecialtyMissing = "concept_subject_specialty_missing";
    public const string AttributeNotResolvable = "attribute_not_resolvable";
    public const string SubjectTypeMismatch = "subject_type_mismatch";
    public const string OutsideEffectiveWindow = "outside_effective_window";
    public const string SegmentNotActive = "segment_not_active";
    public const string DependencyUnavailable = "dependency_unavailable";

    public static readonly IReadOnlyList<string> All = new[]
    {
        CriteriaMatched, CriteriaNotMatched, ManualInclude, ManualExclude, ConsentUnknown, ConsentBlocked,
        TerritoryCoverageUnavailable, ConceptProductNodeMissing, ConceptAffinityNoSpecialtyReached,
        ConceptAffinityNotMatched, ConceptSubjectSpecialtyMissing, AttributeNotResolvable, SubjectTypeMismatch,
        OutsideEffectiveWindow, SegmentNotActive, DependencyUnavailable
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());
}

/// <summary>Canonical machine-readable error codes returned in the response envelope, so a UI (and the smoke script)
/// can branch on the code rather than on a message.</summary>
public static class SegmentErrorCodes
{
    public const string AttributeUnknown = "segment_attribute_unknown";
    public const string OperatorNotSupported = "segment_operator_not_supported";
    public const string AttributeParameterMissing = "segment_attribute_parameter_missing";
    public const string AttributeNotApplicableForSubjectType = "segment_attribute_not_applicable_for_subject_type";
    public const string ConceptDepthExceeded = "segment_concept_depth_exceeded";
    public const string CriteriaFrozen = "segment_criteria_frozen";
    public const string TypeForbidsManualMembership = "segment_type_forbids_manual_membership";
    public const string CriteriaReferenceNotFound = "segment_criteria_reference_not_found";
    public const string DependencyUnavailable = "segment_dependency_unavailable";
    public const string CandidateSetTooLarge = "segment_candidate_set_too_large";
    public const string SubjectSegmentsTooMany = "segment_subject_segments_too_large";
    public const string SubjectTypeMismatch = "subject_type_mismatch";

    public static readonly IReadOnlyList<string> All = new[]
    {
        AttributeUnknown, OperatorNotSupported, AttributeParameterMissing, AttributeNotApplicableForSubjectType,
        ConceptDepthExceeded, CriteriaFrozen, TypeForbidsManualMembership, CriteriaReferenceNotFound,
        DependencyUnavailable, CandidateSetTooLarge, SubjectSegmentsTooMany, SubjectTypeMismatch
    };
}

/// <summary>Hard ceilings on document growth and evaluation cost (D4). Every overflow is an explicit 400/422; silent
/// truncation is forbidden, because a partial member list is more dangerous than no list at all.</summary>
public static class SegmentLimits
{
    public const int MaxCriteriaDepth = 5;
    public const int MaxCriteriaNodes = 100;
    public const int MaxChildrenPerGroup = 20;
    public const int MaxValuesPerInOperator = 50;

    /// <summary>Phase-1 candidate ceiling; beyond it the author must narrow the rule (422).</summary>
    public const int MaxCandidateSet = 10_000;

    /// <summary>Ceiling for the reverse question "which segments is this subject in?" (422 beyond).</summary>
    public const int MaxSegmentsPerSubject = 200;

    public const int MaxSegmentCodeLength = 64;
    public const int MaxSegmentNameLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MaxNotesLength = 2000;
    public const int MaxBusinessUnitIdLength = 64;
    public const int MaxSelectionReasonLength = 1000;

    /// <summary>concept.affinity traversal ceiling. Default 1, maximum 2, 3 or more is a 400. There is no transitive
    /// closure and no traversal engine.</summary>
    public const int DefaultConceptAffinityDepth = 1;
    public const int MaxConceptAffinityDepth = 2;
}

/// <summary>The ONLY concept-relationship types <c>concept.affinity</c> follows. A SUBSET of the MOD-0162 FU03
/// <see cref="ConceptRelationshipTypes"/> vocabulary, not a new vocabulary; MOD-0162 set is not redefined here.
/// leads-to / requires / evidences / custom are narrative or evidential edges, not interest edges, and are
/// deliberately NOT followed.</summary>
public static class ConceptAffinityRelationshipTypes
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        ConceptRelationshipTypes.Addresses, ConceptRelationshipTypes.BelongsTo
    };

    public static bool IsFollowed(string? relationshipType)
        => !string.IsNullOrWhiteSpace(relationshipType)
           && All.Contains(relationshipType.Trim().ToLowerInvariant());
}
