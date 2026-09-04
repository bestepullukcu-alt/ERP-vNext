using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.PlannedVisit.Contract;
using Diten.CrmService.Application.Features.PlannedVisit.Queries;
using MediatR;

namespace Diten.CrmService.Application.Features.PlannedVisit.Handlers.QueryHandlers;

/// <summary>
/// Publishes what this FU is and, just as importantly, what it is NOT. The limitations are the contract a consumer can
/// rely on: they say out loud that a plan produces nothing, that no motor packs a slot or advances a stage, and that
/// availability is a warning here and a hard constraint only in FU05.
/// </summary>
public sealed class GetPlannedVisitContractHandler
    : IRequestHandler<GetPlannedVisitContractQuery, Response<PlannedVisitContractDto>>
{
    public const string ModuleId = "MOD-0155-FU01";
    public const string ModuleName = "Visit Planning / Planned Visit";
    public const string Service = "Diten.CrmService";

    public const string RuntimeScope =
        "FU01-planned-visit (the field team's planning atom: who is visited, when, for what purpose, by which resource, "
        + "in which tenant, plus the richer context later FUs will fill in - motor-filled sequence/slot, content-position "
        + "provenance, selection origin and a per-contact availability snapshot, all STORED and none computed). "
        + "create / read / update / confirm / cancel / archive (NO delete, NO bulk delete), an in-domain fail-closed "
        + "vocabulary, read-only frequency/consent/journey/availability provenance, the legacy overlap + same-day-type "
        + "guards, and a single Compact console. FU01 is NOT an engine (D8): it generates no plan, packs no slot, "
        + "computes no distance/duration, advances no content stage, closes no visit. MOD-0149/0150/0151/0162/0164/0165/"
        + "0167 and MDM are untouched - read only.";

    private static readonly IReadOnlyList<string> CurrentLimitations = new[]
    {
        "a PlannedVisit is a planning FOUNDATION and produces nothing: no route, no packed schedule, no MicroTarget, no visit, no report. There is deliberately no /generate, /optimize, /pack, /advance and no write path into another module's aggregate (D8)",
        "the embedded Slot (SequenceOrder / SlotStartTime / SlotEndTime) is MOTOR-FILLED and born null: FU01 never computes, sorts, optimizes or populates it - the FU03 route optimizer and FU05 packing motor write it (D12). A Slot value in a create/update payload is ignored (V26)",
        "content-position is a SINGLE source of truth (PlannedVisitContentRef, D10): the journey/stage (form fields 26/27) are its editable surface, derive-default from a strategy chain or manual override, marked by ContentSource (strategy|manual). FU01 STORES which stage a visit is on; it never advances the stage (auto-advance is FU04) and StageIndex is read, never incremented",
        "PlannedDate is the SINGLE time axis (D1): there is no second EffectiveFrom/EffectiveTo pair. It is a DateOnly on purpose - a second co-sorted DateTimeOffset field is the parallel-arrays 500. The optional PlannedStartTime/EndTime is a manual INTENT window, distinct from the motor's packed Slot window",
        "duration is STORED, not computed (D14): PlannedDurationMinutes is a manual override only. FU01 never derives it from content (that is FU05 + the CycleCapacity minute-budget extension)",
        "frequency, consent and the journey stage are PROVENANCE ONLY (D5): the decision + matched id + version + time are stored, never the policy/consent/journey record payload. Consent is asked with Channel=visit and the deterministic Purpose map; frequency 'unknown' is stored honestly and never a fabricated default",
        "consent is enforced in exactly ONE place - confirm (D6): draft/planned may carry a blocked/unknown verdict (shown as a badge), but confirm is fail-closed - blocked, unknown, or filter-not-applied each answer 409 and the plan stays planned. Unknown is NEVER treated as allowed. There is no reason-coded override here (F-OVERRIDE)",
        "availability is a per-contact SNAPSHOT and, in FU01, a WARNING not a block (D13): a window conflict still creates the plan (WithinAvailableWindow=false + reason codes). The hard constraint + override lives in FU05, and the FU03 route optimizer honours it there",
        "pharmacy is a first-class target (D9): a pharmacy is an Account whose account-type is pharmacy, and its consent is asked with SubjectType=account. The clinic-pharmacy AccountRelationship is READ context only and is never a precondition for a pharmacy plan; 'the clinic's pharmacies' auto-selection is FU05",
        "the resource is a STRING id (D4): there is no CRM-validated Person/Employee master (MOD-0288 reserved), so Resource.ResourceId is stored and NOT validated. BrandId/ProductId are never opened as authored keys; SegmentId/StrategyTemplateId are snapshot provenance only - not validated, not FKs, not form fields",
        "the legacy overlap (V22) and same-day-same-type (V24) guards are the two executable planning rules FU01 preserves: an active plan whose time windows intersect for one resource on one day is a 409, and a second active plan of the same visit type against one target on one day is a 409. Windowless plans do not enter the overlap check, and cancelled/archived rows never block",
        "the lifecycle is draft -> planned -> confirmed -> cancelled -> archived; archived is TERMINAL (no unarchive). confirmed cannot go back to planned. Source is manual-only in FU01 (the rest are reserved for FU03 / F-IMPORT / F-MIG)",
        "field-force ABAC scoping ('a rep only sees their own plans') is NOT faked (§8.6): MOD-0018-FU15 is reserved, so the list is narrowed by an EXPLICIT resourceId query parameter and tenant isolation is the only security boundary (F-ABAC)",
        "RBAC keys crm.planned-visit.{read,manage,confirm} are DEFINED but NOT seeded; the endpoints run on the documented DEV-ONLY territory fallback (F-RBAC), under which manage and confirm collapse onto one key so SoD cannot be enforced in dev",
        "vocabulary is IN-DOMAIN and fail-closed (D2): an out-of-set TargetType/Purpose/VisitType/Status/Source/ContentSource is refused (400), never quietly ignored, and no hardcoded fallback list exists anywhere - every dropdown is fed from this contract",
        "there is no DELETE, no PATCH and no bulk-delete endpoint anywhere; a plan is cancelled and/or archived, and TenantId is server-resolved and never accepted from a payload"
    };

    private readonly ITenantContext _tenant;

    public GetPlannedVisitContractHandler(ITenantContext tenant) => _tenant = tenant;

    public Task<Response<PlannedVisitContractDto>> Handle(
        GetPlannedVisitContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Task.FromResult(Response<PlannedVisitContractDto>.Fail("Tenant context is required.", 400));
        }

        var dto = new PlannedVisitContractDto(
            ModuleId,
            ModuleName,
            Service,
            RuntimeScope,
            tenantId,
            IsReady: true,
            PlannedVisitFeatureFlags.Current,
            PlannedVisitVocabularyDto.Current,
            PlannedVisitSupportedFilters.Current,
            PlannedVisitContractLimits.Current,
            PlannedVisitErrorCodes.All,
            PlannedVisitPermissions.All,
            CurrentLimitations);

        return Task.FromResult(Response<PlannedVisitContractDto>.Success(dto));
    }
}
