using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Contract;
using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.QueryHandlers;

/// <summary>
/// Publishes what this FU is and, just as importantly, what it is NOT. The limitations below are the contract a
/// consumer can rely on: they say out loud that membership is never stored, that a ceiling breach is a refusal rather
/// than a shortened list, and that an in-service uncertainty and a cross-process one are answered differently on
/// purpose.
/// </summary>
public sealed class GetSegmentContractHandler : IRequestHandler<GetSegmentContractQuery, Response<SegmentContractDto>>
{
    public const string ModuleId = "MOD-0167-FU02";
    public const string ModuleName = "Segment Foundation - Definition, Criteria, Membership Resolution, Target Customer";
    public const string Service = "Diten.CrmService";

    public const string RuntimeScope =
        "FU02-segment-foundation (Segment aggregate with an EMBEDDED typed predicate tree - static / dynamic / hybrid, "
        + "create / read / update / activate / archive, business versioning with a criteria freeze and a new-version "
        + "clone that remaps node ids, effective dating; a separate TargetCustomer aggregate carrying MANUAL "
        + "include / exclude only; deterministic real-time membership resolution that PERSISTS NOTHING; a closed "
        + "attribute catalog with a contract surface; and a read-only consumption seam). NO materialised membership, "
        + "refresh job, membership history, segment-of-segment, ICP scoring, computed attribute, usage log, "
        + "StrategyTemplate / SubjectList / UCLN, CampaignTarget generation or VisitFrequencyPolicy write is opened. "
        + "MOD-0149 / MOD-0150 / MOD-0151 / MOD-0164 and the MOD-0162 FU03 concept graph are READ-ONLY sources and are "
        + "never mutated; MDM is consulted only to prove a criterion VALUE.";

    private static readonly IReadOnlyList<string> CurrentLimitations = new[]
    {
        "a Segment is a DEFINITION, not a list: dynamic membership is never written to any collection, and there is no MemberIds, MemberCount or LastResolvedAt field on the segment, on Contact or on Account. Materialisation is a performance optimisation and an optimisation designed without measurement is a guess (FU-B)",
        "resolve and membership/evaluate are pure reads: they create nothing, update nothing and write no usage log (usage logging is FU-D). Running a resolution leaves every collection byte-identical",
        "the criteria tree is EMBEDDED in the segment document as a typed, stored predicate tree with ParentNodeId - not a query DSL (no parser, no injection surface, no runtime-only errors) and not tags (which cannot express ranges, dates or is-null). It is data and never executes code",
        "scale is bounded by construction: phase 1 is ONE Mongo pushdown (an over-approximation, so it can only return a superset), phase 2 adds ONE bulk read per source, and no code path reads per candidate. Above MaxCandidateSet the answer is 422 and NOTHING is returned - a partial member list is more dangerous than no list because nobody can tell it is partial",
        "fail-closed is asymmetric on purpose: an IN-SERVICE uncertainty (consent unknown, no valid territory model, a product missing from the concept graph) eliminates the candidate with a specific reason code and the resolution COMPLETES; a CROSS-PROCESS uncertainty (MDM unreachable) is a 503 with no partial result and nothing persisted",
        "consent unknown is NEVER allowed. It eliminates the candidate with consent_unknown unless the author explicitly asked for unknown, and no code path turns it into a match",
        "concept.affinity derives product interest from the MOD-0162 FU03 concept graph READ-ONLY: bounded traversal (default depth 1, ceiling 2, no transitive closure), only outbound addresses / belongs-to edges, one bulk graph read per resolution. The graph is never written, no repository signature is widened and no graph aggregate or endpoint is added",
        "concept.affinity is deliberately LIVE: activate freezes the criteria TREE (which question is asked), never the ANSWER of a derivation. Adding a graph edge changes what the same segment version returns - the determinism contract is stated over UNCHANGED source data",
        "a product with no node in the concept graph yields an EMPTY member set with concept_product_node_missing, at 200. It is never a 503 and never everybody-matches",
        "the attribute catalog is CLOSED: an undeclared AttributeCode is a 400 at authoring time. A free field name would make a rule silently breakable - rename the field and the rule matches nothing while reporting no error",
        "a dynamic segment REFUSES manual membership rows (400). A manual exception belongs to a hybrid segment, so the dynamic label never lies about where a member came from",
        "TargetCustomer carries manual-include / manual-exclude and nothing else: derived membership is never written there, and switching mode is an UPDATE of the row rather than a second, contradictory one",
        "activating a segment FREEZES its criteria: changing the rule needs a new version, whose clone gets new NodeIds with the parent references remapped. A superseded version stays RESOLVABLE and reports superseded=true, because explaining a past selection needs the rule that was in force then",
        "vocabulary is IN-DOMAIN (D-VOCAB=A): the runtime validates against its own constants and never fails open on an unpublished MOD-0048 set; publishing the same sets is a separate operator follow-up (F-RD)",
        "this FU writes no VisitFrequencyPolicy and generates no CampaignTarget - both stay in MOD-0165, and the snapshot connection is a separate follow-up (F-SNAPSHOT). StrategyTemplate / SubjectList / UCLN belong to FU-C",
        "RBAC keys crm.segment.{read,manage,activate,resolve,target.read,target.manage} are DEFINED but NOT seeded; the endpoints run on the documented DEV-ONLY territory fallback (follow-up F-RBAC). Under that fallback activate collapses onto manage and resolve onto read, so the SoD and the member-identity (PII) split cannot be enforced in dev",
        "there is no DELETE and no PATCH endpoint anywhere; closing anything is a soft archive, and TenantId is server-resolved and never accepted from a payload"
    };

    private readonly ITenantContext _tenant;

    public GetSegmentContractHandler(ITenantContext tenant) => _tenant = tenant;

    public Task<Response<SegmentContractDto>> Handle(
        GetSegmentContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Task.FromResult(Response<SegmentContractDto>.Fail("Tenant context is required.", 400));
        }

        var dto = new SegmentContractDto(
            ModuleId,
            ModuleName,
            Service,
            RuntimeScope,
            tenantId,
            IsReady: true,
            SegmentFeatureFlags.Current,
            SegmentVocabularyDto.Current,
            SegmentSupportedFilters.Current,
            SegmentContractLimits.Current,
            SegmentReasonCodes.All,
            SegmentErrorCodes.All,
            SegmentPermissions.All,
            CurrentLimitations);

        return Task.FromResult(Response<SegmentContractDto>.Success(dto));
    }
}
