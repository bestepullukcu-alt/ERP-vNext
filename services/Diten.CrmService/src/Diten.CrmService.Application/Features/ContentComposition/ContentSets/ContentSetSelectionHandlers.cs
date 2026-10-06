using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSets;

// SCMM-14 — component / claim selection + arrangement handlers. Every selection is arranged into a template slot
// validated against the PINNED template (cardinality / branch — SCMM-10) and, for components, pinned at the content's
// current version + language (SCMM-13). Invalid slot / branch → 400; cardinality overflow / missing ref → 400/409.

public sealed class AddContentSetComponentHandler : ContentSetWriteHandlerBase,
    IRequestHandler<AddContentSetComponentCommand, Response<Guid>>
{
    private readonly IConceptChainTemplateRepository _templates;
    private readonly IKnowledgeContentRepository _contents;

    public AddContentSetComponentHandler(
        ITenantContext tenant, IActorContext actor, IContentSetRepository sets,
        IConceptChainTemplateRepository templates, IKnowledgeContentRepository contents,
        IContentCompositionAuditPublisher? audit = null)
        : base(tenant, actor, sets, audit)
    {
        _templates = templates;
        _contents = contents;
    }

    public async Task<Response<Guid>> Handle(AddContentSetComponentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (set, tenantId, error, status) = await LoadEditableAsync(request.ContentSetId, cancellationToken);
        if (set is null)
        {
            return Response<Guid>.Fail(error!, status);
        }

        var template = await _templates.GetByIdAsync(tenantId, set.Template.ConceptChainTemplateId, cancellationToken);
        if (template is null)
        {
            return Response<Guid>.Fail("The pinned composition template is no longer available.", 409);
        }

        var (slotError, slotStatus) = ContentSetArrangement.ValidateSlot(
            template, request.TemplateStepId, request.BranchId, request.Position);
        if (slotError is not null)
        {
            return Response<Guid>.Fail(slotError, slotStatus);
        }

        var content = await _contents.GetByIdAsync(tenantId, request.KnowledgeContentId, cancellationToken);
        if (content is null)
        {
            return Response<Guid>.Fail("KnowledgeContentId does not reference content in this tenant.", 404);
        }

        if (content.IsArchived())
        {
            return Response<Guid>.Fail("An archived content component cannot be selected.", 409);
        }

        // Cardinality: a per-position MaxSelection caps components in the same branch slot.
        var max = ContentSetArrangement.MaxSelectionFor(template, request.TemplateStepId, request.BranchId);
        if (max is { } cap)
        {
            var current = set.SelectedComponents.Count(c =>
                ContentSetArrangement.SameSlot(c.Arrangement, request.TemplateStepId, request.BranchId));
            if (current + 1 > cap)
            {
                return Response<Guid>.Fail(
                    $"The slot allows at most {cap} component(s); it already holds {current}.", 409);
            }
        }

        var selection = new ContentSetComponent
        {
            SelectionId = Guid.NewGuid(),
            KnowledgeContentId = content.Id,
            // Ref PIN (D14-d): the content's current version + language are snapshotted now.
            ContentVersion = content.ContentVersion,
            LanguageCode = content.LanguageCode,
            Role = string.IsNullOrWhiteSpace(request.Role) ? null : request.Role.Trim(),
            Arrangement = new ContentArrangement
            {
                TemplateStepId = request.TemplateStepId,
                BranchId = string.IsNullOrWhiteSpace(request.BranchId) ? null : request.BranchId.Trim(),
                Position = request.Position
            }
        };

        set.SelectedComponents.Add(selection);
        Stamp(set);
        await Sets.UpdateAsync(set, cancellationToken);
        await PublishAsync(ContentSetReasonCodes.ComponentAdded, tenantId, set, cancellationToken);
        return Response<Guid>.Success(selection.SelectionId, 201);
    }
}

public sealed class RemoveContentSetComponentHandler : ContentSetWriteHandlerBase,
    IRequestHandler<RemoveContentSetComponentCommand, Response<bool>>
{
    public RemoveContentSetComponentHandler(
        ITenantContext tenant, IActorContext actor, IContentSetRepository sets,
        IContentCompositionAuditPublisher? audit = null)
        : base(tenant, actor, sets, audit) { }

    public async Task<Response<bool>> Handle(RemoveContentSetComponentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (set, tenantId, error, status) = await LoadEditableAsync(request.ContentSetId, cancellationToken);
        if (set is null)
        {
            return Response<bool>.Fail(error!, status);
        }

        var removed = set.SelectedComponents.RemoveAll(c => c.SelectionId == request.SelectionId);
        if (removed == 0)
        {
            return Response<bool>.Fail("Component selection not found in this content set.", 404);
        }

        Stamp(set);
        await Sets.UpdateAsync(set, cancellationToken);
        await PublishAsync(ContentSetReasonCodes.ComponentRemoved, tenantId, set, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArrangeContentSetComponentHandler : ContentSetWriteHandlerBase,
    IRequestHandler<ArrangeContentSetComponentCommand, Response<bool>>
{
    private readonly IConceptChainTemplateRepository _templates;

    public ArrangeContentSetComponentHandler(
        ITenantContext tenant, IActorContext actor, IContentSetRepository sets,
        IConceptChainTemplateRepository templates, IContentCompositionAuditPublisher? audit = null)
        : base(tenant, actor, sets, audit)
        => _templates = templates;

    public async Task<Response<bool>> Handle(ArrangeContentSetComponentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (set, tenantId, error, status) = await LoadEditableAsync(request.ContentSetId, cancellationToken);
        if (set is null)
        {
            return Response<bool>.Fail(error!, status);
        }

        var selection = set.SelectedComponents.FirstOrDefault(c => c.SelectionId == request.SelectionId);
        if (selection is null)
        {
            return Response<bool>.Fail("Component selection not found in this content set.", 404);
        }

        var template = await _templates.GetByIdAsync(tenantId, set.Template.ConceptChainTemplateId, cancellationToken);
        if (template is null)
        {
            return Response<bool>.Fail("The pinned composition template is no longer available.", 409);
        }

        var (slotError, slotStatus) = ContentSetArrangement.ValidateSlot(
            template, request.TemplateStepId, request.BranchId, request.Position);
        if (slotError is not null)
        {
            return Response<bool>.Fail(slotError, slotStatus);
        }

        var max = ContentSetArrangement.MaxSelectionFor(template, request.TemplateStepId, request.BranchId);
        if (max is { } cap)
        {
            var current = set.SelectedComponents.Count(c =>
                c.SelectionId != selection.SelectionId
                && ContentSetArrangement.SameSlot(c.Arrangement, request.TemplateStepId, request.BranchId));
            if (current + 1 > cap)
            {
                return Response<bool>.Fail(
                    $"The slot allows at most {cap} component(s); it already holds {current}.", 409);
            }
        }

        selection.Arrangement = new ContentArrangement
        {
            TemplateStepId = request.TemplateStepId,
            BranchId = string.IsNullOrWhiteSpace(request.BranchId) ? null : request.BranchId.Trim(),
            Position = request.Position
        };
        Stamp(set);
        await Sets.UpdateAsync(set, cancellationToken);
        await PublishAsync(ContentSetReasonCodes.ComponentArranged, tenantId, set, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class AddContentSetClaimHandler : ContentSetWriteHandlerBase,
    IRequestHandler<AddContentSetClaimCommand, Response<Guid>>
{
    private readonly IConceptChainTemplateRepository _templates;
    private readonly IClaimRepository _claims;

    public AddContentSetClaimHandler(
        ITenantContext tenant, IActorContext actor, IContentSetRepository sets,
        IConceptChainTemplateRepository templates, IClaimRepository claims,
        IContentCompositionAuditPublisher? audit = null)
        : base(tenant, actor, sets, audit)
    {
        _templates = templates;
        _claims = claims;
    }

    public async Task<Response<Guid>> Handle(AddContentSetClaimCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (set, tenantId, error, status) = await LoadEditableAsync(request.ContentSetId, cancellationToken);
        if (set is null)
        {
            return Response<Guid>.Fail(error!, status);
        }

        var template = await _templates.GetByIdAsync(tenantId, set.Template.ConceptChainTemplateId, cancellationToken);
        if (template is null)
        {
            return Response<Guid>.Fail("The pinned composition template is no longer available.", 409);
        }

        var (slotError, slotStatus) = ContentSetArrangement.ValidateSlot(
            template, request.TemplateStepId, request.BranchId, request.Position);
        if (slotError is not null)
        {
            return Response<Guid>.Fail(slotError, slotStatus);
        }

        var claim = await _claims.GetByIdAsync(tenantId, request.ClaimId, cancellationToken);
        if (claim is null)
        {
            return Response<Guid>.Fail("ClaimId does not reference a claim in this tenant.", 404);
        }

        if (claim.IsArchived())
        {
            return Response<Guid>.Fail("An archived claim cannot be selected.", 409);
        }

        var selection = new ContentSetClaim
        {
            SelectionId = Guid.NewGuid(),
            ClaimId = claim.Id,
            ClaimVersion = claim.ClaimVersion,   // Ref PIN (D14-d)
            Arrangement = new ContentArrangement
            {
                TemplateStepId = request.TemplateStepId,
                BranchId = string.IsNullOrWhiteSpace(request.BranchId) ? null : request.BranchId.Trim(),
                Position = request.Position
            }
        };

        set.SelectedClaims.Add(selection);
        Stamp(set);
        await Sets.UpdateAsync(set, cancellationToken);
        await PublishAsync(ContentSetReasonCodes.ClaimAdded, tenantId, set, cancellationToken);
        return Response<Guid>.Success(selection.SelectionId, 201);
    }
}

public sealed class RemoveContentSetClaimHandler : ContentSetWriteHandlerBase,
    IRequestHandler<RemoveContentSetClaimCommand, Response<bool>>
{
    public RemoveContentSetClaimHandler(
        ITenantContext tenant, IActorContext actor, IContentSetRepository sets,
        IContentCompositionAuditPublisher? audit = null)
        : base(tenant, actor, sets, audit) { }

    public async Task<Response<bool>> Handle(RemoveContentSetClaimCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (set, tenantId, error, status) = await LoadEditableAsync(request.ContentSetId, cancellationToken);
        if (set is null)
        {
            return Response<bool>.Fail(error!, status);
        }

        var removed = set.SelectedClaims.RemoveAll(c => c.SelectionId == request.SelectionId);
        if (removed == 0)
        {
            return Response<bool>.Fail("Claim selection not found in this content set.", 404);
        }

        Stamp(set);
        await Sets.UpdateAsync(set, cancellationToken);
        await PublishAsync(ContentSetReasonCodes.ClaimRemoved, tenantId, set, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArrangeContentSetClaimHandler : ContentSetWriteHandlerBase,
    IRequestHandler<ArrangeContentSetClaimCommand, Response<bool>>
{
    private readonly IConceptChainTemplateRepository _templates;

    public ArrangeContentSetClaimHandler(
        ITenantContext tenant, IActorContext actor, IContentSetRepository sets,
        IConceptChainTemplateRepository templates, IContentCompositionAuditPublisher? audit = null)
        : base(tenant, actor, sets, audit)
        => _templates = templates;

    public async Task<Response<bool>> Handle(ArrangeContentSetClaimCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (set, tenantId, error, status) = await LoadEditableAsync(request.ContentSetId, cancellationToken);
        if (set is null)
        {
            return Response<bool>.Fail(error!, status);
        }

        var selection = set.SelectedClaims.FirstOrDefault(c => c.SelectionId == request.SelectionId);
        if (selection is null)
        {
            return Response<bool>.Fail("Claim selection not found in this content set.", 404);
        }

        var template = await _templates.GetByIdAsync(tenantId, set.Template.ConceptChainTemplateId, cancellationToken);
        if (template is null)
        {
            return Response<bool>.Fail("The pinned composition template is no longer available.", 409);
        }

        var (slotError, slotStatus) = ContentSetArrangement.ValidateSlot(
            template, request.TemplateStepId, request.BranchId, request.Position);
        if (slotError is not null)
        {
            return Response<bool>.Fail(slotError, slotStatus);
        }

        selection.Arrangement = new ContentArrangement
        {
            TemplateStepId = request.TemplateStepId,
            BranchId = string.IsNullOrWhiteSpace(request.BranchId) ? null : request.BranchId.Trim(),
            Position = request.Position
        };
        Stamp(set);
        await Sets.UpdateAsync(set, cancellationToken);
        await PublishAsync(ContentSetReasonCodes.ClaimArranged, tenantId, set, cancellationToken);
        return Response<bool>.Success(true);
    }
}
