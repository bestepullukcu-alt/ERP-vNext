using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.Concept;
using Diten.CrmService.Application.Features.Knowledge.Content.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Content.Handlers;

/// <summary>
/// SCMM-13 (docx §13) — create a target language variant under an existing source's logical component. Mirrors the FU02
/// content write path (in-domain validation, claim-only tenant, no silent behaviour); classification is inherited from
/// the source, so a variant cannot drift to a different subject. Emits a MOD-0162 audit event (fail-soft).
/// </summary>
public sealed class CreateContentVariantHandler : IRequestHandler<CreateContentVariantCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgeContentRepository _repository;
    private readonly IKnowledgeConceptAuditPublisher? _audit;

    public CreateContentVariantHandler(
        ITenantContext tenant,
        IActorContext actor,
        IKnowledgeContentRepository repository,
        IKnowledgeConceptAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
        _audit = audit;
    }

    public async Task<Response<Guid>> Handle(CreateContentVariantCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        // ContentType is inherited from the source (a translation is the same kind of content), so it is not validated
        // from the payload here — everything else follows the FU02 structural rules.
        var error = KnowledgeValidation.ValidateContentCode(request.ContentCode)
            ?? KnowledgeValidation.ValidateContentTitle(request.ContentTitle)
            ?? KnowledgeValidation.ValidateContentStatus(request.ContentStatus)
            ?? KnowledgeValidation.ValidateLanguageCode(request.LanguageCode)
            ?? KnowledgeValidation.ValidateContentVersion(request.ContentVersion)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo)
            ?? KnowledgeValidation.ValidateSource(request.Source)
            ?? KnowledgeValidation.ValidateContentPointers(
                request.ContentBodyRef, request.ContentAssetRef, request.FileRef, request.Url);
        if (error is not null)
        {
            return Response<Guid>.Fail(error, 400);
        }

        var source = await _repository.GetByIdAsync(tenantId, request.SourceContentId, cancellationToken);
        if (source is null)
        {
            return Response<Guid>.Fail("Source content not found.", 404);
        }

        if (source.IsArchived())
        {
            return Response<Guid>.Fail("A variant cannot be added to archived content.", 409);
        }

        // GetByIdAsync applied the read-time migration, so a legacy source now carries ContentSetId = its own Id.
        source.EnsureVariantDefaults();
        var setId = source.ContentSetId;

        var contentCode = request.ContentCode.Trim();
        if (await _repository.GetActiveByCodeAsync(tenantId, contentCode, cancellationToken) is { } duplicate)
        {
            return Response<Guid>.Fail(
                $"A non-archived content already uses ContentCode '{contentCode}' (contentId={duplicate.Id}).", 409);
        }

        var language = request.LanguageCode.Trim();
        var siblings = (await _repository.ListAsync(tenantId, cancellationToken))
            .Where(c => c.ContentSetId == setId && !c.IsArchived())
            .ToList();

        // At most one active variant per language in a set (this also blocks reusing the source's own language).
        if (siblings.Any(c => string.Equals(c.LanguageCode, language, StringComparison.OrdinalIgnoreCase)))
        {
            return Response<Guid>.Fail(
                $"A non-archived variant already exists for language '{language}' in this content set.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var variant = new KnowledgeContent
        {
            TenantId = tenantId,
            ContentCode = contentCode,
            ContentTitle = request.ContentTitle.Trim(),
            // Inherited classification / type — a variant belongs to the same logical component.
            ContentType = source.ContentType,
            SubjectId = source.SubjectId,
            TopicId = source.TopicId,
            AudienceProfileId = source.AudienceProfileId,
            ConceptNodeId = source.ConceptNodeId,
            BrandId = source.BrandId,
            ProductId = source.ProductId,
            CampaignId = source.CampaignId,
            SegmentId = source.SegmentId,
            ContentStatus = KnowledgeContentStatuses.Normalize(request.ContentStatus),
            LanguageCode = language,
            Summary = KnowledgeValidation.Trim(request.Summary),
            ContentBodyRef = KnowledgeValidation.Trim(request.ContentBodyRef),
            ContentAssetRef = KnowledgeValidation.Trim(request.ContentAssetRef),
            FileRef = KnowledgeValidation.Trim(request.FileRef),
            Url = KnowledgeValidation.Trim(request.Url),
            ContentVersion = request.ContentVersion.Trim(),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Source = KnowledgeContentSources.Normalize(request.Source),
            Tags = KnowledgeMapper.CleanTags(request.Tags),
            // SCMM-13 linkage: a target of the source's logical component; freshly translated → current.
            ContentSetId = setId,
            IsSourceLanguage = false,
            TranslationStatus = ContentTranslationStatuses.Current,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _repository.InsertAsync(variant, cancellationToken);
        if (_audit is not null)
        {
            await _audit.PublishAsync(KnowledgeReasonCodes.ContentVariantCreated, tenantId,
                KnowledgeConceptAuditEntities.KnowledgeContent, variant.Id, variant.Version, variant.ContentCode,
                cancellationToken);
        }

        return Response<Guid>.Success(variant.Id, 201);
    }
}

/// <summary>
/// SCMM-13 — mark a target variant assessed (needs_assessment → current). The distinct SoD counterpart of the automatic
/// source-edit trigger. The source has no translation status to assess (400); an archived variant cannot be assessed
/// (409); an already-current variant is idempotent (200).
/// </summary>
public sealed class MarkTranslationAssessedHandler : IRequestHandler<MarkTranslationAssessedCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgeContentRepository _repository;
    private readonly IKnowledgeConceptAuditPublisher? _audit;

    public MarkTranslationAssessedHandler(
        ITenantContext tenant,
        IActorContext actor,
        IKnowledgeContentRepository repository,
        IKnowledgeConceptAuditPublisher? audit = null)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
        _audit = audit;
    }

    public async Task<Response<bool>> Handle(MarkTranslationAssessedCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var content = await _repository.GetByIdAsync(tenantId, request.ContentId, cancellationToken);
        if (content is null)
        {
            return Response<bool>.Fail("Knowledge content not found.", 404);
        }

        content.EnsureVariantDefaults();

        if (content.IsArchived())
        {
            return Response<bool>.Fail("An archived variant cannot be assessed.", 409);
        }

        if (content.IsSourceLanguage)
        {
            return Response<bool>.Fail(
                "The source variant has no translation status to assess; assess a target variant instead.", 400);
        }

        if (string.Equals(content.TranslationStatus, ContentTranslationStatuses.Current, StringComparison.Ordinal))
        {
            return Response<bool>.Success(true); // idempotent — already current
        }

        var now = DateTimeOffset.UtcNow;
        content.TranslationStatus = ContentTranslationStatuses.Current;
        content.UpdatedAt = now;
        content.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(content, cancellationToken);
        if (_audit is not null)
        {
            await _audit.PublishAsync(KnowledgeReasonCodes.ContentTranslationAssessed, tenantId,
                KnowledgeConceptAuditEntities.KnowledgeContent, content.Id, content.Version, content.ContentCode,
                cancellationToken);
        }

        return Response<bool>.Success(true);
    }
}
