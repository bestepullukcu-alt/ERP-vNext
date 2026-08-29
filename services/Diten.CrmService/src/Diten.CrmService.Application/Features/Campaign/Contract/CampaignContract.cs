using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Campaign.Snapshot;
using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.Campaign.Contract;

public sealed record GetCampaignContractQuery : IRequest<Response<CampaignContractDto>>;

/// <summary>MOD-0165 FU04 contract surface (feature flags + supported vocabulary + permissions + limitations).</summary>
public sealed record CampaignContractDto(
    string ModuleId,
    string ModuleName,
    string Service,
    string RuntimeScope,
    Guid TenantId,
    bool IsReady,
    CampaignFeatureFlags Features,
    CampaignVocabulary Vocabulary,
    CampaignConsentIntegrationDto ConsentIntegration,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations);

/// <summary>
/// The FU04 capability flags. ONLY the campaign/target/snapshot/consent-integration flags are present. The
/// segmentation-engine, dynamic-campaign-rule, visit-planning, route-planning, due-overdue, last-visit-history,
/// frequency-runtime, digital-detailing, recommendation-engine and workflow-approval flags are deliberately ABSENT —
/// and never emitted as <c>false</c> either, because advertising a capability (even as false) would misrepresent the
/// boundary: FU04 opens none of them.
/// </summary>
public sealed record CampaignFeatureFlags(
    bool SupportsCampaignManagement,
    bool SupportsCampaignTargetManagement,
    bool SupportsStaticTargetSnapshot,
    bool SupportsConsentEvaluationIntegration,
    bool SupportsTargetExclusionReason,
    bool SupportsTargetSourceProvenance,
    bool SupportsCyclePeriodBinding,
    bool SupportsScopeAwareCycleBinding,
    bool SupportsSegmentTargeting)
{
    public static CampaignFeatureFlags Current => new(
        SupportsCampaignManagement: true,
        SupportsCampaignTargetManagement: true,
        SupportsStaticTargetSnapshot: true,
        SupportsConsentEvaluationIntegration: true,
        SupportsTargetExclusionReason: true,
        SupportsTargetSourceProvenance: true,
        // FU08. Emitted true because the capability is genuinely open; the rule that a closed capability is never
        // advertised even as false still holds, so nothing new appears here beyond what FU08 actually ships.
        SupportsCyclePeriodBinding: true,
        // FU09. True because the capability is genuinely open: a campaign now carries a discriminated scope and may
        // only bind a period applicable to it.
        SupportsScopeAwareCycleBinding: true,
        // FU10. True because the capability is genuinely open. The two target/snapshot flags above stay true and are
        // now unambiguous: manual targeting keeps both its API and its screen, gated by the mode.
        SupportsSegmentTargeting: true);
}

/// <summary>The in-domain vocabulary the runtime validates against (surfaced so an authoring UI needs no hardcoded list).</summary>
public sealed record CampaignVocabulary(
    IReadOnlyList<string> TargetingModes,
    int MaxTargetedSegments,
    IReadOnlyList<string> CampaignTypes,
    IReadOnlyList<string> CampaignStatuses,
    IReadOnlyList<string> ObjectiveTypes,
    IReadOnlyList<string> TargetTypes,
    IReadOnlyList<string> TargetSources,
    IReadOnlyList<string> TargetStatuses,
    // FU11 - the subset a human may set. Published separately from TargetStatuses because the difference is a RULE,
    // not a UI preference: 'excluded' is written by the consent evaluation with the reason it must carry, and
    // 'archived' has its own endpoint. A UI that offered them would build forms the server refuses.
    IReadOnlyList<string> AuthorableTargetStatuses,
    IReadOnlyList<string> TargetPriorityLevels,
    IReadOnlyList<string> SnapshotRowOutcomes,
    IReadOnlyList<string> ConsentChannels,
    IReadOnlyList<string> ConsentPurposes)
{
    public static CampaignVocabulary Current => new(
        // FU10 — published so a UI never hardcodes the modes or the ceiling.
        CampaignTargetingModes.All,
        CampaignLimits.MaxTargetedSegments,
        Domain.Entities.CampaignTypes.All,
        Domain.Entities.CampaignStatuses.All,
        CampaignObjectiveTypes.All,
        CampaignTargetTypes.All,
        CampaignTargetSources.All,
        CampaignTargetStatuses.All,
        // FU11 - published so the manual target screen needs no hardcoded list for either.
        CampaignTargetStatuses.Authorable,
        CampaignTargetPriorityLevels.All,
        CampaignSnapshotRowOutcome.All,
        ConsentChannel.All,
        ConsentPurpose.All);
}

/// <summary>
/// How FU04 consumes MOD-0164. Surfaced on the contract so a consumer can see — without reading code — that the
/// consent decision is MOD-0164's, which target types are evaluable, and exactly what happens on
/// blocked / unknown / filter-disabled.
/// </summary>
public sealed record CampaignConsentIntegrationDto(
    string ProviderModule,
    string ProviderSeam,
    string EvaluatorVersion,
    IReadOnlyList<string> EvaluableTargetTypes,
    string ScopeType,
    string MissingContextBehavior,
    string BlockedBehavior,
    string UnknownBehavior,
    string FilterDisabledBehavior,
    string NotApplicableBehavior)
{
    public static CampaignConsentIntegrationDto Current => new(
        ProviderModule: "MOD-0164",
        ProviderSeam: nameof(IConsentPreferenceEvaluator),
        EvaluatorVersion: ConsentEvaluationResult.CurrentEvaluatorVersion,
        EvaluableTargetTypes: new[]
        {
            CampaignTargetTypes.Contact, CampaignTargetTypes.AccountContactLink, CampaignTargetTypes.Account
        },
        ScopeType: ConsentScopeType.Campaign,
        MissingContextBehavior:
            $"400 {CreateCampaignTargetSnapshotHandler.ConsentContextRequiredCode} — a consent-filtered snapshot " +
            "requires ConsentChannel + ConsentPurpose (request or campaign default); no channel/purpose is assumed",
        BlockedBehavior:
            $"target is created with TargetStatus=excluded, ExclusionReason={CampaignReasonCodes.ConsentBlocked} " +
            "(kept, not dropped, so the exclusion is auditable)",
        UnknownBehavior:
            $"target is created with TargetStatus=excluded, ExclusionReason={CampaignReasonCodes.ConsentUnknown} — " +
            "unknown is NEVER treated as allowed",
        FilterDisabledBehavior:
            $"ApplyConsentFilter=false produces targets, but every row carries " +
            $"'{CampaignReasonCodes.ConsentFilterNotApplied}' provenance and no eligibility may be inferred",
        NotApplicableBehavior:
            $"a group-shaped target (segment/territory-node/concept-node/audience-profile) reports " +
            $"'{CampaignReasonCodes.ConsentEvaluationNotApplicable}'; member-level consent is the consumer's job");
}

public sealed class GetCampaignContractHandler : IRequestHandler<GetCampaignContractQuery, Response<CampaignContractDto>>
{
    public const string ModuleId = "MOD-0165";
    public const string ModuleName = "Campaign / Targeting";
    public const string Service = "Diten.CrmService";
    public const string RuntimeScope =
        "FU02-campaign-targeting-boundary; " +
        "FU04-campaign-targeting-runtime-static-target-snapshot (campaign + target authoring, static snapshot, " +
        "MOD-0164 consent evaluation integration); " +
        "FU08-campaign-cycle-period-binding (optional one-directional pin to a MOD-0165 FU06/FU07 CyclePeriod); " +
        "FU09-campaign-scope-mirror (discriminated campaign scope + scope-aware cycle binding); " +
        "FU10-campaign-redesign (targeting mode + multi-segment targeting + generated campaign code)";

    private static readonly IReadOnlyList<string> AllReasonCodes = new[]
    {
        CampaignReasonCodes.CampaignCreated,
        CampaignReasonCodes.CampaignUpdated,
        CampaignReasonCodes.CampaignArchived,
        CampaignReasonCodes.CampaignArchivedNoTargetMutation,
        CampaignReasonCodes.CampaignTargetCreated,
        CampaignReasonCodes.CampaignTargetUpdated,
        CampaignReasonCodes.CampaignTargetArchived,
        CampaignReasonCodes.CampaignTargetDuplicate,
        CampaignReasonCodes.CampaignTargetActive,
        CampaignReasonCodes.CampaignTargetExcluded,
        CampaignReasonCodes.CampaignTargetSnapshotCreated,
        CampaignReasonCodes.CampaignTargetSnapshotReconciled,
        CampaignReasonCodes.CampaignTargetSourceConflict,
        CampaignReasonCodes.SegmentSourceSnapshot,
        CampaignReasonCodes.ManualTargetSelected,
        CampaignReasonCodes.TargetSourceProvenanceStored,
        CampaignReasonCodes.ConsentAllowed,
        CampaignReasonCodes.ConsentBlocked,
        CampaignReasonCodes.ConsentUnknown,
        CampaignReasonCodes.ConsentFilterNotApplied,
        CampaignReasonCodes.ConsentEvaluationError,
        CampaignReasonCodes.ConsentEvaluationNotApplicable,
        CampaignReasonCodes.ConsentProvenanceStored,
        CampaignReasonCodes.CampaignOutsideCycleWindow,
        CampaignReasonCodes.CampaignCyclePeriodNotActive,
        CampaignReasonCodes.CampaignCyclePeriodNotFound,
        CampaignReasonCodes.CampaignScopeTypeUnknown,
        CampaignReasonCodes.CampaignScopeReferenceRequired,
        CampaignReasonCodes.CampaignScopeAmbiguous,
        CampaignReasonCodes.CampaignCountryInvalid,
        CampaignReasonCodes.CampaignReferenceSetUnpublished,
        CampaignReasonCodes.CampaignCountryUnknown,
        CampaignReasonCodes.CampaignBusinessUnitUnknown,
        CampaignReasonCodes.CampaignLegalEntityNotReferenceable,
        CampaignReasonCodes.CampaignLegalEntityValidationUnavailable,
        CampaignReasonCodes.CampaignCyclePeriodScopeMismatch,
        CampaignReasonCodes.CampaignTargetingModeUnknown,
        CampaignReasonCodes.CampaignSegmentRequired,
        CampaignReasonCodes.CampaignSegmentNotFound,
        CampaignReasonCodes.CampaignSegmentNotActive,
        CampaignReasonCodes.CampaignSegmentDuplicate,
        CampaignReasonCodes.CampaignSegmentLimitExceeded,
        CampaignReasonCodes.CampaignTargetingModeForbidsManualTarget,
        CampaignReasonCodes.CampaignCodeGenerationFailed
    };

    private static readonly IReadOnlyList<string> CurrentLimitations = new[]
    {
        "a campaign answers ONLY 'what/why/when/which context?' and a target ONLY 'who is in it and why?' — never visit/route planning, due/overdue, last-visit history, frequency, content recommendation, digital detailing or workflow approval",
        "the target snapshot is STATIC: it normalizes the caller-supplied TargetItems and resolves NO membership; a segment-sourced snapshot stores the segment id as provenance only (MOD-0167 runtime is not opened)",
        "a snapshot is ADDITIVE — it never deletes or archives an earlier target; discontinuation is done through exclusion/archive",
        "a snapshot is idempotent per source: a re-run reconciles existing rows instead of duplicating them; a target owned by a DIFFERENT source aborts the whole batch with 409 before any write (never half-applied)",
        "a structurally invalid or duplicated snapshot row rejects the WHOLE request with 400 — no partial snapshot is ever persisted",
        "the consent decision is MOD-0164's: FU04 calls the IConsentPreferenceEvaluator seam, holds no consent logic, never reads the consent/preference store and never writes to it",
        "only decision PROVENANCE is stored on a target (decision, eligibilityStatus, reasonCodes, evaluatedAt, matchedConsentId, matchedPreferenceIds, evaluatorVersion, selectionReason, channel, purpose) — no consentStatus, no preferenceStatus, no record payload is copied",
        "consent unknown is NEVER allowed: both blocked and unknown produce an excluded target WITH a reason, kept rather than dropped so the exclusion is auditable",
        "a consent-filtered snapshot with no channel/purpose is rejected 400 (campaign_consent_context_required) rather than run against an assumed question",
        "ApplyConsentFilter=false still produces targets, but every row carries consent_filter_not_applied and no eligibility may be inferred from it",
        "consent provenance is written ONLY by the snapshot from a live evaluation — the target update endpoint cannot set it, so a caller can never hand-craft a consent verdict",
        "'campaign-target' is deliberately NOT a campaign target type (self-referential loop, MOD-0048 reconciliation F6); the separate visit-frequency-target-type set does contain it and the two are never unified",
        "Brand / Product / Subject / Topic / ConceptChainTemplate / EngagementJourney / KnowledgePath / KnowledgeContent are REFERENCES validated at format level only — MOD-0290 and MOD-0162 have no runtime yet, so no master is resolved and no master field is copied",
        "campaigns without Brand/Product are fully valid (non-pharma); ATC / therapeutic-area is not opened here",
        "TargetDisplayName is a snapshot LABEL for display/audit only, never a source of truth — consumers resolve names from the owning master",
        "a campaign or target is never hard-deleted; closing one is a soft archive that stays readable, and an archived campaign accepts no target mutation (campaign_archived_no_target_mutation)",
        "archiving a campaign does NOT cascade to its targets — a silent cascade would rewrite targeting history; the campaign status is visible to consumers instead",
        "SelectionReason and ReasonCodes are mandatory on every target: a silent target selection is not authorable",
        "campaign results / KPI measurement is out of FU04 scope (MOD-0165 boundary F6)",
        "RBAC keys crm.campaign.* are defined but NOT seeded; the endpoints run on the documented territory fallback (follow-up MOD-0165-FU-RBAC)",
        "UI is a follow-up (MOD-0165-FU05); FU04 ships the API + snapshot + consent integration + tests",
        "FU10: a campaign declares HOW it is targeted — targetingMode is either 'segment' (targeted segments) or 'manual' (hand-authored CampaignTarget rows). Only the ACTIVE mode's data is validated and used; the other mode's existing data is kept and is never cleared by a mode switch",
        "FU10: while a campaign is in 'segment' mode a manual target cannot be added or changed (400 campaign_targeting_mode_forbids_manual_target) — the mode is a rule, not a UI convention; archiving an existing target stays allowed because closing history is not adding data",
        "FU10: in 'segment' mode a campaign declares WHO it targets through targetedSegments — segment membership is never resolved here, no CampaignTarget row is produced and no consent is evaluated; turning targeted segments into an audience is a separate follow-up",
        "FU10: a targeted segment is pinned by SEGMENT VERSION, not by lineage — a newer version does not change what an existing campaign targets; the superseded state is surfaced so an author can move it deliberately",
        "FU10: segments are validated when the targeted set CHANGES, so a campaign whose segment was archived later stays editable; the 'at least one segment' rule is checked on every write instead, so the mode cannot be satisfied once and then emptied",
        "FU10: campaigns written before this release carry no targeting mode and are read as 'manual' — the only way targeting existed at the time; nothing is migrated and no stored row is rewritten",
        "FU10: Brand / Product / Subject / Topic / ConceptChainTemplate / EngagementJourney / KnowledgePath / KnowledgeContent / OwnerUserId / ExternalReferences are no longer authored or returned, and the list filters over them were removed with them. The stored values are untouched — there is no migration — and 'what to promote' belongs to a per-segment model that is not opened here",
        "FU10: CampaignCode is generated server-side when left empty (CMP-{YYYY}-{sequence}) at write time, so an abandoned create screen never consumes a number; it stays author-editable on create and is immutable afterwards",
        "FU08: a campaign may PIN a cycle period (CyclePeriodId) but the binding is ONE-DIRECTIONAL — CyclePeriod holds no campaign reference, no campaign list and no cascade, so its own supportsCampaignBinding flag stays false and remains correct as a statement about the CyclePeriod surface",
        "FU08: while bound, the campaign window must be CONTAINED in the period window (both ends inclusive, compared on the canonical UTC day); the campaign's own StartDate/EndDate are never derived from, filled from or updated by the period",
        "FU08: a period must be ACTIVE at the moment the binding is set or changed; a period that CLOSES afterwards keeps its bindings and changes no campaign date — closing a period never cascades, and the active check therefore fires only when the binding itself changes",
        "FU08: a bound campaign cannot be open-ended — an EndDate is required, because a window with no last day can never be contained in a period that has one; the period's end is never implied as the campaign's",
        "FU09: the binding IS scope-aware — a campaign carries a discriminated scope (tenant / country / legal-entity / business-unit) and may only bind a period APPLICABLE to it: its own address, or the tenant-wide fallback. A period at a different address of the same level is never offered and never accepted (this supersedes FU08, which deliberately did not match scope)",
        "FU09: campaign scope is DATA, not authorization — it says where a campaign lives, never who may see it; no read is filtered by scope and no permission is derived from it",
        "FU09: campaign scope is EDITABLE (unlike a cycle period's, which is identity), and changing it re-validates the bound period on EVERY write — a period that is no longer applicable refuses the write rather than being silently unbound",
        "FU09: applicability follows the resolve-active precedence and a campaign names exactly ONE level, so a business-unit-scoped campaign sees business-unit and tenant periods only — a country period is not offered to it, because the campaign names no country",
        "FU09: the campaign scope model MIRRORS MOD-0165 FU07 rather than sharing its code; CyclePeriod is untouched and its own supportsCampaignBinding flag stays false. Consolidating the two rule sets is a follow-up, and a behaviour-equivalence test keeps them honest meanwhile",
        "FU09: the business-unit reference is validated against the published set only when it CHANGES, so a campaign carrying a pre-FU09 code stays editable; the governed country set is currently narrow, which limits country-scoped campaigns and nothing else",
        "FU08: the cycle period shown on a campaign read is a READ-TIME projection and is never persisted onto the campaign — only the id is stored, because a copied code or window goes stale when the period is renamed or re-dated",
        "FU11: a manual target is authored through an account/contact picker — the id comes from the picker and targetDisplayName remains a LABEL for display and audit only; consumers still resolve the current name from the owning master, never from the target",
        "FU11: manual authoring fills targetSource=manual, reasonCodes=[manual_target_selected], effectiveFrom=now and, when the author states none, a generated selectionReason naming the actor and the date. FU04's rule that a target may never be selected without a stated reason is UNCHANGED — the server now states a fact instead of asking the author to invent prose, and flags the row with campaign_target_selection_reason_generated so a stated reason stays distinguishable from a filled-in one",
        "FU11: target priority is a BAND (low/medium/high) on priorityLevel. The former integer priority is DEPRECATED and kept, never migrated: pre-FU11 rows are read as bands at READ time under the integer's own 'smaller wins' contract (1 → high, 2 → medium, 3 and above → low), and an edit preserves the integer rather than erasing it. Values above 3 collapse into low, which is safe because no consumer has ever ordered by this field",
        "FU11: the manual screen authors only account and contact targets — the two that have a picker. The API still accepts every target type; the restriction is the screen's, not the contract's",
        "FU11: excluded and archived are not authorable target statuses — see authorableTargetStatuses. excluded is the OUTCOME of a consent evaluation, which writes it together with the reason it is required to carry; archived is set by the archive endpoint. Both stay valid on the aggregate and the snapshot still writes excluded exactly as before",
        "FU11: the static target snapshot has NO SCREEN in this release. The endpoint and supportsStaticTargetSnapshot stay true because the API genuinely supports it — a snapshot belongs to resolving a segment into targets, which is a separate follow-up, and running one over hand-authored rows would overwrite what an author just typed"
    };

    private readonly ITenantContext _tenant;

    public GetCampaignContractHandler(ITenantContext tenant)
    {
        _tenant = tenant;
    }

    public Task<Response<CampaignContractDto>> Handle(
        GetCampaignContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Task.FromResult(Response<CampaignContractDto>.Fail("Tenant context is required.", 400));
        }

        var dto = new CampaignContractDto(
            ModuleId,
            ModuleName,
            Service,
            RuntimeScope,
            tenantId,
            IsReady: true, // vocabulary is in-domain, so authoring is ready without a MOD-0048 publish
            CampaignFeatureFlags.Current,
            CampaignVocabulary.Current,
            CampaignConsentIntegrationDto.Current,
            AllReasonCodes,
            CampaignPermissions.All,
            CurrentLimitations);

        return Task.FromResult(Response<CampaignContractDto>.Success(dto));
    }
}
