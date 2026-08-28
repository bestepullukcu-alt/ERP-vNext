namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0165 FU04 — Campaign. Answers <b>one</b> question: "what is this campaign, for which objective, over which
/// period, and in which business context?" It deliberately does NOT answer "who is in it?" (that is
/// <see cref="CampaignTarget"/>), "may we contact them?" (MOD-0164), "how often?" (MOD-0165 FU03 VisitFrequencyPolicy),
/// "when/which route?" (MOD-0155), "what to show?" (MOD-0162) or "who is in the segment?" (MOD-0167).
/// <para>
/// A campaign is <b>not a master</b>. Every Brand / Product / Subject / Topic / Concept / Journey / Path / Content
/// field on it is a <b>reference</b> — the referenced master's name, code or payload is never copied here, because a
/// copy goes stale the moment the master changes. Frequency, consent and route state are likewise never flattened
/// onto a campaign.
/// </para>
/// <para>
/// <see cref="EntityBase.Id"/> is the CampaignId and <see cref="CampaignCode"/> is the stable business key (rename is
/// done through <see cref="CampaignName"/> only). Closing a campaign is the soft <see cref="ArchivedAt"/> lifecycle;
/// there is no hard delete, and an archived campaign accepts no target mutation.
/// </para>
/// </summary>
public sealed class Campaign : EntityBase
{
    /// <summary>Stable business key, unique per tenant among non-archived campaigns. Never renamed.</summary>
    public string CampaignCode { get; set; } = string.Empty;

    public string CampaignName { get; set; } = string.Empty;

    /// <summary><see cref="CampaignTypes"/> — product / education / awareness / service / compliance / training / other.</summary>
    public string CampaignType { get; set; } = string.Empty;

    /// <summary><see cref="CampaignStatuses"/> — draft / active / paused / completed / cancelled / archived.</summary>
    public string CampaignStatus { get; set; } = CampaignStatuses.Draft;

    /// <summary><see cref="CampaignObjectiveTypes"/> — optional; absent means the objective was not authored, and no
    /// objective is ever invented.</summary>
    public string? ObjectiveType { get; set; }

    /// <summary>
    /// MOD-0165 FU10 — HOW this campaign is targeted: <see cref="CampaignTargetingModes"/> —
    /// <c>segment</c> (targeted segments) or <c>manual</c> (hand-authored <see cref="CampaignTarget"/> rows).
    ///
    /// <para><b>A deliberate mirror of the segment's own static/dynamic switch.</b> There, the type decides whether
    /// membership comes from criteria or from a manual list, and the surface that cannot apply is hidden rather than
    /// disabled. Here the mode decides whether the audience comes from segments or from hand-authored rows, and the
    /// same rule holds: only the ACTIVE mode's data is validated and used.</para>
    ///
    /// <para><b>Switching the mode never destroys data.</b> The passive mode's rows stay exactly where they are, so a
    /// campaign switched back finds its earlier work intact. What the passive mode does NOT accept is NEW data — a
    /// mode that only steered the UI would be a convention, and a direct API call would walk straight past it.</para>
    ///
    /// <para>Rows written before FU10 carry no value here and are NOT migrated: <see cref="EffectiveTargetingMode"/>
    /// reads them as <c>manual</c>, which is the only way targeting existed at the time.</para>
    /// </summary>
    public string TargetingMode { get; set; } = string.Empty;

    /// <summary>
    /// MOD-0165 FU10 — the segments this campaign targets, when <see cref="TargetingMode"/> is <c>segment</c>.
    ///
    /// <para><b>Intent, not audience.</b> It records WHO the campaign aims at; it does not resolve who is actually in
    /// those segments. No <see cref="CampaignTarget"/> row is produced here, no consent is evaluated, and no snapshot
    /// runs — turning targeted segments into an audience is a separate follow-up.</para>
    ///
    /// <para><b>A pinned segment VERSION, not a lineage.</b> Each entry names one concrete segment id. A newer
    /// version of the same segment does not change what this campaign targets; the superseded state is surfaced so an
    /// author can move it deliberately. Only the id is kept — a copied code or name goes stale the moment the segment
    /// is renamed.</para>
    ///
    /// <para>Dormant while the mode is <c>manual</c>: kept, not validated, not used.</para>
    /// </summary>
    public List<CampaignTargetedSegment> TargetedSegments { get; set; } = new();

    /// <summary>
    /// MOD-0165 FU09 — which LEVEL this campaign lives at: <see cref="CampaignScopeTypes"/> (tenant / country /
    /// legal-entity / business-unit). Exactly one of <see cref="CountryScope"/> / <see cref="LegalEntityId"/> /
    /// <see cref="BusinessUnitId"/> carries the reference; none of them at <c>tenant</c>.
    /// <para><b>Unlike a cycle period's scope, this one is NOT identity — it is EDITABLE.</b> A period at the wrong
    /// address is closed and reopened because MicroTarget rows point at it by id; a campaign filed under the wrong
    /// business unit is simply corrected. What editing does trigger is a re-check of the bound cycle period: a period
    /// that is no longer applicable refuses the write instead of being silently unbound.</para>
    /// <para>Rows written before FU09 carry no value here and are NOT migrated: <see cref="EffectiveScopeType"/>
    /// derives it on read (a business unit -> business-unit, otherwise -> tenant), which is exactly the context those
    /// campaigns already had.</para>
    /// <para><b>Scope is DATA, not authorization.</b> It says where a campaign lives; it never says who may see it. No
    /// read is filtered by it and no permission is derived from it.</para>
    /// </summary>
    public string ScopeType { get; set; } = string.Empty;

    /// <summary>FU09 — the reference when <see cref="ScopeType"/> is <c>country</c>: an ISO alpha-2 code from the
    /// governed MOD-0048 country set, upper-cased so "TR" and "tr" can never become two addresses. Null at every other
    /// scope type.</summary>
    public string? CountryScope { get; set; }

    /// <summary>FU09 — the reference when <see cref="ScopeType"/> is <c>legal-entity</c>: an MDM legal entity proved
    /// referenceable through a fail-closed cross-service check BEFORE anything is persisted. Only the id is kept — a
    /// copied name would go stale the moment MDM changes it. Null at every other scope type.</summary>
    public Guid? LegalEntityId { get; set; }

    /// <summary>
    /// The reference when <see cref="ScopeType"/> is <c>business-unit</c> — a MOD-0048 published
    /// <c>business-unit</c> value code.
    /// <para><b>FU09 narrowed what this field means.</b> FU04 stored it as an opaque context code validated only as a
    /// non-empty string; it is now the business-unit scope reference and is validated against the same published set
    /// MOD-0151 Territory validates against, so a business-unit code means one thing across CRM.</para>
    /// <para><b>The narrowing is applied only when the value CHANGES.</b> An existing campaign carrying a code that
    /// predates the governed set keeps working — a campaign must not become uneditable because someone wants to fix
    /// its description. The rule engages the moment an author touches the reference itself.</para>
    /// </summary>
    public string? BusinessUnitId { get; set; }

    /// <summary>
    /// MOD-0165 FU08 — the planning PERIOD this campaign belongs to (<see cref="CyclePeriod"/>), or <c>null</c> when
    /// the campaign is not cycle-bound. Optional by design: most campaigns never belong to a cycle.
    /// <para><b>A pin, and only a pin.</b> The direction is one-way — a campaign points at a period, and a period
    /// neither knows nor lists its campaigns. Only the ID is kept: the period's code, name and window are never copied
    /// here, because a copy goes stale the moment the period is renamed or re-dated.</para>
    /// <para><b>While bound, one rule holds:</b> the campaign window must be CONTAINED in the period window, both ends
    /// inclusive, compared on the canonical UTC day. The campaign's own <see cref="StartDate"/> /
    /// <see cref="EndDate"/> are never derived from, filled from or updated by the period — they stay the campaign's
    /// own truth, and the containment is checked rather than imposed.</para>
    /// <para><b>Binding requires an ACTIVE period; a period that CLOSES afterwards keeps its bindings.</b> Closing a
    /// period changes no campaign: no cascade, no archive, no date clipping. That is why the active check fires only
    /// when the binding itself changes — otherwise every campaign bound to a period would become uneditable the day
    /// that period closed.</para>
    /// <para>Scope is deliberately NOT matched: the campaign's <see cref="BusinessUnitId"/> is not compared against the
    /// period's scope, so a campaign may be bound to a period filed at a different address. Campaign scope is a
    /// separate follow-up and is not silently implied here.</para>
    /// </summary>
    public Guid? CyclePeriodId { get; set; }

    /// <summary>Optional MOD-0290 brand reference. Absent for non-pharma campaigns, which stay fully valid.</summary>
    /// <summary><b>DEPRECATED (MOD-0165 FU10).</b> No longer authored, no longer projected onto any DTO. The stored
    /// values are left exactly as they were — there is no migration and nothing rewrites them — because "what to
    /// promote" is answered per targeted segment and belongs to a separate model (MicroTarget / StrategyTemplate),
    /// not to the campaign. Removing the field itself waits until that successor is in place.</summary>
    public Guid? BrandId { get; set; }

    /// <summary>Optional MOD-0290 product reference.</summary>
    /// <summary><b>DEPRECATED (MOD-0165 FU10).</b> No longer authored, no longer projected onto any DTO. The stored
    /// values are left exactly as they were — there is no migration and nothing rewrites them — because "what to
    /// promote" is answered per targeted segment and belongs to a separate model (MicroTarget / StrategyTemplate),
    /// not to the campaign. Removing the field itself waits until that successor is in place.</summary>
    public Guid? ProductId { get; set; }

    /// <summary>Optional MOD-0162 subject reference.</summary>
    /// <summary><b>DEPRECATED (MOD-0165 FU10).</b> No longer authored, no longer projected onto any DTO. The stored
    /// values are left exactly as they were — there is no migration and nothing rewrites them — because "what to
    /// promote" is answered per targeted segment and belongs to a separate model (MicroTarget / StrategyTemplate),
    /// not to the campaign. Removing the field itself waits until that successor is in place.</summary>
    public Guid? SubjectId { get; set; }

    /// <summary>Optional MOD-0162 topic reference.</summary>
    /// <summary><b>DEPRECATED (MOD-0165 FU10).</b> No longer authored, no longer projected onto any DTO. The stored
    /// values are left exactly as they were — there is no migration and nothing rewrites them — because "what to
    /// promote" is answered per targeted segment and belongs to a separate model (MicroTarget / StrategyTemplate),
    /// not to the campaign. Removing the field itself waits until that successor is in place.</summary>
    public Guid? TopicId { get; set; }

    /// <summary>Optional MOD-0162-FU01C concept chain template reference. No concept graph is traversed here.</summary>
    /// <summary><b>DEPRECATED (MOD-0165 FU10).</b> No longer authored, no longer projected onto any DTO. The stored
    /// values are left exactly as they were — there is no migration and nothing rewrites them — because "what to
    /// promote" is answered per targeted segment and belongs to a separate model (MicroTarget / StrategyTemplate),
    /// not to the campaign. Removing the field itself waits until that successor is in place.</summary>
    public Guid? ConceptChainTemplateId { get; set; }

    /// <summary>Optional MOD-0162-FU01B engagement journey reference. No journey runtime is opened here.</summary>
    /// <summary><b>DEPRECATED (MOD-0165 FU10).</b> No longer authored, no longer projected onto any DTO. The stored
    /// values are left exactly as they were — there is no migration and nothing rewrites them — because "what to
    /// promote" is answered per targeted segment and belongs to a separate model (MicroTarget / StrategyTemplate),
    /// not to the campaign. Removing the field itself waits until that successor is in place.</summary>
    public Guid? EngagementJourneyId { get; set; }

    /// <summary>Optional MOD-0162-FU01A knowledge path reference (a default suggestion, not a sequence definition).</summary>
    /// <summary><b>DEPRECATED (MOD-0165 FU10).</b> No longer authored, no longer projected onto any DTO. The stored
    /// values are left exactly as they were — there is no migration and nothing rewrites them — because "what to
    /// promote" is answered per targeted segment and belongs to a separate model (MicroTarget / StrategyTemplate),
    /// not to the campaign. Removing the field itself waits until that successor is in place.</summary>
    public Guid? DefaultKnowledgePathId { get; set; }

    /// <summary>Optional MOD-0162-FU01 knowledge content reference.</summary>
    /// <summary><b>DEPRECATED (MOD-0165 FU10).</b> No longer authored, no longer projected onto any DTO. The stored
    /// values are left exactly as they were — there is no migration and nothing rewrites them — because "what to
    /// promote" is answered per targeted segment and belongs to a separate model (MicroTarget / StrategyTemplate),
    /// not to the campaign. Removing the field itself waits until that successor is in place.</summary>
    public Guid? DefaultKnowledgeContentId { get; set; }

    /// <summary>Default consent channel used by the snapshot when the request omits one (<see cref="ConsentChannel"/>).
    /// Optional and NEVER defaulted: if neither the campaign nor the request supplies a channel, a consent-filtered
    /// snapshot is rejected rather than run against a guessed channel.</summary>
    public string? DefaultConsentChannel { get; set; }

    /// <summary>Default consent purpose used by the snapshot when the request omits one (<see cref="ConsentPurpose"/>).
    /// Same rule: optional, never invented.</summary>
    public string? DefaultConsentPurpose { get; set; }

    public DateTimeOffset StartDate { get; set; }

    /// <summary>Open-ended when null.</summary>
    public DateTimeOffset? EndDate { get; set; }

    public string? Description { get; set; }

    /// <summary><b>DEPRECATED (MOD-0165 FU10).</b> No longer authored, no longer projected onto any DTO. The stored
    /// values are left exactly as they were — there is no migration and nothing rewrites them — because "what to
    /// promote" is answered per targeted segment and belongs to a separate model (MicroTarget / StrategyTemplate),
    /// not to the campaign. Removing the field itself waits until that successor is in place.</summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary><b>DEPRECATED (MOD-0165 FU10).</b> No longer authored or projected. The stored mappings are kept and
    /// the duplicate-mapping guard is kept with them: nothing writes external references any more, so the guard never
    /// fires, but deleting a working guard is expensive to undo when an integration surface returns.</summary>
    public List<CampaignExternalReference> ExternalReferences { get; set; } = new();

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;

    /// <summary>Effective at a given instant: StartDate ≤ at ≤ EndDate (open end when EndDate is null). Read-only
    /// helper for consumers; this class draws no visit/route conclusion from it.</summary>
    public bool IsEffectiveAt(DateTimeOffset at)
        => StartDate <= at && (EndDate is null || at <= EndDate);

    /// <summary>
    /// FU10 — <see cref="TargetingMode"/>, or <c>manual</c> for a row written before the field existed.
    ///
    /// <para>Read-only and idempotent: it never writes, so a pre-FU10 campaign keeps behaving exactly as it did until
    /// something else edits it. There is no backfill script anywhere.</para>
    ///
    /// <para>The derivation is uniform on purpose. Deriving <c>segment</c> for a campaign that happens to have no
    /// manual targets would be worse than useless: <c>segment</c> mode requires at least one targeted segment, so
    /// every such campaign would become invalid the moment it was read — a derivation must never make an existing
    /// record unsaveable.</para>
    /// </summary>
    public string EffectiveTargetingMode()
        => CampaignTargetingModes.IsKnown(TargetingMode)
            ? CampaignTargetingModes.Normalize(TargetingMode)
            : CampaignTargetingModes.Manual;

    /// <summary>FU10 — is this campaign targeted through segments right now?</summary>
    public bool IsSegmentTargeted()
        => string.Equals(EffectiveTargetingMode(), CampaignTargetingModes.Segment, StringComparison.Ordinal);

    /// <summary>
    /// FU09 — the reference belonging to <see cref="ScopeType"/>, normalised. <c>null</c> for the tenant scope, which
    /// is a scope of its OWN rather than the absence of one.
    /// </summary>
    public string? ScopeRef() => EffectiveScopeType() switch
    {
        CampaignScopeTypes.Country => NormalizeScopeValue(CountryScope)?.ToUpperInvariant(),
        CampaignScopeTypes.LegalEntity => LegalEntityId?.ToString("D"),
        CampaignScopeTypes.BusinessUnit => NormalizeScopeValue(BusinessUnitId),
        _ => null
    };

    /// <summary>
    /// FU09 — <see cref="ScopeType"/>, or the pre-FU09 scope derived from <see cref="BusinessUnitId"/> when the row
    /// predates the field. Read-only and idempotent: it never writes, so a legacy campaign keeps behaving exactly as
    /// it did until something else edits it. There is no backfill script anywhere.
    /// </summary>
    public string EffectiveScopeType()
        => CampaignScopeTypes.IsKnown(ScopeType)
            ? CampaignScopeTypes.Normalize(ScopeType)
            : NormalizeScopeValue(BusinessUnitId) is null
                ? CampaignScopeTypes.Tenant
                : CampaignScopeTypes.BusinessUnit;

    /// <summary>
    /// FU09 invariant: exactly the reference belonging to <see cref="ScopeType"/> is present and the other two are
    /// null (all three null for <c>tenant</c>). Every pre-FU09 row satisfies this too.
    /// </summary>
    public bool HasConsistentScope()
    {
        var hasCountry = NormalizeScopeValue(CountryScope) is not null;
        var hasLegalEntity = LegalEntityId is { } id && id != Guid.Empty;
        var hasBusinessUnit = NormalizeScopeValue(BusinessUnitId) is not null;

        return EffectiveScopeType() switch
        {
            CampaignScopeTypes.Tenant => !hasCountry && !hasLegalEntity && !hasBusinessUnit,
            CampaignScopeTypes.Country => hasCountry && !hasLegalEntity && !hasBusinessUnit,
            CampaignScopeTypes.LegalEntity => hasLegalEntity && !hasCountry && !hasBusinessUnit,
            CampaignScopeTypes.BusinessUnit => hasBusinessUnit && !hasCountry && !hasLegalEntity,
            _ => false
        };
    }

    private static string? NormalizeScopeValue(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// MOD-0165 FU10 — one targeted segment reference. Carries the id and when it was linked, and NOTHING else: the
/// segment's code, name and subject type are read from the segment on every read, because a copy goes stale.
/// </summary>
public sealed class CampaignTargetedSegment
{
    /// <summary>The pinned segment — a concrete VERSION, not a lineage (see <c>Campaign.TargetedSegments</c>).</summary>
    public Guid SegmentId { get; set; }

    /// <summary>When the link was made. Provenance for a reader; no rule branches on it.</summary>
    public DateTimeOffset LinkedAt { get; set; }
}

/// <summary>
/// MOD-0165 FU10 — how a campaign is targeted. In-domain and fail-closed: an unknown value is refused (400) and is
/// never quietly read as one of the two, exactly like the segment's own type vocabulary.
/// <para>There is deliberately no <c>hybrid</c>. The segment has one because a membership list and a rule can
/// genuinely coexist; here, "some of the audience comes from segments and some was typed in" has no owner yet and
/// would make "who is in this campaign?" ambiguous. A third mode waits for a real need.</para>
/// </summary>
public static class CampaignTargetingModes
{
    /// <summary>Audience comes from <c>Campaign.TargetedSegments</c>. Manual target rows are refused while active.</summary>
    public const string Segment = "segment";

    /// <summary>Audience is hand-authored <see cref="CampaignTarget"/> rows (the pre-FU10 way, fully preserved).</summary>
    public const string Manual = "manual";

    public static readonly IReadOnlyList<string> All = new[] { Segment, Manual };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>Published ceilings for the FU10 campaign write path, so a UI needs no hardcoded limit.</summary>
public static class CampaignLimits
{
    /// <summary>How many segments one campaign may target. A ceiling exists because an unbounded list makes the
    /// batch read — and, later, the snapshot that resolves it — unbounded too.</summary>
    public const int MaxTargetedSegments = 50;
}

/// <summary>
/// MOD-0165 FU09 — the levels a campaign can live at. A deliberate MIRROR of the cycle period's scope levels: the same
/// four names, the same precedence, so a reader of both modules learns ONE mental model.
/// <para><b>Mirrored, not shared.</b> No code is imported from the cycle-period rules, because the two scopes do not
/// mean the same thing: a period's scope is its IDENTITY and immutable, a campaign's is an editable attribute. Sharing
/// one implementation would forbid a divergence that is already true. Consolidating them is a documented follow-up, and
/// a behaviour-equivalence test keeps the two honest in the meantime.</para>
/// <para>In-domain and fail-closed: an unknown value is refused (400), never quietly read as <see cref="Tenant"/>.
/// CRM has no <c>organization-unit</c> level, exactly as the cycle period has none.</para>
/// </summary>
public static class CampaignScopeTypes
{
    /// <summary>The whole tenant. A scope of its OWN, not the absence of one.</summary>
    public const string Tenant = "tenant";

    /// <summary>One country, referenced by an ISO alpha-2 code from the governed reference set.</summary>
    public const string Country = "country";

    /// <summary>One MDM legal entity, referenced by id and proved referenceable before persistence.</summary>
    public const string LegalEntity = "legal-entity";

    /// <summary>One business unit, referenced by a published MOD-0048 <c>business-unit</c> value code.</summary>
    public const string BusinessUnit = "business-unit";

    /// <summary>
    /// Resolution precedence, MOST SPECIFIC FIRST — the same order the cycle-period resolver walks. Defined once here;
    /// no second if/else chain restates it, because an order written twice is two orders.
    /// </summary>
    public static readonly IReadOnlyList<string> ByPrecedence =
        new[] { BusinessUnit, LegalEntity, Country, Tenant };

    public static readonly IReadOnlyList<string> All = ByPrecedence;

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>Published ceilings for the campaign scope write path, so a UI needs no hardcoded limit.</summary>
public static class CampaignScopeLimits
{
    /// <summary>ISO alpha-2, so exactly two characters.</summary>
    public const int CountryScopeLength = 2;

    public const int MaxBusinessUnitIdLength = 160;
}

/// <summary>
/// MOD-0165 FU04 — CampaignTarget. Answers "who (or what) is in this campaign, why, and with what provenance?".
/// <para>
/// It is <b>not master data and not a consent record</b>. <see cref="TargetId"/> is a resolution key only: no
/// account/contact/segment/territory field is copied onto it, and no consent or preference record content is stored on
/// it. What <i>may</i> be stored is the consent <b>evaluation result + provenance</b>
/// (<see cref="ConsentEvaluation"/>) so that "why is this target in or out?" is auditable without duplicating the
/// MOD-0164 store.
/// </para>
/// <para>
/// <see cref="SelectionReason"/> and <see cref="ReasonCodes"/> are mandatory: a silent or unexplained target
/// selection is forbidden. Closing a target is the soft <see cref="ArchivedAt"/> lifecycle; there is no hard delete,
/// and a snapshot never removes an earlier target.
/// </para>
/// </summary>
public sealed class CampaignTarget : EntityBase
{
    public Guid CampaignId { get; set; }

    /// <summary><see cref="CampaignTargetTypes"/>. Note that <c>campaign-target</c> is deliberately NOT a member — a
    /// campaign target pointing at a campaign target is a self-referential loop (MOD-0048 reconciliation F6).</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>Identity of the target within <see cref="TargetType"/>. Never empty. The referenced master is NOT read
    /// or mutated here — the caller supplies the id.</summary>
    public Guid TargetId { get; set; }

    /// <summary>Optional snapshot LABEL for display/audit only. Explicitly NOT a source of truth: the master may have
    /// been renamed since, and consumers must resolve the name from the owning master, never from here.</summary>
    public string? TargetDisplayName { get; set; }

    /// <summary><see cref="CampaignTargetStatuses"/> — draft / active / inactive / completed / excluded / archived.</summary>
    public string TargetStatus { get; set; } = CampaignTargetStatuses.Draft;

    /// <summary><see cref="CampaignTargetSources"/> — provenance of how this target got selected.</summary>
    public string TargetSource { get; set; } = string.Empty;

    /// <summary>What the target was derived FROM (e.g. <c>segment</c>). Provenance only.</summary>
    public string? SourceReferenceType { get; set; }

    /// <summary>Identity within <see cref="SourceReferenceType"/> (e.g. the segment id). Stored as provenance —
    /// segment membership is NEVER resolved or computed here (MOD-0167 boundary).</summary>
    public Guid? SourceReferenceId { get; set; }

    /// <summary>Groups every row produced by one snapshot run, so a batch stays auditable as a unit. Null for a
    /// manually authored target.</summary>
    public Guid? SnapshotBatchId { get; set; }

    /// <summary>Human-readable justification. MANDATORY — a target with no stated reason is not authorable.</summary>
    public string SelectionReason { get; set; } = string.Empty;

    /// <summary>Machine-readable justification (<see cref="CampaignReasonCodes"/>). MANDATORY and non-empty.</summary>
    public List<string> ReasonCodes { get; set; } = new();

    /// <summary>
    /// DEPRECATED (MOD-0165 FU11) - superseded by <see cref="PriorityLevel"/>. The field is KEPT and still read so
    /// that rows written before FU11 stay readable and stay meaningful; nothing was migrated. New writes leave it
    /// null. Its original contract was "deterministic ordering weight, smaller wins", which is why
    /// <see cref="DerivedPriorityLevel"/> maps 1 to <c>high</c> and not to <c>low</c>.
    /// <para>Removal is deliberately a separate decision (F-PRIORITY-INT-REMOVAL): dropping the field would erase the
    /// only record that those rows were ever prioritised.</para>
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// <see cref="CampaignTargetPriorityLevels"/> - <c>low</c> / <c>medium</c> / <c>high</c>. Null means no priority
    /// was stated, which is NOT the same as "low"; a band is never invented for a target whose author did not pick one.
    /// <para>Deliberately a NEW field rather than a retyped <see cref="Priority"/>: existing documents store an Int32
    /// under that element name and Mongo cannot deserialize an Int32 into a string, so reusing the name would break
    /// every read of pre-FU11 data.</para>
    /// </summary>
    public string? PriorityLevel { get; set; }

    /// <summary>
    /// The band to SHOW for this target: the stated band when there is one, otherwise the band derived from the
    /// deprecated integer under its own "smaller wins" contract (1 -> high, 2 -> medium, 3 and above -> low).
    /// <para>Read-time only. It never writes, and no backfill exists - an old row keeps its integer forever and is
    /// simply read as a band. Values above 3 collapse into <c>low</c>; that rounding is safe because no consumer has
    /// ever ordered by this field (verified across the repository), so the band is a label, not a sort key.</para>
    /// </summary>
    public string? DerivedPriorityLevel()
    {
        if (!string.IsNullOrWhiteSpace(PriorityLevel))
        {
            return CampaignTargetPriorityLevels.Normalize(PriorityLevel);
        }

        return Priority switch
        {
            null => null,
            <= 1 => CampaignTargetPriorityLevels.High,
            2 => CampaignTargetPriorityLevels.Medium,
            _ => CampaignTargetPriorityLevels.Low
        };
    }

    /// <summary>Read-only projection of a MOD-0164 evaluation: decision + provenance ONLY, never consent content.</summary>
    public CampaignTargetConsentEvaluation? ConsentEvaluation { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    /// <summary>Why the target is out of scope. MANDATORY whenever <see cref="TargetStatus"/> is
    /// <c>excluded</c> — silently dropping a target is forbidden.</summary>
    public string? ExclusionReason { get; set; }

    public string? Notes { get; set; }

    public List<CampaignExternalReference> ExternalReferences { get; set; } = new();

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;

    /// <summary>A target that still counts as a live campaign member: not archived and not in a closed status.</summary>
    public bool IsActiveMembership()
        => !IsArchived()
           && !string.Equals(TargetStatus, CampaignTargetStatuses.Archived, StringComparison.OrdinalIgnoreCase);

    public bool IsEffectiveAt(DateTimeOffset at)
        => EffectiveFrom <= at && (EffectiveTo is null || at <= EffectiveTo);
}

/// <summary>
/// Read-only provenance of one MOD-0164 consent evaluation, stored on a campaign target so that inclusion/exclusion is
/// auditable. <b>This is a decision record, not consent data.</b> It deliberately carries NO <c>ConsentStatus</c>,
/// NO <c>PreferenceStatus</c> and no consent/preference record payload — only the evaluator's verdict, the ids it
/// matched, and the question that was asked. MOD-0165 never writes to the MOD-0164 store.
/// </summary>
public sealed class CampaignTargetConsentEvaluation
{
    /// <summary>The evaluator's decision axis (MOD-0164 <c>ConsentDecision</c>), e.g. <c>consent_granted</c>.</summary>
    public string Decision { get; set; } = string.Empty;

    /// <summary>The evaluator's eligibility verdict (MOD-0164 <c>ConsentEligibilityStatus</c>):
    /// <c>allowed</c> / <c>blocked</c> / <c>unknown</c> / <c>not_applicable</c>. <b>unknown is never allowed.</b></summary>
    public string EligibilityStatus { get; set; } = string.Empty;

    public List<string> ReasonCodes { get; set; } = new();

    public DateTimeOffset EvaluatedAt { get; set; }

    /// <summary>Which consent record governed the decision. An ID reference — the record itself is not copied.</summary>
    public Guid? MatchedConsentId { get; set; }

    public List<Guid> MatchedPreferenceIds { get; set; } = new();

    /// <summary>MOD-0164 evaluator version, so a stored decision stays interpretable when the rules change.</summary>
    public string EvaluatorVersion { get; set; } = string.Empty;

    /// <summary>The evaluator's human-readable explanation of how it chose.</summary>
    public string SelectionReason { get; set; } = string.Empty;

    /// <summary>The question that was asked (provenance): which channel and purpose the decision applies to. A
    /// decision for one channel/purpose must never be read as covering another.</summary>
    public string? Channel { get; set; }

    public string? Purpose { get; set; }

    /// <summary>False when the snapshot deliberately ran without the consent filter. Kept explicit so an unfiltered
    /// target can never look like an evaluated one.</summary>
    public bool FilterApplied { get; set; }
}

/// <summary>
/// External/legacy identity carried by a campaign or campaign target. Same six-field contract as MOD-0290-FU01 /
/// MOD-0164-FU02 (<c>SourceSystem</c> · <c>ExternalId</c> · <c>ExternalCode</c> · <c>ExternalName</c> ·
/// <c>ImportedAt</c> · <c>IsPrimary</c>). It is declared separately from the MOD-0164 type on purpose: FU04 must not
/// edit MOD-0164 runtime code, and coupling two modules through a shared value object would do exactly that.
/// Unifying the three declarations is a documented follow-up.
/// </summary>
public sealed class CampaignExternalReference
{
    public string SourceSystem { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? ExternalCode { get; set; }
    public string? ExternalName { get; set; }
    public DateTimeOffset? ImportedAt { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>Campaign lifecycle. Hard delete does not exist. Structural (in-domain) vocabulary — validated here rather
/// than through MOD-0048, so the runtime never fails open on an unpublished set.</summary>
public static class CampaignStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Paused = "paused";
    public const string Completed = "completed";
    public const string Cancelled = "cancelled";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Draft, Active, Paused, Completed, Cancelled, Archived
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Draft : value.Trim().ToLowerInvariant();
}

/// <summary>What kind of campaign this is. In-domain (structural).</summary>
public static class CampaignTypes
{
    public const string ProductCampaign = "product-campaign";
    public const string EducationCampaign = "education-campaign";
    public const string AwarenessCampaign = "awareness-campaign";
    public const string ServiceCampaign = "service-campaign";
    public const string ComplianceCampaign = "compliance-campaign";
    public const string TrainingCampaign = "training-campaign";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        ProductCampaign, EducationCampaign, AwarenessCampaign, ServiceCampaign, ComplianceCampaign,
        TrainingCampaign, Other
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>Why the campaign exists. Optional on the aggregate; when supplied it must be a known value.</summary>
public static class CampaignObjectiveTypes
{
    public const string Awareness = "awareness";
    public const string Education = "education";
    public const string Conversion = "conversion";
    public const string Reinforcement = "reinforcement";
    public const string ObjectionHandling = "objection-handling";
    public const string Retention = "retention";
    public const string Compliance = "compliance";
    public const string Training = "training";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Awareness, Education, Conversion, Reinforcement, ObjectionHandling, Retention, Compliance, Training, Other
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>
/// What a campaign target can point at — the MOD-0048 <c>campaign-target-type</c> canonical set (reconciliation F6):
/// exactly seven values. <b><c>campaign-target</c> is deliberately absent</b> (self-referential loop), and this set is
/// deliberately NOT unified with <see cref="FrequencyTargetType"/> (<c>visit-frequency-target-type</c>), which does
/// contain <c>campaign-target</c> because a frequency policy legitimately targets a campaign target.
/// </summary>
public static class CampaignTargetTypes
{
    public const string Account = "account";
    public const string Contact = "contact";
    public const string AccountContactLink = "account-contact-link";
    public const string Segment = "segment";
    public const string TerritoryNode = "territory-node";
    public const string ConceptNode = "concept-node";
    public const string AudienceProfile = "audience-profile";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Account, Contact, AccountContactLink, Segment, TerritoryNode, ConceptNode, AudienceProfile
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;

    /// <summary>
    /// Whether a consent evaluation is meaningful for this target type. Only the three person/relationship-shaped
    /// types map onto a MOD-0164 consent subject. A <c>segment</c> / <c>territory-node</c> / <c>concept-node</c> /
    /// <c>audience-profile</c> target is a GROUP, not a subject: evaluating it would require resolving members, which
    /// is the MOD-0167 / MOD-0155 boundary, not FU04's. Such a target is reported
    /// <c>consent_evaluation_not_applicable</c> so the gap is visible instead of looking evaluated.
    /// </summary>
    public static bool SupportsConsentEvaluation(string? value) => Normalize(value) switch
    {
        Contact or AccountContactLink or Account => true,
        _ => false
    };
}

/// <summary>How a target got selected. Audit visible. In-domain (structural).</summary>
public static class CampaignTargetSources
{
    public const string Manual = "manual";
    public const string Segment = "segment";
    public const string Import = "import";
    public const string LegacyImport = "legacy-import";
    public const string BusinessRule = "business-rule";
    public const string ManagerSelection = "manager-selection";
    public const string CampaignRule = "campaign-rule";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Manual, Segment, Import, LegacyImport, BusinessRule, ManagerSelection, CampaignRule, Other
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}

/// <summary>
/// MOD-0165 FU11 - how important a target is, as a BAND rather than a free integer.
/// <para>The integer it replaces promised deterministic ordering and never delivered it: no consumer anywhere in the
/// platform ordered by it, while every author still had to invent a number. Three bands ask for the judgement an
/// author can actually make. Ordering, if a consumer ever needs it, stays deterministic: high before medium before
/// low.</para>
/// </summary>
public static class CampaignTargetPriorityLevels
{
    public const string Low = "low";
    public const string Medium = "medium";
    public const string High = "high";

    /// <summary>Most important first - the order a consumer should sort by, and the order the UI lists.</summary>
    public static readonly IReadOnlyList<string> All = new[] { High, Medium, Low };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    /// <summary>Normalizes a stated band. Blank stays blank: "no band" is a real answer and is never turned into
    /// <see cref="Low"/>.</summary>
    public static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    /// <summary>Sort weight, most important first. Used only if a consumer opts into ordering; an unstated band sorts
    /// last because it makes no claim.</summary>
    public static int Weight(string? value) => Normalize(value) switch
    {
        High => 0,
        Medium => 1,
        Low => 2,
        _ => 3
    };
}

/// <summary>Target lifecycle. Hard delete does not exist; <c>excluded</c> always carries a reason.</summary>
public static class CampaignTargetStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Completed = "completed";
    public const string Excluded = "excluded";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Draft, Active, Inactive, Completed, Excluded, Archived
    };

    /// <summary>
    /// MOD-0165 FU11 - the statuses a human may pick when authoring a manual target.
    /// <para><c>excluded</c> is absent because it is an OUTCOME, not a choice: the snapshot's consent evaluation
    /// writes it together with the reason it is required to carry, and an author who picked it by hand would produce a
    /// row that cannot satisfy its own "excluded always states why" rule. <c>archived</c> is absent because archiving
    /// is its own action, not a status to type. Both remain fully valid on the aggregate and on the API.</para>
    /// </summary>
    public static readonly IReadOnlyList<string> Authorable = new[] { Draft, Active, Inactive, Completed };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    /// <summary>Whether a human may set this status directly on a manual target.</summary>
    public static bool IsAuthorable(string? value)
        => !string.IsNullOrWhiteSpace(value) && Authorable.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Draft : value.Trim().ToLowerInvariant();
}

/// <summary>What a snapshot request declares as the origin of its items. Maps onto
/// <see cref="CampaignTargetSources"/>; kept as its own accessor so a snapshot cannot claim an origin the target
/// vocabulary does not know.</summary>
public static class CampaignSnapshotSourceTypes
{
    public static readonly IReadOnlyList<string> All = CampaignTargetSources.All;

    public static bool IsValid(string? value) => CampaignTargetSources.IsValid(value);

    public static string Normalize(string? value) => CampaignTargetSources.Normalize(value);
}

/// <summary>
/// Canonical FU04 reason codes surfaced on targets, snapshot results and audit. Nothing in this feature is silent:
/// every target and every snapshot outcome carries at least one of these.
/// </summary>
public static class CampaignReasonCodes
{
    // Campaign lifecycle
    public const string CampaignCreated = "campaign_created";
    public const string CampaignUpdated = "campaign_updated";
    public const string CampaignArchived = "campaign_archived";
    public const string CampaignArchivedNoTargetMutation = "campaign_archived_no_target_mutation";

    // Target lifecycle
    public const string CampaignTargetCreated = "campaign_target_created";
    public const string CampaignTargetUpdated = "campaign_target_updated";
    public const string CampaignTargetArchived = "campaign_target_archived";
    public const string CampaignTargetDuplicate = "campaign_target_duplicate";
    public const string CampaignTargetActive = "campaign_target_active";
    public const string CampaignTargetExcluded = "campaign_target_excluded";

    // Snapshot
    public const string CampaignTargetSnapshotCreated = "campaign_target_snapshot_created";
    public const string CampaignTargetSnapshotReconciled = "campaign_target_snapshot_reconciled";
    public const string SegmentSourceSnapshot = "segment_source_snapshot";
    public const string ManualTargetSelected = "manual_target_selected";
    public const string TargetSourceProvenanceStored = "target_source_provenance_stored";

    // Consent (mirrors the MOD-0164 outcome; FU04 reports, it does not decide)
    public const string ConsentAllowed = "consent_allowed";
    public const string ConsentBlocked = "consent_blocked";
    public const string ConsentUnknown = "consent_unknown";
    public const string ConsentFilterNotApplied = "consent_filter_not_applied";
    public const string ConsentEvaluationError = "consent_evaluation_error";
    public const string ConsentProvenanceStored = "consent_provenance_stored";

    /// <summary>FU04 extension: the target type is a group, not a consent subject, so no evaluation was possible.
    /// Surfaced rather than silently treated as evaluated.</summary>
    public const string ConsentEvaluationNotApplicable = "consent_evaluation_not_applicable";

    /// <summary>FU04 extension: a snapshot row conflicted with an existing target owned by a different source.</summary>
    public const string CampaignTargetSourceConflict = "campaign_target_source_conflict";

    // ---- MOD-0165 FU08 — cycle period binding. Nothing about a refused binding is silent: each of the three ways a
    // bind can fail has its own code, so a caller can tell "no such period" from "period not active" from "campaign
    // window is outside the period window" without parsing a message.

    /// <summary>FU08: the campaign window is not contained in the bound period's window (both ends inclusive, compared
    /// on the canonical UTC day). Also raised when a bound campaign is open-ended, because an open-ended window can
    /// never be contained in a period that has a last day.</summary>
    public const string CampaignOutsideCycleWindow = "campaign_outside_cycle_window";

    /// <summary>FU08: the period a caller is binding to (or re-binding to) is draft or closed. Only an ACTIVE period
    /// may be bound; a period that closes AFTER the binding was made keeps it.</summary>
    public const string CampaignCyclePeriodNotActive = "campaign_cycle_period_not_active";

    /// <summary>FU08: the referenced period does not exist in the caller's tenant. Fail-closed — the binding is not
    /// written, and a period belonging to another tenant is reported exactly like one that never existed.</summary>
    public const string CampaignCyclePeriodNotFound = "campaign_cycle_period_not_found";

    // ---- MOD-0165 FU09 - campaign scope + scope-aware cycle binding. Nothing is silent here either: an unpublished
    // SET and an unknown VALUE get different codes because one is fixed by an operator and the other by retyping, and
    // "the dependency said no" is never conflated with "the dependency did not answer".

    /// <summary>FU09: the supplied ScopeType is not one of the four known levels.</summary>
    public const string CampaignScopeTypeUnknown = "campaign_scope_type_unknown";

    /// <summary>FU09: the level named needs a reference that was not supplied.</summary>
    public const string CampaignScopeReferenceRequired = "campaign_scope_reference_required";

    /// <summary>FU09: more than one scope reference was supplied. Refused rather than silently narrowed - dropping a
    /// value the author typed would let them believe they filed the campaign somewhere they did not.</summary>
    public const string CampaignScopeAmbiguous = "campaign_scope_ambiguous";

    /// <summary>FU09: CountryScope is not an ISO alpha-2 code.</summary>
    public const string CampaignCountryInvalid = "campaign_country_invalid";

    /// <summary>FU09: the governed reference set backing a scope level is not published yet - an operator must publish
    /// it. Deliberately distinct from "value unknown", which the author fixes themselves.</summary>
    public const string CampaignReferenceSetUnpublished = "campaign_reference_set_unpublished";

    /// <summary>FU09: the country code is not in the governed set.</summary>
    public const string CampaignCountryUnknown = "campaign_country_unknown";

    /// <summary>FU09: the business-unit code is not in the published set. Raised only when the reference CHANGES, so a
    /// campaign carrying a pre-FU09 code stays editable.</summary>
    public const string CampaignBusinessUnitUnknown = "campaign_business_unit_unknown";

    /// <summary>FU09: MDM answered, and the legal entity does not exist, is not active, or may not be referenced.</summary>
    public const string CampaignLegalEntityNotReferenceable = "campaign_legal_entity_not_referenceable";

    /// <summary>FU09: MDM did not answer. 503 with nothing persisted - we do not KNOW, so we must not tell the author
    /// their input was wrong.</summary>
    public const string CampaignLegalEntityValidationUnavailable = "campaign_legal_entity_validation_unavailable";

    /// <summary>FU09: the bound cycle period is not applicable to the campaign's scope. Raised on binding AND on a
    /// scope change, because the scope is editable and checking only at bind time would let an author bind inside the
    /// rule and then move the campaign out of it.</summary>
    public const string CampaignCyclePeriodScopeMismatch = "campaign_cycle_period_scope_mismatch";

    // ---- MOD-0165 FU10 — targeting mode + segment targeting. Each way a targeting write can fail has its own code:
    // a caller can tell "unknown mode" from "this mode needs a segment" from "that segment is not usable" without
    // parsing prose.

    /// <summary>FU10: the supplied TargetingMode is neither <c>segment</c> nor <c>manual</c>. Refused rather than
    /// defaulted — a campaign silently switched to the wrong mode would target the wrong people.</summary>
    public const string CampaignTargetingModeUnknown = "campaign_targeting_mode_unknown";

    /// <summary>FU10: the campaign is in <c>segment</c> mode with no targeted segment. Checked on EVERY write, not
    /// only when the set changes, so the rule cannot be walked around by emptying the list afterwards.</summary>
    public const string CampaignSegmentRequired = "campaign_segment_required";

    /// <summary>FU10: a targeted segment does not exist in the caller's tenant. Fail-closed; a segment belonging to
    /// another tenant is reported exactly like one that never existed.</summary>
    public const string CampaignSegmentNotFound = "campaign_segment_not_found";

    /// <summary>FU10: a segment being ADDED is draft or archived. Raised only for segments the author is adding, so a
    /// campaign whose segment was archived later stays editable.</summary>
    public const string CampaignSegmentNotActive = "campaign_segment_not_active";

    /// <summary>FU10: the same segment was supplied twice. Refused rather than silently de-duplicated.</summary>
    public const string CampaignSegmentDuplicate = "campaign_segment_duplicate";

    /// <summary>FU10: more targeted segments than <see cref="CampaignLimits.MaxTargetedSegments"/>.</summary>
    public const string CampaignSegmentLimitExceeded = "campaign_segment_limit_exceeded";

    /// <summary>FU10: a manual target was written while the campaign is in <c>segment</c> mode. The mode is a rule,
    /// not a UI convention; existing manual rows are preserved and become active again if the mode is switched
    /// back.</summary>
    public const string CampaignTargetingModeForbidsManualTarget = "campaign_targeting_mode_forbids_manual_target";

    /// <summary>FU10: a unique CampaignCode could not be generated after the retry budget. Reported, never silently
    /// replaced by a fallback.</summary>
    public const string CampaignCodeGenerationFailed = "campaign_code_generation_failed";

    // ---- MOD-0165 FU11 - manual targeting redesign. The author now states less, so the server states more: what it
    // filled in, and why, is recorded here rather than left to be inferred from an empty field.

    /// <summary>FU11: the supplied priority band is not one of <see cref="CampaignTargetPriorityLevels"/>. Refused
    /// rather than rounded to a neighbour - a target quietly demoted to <c>low</c> would be worked on last for a
    /// reason nobody chose.</summary>
    public const string CampaignTargetPriorityLevelUnknown = "campaign_target_priority_level_unknown";

    /// <summary>FU11: a human tried to set a status only the system may write (<c>excluded</c>, <c>archived</c>).
    /// <c>excluded</c> belongs to the consent evaluation, which supplies the reason it is required to carry.</summary>
    public const string CampaignTargetStatusNotAuthorable = "campaign_target_status_not_authorable";

    /// <summary>FU11: the selection reason was written by the server because the author did not supply one. FU04's
    /// invariant is intact - every target still states why it exists; the statement is now a fact the server knows
    /// (who selected it, and when) instead of prose the author had to invent.</summary>
    public const string CampaignTargetSelectionReasonGenerated = "campaign_target_selection_reason_generated";
}
