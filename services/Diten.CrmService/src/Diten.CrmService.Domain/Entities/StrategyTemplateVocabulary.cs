namespace Diten.CrmService.Domain.Entities;

// ---------------------------------------------------------------------------------------------------------------
// MOD-0167 FU04 in-domain vocabulary (D-VOCAB = A). Validated against these constants in the runtime, never through
// MOD-0048, so authoring never fails open on an unpublished reference set and never blocks on an operator task. An
// out-of-set value is a 400. Publishing the same values as MOD-0048 sets is a separate operator follow-up (F-RD).
// Kept beside StrategyTemplate.cs as its own file so the aggregate file stays readable.
//
// NOTE: the frequency vocabulary (FrequencyType / FrequencyPeriodType) is NOT redefined here. A declared intent is
// validated against MOD-0165's OWN constants, read-only — copying them would create a second source of truth that
// drifts, and this FU is a consumer of MOD-0165, not a co-owner.
// ---------------------------------------------------------------------------------------------------------------

/// <summary>Template lifecycle. Hard delete does not exist; the only legal transitions are draft to active,
/// draft to archived and active to archived.</summary>
public static class StrategyTemplateStatuses
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

/// <summary>What the play targets. Immutable after create, and every bound segment must match it. Same value set as
/// <see cref="SegmentSubjectTypes"/> — restated, not redefined: a template and a segment must speak of the same kind of
/// subject or the binding is meaningless.</summary>
public static class StrategyTemplateSubjectTypes
{
    public const string Account = "account";
    public const string Contact = "contact";

    public static readonly IReadOnlyList<string> All = new[] { Account, Contact };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>A LABEL on a segment binding. Deliberately behaviour-free: no handler branches on it and no set algebra is
/// applied to the bound list, so <c>exclusion-note</c> excludes nothing — it is the author's annotation.</summary>
public static class StrategySegmentBindingRoles
{
    public const string Primary = "primary";
    public const string Secondary = "secondary";
    public const string ExclusionNote = "exclusion-note";

    public static readonly IReadOnlyList<string> All = new[] { Primary, Secondary, ExclusionNote };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}

/// <summary>The three shapes a frequency intent may take. Exactly one shape is valid per template; a mixed shape is a
/// 400 because choosing a winner would need a conflict resolver, and this FU opens no engine.</summary>
public static class StrategyFrequencyIntentModes
{
    /// <summary>Points at an existing ACTIVE MOD-0165 policy.</summary>
    public const string PolicyReference = "policy-reference";

    /// <summary>States a rhythm in MOD-0165's vocabulary. Machine-readable but explicitly NON-BINDING: the MOD-0165
    /// resolve provider does not read it.</summary>
    public const string DeclaredIntent = "declared-intent";

    /// <summary>An answer, not an omission: this play carries no rhythm.</summary>
    public const string None = "none";

    public static readonly IReadOnlyList<string> All = new[] { PolicyReference, DeclaredIntent, None };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? None : value.Trim().ToLowerInvariant();
}

/// <summary>Whether a product line carries a SKU split. <c>product-only</c> exists so a product-level play states that
/// honestly instead of masquerading as a SKU split with no rows.</summary>
public static class StrategySkuAllocationModes
{
    public const string ProductOnly = "product-only";
    public const string SkuAllocated = "sku-allocated";

    public static readonly IReadOnlyList<string> All = new[] { ProductOnly, SkuAllocated };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? ProductOnly : value.Trim().ToLowerInvariant();
}

/// <summary>Which MOD-0162 presentation a content binding points at. A typed reference is required: a bare id cannot be
/// resolved to an aggregate, and guessing is how a binding silently points at nothing.</summary>
public static class StrategyContentRefTypes
{
    public const string KnowledgePath = "knowledge-path";
    public const string ContentEngagementJourney = "content-engagement-journey";

    public static readonly IReadOnlyList<string> All = new[] { KnowledgePath, ContentEngagementJourney };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>
/// MOD-0167 FU04 (WP-ST-SCOPE) — the levels a play can live at. A deliberate MIRROR of the campaign's scope levels: the
/// same four names, the same precedence, so a reader of both modules learns ONE mental model.
/// <para><b>Mirrored, not shared.</b> No code is imported from <see cref="CampaignScopeTypes"/> or the cycle-period
/// rules, because the three scopes do not mean the same thing: a period's scope is immutable identity, while a campaign's
/// and a play's are editable attributes. Sharing one implementation would forbid a divergence that is already true.</para>
/// <para>In-domain and fail-closed: an unknown value is refused (400), never quietly read as <see cref="Tenant"/>.
/// CRM has no <c>organization-unit</c> level, exactly as the campaign and the cycle period have none.</para>
/// </summary>
public static class StrategyTemplateScopeTypes
{
    /// <summary>The whole tenant. A scope of its OWN, not the absence of one.</summary>
    public const string Tenant = "tenant";

    /// <summary>One country, referenced by an ISO alpha-2 code from the governed reference set.</summary>
    public const string Country = "country";

    /// <summary>One MDM legal entity, referenced by id and proved referenceable before persistence.</summary>
    public const string LegalEntity = "legal-entity";

    /// <summary>One business unit, referenced by a published MOD-0048 <c>business-unit</c> value code.</summary>
    public const string BusinessUnit = "business-unit";

    /// <summary>Resolution precedence, MOST SPECIFIC FIRST — the same order the campaign and cycle-period resolvers walk.
    /// Defined once here; no second if/else chain restates it.</summary>
    public static readonly IReadOnlyList<string> ByPrecedence =
        new[] { BusinessUnit, LegalEntity, Country, Tenant };

    public static readonly IReadOnlyList<string> All = ByPrecedence;

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>Published ceilings for the play scope write path. The business-unit ceiling reuses the existing
/// <see cref="StrategyTemplateLimits.MaxBusinessUnitIdLength"/> so the scope reference and the shape check can never
/// disagree about how long a business-unit code may be.</summary>
public static class StrategyTemplateScopeLimits
{
    /// <summary>ISO alpha-2, so exactly two characters.</summary>
    public const int CountryScopeLength = 2;

    public const int MaxBusinessUnitIdLength = StrategyTemplateLimits.MaxBusinessUnitIdLength;
}

/// <summary>Canonical machine-readable error codes returned in the response envelope, so a UI (and the smoke script)
/// can branch on the code rather than on a message.</summary>
public static class StrategyTemplateErrorCodes
{
    public const string SegmentReferenceNotFound = "segment_reference_not_found";
    public const string SegmentArchived = "segment_archived";
    public const string SegmentSubjectTypeMismatch = "segment_subject_type_mismatch";
    public const string SegmentNotActive = "segment_not_active";
    public const string SegmentBindingDuplicate = "segment_binding_duplicate";
    public const string FrequencyIntentShapeInvalid = "frequency_intent_shape_invalid";
    public const string FrequencyPolicyNotFound = "frequency_policy_not_found";
    public const string FrequencyPolicyNotActive = "frequency_policy_not_active";
    public const string FrequencyPolicyTargetMismatch = "frequency_policy_target_mismatch";
    public const string ContentReferenceNotFound = "content_reference_not_found";
    public const string ContentNotPublished = "content_not_published";
    public const string ContentArchived = "content_archived";
    public const string ContentBindingDuplicate = "content_binding_duplicate";
    public const string ProductReferenceNotFound = "product_reference_not_found";
    public const string ProductLineDuplicate = "product_line_duplicate";
    public const string SkuReferenceNotFound = "sku_reference_not_found";
    public const string SkuAllocationDuplicate = "sku_allocation_duplicate";
    public const string SkuAllocationTotalInvalid = "sku_allocation_total_invalid";
    public const string SkuAllocationModeMismatch = "sku_allocation_mode_mismatch";
    public const string LineWeightPartiallySpecified = "line_weight_partially_specified";
    public const string LineWeightTotalInvalid = "line_weight_total_invalid";
    public const string ReferenceFanoutExceeded = "strategy_reference_fanout_exceeded";
    public const string DependencyUnavailable = "strategy_dependency_unavailable";
    public const string BindingsFrozen = "bindings_frozen";

    // ---- WP-ST-SCOPE - play scope. Nothing is silent: an unpublished SET and an unknown VALUE get different codes
    // because one is fixed by an operator and the other by retyping, and "the dependency said no" is never conflated
    // with "the dependency did not answer". A deliberate mirror of the campaign's scope reason codes.

    /// <summary>The supplied ScopeType is not one of the four known levels.</summary>
    public const string ScopeTypeUnknown = "strategy_scope_type_unknown";

    /// <summary>The level named needs a reference that was not supplied.</summary>
    public const string ScopeReferenceRequired = "strategy_scope_reference_required";

    /// <summary>More than one scope reference was supplied. Refused rather than silently narrowed - dropping a value the
    /// author typed would let them believe they filed the play somewhere they did not.</summary>
    public const string ScopeAmbiguous = "strategy_scope_ambiguous";

    /// <summary>CountryScope is not an ISO alpha-2 code.</summary>
    public const string ScopeCountryInvalid = "strategy_country_invalid";

    /// <summary>The governed reference set backing a scope level is not published yet - an operator must publish it.
    /// Deliberately distinct from "value unknown", which the author fixes themselves.</summary>
    public const string ScopeReferenceSetUnpublished = "strategy_scope_reference_set_unpublished";

    /// <summary>The country code is not in the governed set.</summary>
    public const string ScopeCountryUnknown = "strategy_country_unknown";

    /// <summary>The business-unit code is not in the published set. Raised only when the reference CHANGES, so a play
    /// carrying a pre-scope code stays editable.</summary>
    public const string ScopeBusinessUnitUnknown = "strategy_business_unit_unknown";

    /// <summary>MDM answered, and the legal entity does not exist, is not active, or may not be referenced.</summary>
    public const string ScopeLegalEntityNotReferenceable = "strategy_legal_entity_not_referenceable";

    /// <summary>MDM did not answer. 503 with nothing persisted - we do not KNOW, so we must not tell the author their
    /// input was wrong.</summary>
    public const string ScopeLegalEntityValidationUnavailable = "strategy_legal_entity_validation_unavailable";

    public static readonly IReadOnlyList<string> All = new[]
    {
        SegmentReferenceNotFound, SegmentArchived, SegmentSubjectTypeMismatch, SegmentNotActive,
        SegmentBindingDuplicate, FrequencyIntentShapeInvalid, FrequencyPolicyNotFound, FrequencyPolicyNotActive,
        FrequencyPolicyTargetMismatch, ContentReferenceNotFound, ContentNotPublished, ContentArchived,
        ContentBindingDuplicate, ProductReferenceNotFound, ProductLineDuplicate, SkuReferenceNotFound,
        SkuAllocationDuplicate, SkuAllocationTotalInvalid, SkuAllocationModeMismatch, LineWeightPartiallySpecified,
        LineWeightTotalInvalid, ReferenceFanoutExceeded, DependencyUnavailable, BindingsFrozen,
        ScopeTypeUnknown, ScopeReferenceRequired, ScopeAmbiguous, ScopeCountryInvalid, ScopeReferenceSetUnpublished,
        ScopeCountryUnknown, ScopeBusinessUnitUnknown, ScopeLegalEntityNotReferenceable,
        ScopeLegalEntityValidationUnavailable
    };
}

/// <summary>Hard ceilings on document growth and on the cross-service call fan-out. Every overflow is an explicit
/// 400/422; silent truncation is forbidden.</summary>
public static class StrategyTemplateLimits
{
    public const int MaxSegmentBindings = 20;
    public const int MaxProductLines = 50;
    public const int MaxSkuAllocationsPerLine = 50;
    public const int MaxContentBindings = 50;

    /// <summary>Maximum distinct MDM references proven in one write. Beyond it the answer is 422: the risk of this FU is
    /// the number of cross-service calls, not the document size.</summary>
    public const int MaxReferenceFanout = 100;

    /// <summary>Ceiling for the reverse question "which templates bind this segment?" (422 beyond).</summary>
    public const int MaxTemplatesPerSegment = 200;

    public const int MaxTemplateCodeLength = 64;
    public const int MaxTemplateNameLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MaxNotesLength = 2000;
    public const int MaxBindingNotesLength = 500;
    public const int MaxIntentNoteLength = 1000;
    public const int MaxBusinessUnitIdLength = 64;
    public const int MaxRequiredVisitCount = 365;

    /// <summary>Percentages are two-decimal and total EXACTLY this. No tolerance band: a tolerance decides, silently,
    /// which row absorbs the rounding.</summary>
    public const decimal RequiredAllocationTotal = 100.00m;
    public const int PercentageScale = 2;
}
