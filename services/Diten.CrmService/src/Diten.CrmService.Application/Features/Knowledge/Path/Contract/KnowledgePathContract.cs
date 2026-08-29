using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Path.Contract;

public sealed record GetKnowledgePathContractQuery : IRequest<Response<KnowledgePathContractDto>>;

/// <summary>MOD-0162 FU04 KnowledgePath contract surface (feature flags + in-domain vocabulary + supported filters +
/// permissions + reason codes + limits + limitations). Published so a contract-driven UI needs no hardcoded list.</summary>
public sealed record KnowledgePathContractDto(
    string ModuleId,
    string ModuleName,
    string Service,
    string RuntimeScope,
    Guid TenantId,
    bool IsReady,
    KnowledgePathFeatureFlags Features,
    KnowledgePathVocabulary Vocabularies,
    KnowledgePathSupportedFilters SupportedFilters,
    KnowledgePathContractLimits Limits,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations);

/// <summary>
/// The FU04 capability flags. ONLY the thirteen documented flags are present. The branch-evaluator, recommendation,
/// best-next-content, completion-tracking, progress, ai-personalization, digital-detailing, visit-planning,
/// route-planning, workflow-approval and hard-delete flags are deliberately ABSENT — and never emitted as <c>false</c>
/// either, because advertising a capability (even as false) would misrepresent the boundary. This is a sequencing
/// authoring surface, not an engine.
/// </summary>
public sealed record KnowledgePathFeatureFlags(
    bool SupportsKnowledgePath,
    bool SupportsKnowledgePathStep,
    bool SupportsContentSequence,
    bool SupportsKnowledgePathVersioning,
    bool SupportsPublishedStepSetFreeze,
    bool SupportsRequiredOptionalSteps,
    bool SupportsPrerequisiteChain,
    bool SupportsVersionPinPolicy,
    bool SupportsStepConceptNodeReference,
    bool SupportsFutureBranchingMetadata,
    bool SupportsArchiveLifecycle,
    bool SupportsEffectiveDating,
    bool SupportsContractDrivenUi)
{
    public static KnowledgePathFeatureFlags Current => new(
        SupportsKnowledgePath: true,
        SupportsKnowledgePathStep: true,
        SupportsContentSequence: true,
        SupportsKnowledgePathVersioning: true,
        SupportsPublishedStepSetFreeze: true,
        SupportsRequiredOptionalSteps: true,
        SupportsPrerequisiteChain: true,
        SupportsVersionPinPolicy: true,
        SupportsStepConceptNodeReference: true,
        SupportsFutureBranchingMetadata: true,
        SupportsArchiveLifecycle: true,
        SupportsEffectiveDating: true,
        SupportsContractDrivenUi: true);
}

/// <summary>The in-domain vocabulary the runtime validates against (structural — never fails open on an unpublished
/// MOD-0048 set). D6 note: assessment-passed is validated against FU02's existing quiz content type, not a new set.</summary>
public sealed record KnowledgePathVocabulary(
    IReadOnlyList<string> PathStatuses,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> StepTypes,
    IReadOnlyList<string> CompletionRules,
    IReadOnlyList<string> VersionPinPolicies,
    IReadOnlyList<string> StepStatuses,
    IReadOnlyList<string> ContentResolutionStatuses)
{
    public static KnowledgePathVocabulary Current => new(
        KnowledgePathStatuses.All,
        KnowledgePathSources.All,
        KnowledgePathStepTypes.All,
        KnowledgePathCompletionRules.All,
        KnowledgePathVersionPin.All,
        KnowledgePathStepStatuses.All,
        new[]
        {
            KnowledgePathContentResolutionStatuses.Pinned,
            KnowledgePathContentResolutionStatuses.ResolvedLatest,
            KnowledgePathContentResolutionStatuses.Unresolved
        });
}

/// <summary>Which list filters the runtime actually supports server-side, so a UI never fakes an unsupported filter.</summary>
public sealed record KnowledgePathSupportedFilters(
    IReadOnlyList<string> Paths,
    IReadOnlyList<string> Steps)
{
    public static KnowledgePathSupportedFilters Current => new(
        Paths: new[]
        {
            "subjectId", "topicId", "audienceProfileId", "language", "status", "effectiveAt", "pathCode", "search",
            "includeArchived"
        },
        Steps: new[] { "includeArchived", "effectiveAt" });
}

/// <summary>Published document-growth limits (§4.2) — no surprise for a UI.</summary>
public sealed record KnowledgePathContractLimits(
    int MaxStepsPerPath,
    int MaxBranchConditionsPerStep,
    int MinEstimatedDurationMinutes,
    int MaxEstimatedDurationMinutes,
    bool StepsAreEmbeddedInPathDocument)
{
    public static KnowledgePathContractLimits Current => new(
        KnowledgePathLimits.MaxStepsPerPath,
        KnowledgePathLimits.MaxBranchConditionsPerStep,
        KnowledgePathLimits.MinEstimatedDurationMinutes,
        KnowledgePathLimits.MaxEstimatedDurationMinutes,
        StepsAreEmbeddedInPathDocument: true);
}

public sealed class GetKnowledgePathContractHandler
    : IRequestHandler<GetKnowledgePathContractQuery, Response<KnowledgePathContractDto>>
{
    public const string ModuleId = "MOD-0162-FU04";
    public const string ModuleName = "KnowledgePath Runtime + UI";
    public const string Service = "Diten.CrmService";
    public const string RuntimeScope =
        "FU04-knowledge-path-runtime (KnowledgePath authoring with EMBEDDED steps — create/read/update/archive, path " +
        "versioning, published step-set freeze, effective dating, StepOrder/StepCode uniqueness, prerequisite chain, " +
        "VersionPinPolicy content resolution, step→ConceptNode reference, authorable-but-never-evaluated branch " +
        "conditions, in-domain vocabulary, read-only consumption seam). NO branch evaluator / recommendation / " +
        "best-next-content / completion / progress / AI / digital-detailing / visit-route planning engine is opened; " +
        "FU02 content and FU03 concept nodes are read-only references and are never mutated.";

    private static readonly IReadOnlyList<string> CurrentLimitations = new[]
    {
        "a KnowledgePath answers ONLY 'in which order is content told / learned / shown?' — never how-concepts-link (FU03), across-which-visits (FU01B EngagementJourney), how-often/to-whom (MOD-0165/0167), who/when-to-visit (MOD-0155), completed-by-whom/what-score (MOD-0309) or best-next-content (F-DETAIL digital detailing)",
        "a path is a TEMPLATE, not a run: whether a step was actually shown, completed or skipped is NOT modelled here",
        "steps are EMBEDDED in the path document (D2): one collection, one optimistic Version token — a step write bumps the path's token; there is no second collection, repository, controller or step-level Version",
        "vocabulary (path-status / source / step-type / completion-rule / version-pin / step-status) is IN-DOMAIN (structural): the runtime validates against it and never fails open on an unpublished MOD-0048 set; an unknown value is a 400; MOD-0048 publish is a separate operator follow-up (F-RD)",
        "CompletionRule is a DECLARATION, never an engine — MOD-0309 measures completion; assessment-passed is accepted only when the referenced content is a quiz (D6=A) and no field is added to FU02 KnowledgeContent",
        "branch conditions are AUTHORABLE but DATA ONLY (D7): no branch is ever evaluated (supportsBranchEvaluator is absent), and a path is always walkable start-to-finish without any branch condition; TargetStepId must reference a step in the same path (400 otherwise, referential sanity only)",
        "VersionPinPolicy resolves content deterministically: pinned stays fixed to its ContentId; latest-published resolves the published + effective version at read time; a step that cannot resolve is surfaced as unresolved (ResolvedContentId=null) — never hidden, dropped or filled with a guess (no silent version drift)",
        "content version resolution does NOT widen the FU02 IKnowledgeContentLinkageReader signature; FU04 reads content through its own read-only resolver",
        "in-array StepOrder/StepCode uniqueness cannot be a Mongo index — the create/update handler is the only defence; a duplicate is a controlled 409",
        "document growth is bounded: at most 200 steps per path and 20 branch conditions per step (400 beyond); the path list projects the Steps array out and shows only counters",
        "a published version's step set is FROZEN (StepSetFrozenAt set): step add/update/archive returns 409 and a change needs a new version; two published versions of one (PathCode, LanguageCode) may not overlap in effective window (409)",
        "publish is a SEPARATE endpoint and permission (crm.knowledge.path.publish, SoD: author ≠ publisher); Update never transitions to published (400)",
        "a path / step is never hard-deleted; closing one is a soft archive that stays readable, and an archived step is kept in the document (never removed from the array)",
        "RBAC keys crm.knowledge.path.{read,manage,publish} are defined but NOT seeded; the endpoints run on the documented DEV-ONLY territory fallback (follow-up MOD-0162-FU04-RBAC); under the fallback publish collapses onto manage, so D4's SoD cannot be enforced in dev",
        "there is no DELETE and no PATCH endpoint; TenantId is server-resolved and never accepted from a payload"
    };

    private readonly ITenantContext _tenant;

    public GetKnowledgePathContractHandler(ITenantContext tenant) => _tenant = tenant;

    public Task<Response<KnowledgePathContractDto>> Handle(
        GetKnowledgePathContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Task.FromResult(Response<KnowledgePathContractDto>.Fail("Tenant context is required.", 400));
        }

        var dto = new KnowledgePathContractDto(
            ModuleId,
            ModuleName,
            Service,
            RuntimeScope,
            tenantId,
            IsReady: true,
            KnowledgePathFeatureFlags.Current,
            KnowledgePathVocabulary.Current,
            KnowledgePathSupportedFilters.Current,
            KnowledgePathContractLimits.Current,
            KnowledgePathReasonCodes.All,
            KnowledgePathPermissions.All,
            CurrentLimitations);

        return Task.FromResult(Response<KnowledgePathContractDto>.Success(dto));
    }
}
