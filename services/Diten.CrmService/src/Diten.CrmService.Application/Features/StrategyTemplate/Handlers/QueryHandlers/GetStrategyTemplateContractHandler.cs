using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.StrategyTemplate.Contract;
using Diten.CrmService.Application.Features.StrategyTemplate.Queries;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Handlers.QueryHandlers;

/// <summary>
/// Publishes what this FU is and, just as importantly, what it is NOT. The limitations below are the contract a
/// consumer can rely on: they say out loud that a template produces nothing, that a declared rhythm is not binding, and
/// that the product-to-SKU containment is not verified.
/// </summary>
public sealed class GetStrategyTemplateContractHandler
    : IRequestHandler<GetStrategyTemplateContractQuery, Response<StrategyTemplateContractDto>>
{
    public const string ModuleId = "MOD-0167-FU04";
    public const string ModuleName = "Strategy Template - Segment x Product SKU Mix x Content Playbook";
    public const string Service = "Diten.CrmService";

    public const string RuntimeScope =
        "FU04-strategy-template (a reusable playbook that BINDS and never produces: one or more MOD-0167 FU02 segments "
        + "pinned by id, a frequency INTENT that is either a reference to an existing MOD-0165 policy or a declared "
        + "non-binding rhythm, MDM product lines with an exact-100.00 SKU percentage split, and pinned published "
        + "MOD-0162 KnowledgePath / ContentEngagementJourney content; create / read / update / activate / archive, "
        + "business versioning with a binding freeze and a new-version clone, effective dating, and a read-only "
        + "consumption seam). NO apply / generate, MicroTarget row, cycle period, VisitFrequencyPolicy write, "
        + "CampaignTarget generation, membership resolution, UCLN loyalty / promo-week / patient planning, SubjectList "
        + "or audience aggregate, brand binding, Lsku binding or strategy engine is opened. MOD-0167 FU02, MOD-0165 and "
        + "MOD-0162 are READ-ONLY sources and are never mutated; MDM is consulted only to prove that a bound reference "
        + "exists.";

    private static readonly IReadOnlyList<string> CurrentLimitations = new[]
    {
        "a StrategyTemplate BINDS and never produces: no member, no VisitFrequencyPolicy, no CampaignTarget, no cycle row and no MicroTarget is created by any endpoint here. Applying a play to a period is MOD-0155, and there is deliberately no /apply, /generate or /resolve path in this FU",
        "the frequency intent NEVER writes a policy. In policy-reference mode it points at an existing active MOD-0165 policy; in declared-intent mode it records the author's rhythm in MOD-0165's own vocabulary but is explicitly NON-BINDING and the MOD-0165 resolve provider does not read it; 'none' is an answer, not an omission",
        "a segment binding pins a CONCRETE segment id, which in MOD-0167 FU02 means a concrete version. It never follows the lineage to 'the latest active version', because that would silently change who the play is about",
        "every bound segment must share the template's SubjectType, and at activate time every one of them must be active. A play whose population is still a draft is not put live",
        "this FU sees NO member: ISegmentMembershipReader is not injected anywhere, resolve is never called, and no response carries a subject id or a member count. Reading a play never implies the right to see the people inside its segments (that stays crm.segment.resolve)",
        "SKU percentages on a sku-allocated line total EXACTLY 100.00, computed in decimal. There is no tolerance band, no auto-normalisation and no 'add the remainder to the last row': the refusal reports the computed total so the author can see their own arithmetic",
        "whether a bound Gsku actually belongs to the bound GlobalProduct is NOT verified and is never reported as verified. MDM's Gsku carries no GlobalProductId and its selector offers no product filter, and this FU may not open a new MDM read surface, so containment is the author's responsibility and containmentVerified is always false (F-SKU-PRODUCT-LINK)",
        "MDM references are proven fail-closed BEFORE any write: a 404 means the binding is not authorable (400) and an unreachable dependency, an auth rejection or a malformed body means we do not know (503, nothing persisted). Each distinct id is proven once per request - a per-request dedup, never a cache",
        "content bindings are TYPED and must be published and non-archived at binding time; a draft story cannot be promised to the field. The binding is pinned to that concrete row, so a later version of the same code is a different binding",
        "activating a template FREEZES all four binding lists: changing a binding needs a new version, whose clone gets fresh child ids. Metadata (name, description, notes, effective window) stays editable, and the freeze guard compares what the play BINDS rather than the ids a payload arrived with",
        "brand is absent by product decision (D-BRAND): there is no BrandId field, no brand picker and no brand reference kind. Lsku (market-local SKU) is deferred (F-LSKU) because it would add a market dimension the template does not have",
        "vocabulary is IN-DOMAIN (D-VOCAB=A): the runtime validates against its own constants and never fails open on an unpublished MOD-0048 set. The declared frequency values are validated against MOD-0165's own constants rather than a copy of them",
        "a bound list is an ENUMERATION, not an expression: no union, intersect or minus is applied to the segment bindings, and BindingRole (including exclusion-note) is a label no handler branches on. A consumer applies its own combination rule",
        "the /bindings view reports derived freshness hints (a superseded segment, an archived content row, an inactive policy) as WARNINGS. They never invalidate an active play, because a past play must stay explainable",
        "RBAC keys crm.strategy-template.{read,manage,activate} are DEFINED but NOT seeded; the endpoints run on the documented DEV-ONLY territory fallback (follow-up F-RBAC), under which activate collapses onto manage so the SoD cannot be enforced in dev",
        "there is no DELETE and no PATCH endpoint anywhere; closing a template is a soft archive, and TenantId is server-resolved and never accepted from a payload"
    };

    private readonly ITenantContext _tenant;

    public GetStrategyTemplateContractHandler(ITenantContext tenant) => _tenant = tenant;

    public Task<Response<StrategyTemplateContractDto>> Handle(
        GetStrategyTemplateContractQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Task.FromResult(
                Response<StrategyTemplateContractDto>.Fail("Tenant context is required.", 400));
        }

        var dto = new StrategyTemplateContractDto(
            ModuleId,
            ModuleName,
            Service,
            RuntimeScope,
            tenantId,
            IsReady: true,
            StrategyTemplateFeatureFlags.Current,
            StrategyTemplateVocabularyDto.Current,
            StrategyTemplateSupportedFilters.Current,
            StrategyTemplateContractLimits.Current,
            StrategyTemplateErrorCodes.All,
            StrategyTemplatePermissions.All,
            CurrentLimitations);

        return Task.FromResult(Response<StrategyTemplateContractDto>.Success(dto));
    }
}
