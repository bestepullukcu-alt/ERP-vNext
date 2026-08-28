using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Contract;

public sealed record GetConceptGraphContractQuery : IRequest<Response<ConceptGraphContractDto>>;

/// <summary>MOD-0162 FU03 concept-graph contract surface (feature flags + in-domain vocabulary + supported filters +
/// permissions + reason codes + limitations). Published so a contract-driven UI needs no hardcoded list.</summary>
public sealed record ConceptGraphContractDto(
    string ModuleId,
    string ModuleName,
    string Service,
    string RuntimeScope,
    Guid TenantId,
    bool IsReady,
    ConceptGraphFeatureFlags Features,
    ConceptGraphVocabulary Vocabularies,
    ConceptGraphSupportedFilters SupportedFilters,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Limitations);

/// <summary>
/// The FU03 capability flags. ONLY the twelve documented flags are present. The recommendation-engine,
/// ai-personalization, graph-traversal-engine, best-next-content, visit-planning, route-planning, digital-detailing,
/// workflow-approval and hard-delete flags are deliberately ABSENT — and never emitted as <c>false</c> either, because
/// advertising a capability (even as false) would misrepresent the boundary: FU03 opens none of them. This is a
/// configuration + adjacency-read surface, not an engine.
/// </summary>
public sealed record ConceptGraphFeatureFlags(
    bool SupportsSubjectConceptGraph,
    bool SupportsConfigurableConceptChain,
    bool SupportsConceptType,
    bool SupportsConceptNode,
    bool SupportsConceptRelationship,
    bool SupportsConceptChainTemplate,
    bool SupportsContentConceptLink,
    bool SupportsArchiveLifecycle,
    bool SupportsEffectiveDating,
    bool SupportsCycleDetection,
    bool SupportsTemplateConformanceDiagnostics,
    bool SupportsContractDrivenUi)
{
    public static ConceptGraphFeatureFlags Current => new(
        SupportsSubjectConceptGraph: true,
        SupportsConfigurableConceptChain: true,
        SupportsConceptType: true,
        SupportsConceptNode: true,
        SupportsConceptRelationship: true,
        SupportsConceptChainTemplate: true,
        SupportsContentConceptLink: true,
        SupportsArchiveLifecycle: true,
        SupportsEffectiveDating: true,
        SupportsCycleDetection: true,
        SupportsTemplateConformanceDiagnostics: true,
        SupportsContractDrivenUi: true);
}

/// <summary>The in-domain vocabulary the runtime validates against (structural — never fails open on an unpublished
/// MOD-0048 set). The relationship-type set is the boundary FU01C §5 canonical (D3=A).</summary>
public sealed record ConceptGraphVocabulary(
    IReadOnlyList<string> ConceptStatuses,
    IReadOnlyList<string> ChainStatuses,
    IReadOnlyList<string> RelationshipTypes,
    IReadOnlyList<string> Directions,
    IReadOnlyList<string> ExternalRefTypes,
    IReadOnlyList<string> LinkRoles)
{
    public static ConceptGraphVocabulary Current => new(
        Domain.Entities.ConceptStatuses.All,
        Domain.Entities.ConceptChainStatuses.All,
        Domain.Entities.ConceptRelationshipTypes.All,
        Domain.Entities.ConceptDirections.All,
        Domain.Entities.ConceptExternalRefTypes.All,
        Domain.Entities.ConceptLinkRoles.All);
}

/// <summary>Which list filters the runtime actually supports server-side, so a UI never fakes an unsupported filter.</summary>
public sealed record ConceptGraphSupportedFilters(
    IReadOnlyList<string> ConceptTypes,
    IReadOnlyList<string> ConceptNodes,
    IReadOnlyList<string> ConceptRelationships,
    IReadOnlyList<string> ConceptChainTemplates,
    IReadOnlyList<string> ContentConceptLinks,
    IReadOnlyList<string> Graph)
{
    public static ConceptGraphSupportedFilters Current => new(
        ConceptTypes: new[] { "subjectId", "status", "search", "includeArchived" },
        ConceptNodes: new[]
        {
            "subjectId", "conceptTypeId", "status", "externalRefType", "effectiveAt", "search", "includeArchived"
        },
        ConceptRelationships: new[]
        {
            "subjectId", "fromNodeId", "toNodeId", "relationshipType", "conformance", "status", "includeArchived"
        },
        ConceptChainTemplates: new[] { "subjectId", "status", "effectiveAt", "search", "includeArchived" },
        ContentConceptLinks: new[] { "contentId", "conceptNodeId", "linkRole", "includeArchived" },
        Graph: new[] { "subjectId", "effectiveAt", "includeArchived" });
}

public sealed class GetConceptGraphContractHandler
    : IRequestHandler<GetConceptGraphContractQuery, Response<ConceptGraphContractDto>>
{
    public const string ModuleId = "MOD-0162-FU03";
    public const string ModuleName = "Concept Graph Runtime + UI";
    public const string Service = "Diten.CrmService";
    public const string RuntimeScope =
        "FU03-concept-graph-runtime (ConceptType / ConceptNode / ConceptRelationship / ConceptChainTemplate / " +
        "KnowledgeContentConceptLink authoring, archive lifecycle, effective dating, cycle detection, template " +
        "conformance diagnostics, adjacency read). NO traversal / resolution / recommendation / AI / best-next-content " +
        "/ digital-detailing / visit-route planning engine is opened; MDM master (Global Product included) is a " +
        "read-only ExternalRef and is never mutated.";

    private static readonly IReadOnlyList<string> AllReasonCodes = new[]
    {
        ConceptGraphReasonCodes.TypeCreated, ConceptGraphReasonCodes.TypeUpdated, ConceptGraphReasonCodes.TypeArchived,
        ConceptGraphReasonCodes.TypeDuplicateCode,
        ConceptGraphReasonCodes.NodeCreated, ConceptGraphReasonCodes.NodeUpdated, ConceptGraphReasonCodes.NodeArchived,
        ConceptGraphReasonCodes.NodeDuplicateCode, ConceptGraphReasonCodes.NodeSubjectTypeMismatch,
        ConceptGraphReasonCodes.RelationshipCreated, ConceptGraphReasonCodes.RelationshipUpdated,
        ConceptGraphReasonCodes.RelationshipArchived, ConceptGraphReasonCodes.RelationshipSelfLoop,
        ConceptGraphReasonCodes.RelationshipCrossSubject, ConceptGraphReasonCodes.RelationshipCycle,
        ConceptGraphReasonCodes.RelationshipDuplicateActive, ConceptGraphReasonCodes.RelationshipNonConforming,
        ConceptGraphReasonCodes.ChainTemplateCreated, ConceptGraphReasonCodes.ChainTemplateUpdated,
        ConceptGraphReasonCodes.ChainTemplateArchived, ConceptGraphReasonCodes.ChainTemplateInvalidSequence,
        ConceptGraphReasonCodes.ChainTemplatePublishOverlap,
        ConceptGraphReasonCodes.ContentLinkCreated, ConceptGraphReasonCodes.ContentLinkArchived,
        ConceptGraphReasonCodes.ContentLinkRelationshipMismatch,
        ConceptGraphReasonCodes.ArchivedNoMutation, ConceptGraphReasonCodes.ReferenceArchived,
        ConceptGraphReasonCodes.ContentConceptNodeUnresolved
    };

    private static readonly IReadOnlyList<string> CurrentLimitations = new[]
    {
        "the concept graph answers ONLY 'which concepts exist in this subject, how are they linked, what is the expected chain, and which content links to which concept?' — never in-what-order (FU01A), across-which-visits (FU01B), how-often/to-whom (MOD-0165/0167), who/when-to-visit (MOD-0155) or best-next-content (F4 digital detailing)",
        "/concept-graph reads ADJACENCY only: by-node is exactly 1 hop, by-content is exactly 2 edge layers; there is no depth/maxHops parameter and no transitive closure — traversal / best-path / scoring / recommendation is an engine (F4/MOD-0058)",
        "vocabulary (concept-status / chain-status / relationship-type / direction / external-ref-type / link-role) is IN-DOMAIN (structural): the runtime validates against it and never fails open on an unpublished MOD-0048 set; an unknown value is a 400; MOD-0048 publish is a separate operator follow-up (F-RD). RelationshipType is the boundary FU01C §5 canonical set (D3=A)",
        "a ConceptNode is never the SoR of any master: it carries at most one ExternalRef (global-product / document / audience-profile / reference-data-value / other) and copies no master field; the product target is the MDM Global Product and MDM is never mutated (read-only reference)",
        "cycle detection runs at read-time over ACTIVE edges with no cache; a self-loop and a cross-subject edge are 400; a duplicate active (From, To, RelationshipType) is 409; a non-conforming (fromType → toType) pair is NOT rejected — it is stored IsTemplateConforming=false and stays visible",
        "a published chain template freezes its OrderedConceptTypes (a change needs a new version) and two published versions of one ChainCode may not overlap in effective window (409); the same type never appears twice in a sequence (v1; recursion is F7)",
        "KnowledgeContentConceptLink is many-to-many and always anchored to a node; an optional relationship context must contain that node — there is no node-less pure relationship link; the FU02 KnowledgeContent.ConceptNodeId shortcut stays and is neither removed nor moved (additive)",
        "KnowledgeContent.ConceptNodeId is now resolved to a live, non-archived, same-tenant node — but ONLY on create or when the value actually changes (dirty-check); an untouched legacy value never trips a 400 on save",
        "concept / node / relationship / chain-template / link is never hard-deleted; closing one is a soft archive that stays readable, and archived rows accept no update (409)",
        "RBAC keys crm.knowledge.concept.* are defined but NOT seeded; the endpoints run on the documented DEV-ONLY territory fallback (follow-up MOD-0162-FU03-RBAC); the Global Product picker additionally needs the MDM-owned mdm.global-products.read",
        "there is no DELETE and no PATCH endpoint; TenantId is server-resolved and never accepted from a payload"
    };

    private readonly ITenantContext _tenant;

    public GetConceptGraphContractHandler(ITenantContext tenant)
    {
        _tenant = tenant;
    }

    public Task<Response<ConceptGraphContractDto>> Handle(
        GetConceptGraphContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Task.FromResult(Response<ConceptGraphContractDto>.Fail("Tenant context is required.", 400));
        }

        var dto = new ConceptGraphContractDto(
            ModuleId,
            ModuleName,
            Service,
            RuntimeScope,
            tenantId,
            IsReady: true,
            ConceptGraphFeatureFlags.Current,
            ConceptGraphVocabulary.Current,
            ConceptGraphSupportedFilters.Current,
            AllReasonCodes,
            ConceptPermissions.All,
            CurrentLimitations);

        return Task.FromResult(Response<ConceptGraphContractDto>.Success(dto));
    }
}
