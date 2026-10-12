using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Knowledge.Chain;
using Diten.CrmService.Application.Features.Knowledge.Path.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Path.Handlers;

/// <summary>
/// WP-KP-1 — binds a legacy (chain-less) DRAFT path to a chain + country + language, once (the back end of the
/// legacy-path wizard, DESIGN-KP-STUDIO §6). The path subject must already be the chain's subject (409
/// <c>chain_subject_mismatch</c>) and the active steps' content must be in the chosen language (409
/// <c>component_language_mismatch</c>). Existing steps keep their order and get no arrangement — the wizard places them
/// through the step update. A second bind is 409 <c>path_identity_locked</c>.
/// </summary>
public sealed class BindKnowledgePathChainHandler : IRequestHandler<BindKnowledgePathChainCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;
    private readonly IConceptChainTemplateRepository _templates;
    private readonly IKnowledgeContentRepository _contents;
    private readonly IChainContextResolver _chainContext;
    private readonly IReferenceDataCatalogReader? _catalog;

    public BindKnowledgePathChainHandler(
        ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths,
        IConceptChainTemplateRepository templates, IKnowledgeContentRepository contents,
        IChainContextResolver chainContext, IReferenceDataCatalogReader? catalog = null)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
        _templates = templates;
        _contents = contents;
        _chainContext = chainContext;
        _catalog = catalog;
    }

    public async Task<Response<bool>> Handle(BindKnowledgePathChainCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var path = await _paths.GetByIdAsync(tenantId, request.PathId, cancellationToken);
        if (path is null)
        {
            return Response<bool>.Fail("Knowledge path not found.", 404);
        }

        if (path.IsArchived())
        {
            return Response<bool>.Fail("An archived path cannot be modified.", 409);
        }

        if (request.ExpectedVersion is { } ev && ev != path.Version)
        {
            return Response<bool>.Fail("The path was modified by another writer; reload and retry.", 409);
        }

        if (path.ChainTemplate is not null)
        {
            return Response<bool>.Fail(new[] { KnowledgePathStudioErrors.PathIdentityLocked,
                "This path is already bound to a chain; a path is bound once (D-KP-3)." }, 409);
        }

        if (!string.Equals(path.PathStatus, KnowledgePathStatuses.Draft, StringComparison.OrdinalIgnoreCase)
            || path.IsStepSetFrozen())
        {
            return Response<bool>.Fail("Only a draft path can be bound to a chain.", 409);
        }

        var (template, chainFailure) = await KnowledgePathStudio.BindableChainAsync(
            _templates, tenantId, request.ChainTemplateId, cancellationToken);
        if (chainFailure is not null)
        {
            return chainFailure.To<bool>();
        }

        if (template!.SubjectId != path.SubjectId)
        {
            return Response<bool>.Fail(new[] { KnowledgePathStudioErrors.ChainSubjectMismatch,
                "The path subject is not the chain template's subject." }, 409);
        }

        var context = await ChainContextValidation.ValidateAsync(
            _catalog, request.CountryCode, request.LanguageCode, cancellationToken);
        if (!context.IsValid)
        {
            return Response<bool>.Fail(context.Errors!, context.StatusCode);
        }

        foreach (var step in path.ActiveSteps())
        {
            var content = await _contents.GetByIdAsync(tenantId, step.ContentId, cancellationToken);
            if (content is not null && !string.IsNullOrWhiteSpace(content.LanguageCode)
                && !ChainContextValidation.SameLanguage(content.LanguageCode, context.Language))
            {
                return Response<bool>.Fail(new[] { ChainContextErrors.ComponentLanguageMismatch,
                    $"Step '{step.StepCode}' content is in '{content.LanguageCode}', not '{context.Language}'; replace or "
                    + "archive it before binding." }, 409);
            }
        }

        var derived = await _chainContext.ResolveAsync(tenantId, template, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        path.ChainTemplate = new KnowledgePathChainRef
        {
            ConceptChainTemplateId = template.Id, ChainVersion = template.ChainVersion
        };
        path.CountryCode = context.Country;
        path.LanguageCode = context.Language;
        path.AudienceProfileId = derived.SingleAudienceProfileId;
        path.UpdatedAt = now;
        path.UpdatedBy = _actor.ActorName;

        return await KnowledgePathClaimWrite.SaveAsync(_paths, path, cancellationToken);
    }
}

/// <summary>Shared loading for the claim sub-commands: tenant, path, archive / freeze / concurrency, chain-bound.</summary>
internal static class KnowledgePathClaimWrite
{
    public static async Task<(KnowledgePath? Path, Guid TenantId, Response<bool>? Error)> LoadEditableAsync(
        ITenantContext tenant, IKnowledgePathRepository paths, Guid pathId, int? expectedVersion, CancellationToken ct)
    {
        if (tenant.TenantId is not { } tenantId)
        {
            return (null, Guid.Empty, Response<bool>.Fail("Tenant context is required.", 400));
        }

        var path = await paths.GetByIdAsync(tenantId, pathId, ct);
        if (path is null)
        {
            return (null, tenantId, Response<bool>.Fail("Knowledge path not found.", 404));
        }

        if (path.IsArchived())
        {
            return (null, tenantId, Response<bool>.Fail("An archived path cannot be modified.", 409));
        }

        // V-S02 pattern — a published (frozen) path takes no claim change; a change needs a new version.
        if (KnowledgePathWrite.EnsureNotFrozen(path) is { } frozen)
        {
            return (null, tenantId, Response<bool>.Fail(frozen, 409));
        }

        if (expectedVersion is { } ev && ev != path.Version)
        {
            return (null, tenantId, Response<bool>.Fail("The path was modified by another writer; reload and retry.", 409));
        }

        if (path.ChainTemplate is null)
        {
            return (null, tenantId, KnowledgePathStudio.ChainRequired("A claim").To<bool>());
        }

        return (path, tenantId, null);
    }

    public static async Task<Response<bool>> SaveAsync(IKnowledgePathRepository paths, KnowledgePath path, CancellationToken ct)
    {
        var ok = await paths.ReplaceAsync(path, path.Version, ct);
        return ok
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The path was modified by another writer; reload and retry.", 409);
    }
}

/// <summary>
/// WP-KP-1 — places a claim on a slot of a chain-bound draft path. The claim must be in the tenant (404
/// <c>claim_not_found</c>), about the path's derived product (409 <c>claim_product_mismatch</c>), not already on the path
/// (409 <c>claim_ref_duplicate</c>) and on a slot of the pinned chain (400 <c>chain_slot_invalid</c>; claims are not the
/// chain's typed items, so no MaxSelection applies). An unapproved claim or one without a version in the path country IS
/// accepted — the read flags it and the release decides (KP-3).
/// </summary>
public sealed class AddKnowledgePathClaimHandler : IRequestHandler<AddKnowledgePathClaimCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;
    private readonly IConceptChainTemplateRepository _templates;
    private readonly ISubjectRepository _subjects;
    private readonly IClaimRepository _claims;

    public AddKnowledgePathClaimHandler(
        ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths,
        IConceptChainTemplateRepository templates, ISubjectRepository subjects, IClaimRepository claims)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
        _templates = templates;
        _subjects = subjects;
        _claims = claims;
    }

    public async Task<Response<bool>> Handle(AddKnowledgePathClaimCommand request, CancellationToken cancellationToken)
    {
        var (path, tenantId, error) = await KnowledgePathClaimWrite.LoadEditableAsync(
            _tenant, _paths, request.PathId, request.ExpectedVersion, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var claim = request.ClaimId == Guid.Empty
            ? null
            : await _claims.GetByIdAsync(tenantId, request.ClaimId, cancellationToken);
        if (claim is null)
        {
            return Response<bool>.Fail(new[] { KnowledgeContentClaimErrors.ClaimNotFound,
                "ClaimId does not reference a claim in this tenant." }, 404);
        }

        var (template, chainFailure) = await KnowledgePathStudio.PinnedChainAsync(_templates, tenantId, path!, cancellationToken);
        if (chainFailure is not null)
        {
            return chainFailure.To<bool>();
        }

        var (arrangement, slotFailure) = KnowledgePathStudio.Arrangement(template!, request.Arrangement);
        if (slotFailure is not null)
        {
            return slotFailure.To<bool>();
        }

        if (path!.Claims.Any(c => c.ClaimId == claim.Id
                || string.Equals(c.ClaimCode, claim.ClaimCode, StringComparison.OrdinalIgnoreCase)))
        {
            return Response<bool>.Fail(new[] { KnowledgeContentClaimErrors.ClaimRefDuplicate,
                $"Claim '{claim.ClaimCode}' is already on this path." }, 409);
        }

        var subject = await _subjects.GetByIdAsync(tenantId, template!.SubjectId, cancellationToken);
        if (ChainContextResolver.PrimaryGlobalProduct(subject)?.Id is { } productId
            && claim.ProductId is { } claimProduct && claimProduct != Guid.Empty && claimProduct != productId)
        {
            return Response<bool>.Fail(new[] { KnowledgeContentClaimErrors.ClaimProductMismatch,
                $"Claim '{claim.ClaimCode}' is about another product than this path's chain." }, 409);
        }

        path.Claims.Add(new KnowledgePathClaim { ClaimId = claim.Id, ClaimCode = claim.ClaimCode, Arrangement = arrangement! });
        path.UpdatedAt = DateTimeOffset.UtcNow;
        path.UpdatedBy = _actor.ActorName;
        return await KnowledgePathClaimWrite.SaveAsync(_paths, path, cancellationToken);
    }
}

/// <summary>WP-KP-1 — re-positions a claim inside its slot (the slot itself never changes — D-KP-7).</summary>
public sealed class ArrangeKnowledgePathClaimHandler : IRequestHandler<ArrangeKnowledgePathClaimCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;

    public ArrangeKnowledgePathClaimHandler(ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
    }

    public async Task<Response<bool>> Handle(ArrangeKnowledgePathClaimCommand request, CancellationToken cancellationToken)
    {
        var (path, _, error) = await KnowledgePathClaimWrite.LoadEditableAsync(
            _tenant, _paths, request.PathId, request.ExpectedVersion, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var placed = path!.Claims.FirstOrDefault(c => c.ClaimId == request.ClaimId);
        if (placed is null)
        {
            return Response<bool>.Fail("The claim is not on this path.", 404);
        }

        if (request.Position < 0)
        {
            return KnowledgePathStudio.SlotInvalid("Position must be zero or greater.").To<bool>();
        }

        placed.Arrangement.Position = request.Position;
        path.UpdatedAt = DateTimeOffset.UtcNow;
        path.UpdatedBy = _actor.ActorName;
        return await KnowledgePathClaimWrite.SaveAsync(_paths, path, cancellationToken);
    }
}

/// <summary>WP-KP-1 — takes a claim off a draft path (a reference only; the claim record is untouched).</summary>
public sealed class RemoveKnowledgePathClaimHandler : IRequestHandler<RemoveKnowledgePathClaimCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgePathRepository _paths;

    public RemoveKnowledgePathClaimHandler(ITenantContext tenant, IActorContext actor, IKnowledgePathRepository paths)
    {
        _tenant = tenant;
        _actor = actor;
        _paths = paths;
    }

    public async Task<Response<bool>> Handle(RemoveKnowledgePathClaimCommand request, CancellationToken cancellationToken)
    {
        var (path, _, error) = await KnowledgePathClaimWrite.LoadEditableAsync(
            _tenant, _paths, request.PathId, request.ExpectedVersion, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (path!.Claims.RemoveAll(c => c.ClaimId == request.ClaimId) == 0)
        {
            return Response<bool>.Fail("The claim is not on this path.", 404);
        }

        path.UpdatedAt = DateTimeOffset.UtcNow;
        path.UpdatedBy = _actor.ActorName;
        return await KnowledgePathClaimWrite.SaveAsync(_paths, path, cancellationToken);
    }
}
