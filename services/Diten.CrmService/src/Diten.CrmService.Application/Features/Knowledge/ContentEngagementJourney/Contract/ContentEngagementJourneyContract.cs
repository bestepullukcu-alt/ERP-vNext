using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.Contract;

public sealed record GetContentEngagementJourneyContractQuery
    : IRequest<Response<ContentEngagementJourneyContractDto>>;

/// <summary>MOD-0162 FU05 ContentEngagementJourney contract surface (feature flags + in-domain vocabulary + supported
/// filters + permissions + reason codes + limits + limitations). Published so a contract-driven UI needs no hardcoded
/// list.</summary>
public sealed record ContentEngagementJourneyContractDto(
    string ModuleId,
    string ModuleName,
    string Service,
    string RuntimeScope,
    Guid TenantId,
    bool IsReady,
    ContentEngagementJourneyFeatureFlags Features,
    ContentEngagementJourneyVocabulary Vocabularies,
    ContentEngagementJourneySupportedFilters SupportedFilters,
    ContentEngagementJourneyContractLimits Limits,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations);

/// <summary>
/// The FU05 capability flags. ONLY the fourteen documented flags are present. The stage-advancement-engine,
/// branch-evaluator, recommendation, best-next-stage, journey-runtime-progress, current-stage-state,
/// journey-target-assignment, completion-tracking, digital-detailing, visit-planning, route-planning, campaign-engine,
/// frequency-engine, workflow-approval and hard-delete flags are deliberately ABSENT — and never emitted as <c>false</c>
/// either, because advertising a capability (even as false) would misrepresent the boundary. This is a multi-session
/// progression authoring surface, not an engine.
/// </summary>
public sealed record ContentEngagementJourneyFeatureFlags(
    bool SupportsContentEngagementJourney,
    bool SupportsContentEngagementJourneyStage,
    bool SupportsMultiVisitContentProgression,
    bool SupportsJourneyVersioning,
    bool SupportsPublishedStageSetFreeze,
    bool SupportsRequiredOptionalStages,
    bool SupportsRepeatableStages,
    bool SupportsStageKnowledgePathBinding,
    bool SupportsPathVersionPinPolicy,
    bool SupportsFutureStageAdvancementMetadata,
    bool SupportsFutureBranchingMetadata,
    bool SupportsArchiveLifecycle,
    bool SupportsEffectiveDating,
    bool SupportsContractDrivenUi)
{
    public static ContentEngagementJourneyFeatureFlags Current => new(
        SupportsContentEngagementJourney: true,
        SupportsContentEngagementJourneyStage: true,
        SupportsMultiVisitContentProgression: true,
        SupportsJourneyVersioning: true,
        SupportsPublishedStageSetFreeze: true,
        SupportsRequiredOptionalStages: true,
        SupportsRepeatableStages: true,
        SupportsStageKnowledgePathBinding: true,
        SupportsPathVersionPinPolicy: true,
        SupportsFutureStageAdvancementMetadata: true,
        SupportsFutureBranchingMetadata: true,
        SupportsArchiveLifecycle: true,
        SupportsEffectiveDating: true,
        SupportsContractDrivenUi: true);
}

/// <summary>The in-domain vocabulary the runtime validates against (D-VOCAB = A — structural; never fails open on an
/// unpublished MOD-0048 set).</summary>
public sealed record ContentEngagementJourneyVocabulary(
    IReadOnlyList<string> JourneyStatuses,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> StageTypes,
    IReadOnlyList<string> AdvancementRules,
    IReadOnlyList<string> PathVersionPinPolicies,
    IReadOnlyList<string> StageStatuses,
    IReadOnlyList<string> PathResolutionStatuses)
{
    public static ContentEngagementJourneyVocabulary Current => new(
        ContentEngagementJourneyStatuses.All,
        ContentEngagementJourneySources.All,
        ContentEngagementJourneyStageTypes.All,
        ContentEngagementJourneyAdvancementRules.All,
        ContentEngagementJourneyPathPin.All,
        ContentEngagementJourneyStageStatuses.All,
        new[]
        {
            ContentEngagementJourneyPathResolutionStatuses.Pinned,
            ContentEngagementJourneyPathResolutionStatuses.ResolvedLatest,
            ContentEngagementJourneyPathResolutionStatuses.Unresolved
        });
}

/// <summary>Which list filters the runtime actually supports server-side, so a UI never fakes an unsupported filter.
/// There is no recommend / nextStage / currentStage / advance / score parameter — by construction.</summary>
public sealed record ContentEngagementJourneySupportedFilters(
    IReadOnlyList<string> Journeys,
    IReadOnlyList<string> Stages)
{
    public static ContentEngagementJourneySupportedFilters Current => new(
        Journeys: new[]
        {
            "subjectId", "topicId", "audienceProfileId", "language", "status", "effectiveAt", "journeyCode",
            "knowledgePathId", "search", "includeArchived"
        },
        Stages: new[] { "includeArchived", "effectiveAt" });
}

/// <summary>Published document-growth limits (§4.2) — no surprise for a UI.</summary>
public sealed record ContentEngagementJourneyContractLimits(
    int MaxStagesPerJourney,
    int MaxBranchConditionsPerStage,
    int MinVisitNumber,
    bool StagesAreEmbeddedInJourneyDocument)
{
    public static ContentEngagementJourneyContractLimits Current => new(
        ContentEngagementJourneyLimits.MaxStagesPerJourney,
        ContentEngagementJourneyLimits.MaxBranchConditionsPerStage,
        ContentEngagementJourneyLimits.MinVisitNumber,
        StagesAreEmbeddedInJourneyDocument: true);
}

public sealed class GetContentEngagementJourneyContractHandler
    : IRequestHandler<GetContentEngagementJourneyContractQuery, Response<ContentEngagementJourneyContractDto>>
{
    public const string ModuleId = "MOD-0162-FU05";
    public const string ModuleName = "ContentEngagementJourney Runtime + UI";
    public const string Service = "Diten.CrmService";
    public const string RuntimeScope =
        "FU05-content-engagement-journey-runtime (ContentEngagementJourney authoring with EMBEDDED stages — " +
        "create/read/update/archive, journey versioning, published stage-set freeze, effective dating, " +
        "StageOrder/StageCode uniqueness, stage → published+effective KnowledgePath binding, PathVersionPinPolicy " +
        "resolution, repeat visibility, authorable-but-never-evaluated advancement rule / fallback / branch conditions, " +
        "in-domain vocabulary, read-only consumption seam). NO stage-advancement / branch evaluator / recommendation / " +
        "journey progress / current-stage state / target assignment / campaign / frequency / visit-route planning / " +
        "digital detailing / completion engine is opened; FU02 taxonomy and FU04 KnowledgePath are read-only " +
        "references and are never mutated; FU03 concept aggregates are not touched at all.";

    private static readonly IReadOnlyList<string> CurrentLimitations = new[]
    {
        "a ContentEngagementJourney answers ONLY 'across several visits/sessions, which stage comes next and which path applies in it?' — never in-which-order-inside-one-session (FU04 KnowledgePath), how-concepts-link (FU03), how-often/to-whom (MOD-0165/0167), who/when-to-visit or current progress (MOD-0155), completed-by-whom/what-score (MOD-0309), or trigger/suppression/channel/run-log automation (MOD-0166)",
        "a journey is a TEMPLATE, not a run: current stage, journey progress, stage advancement and target assignment are NOT modelled here — no such field exists on the journey, on a stage, on Contact or on Account",
        "stages are EMBEDDED in the journey document (S2): one collection, one optimistic Version token — a stage write bumps the journey's token; there is no second collection, repository, controller or stage-level Version",
        "vocabulary (journey-status / source / stage-type / advancement-rule / path-pin / stage-status) is IN-DOMAIN (D-VOCAB=A, structural): the runtime validates against it and never fails open on an unpublished MOD-0048 set; an unknown value is a 400; MOD-0048 publish is a separate operator follow-up (F-RD)",
        "AdvancementRule, FallbackStageId and BranchConditions are DECLARED metadata: authorable, echoed back as data, and NEVER evaluated (supportsStageAdvancementEngine / supportsBranchEvaluator are absent); a journey is always walkable start-to-finish by StageOrder alone; FallbackStageId may point BACKWARDS and both it and TargetStageId must reference a stage of the same journey (400 otherwise, referential sanity only)",
        "a stage BINDS to a published + effective FU04 KnowledgePath and never copies its steps — only ResolvedPathStepCount (a counter) is surfaced; a draft / archived / not-yet-effective path is a 400",
        "PathVersionPinPolicy resolves the path deterministically: pinned stays fixed to its RecommendedKnowledgePathId; latest-published resolves the published + effective version by PathCode at read time; a stage that cannot resolve is surfaced as unresolved (ResolvedKnowledgePathId=null) — never hidden, dropped or filled with a guess (no silent version drift)",
        "path resolution does NOT widen the FU04 IKnowledgePathReader signature; FU05 reads paths through its own read-only resolver and mutates no FU04 aggregate",
        "in-array StageOrder/StageCode uniqueness cannot be a Mongo index — the create/update handler is the only defence; a duplicate is a controlled 409",
        "document growth is bounded: at most 100 stages per journey and 20 branch conditions per stage (400 beyond); the journey list projects the Stages array out and shows only counters",
        "a published version's stage set is FROZEN (StageSetFrozenAt set): stage add/update/archive returns 409 and a change needs a new version; new-version clones the stages with NEW StageIds and REMAPS FallbackStageId / BranchConditions[].TargetStageId onto the clone's own ids; two published versions of one (JourneyCode, LanguageCode) may not overlap in effective window (409)",
        "publish is a SEPARATE endpoint and permission (crm.knowledge.content-engagement-journey.publish, SoD: author ≠ publisher); Update never transitions to published (400); publishing requires at least one active required stage (400)",
        "Campaign / Brand / Product / Segment references are deliberately ABSENT (§2.1/S6): sending campaignId / brandId / productId / segmentId is a 400, not a silently ignored field — journey target assignment belongs to MOD-0165/0167 + MOD-0155 (F-TARGET) and the linkage is follow-up F-CAMPAIGN-LINK",
        "a journey / stage is never hard-deleted; closing one is a soft archive that stays readable, and an archived stage is kept in the document (never removed from the array)",
        "RBAC keys crm.knowledge.content-engagement-journey.{read,manage,publish} are defined but NOT seeded; the endpoints run on the documented DEV-ONLY territory fallback (follow-up MOD-0162-FU05-RBAC); under the fallback publish collapses onto manage, so the SoD cannot be enforced in dev",
        "there is no DELETE and no PATCH endpoint; TenantId is server-resolved and never accepted from a payload"
    };

    private readonly ITenantContext _tenant;

    public GetContentEngagementJourneyContractHandler(ITenantContext tenant) => _tenant = tenant;

    public Task<Response<ContentEngagementJourneyContractDto>> Handle(
        GetContentEngagementJourneyContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Task.FromResult(
                Response<ContentEngagementJourneyContractDto>.Fail("Tenant context is required.", 400));
        }

        var dto = new ContentEngagementJourneyContractDto(
            ModuleId,
            ModuleName,
            Service,
            RuntimeScope,
            tenantId,
            IsReady: true,
            ContentEngagementJourneyFeatureFlags.Current,
            ContentEngagementJourneyVocabulary.Current,
            ContentEngagementJourneySupportedFilters.Current,
            ContentEngagementJourneyContractLimits.Current,
            ContentEngagementJourneyReasonCodes.All,
            ContentEngagementJourneyPermissions.All,
            CurrentLimitations);

        return Task.FromResult(Response<ContentEngagementJourneyContractDto>.Success(dto));
    }
}
