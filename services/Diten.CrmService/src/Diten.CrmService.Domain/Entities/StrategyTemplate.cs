namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0167 FU04 — <b>StrategyTemplate</b> aggregate root: a reusable, named BUNDLE OF BINDINGS ("the standard play we
/// run for cardiology A-segment"). Its one architectural rule is that it <b>binds, it does not produce</b>: no member,
/// no <c>VisitFrequencyPolicy</c>, no <c>CampaignTarget</c>, no cycle row and no MicroTarget is ever born from a
/// template. Applying a play to a period is MOD-0155 (MicroTarget), and this FU deliberately has no
/// <c>apply</c> / <c>generate</c> endpoint at all.
/// <para>The four bindings answer four questions owned elsewhere: <see cref="SegmentBindings"/> = who
/// (MOD-0167 FU02, read-only), <see cref="FrequencyIntent"/> = how often (MOD-0165 — REFERENCED or DECLARED, never
/// written), <see cref="ProductLines"/> = what is promoted (MDM GlobalProduct + Gsku, proven cross-service before any
/// write) and <see cref="ContentBindings"/> = which story (MOD-0162 KnowledgePath / ContentEngagementJourney,
/// read-only, published + pinned).</para>
/// <para><b>Versioning (D-VER):</b> <see cref="TemplateVersion"/> is the BUSINESS version and is never confused with
/// <see cref="EntityBase.Version"/> (the technical concurrency token). Activating a template FREEZES its bindings
/// (<see cref="BindingsFrozenAt"/>); changing a binding means a <c>new-version</c> clone with fresh child ids. A
/// superseded version stays readable so a past play can still be explained.</para>
/// <para>Tenant-owned (<see cref="EntityBase"/>); TenantId is server-resolved and never accepted from a payload. There
/// is no hard delete — closing a template is the soft archive lifecycle. Brand is deliberately absent: the product does
/// not use it, and a nullable FK nobody fills is a permanent lie in the data model (D-BRAND).</para>
/// </summary>
public sealed class StrategyTemplate : EntityBase
{
    /// <summary>Stable business key, unique within the tenant among non-archived rows. Never renamed (rename the name).</summary>
    public string TemplateCode { get; set; } = string.Empty;

    public string TemplateName { get; set; } = string.Empty;

    /// <summary><see cref="StrategyTemplateSubjectTypes"/> — what the play targets. IMMUTABLE after create, and every
    /// bound segment must match it: otherwise one play would target both accounts and people and no consumer could tell
    /// which dimension it is working in.</summary>
    public string SubjectType { get; set; } = StrategyTemplateSubjectTypes.Contact;

    /// <summary><see cref="StrategyTemplateStatuses"/> — draft / active / archived.</summary>
    public string TemplateStatus { get; set; } = StrategyTemplateStatuses.Draft;

    /// <summary>BUSINESS version (first = 1). NOT <see cref="EntityBase.Version"/>.</summary>
    public int TemplateVersion { get; set; } = 1;

    /// <summary>Root identity binding every version of the same template. Equals <see cref="EntityBase.Id"/> on v1.</summary>
    public Guid VersionLineageId { get; set; }

    /// <summary>Filled server-side when a newer version is activated. A superseded version stays readable.</summary>
    public Guid? SupersededByTemplateId { get; set; }

    /// <summary>Opaque MOD-0048 business-unit code (non-empty string check only; no master read).</summary>
    public string? BusinessUnitId { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>Open-ended when null. EffectiveFrom/EffectiveTo are DateTimeOffset (BSON array): never both index keys
    /// and never sorted together server-side (the parallel-array trap).</summary>
    public DateTimeOffset? EffectiveTo { get; set; }

    /// <summary>WHO the play is for. At least one binding: a play without a segment leaves "who?" unanswered.</summary>
    public List<StrategyTemplateSegmentBinding> SegmentBindings { get; set; } = new();

    /// <summary>HOW OFTEN — always present as an object, because <c>none</c> is an ANSWER and a missing field is a
    /// silent assumption. It never writes a policy in any mode.</summary>
    public StrategyTemplateFrequencyIntent FrequencyIntent { get; set; } = new();

    /// <summary>WHAT is promoted. Empty means the play has no product dimension; one is never invented.</summary>
    public List<StrategyTemplateProductLine> ProductLines { get; set; } = new();

    /// <summary>WHICH story is told. Empty means the play has no content dimension.</summary>
    public List<StrategyTemplateContentBinding> ContentBindings { get; set; } = new();

    public string? Notes { get; set; }

    /// <summary>Stamped at <c>activate</c>. While set, every binding list is frozen (409 on any binding change);
    /// metadata such as the name or the notes stays editable.</summary>
    public DateTimeOffset? BindingsFrozenAt { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }
    public string? ActivatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null
        || string.Equals(TemplateStatus, StrategyTemplateStatuses.Archived, StringComparison.Ordinal);

    public bool IsActive() => !IsArchived()
        && string.Equals(TemplateStatus, StrategyTemplateStatuses.Active, StringComparison.Ordinal);

    public bool AreBindingsFrozen() => BindingsFrozenAt is not null;

    public bool IsSuperseded() => SupersededByTemplateId is not null;

    /// <summary>Effective at the instant. Read-only helper; produces nothing and decides nothing.</summary>
    public bool IsEffectiveAt(DateTimeOffset at)
        => EffectiveFrom <= at && (EffectiveTo is null || at <= EffectiveTo);
}

/// <summary>
/// MOD-0167 FU04 — one bound MOD-0167 FU02 segment ("who"). <see cref="SegmentId"/> points at a CONCRETE segment row,
/// which in FU02 means a concrete VERSION (each version is its own document): pinning the id is pinning the version, so
/// a play can never silently start being about a different population. The lineage id and the business version are
/// stamped at binding time for traceability only — the segment itself stays the source of truth.
/// <para><see cref="BindingRole"/> is a LABEL and nothing else. No handler branches on it, and no set algebra (union,
/// intersect, minus) is applied to the list: a bound list is an enumeration, not an expression. Adding algebra here
/// would quietly turn this aggregate into a second membership language beside FU02's criteria tree.</para>
/// </summary>
public sealed class StrategyTemplateSegmentBinding
{
    public Guid BindingId { get; set; } = Guid.NewGuid();

    /// <summary>MOD-0167 FU02 <c>Segment.Id</c> — a specific version row (D-SEG).</summary>
    public Guid SegmentId { get; set; }

    /// <summary>Stamped from the segment when bound; traceability only.</summary>
    public Guid SegmentLineageId { get; set; }

    /// <summary>Stamped from the segment when bound; never refreshed, so drift stays visible.</summary>
    public int SegmentVersionAtBinding { get; set; }

    /// <summary>Display/audit only. Explicitly NOT the source of truth for the code.</summary>
    public string? SegmentCodeDisplay { get; set; }

    /// <summary><see cref="StrategySegmentBindingRoles"/> — a label with no behaviour. Even <c>exclusion-note</c>
    /// excludes nothing.</summary>
    public string? BindingRole { get; set; }

    /// <summary>Unique inside the template; part of the deterministic read order.</summary>
    public int SortOrder { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// MOD-0167 FU04 — the frequency INTENT ("how often"). This is <b>not a policy and it never becomes one</b>:
/// MOD-0165 owns <c>VisitFrequencyPolicy</c>, and no code path in this FU inserts or replaces one.
/// <list type="bullet">
/// <item><c>policy-reference</c> — points at an existing ACTIVE policy. The strongest binding: the real system of
/// record answers.</item>
/// <item><c>declared-intent</c> — states the rhythm with MOD-0165's own vocabulary constants so it is machine-readable,
/// while being explicitly NON-BINDING: the MOD-0165 resolve provider does not read it and will not.</item>
/// <item><c>none</c> — a legitimate answer. Not every play carries a rhythm.</item>
/// </list>
/// A mixed shape (a policy AND a declaration) is rejected, because deciding which one wins would require a conflict
/// resolver — that is an engine, and this FU has none.
/// </summary>
public sealed class StrategyTemplateFrequencyIntent
{
    /// <summary><see cref="StrategyFrequencyIntentModes"/>.</summary>
    public string Mode { get; set; } = StrategyFrequencyIntentModes.None;

    /// <summary>MOD-0165 <c>VisitFrequencyPolicy.Id</c>. Required for <c>policy-reference</c>, empty otherwise.</summary>
    public Guid? VisitFrequencyPolicyId { get; set; }

    /// <summary>Display only; the policy is the source of truth.</summary>
    public string? PolicyCodeDisplay { get; set; }

    /// <summary>MOD-0165 <c>FrequencyType</c> value. Required for <c>declared-intent</c>, empty otherwise.</summary>
    public string? FrequencyType { get; set; }

    /// <summary>Visits per period. Required for <c>declared-intent</c>; must be greater than zero.</summary>
    public int? RequiredVisitCount { get; set; }

    /// <summary>MOD-0165 <c>FrequencyPeriodType</c> value. Required for <c>declared-intent</c>, empty otherwise.</summary>
    public string? PeriodType { get; set; }

    public string? IntentNote { get; set; }

    public bool IsPolicyReference()
        => string.Equals(Mode, StrategyFrequencyIntentModes.PolicyReference, StringComparison.Ordinal);

    public bool IsDeclaredIntent()
        => string.Equals(Mode, StrategyFrequencyIntentModes.DeclaredIntent, StringComparison.Ordinal);
}

/// <summary>
/// MOD-0167 FU04 — one promoted MDM product and, optionally, how it splits across SKUs. This is the legacy CrmV2
/// <c>SubjectList</c> in its real role (brand + SKU percentage plan), minus the brand: the product does not use brands
/// (D-BRAND), so the line binds an MDM <c>GlobalProduct</c> directly.
/// <para><see cref="SkuAllocationMode"/> exists so a product-level play can say so honestly instead of pretending to be
/// a SKU split with zero rows.</para>
/// </summary>
public sealed class StrategyTemplateProductLine
{
    public Guid LineId { get; set; } = Guid.NewGuid();

    /// <summary>MDM <c>GlobalProduct.Id</c> — proven to exist cross-service BEFORE any write.</summary>
    public Guid GlobalProductId { get; set; }

    /// <summary>Display only; MDM stays the source of truth for the code and the name.</summary>
    public string? GlobalProductCodeDisplay { get; set; }

    /// <summary>Weight of this line among the lines. Either EVERY line carries one (and they total exactly 100.00) or
    /// none does: a half-specified weighting is worse than none, because the missing half reads as zero.</summary>
    public decimal? LineWeightPercentage { get; set; }

    /// <summary><see cref="StrategySkuAllocationModes"/>.</summary>
    public string SkuAllocationMode { get; set; } = StrategySkuAllocationModes.ProductOnly;

    /// <summary>The SKU split. Non-empty exactly when the mode is <c>sku-allocated</c>, and then the percentages total
    /// exactly 100.00 — no tolerance, no silent normalisation.</summary>
    public List<StrategyTemplateSkuAllocation> SkuAllocations { get; set; } = new();

    public int SortOrder { get; set; }

    public string? Notes { get; set; }

    public bool IsSkuAllocated()
        => string.Equals(SkuAllocationMode, StrategySkuAllocationModes.SkuAllocated, StringComparison.Ordinal);
}

/// <summary>
/// MOD-0167 FU04 — one SKU share of a product line (legacy <c>SkuAllocation</c>: SubjectListId + SkuId + Percentage).
/// <para><b>Deliberate honesty gap (D-SKU-LINK):</b> whether this Gsku actually belongs to the line's GlobalProduct is
/// NOT verified. MDM's <c>Gsku</c> carries a <c>ProductDefinitionRevisionId</c> and no <c>GlobalProductId</c>, and its
/// selector offers no product filter; opening a new MDM read surface is out of scope here. Both ids are proven to
/// EXIST, the containment is the author's responsibility, and the contract says so out loud
/// (<c>containmentVerified: false</c>) instead of implying a check that never runs.</para>
/// </summary>
public sealed class StrategyTemplateSkuAllocation
{
    public Guid AllocationId { get; set; } = Guid.NewGuid();

    /// <summary>MDM <c>Gsku.Id</c> — proven to exist cross-service BEFORE any write.</summary>
    public Guid GskuId { get; set; }

    /// <summary>Display only; MDM stays the source of truth for the canonical code.</summary>
    public string? GskuCanonicalCodeDisplay { get; set; }

    /// <summary>Share of the line, two decimals, greater than zero and at most 100. Stored exactly as authored.</summary>
    public decimal Percentage { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>
/// MOD-0167 FU04 — one bound MOD-0162 presentation ("which story"). The reference is TYPED, because a bare content id
/// cannot be resolved, and it is PINNED to a concrete published row: a draft cannot be promised to the field, and a
/// later version of the same code is a different story (the MOD-0162 FU05 pinned-published rule).
/// </summary>
public sealed class StrategyTemplateContentBinding
{
    public Guid BindingId { get; set; } = Guid.NewGuid();

    /// <summary><see cref="StrategyContentRefTypes"/> — knowledge-path or content-engagement-journey.</summary>
    public string ContentRefType { get; set; } = string.Empty;

    /// <summary><c>KnowledgePath.Id</c> or <c>ContentEngagementJourney.Id</c> — a concrete published row.</summary>
    public Guid ContentRefId { get; set; }

    /// <summary>Display only: <c>PathCode</c> / <c>JourneyCode</c>.</summary>
    public string? ContentCodeDisplay { get; set; }

    /// <summary>Display only: the BUSINESS version (<c>PathVersion</c> / <c>JourneyVersion</c>), never the concurrency
    /// token.</summary>
    public string? ContentVersionAtBinding { get; set; }

    public int SortOrder { get; set; }

    public string? Notes { get; set; }

    public bool IsKnowledgePath()
        => string.Equals(ContentRefType, StrategyContentRefTypes.KnowledgePath, StringComparison.Ordinal);

    public bool IsEngagementJourney()
        => string.Equals(ContentRefType, StrategyContentRefTypes.ContentEngagementJourney, StringComparison.Ordinal);
}
