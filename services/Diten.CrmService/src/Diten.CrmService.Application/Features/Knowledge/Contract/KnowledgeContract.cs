using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Contract;

public sealed record GetKnowledgeContractQuery : IRequest<Response<KnowledgeContractDto>>;

/// <summary>MOD-0162 FU02 contract surface (feature flags + in-domain vocabulary + supported filters + permissions +
/// reason codes + limitations). Published so a contract-driven UI needs no hardcoded list.</summary>
public sealed record KnowledgeContractDto(
    string ModuleId,
    string ModuleName,
    string Service,
    string RuntimeScope,
    Guid TenantId,
    bool IsReady,
    KnowledgeFeatureFlags Features,
    KnowledgeVocabulary Vocabularies,
    KnowledgeSupportedFilters SupportedFilters,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations);

/// <summary>
/// The FU02 capability flags. ONLY the seven documented flags are present. The visit-planning, route-planning,
/// recommendation-engine, digital-detailing-runtime, workflow-approval, campaign-runtime-mutation,
/// brand-product-master-ownership, file-storage and hard-delete flags are deliberately ABSENT — and never emitted as
/// <c>false</c> either, because advertising a capability (even as false) would misrepresent the boundary: FU02 opens
/// none of them.
/// </summary>
public sealed record KnowledgeFeatureFlags(
    bool SupportsKnowledgeContentManagement,
    bool SupportsSubjectTaxonomyManagement,
    bool SupportsConceptGraphReference,
    bool SupportsBrandProductReference,
    bool SupportsArchiveLifecycle,
    bool SupportsEffectiveDating,
    bool SupportsContractDrivenUi)
{
    public static KnowledgeFeatureFlags Current => new(
        SupportsKnowledgeContentManagement: true,
        SupportsSubjectTaxonomyManagement: true,
        SupportsConceptGraphReference: true,   // format-level ConceptNodeId reference only (no FU01C runtime)
        SupportsBrandProductReference: true,   // optional MOD-0290 reference only (no master ownership)
        SupportsArchiveLifecycle: true,
        SupportsEffectiveDating: true,
        SupportsContractDrivenUi: true);
}

/// <summary>The in-domain vocabulary the runtime validates against (structural — never fails open on an unpublished
/// MOD-0048 set).</summary>
public sealed record KnowledgeVocabulary(
    IReadOnlyList<string> ContentTypes,
    IReadOnlyList<string> ContentStatuses,
    IReadOnlyList<string> ContentSources,
    IReadOnlyList<string> AudienceProfileTypes,
    IReadOnlyList<string> TaxonomyStatuses)
{
    public static KnowledgeVocabulary Current => new(
        KnowledgeContentTypes.All,
        KnowledgeContentStatuses.All,
        KnowledgeContentSources.All,
        Domain.Entities.AudienceProfileTypes.All,
        Domain.Entities.TaxonomyStatuses.All);
}

/// <summary>Which list filters the runtime actually supports server-side, so a UI never fakes an unsupported filter.</summary>
public sealed record KnowledgeSupportedFilters(
    IReadOnlyList<string> Contents,
    IReadOnlyList<string> Subjects,
    IReadOnlyList<string> Topics,
    IReadOnlyList<string> AudienceProfiles)
{
    public static KnowledgeSupportedFilters Current => new(
        Contents: new[]
        {
            "contentType", "contentStatus", "subjectId", "topicId", "audienceProfileId", "languageCode", "brandId",
            "productId", "campaignId", "effectiveAt", "search", "includeArchived"
        },
        Subjects: new[] { "status", "search", "includeArchived" },
        Topics: new[] { "subjectId", "status", "search", "includeArchived" },
        AudienceProfiles: new[] { "status", "profileType", "search", "includeArchived" });
}

public sealed class GetKnowledgeContractHandler
    : IRequestHandler<GetKnowledgeContractQuery, Response<KnowledgeContractDto>>
{
    public const string ModuleId = "MOD-0162";
    public const string ModuleName = "Knowledge / Content Taxonomy";
    public const string Service = "Diten.CrmService";
    public const string RuntimeScope =
        "FU02-knowledge-content-runtime (KnowledgeContent + Subject/Topic/AudienceProfile authoring, archive lifecycle, " +
        "effective dating, content-linkage read provider). KnowledgePath (FU01A), EngagementJourney (FU01B) and " +
        "ConceptGraph (FU01C) runtime are NOT opened here.";

    private static readonly IReadOnlyList<string> AllReasonCodes = new[]
    {
        KnowledgeReasonCodes.ContentCreated,
        KnowledgeReasonCodes.ContentUpdated,
        KnowledgeReasonCodes.ContentArchived,
        KnowledgeReasonCodes.ContentDuplicateCode,
        KnowledgeReasonCodes.SubjectCreated,
        KnowledgeReasonCodes.SubjectUpdated,
        KnowledgeReasonCodes.SubjectArchived,
        KnowledgeReasonCodes.SubjectDuplicateCode,
        KnowledgeReasonCodes.TopicCreated,
        KnowledgeReasonCodes.TopicUpdated,
        KnowledgeReasonCodes.TopicArchived,
        KnowledgeReasonCodes.TopicDuplicateCode,
        KnowledgeReasonCodes.TopicCrossSubjectParent,
        KnowledgeReasonCodes.TopicParentCycle,
        KnowledgeReasonCodes.AudienceProfileCreated,
        KnowledgeReasonCodes.AudienceProfileUpdated,
        KnowledgeReasonCodes.AudienceProfileArchived,
        KnowledgeReasonCodes.AudienceProfileDuplicateCode,
        KnowledgeReasonCodes.ArchivedNoMutation,
        KnowledgeReasonCodes.ReferenceArchived
    };

    private static readonly IReadOnlyList<string> CurrentLimitations = new[]
    {
        "content answers ONLY 'what to teach/present, for which subject-topic-audience, which version, when?' — never in-what-order (MOD-0162-FU01A), across-which-visits (MOD-0162-FU01B), which-concept-chain (MOD-0162-FU01C runtime), how-often (MOD-0165/0167), who/when-to-visit (MOD-0155) or may-we-contact (MOD-0164)",
        "vocabulary (content-type / -status / -source / audience-profile-type / taxonomy-status) is IN-DOMAIN (structural): the runtime validates against it and never fails open on an unpublished MOD-0048 set; an unknown value is a 400; MOD-0048 publish is a separate operator follow-up (F-RD)",
        "Brand / Product / ConceptNode / Campaign / Segment are REFERENCES validated at format level only — MOD-0290 and MOD-0162-FU01C have no runtime consumed here, so no master is resolved and no master field is copied; content without Brand/Product is fully valid (non-pharma)",
        "the business version is ContentVersion; the EntityBase Version is the technical concurrency token and is never a business field",
        "no binary is stored here: FileRef / ContentAssetRef / ContentBodyRef / Url are POINTERS; the document store is MOD-0028/0029 and is never duplicated",
        "content / subject / topic / audience-profile is never hard-deleted; closing one is a soft archive that stays readable, and archived rows accept no update (409)",
        "archiving a subject/topic does NOT cascade: existing content keeps its classification and stays readable; only NEW attachment to the archived row is blocked",
        "a topic lives only inside its own subject: a cross-subject parent, a self-parent or a parent cycle is rejected 400",
        "the content-linkage read provider (IKnowledgeContentLinkageReader) returns published + effective content only and makes NO decision — no scoring, no best-content, no recommendation; a Campaign consumer is future and Campaign runtime is never mutated from here",
        "RBAC keys crm.knowledge.* are defined but NOT seeded; the endpoints run on the documented territory fallback (follow-up MOD-0162-FU02-RBAC)",
        "there is no DELETE and no PATCH endpoint; TenantId is server-resolved and never accepted from a payload"
    };

    private readonly ITenantContext _tenant;

    public GetKnowledgeContractHandler(ITenantContext tenant)
    {
        _tenant = tenant;
    }

    public Task<Response<KnowledgeContractDto>> Handle(
        GetKnowledgeContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Task.FromResult(Response<KnowledgeContractDto>.Fail("Tenant context is required.", 400));
        }

        var dto = new KnowledgeContractDto(
            ModuleId,
            ModuleName,
            Service,
            RuntimeScope,
            tenantId,
            IsReady: true, // vocabulary is in-domain, so authoring is ready without a MOD-0048 publish
            KnowledgeFeatureFlags.Current,
            KnowledgeVocabulary.Current,
            KnowledgeSupportedFilters.Current,
            AllReasonCodes,
            KnowledgePermissions.All,
            CurrentLimitations);

        return Task.FromResult(Response<KnowledgeContractDto>.Success(dto));
    }
}
