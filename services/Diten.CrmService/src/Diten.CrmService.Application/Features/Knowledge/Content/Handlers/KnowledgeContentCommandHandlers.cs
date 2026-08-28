using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.Content.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Content.Handlers;

/// <summary>Shared FU02 content write-path validation. TenantId is always the claim-resolved value; vocabulary is
/// validated in-domain (structural). Nothing here deletes content.</summary>
internal static class KnowledgeContentWrite
{
    public static (string? Error, int StatusCode) ValidateStructural(
        string contentTitle,
        string contentType,
        string? contentStatus,
        Guid subjectId,
        string languageCode,
        string contentVersion,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        string? source,
        string? bodyRef,
        string? assetRef,
        string? fileRef,
        string? url,
        Guid? topicId,
        Guid? audienceProfileId,
        Guid? conceptNodeId,
        Guid? brandId,
        Guid? productId,
        Guid? campaignId,
        Guid? segmentId,
        IReadOnlyList<KnowledgeExternalReferenceInput>? externalReferences)
    {
        var error = KnowledgeValidation.ValidateContentTitle(contentTitle)
            ?? KnowledgeValidation.ValidateContentType(contentType)
            ?? KnowledgeValidation.ValidateContentStatus(contentStatus)
            ?? KnowledgeValidation.ValidateRequiredSubject(subjectId)
            ?? KnowledgeValidation.ValidateLanguageCode(languageCode)
            ?? KnowledgeValidation.ValidateContentVersion(contentVersion)
            ?? KnowledgeValidation.ValidateEffectiveFrom(effectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(effectiveFrom, effectiveTo)
            ?? KnowledgeValidation.ValidateSource(source)
            ?? KnowledgeValidation.ValidateContentPointers(bodyRef, assetRef, fileRef, url)
            // Format-level only: MOD-0290 / MOD-0162-FU01C have no runtime, so there is no master to resolve against.
            ?? KnowledgeValidation.ValidateOptionalReference(topicId, "TopicId")
            ?? KnowledgeValidation.ValidateOptionalReference(audienceProfileId, "AudienceProfileId")
            ?? KnowledgeValidation.ValidateOptionalReference(conceptNodeId, "ConceptNodeId")
            ?? KnowledgeValidation.ValidateOptionalReference(brandId, "BrandId")
            ?? KnowledgeValidation.ValidateOptionalReference(productId, "ProductId")
            ?? KnowledgeValidation.ValidateOptionalReference(campaignId, "CampaignId")
            ?? KnowledgeValidation.ValidateOptionalReference(segmentId, "SegmentId");

        if (error is not null)
        {
            return (error, 400);
        }

        var (referenceError, isConflict) = KnowledgeValidation.ValidateExternalReferences(externalReferences);
        return referenceError is null ? (null, 0) : (referenceError, isConflict ? 409 : 400);
    }

    /// <summary>Subject (required) and Topic (optional) must exist, be non-archived and — for Topic — belong to the
    /// subject. New content may never be attached to an archived classification (existing content stays attached).</summary>
    public static async Task<(string? Error, int StatusCode)> ValidateClassificationAsync(
        ISubjectRepository subjects,
        ITopicRepository topics,
        Guid tenantId,
        Guid subjectId,
        Guid? topicId,
        Guid? audienceProfileId,
        IAudienceProfileRepository profiles,
        CancellationToken cancellationToken)
    {
        var subject = await subjects.GetByIdAsync(tenantId, subjectId, cancellationToken);
        if (subject is null)
        {
            return ("SubjectId does not reference an existing subject.", 400);
        }

        if (subject.IsArchived())
        {
            return ("New content cannot be attached to an archived subject.", 409);
        }

        if (topicId is { } tid && tid != Guid.Empty)
        {
            var topic = await topics.GetByIdAsync(tenantId, tid, cancellationToken);
            if (topic is null)
            {
                return ("TopicId does not reference an existing topic.", 400);
            }

            if (topic.SubjectId != subjectId)
            {
                return ("TopicId must belong to the same SubjectId.", 400);
            }

            if (topic.IsArchived())
            {
                return ("New content cannot be attached to an archived topic.", 409);
            }
        }

        if (audienceProfileId is { } pid && pid != Guid.Empty)
        {
            var profile = await profiles.GetByIdAsync(tenantId, pid, cancellationToken);
            if (profile is null)
            {
                return ("AudienceProfileId does not reference an existing profile.", 400);
            }

            if (profile.IsArchived())
            {
                return ("New content cannot be attached to an archived audience profile.", 409);
            }
        }

        return (null, 0);
    }

    /// <summary>MOD-0162 FU03 V17 — resolve <c>KnowledgeContent.ConceptNodeId</c> to a live, non-archived, same-tenant
    /// concept node. An empty value is a no-op (the reference is optional). Callers gate this with a dirty-check so an
    /// untouched legacy value never trips a 400 on save.</summary>
    public static async Task<(string? Error, int StatusCode)> ValidateConceptNodeAsync(
        IConceptNodeRepository conceptNodes, Guid tenantId, Guid? conceptNodeId, CancellationToken cancellationToken)
    {
        if (conceptNodeId is not { } id || id == Guid.Empty)
        {
            return (null, 0);
        }

        var node = await conceptNodes.GetByIdAsync(tenantId, id, cancellationToken);
        if (node is null)
        {
            return ("ConceptNodeId does not reference a live concept node in this tenant.", 400);
        }

        if (node.IsArchived())
        {
            return ("ConceptNodeId cannot reference an archived concept node.", 400);
        }

        return (null, 0);
    }
}

public sealed class CreateKnowledgeContentHandler : IRequestHandler<CreateKnowledgeContentCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgeContentRepository _repository;
    private readonly ISubjectRepository _subjects;
    private readonly ITopicRepository _topics;
    private readonly IAudienceProfileRepository _profiles;
    private readonly IConceptNodeRepository _conceptNodes;

    public CreateKnowledgeContentHandler(
        ITenantContext tenant,
        IActorContext actor,
        IKnowledgeContentRepository repository,
        ISubjectRepository subjects,
        ITopicRepository topics,
        IAudienceProfileRepository profiles,
        IConceptNodeRepository conceptNodes)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
        _subjects = subjects;
        _topics = topics;
        _profiles = profiles;
        _conceptNodes = conceptNodes;
    }

    public async Task<Response<Guid>> Handle(CreateKnowledgeContentCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        if (KnowledgeValidation.ValidateContentCode(request.ContentCode) is { } codeError)
        {
            return Response<Guid>.Fail(codeError, 400);
        }

        var (error, statusCode) = KnowledgeContentWrite.ValidateStructural(
            request.ContentTitle, request.ContentType, request.ContentStatus, request.SubjectId, request.LanguageCode,
            request.ContentVersion, request.EffectiveFrom, request.EffectiveTo, request.Source, request.ContentBodyRef,
            request.ContentAssetRef, request.FileRef, request.Url, request.TopicId, request.AudienceProfileId,
            request.ConceptNodeId, request.BrandId, request.ProductId, request.CampaignId, request.SegmentId,
            request.ExternalReferences);
        if (error is not null)
        {
            return Response<Guid>.Fail(error, statusCode);
        }

        var contentCode = request.ContentCode.Trim();
        if (await _repository.GetActiveByCodeAsync(tenantId, contentCode, cancellationToken) is { } duplicate)
        {
            return Response<Guid>.Fail(
                $"A non-archived content already uses ContentCode '{contentCode}' (contentId={duplicate.Id}).", 409);
        }

        var (classError, classStatus) = await KnowledgeContentWrite.ValidateClassificationAsync(
            _subjects, _topics, tenantId, request.SubjectId, request.TopicId, request.AudienceProfileId, _profiles,
            cancellationToken);
        if (classError is not null)
        {
            return Response<Guid>.Fail(classError, classStatus);
        }

        // MOD-0162 FU03 V17 — a supplied ConceptNodeId must resolve to a live, non-archived, same-tenant node.
        var (nodeError, nodeStatus) = await KnowledgeContentWrite.ValidateConceptNodeAsync(
            _conceptNodes, tenantId, request.ConceptNodeId, cancellationToken);
        if (nodeError is not null)
        {
            return Response<Guid>.Fail(nodeError, nodeStatus);
        }

        var now = DateTimeOffset.UtcNow;
        var content = new KnowledgeContent
        {
            TenantId = tenantId,
            ContentCode = contentCode,
            ContentTitle = request.ContentTitle.Trim(),
            ContentType = KnowledgeContentTypes.Normalize(request.ContentType),
            ContentStatus = KnowledgeContentStatuses.Normalize(request.ContentStatus),
            SubjectId = request.SubjectId,
            TopicId = request.TopicId,
            AudienceProfileId = request.AudienceProfileId,
            ConceptNodeId = request.ConceptNodeId,
            BrandId = request.BrandId,
            ProductId = request.ProductId,
            CampaignId = request.CampaignId,
            SegmentId = request.SegmentId,
            LanguageCode = request.LanguageCode.Trim(),
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
            ExternalReferences = KnowledgeMapper.ToEntities(request.ExternalReferences, now),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _repository.InsertAsync(content, cancellationToken);
        return Response<Guid>.Success(content.Id, 201);
    }
}

public sealed class UpdateKnowledgeContentHandler : IRequestHandler<UpdateKnowledgeContentCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgeContentRepository _repository;
    private readonly ISubjectRepository _subjects;
    private readonly ITopicRepository _topics;
    private readonly IAudienceProfileRepository _profiles;
    private readonly IConceptNodeRepository _conceptNodes;

    public UpdateKnowledgeContentHandler(
        ITenantContext tenant,
        IActorContext actor,
        IKnowledgeContentRepository repository,
        ISubjectRepository subjects,
        ITopicRepository topics,
        IAudienceProfileRepository profiles,
        IConceptNodeRepository conceptNodes)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
        _subjects = subjects;
        _topics = topics;
        _profiles = profiles;
        _conceptNodes = conceptNodes;
    }

    public async Task<Response<bool>> Handle(UpdateKnowledgeContentCommand request, CancellationToken cancellationToken)
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

        if (content.IsArchived())
        {
            return Response<bool>.Fail(
                "Archived content cannot be updated. Archived content is read-only history.", 409);
        }

        if (string.Equals(request.ContentStatus?.Trim(), KnowledgeContentStatuses.Archived,
                StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail(
                "Use the archive endpoint to archive content; update cannot set status=archived.", 400);
        }

        var (error, statusCode) = KnowledgeContentWrite.ValidateStructural(
            request.ContentTitle, request.ContentType, request.ContentStatus, request.SubjectId, request.LanguageCode,
            request.ContentVersion, request.EffectiveFrom, request.EffectiveTo, request.Source, request.ContentBodyRef,
            request.ContentAssetRef, request.FileRef, request.Url, request.TopicId, request.AudienceProfileId,
            request.ConceptNodeId, request.BrandId, request.ProductId, request.CampaignId, request.SegmentId,
            request.ExternalReferences);
        if (error is not null)
        {
            return Response<bool>.Fail(error, statusCode);
        }

        var (classError, classStatus) = await KnowledgeContentWrite.ValidateClassificationAsync(
            _subjects, _topics, tenantId, request.SubjectId, request.TopicId, request.AudienceProfileId, _profiles,
            cancellationToken);
        if (classError is not null)
        {
            return Response<bool>.Fail(classError, classStatus);
        }

        // MOD-0162 FU03 V17 — dirty-check: resolve ConceptNodeId ONLY when the value actually changed (or is being set).
        // An untouched legacy value never trips a 400, so editing another field and saving does not fail on dangling data.
        if (request.ConceptNodeId != content.ConceptNodeId)
        {
            var (nodeError, nodeStatus) = await KnowledgeContentWrite.ValidateConceptNodeAsync(
                _conceptNodes, tenantId, request.ConceptNodeId, cancellationToken);
            if (nodeError is not null)
            {
                return Response<bool>.Fail(nodeError, nodeStatus);
            }
        }

        // ContentCode is immutable — renaming goes through ContentTitle.
        var now = DateTimeOffset.UtcNow;
        content.ContentTitle = request.ContentTitle.Trim();
        content.ContentType = KnowledgeContentTypes.Normalize(request.ContentType);
        content.ContentStatus = KnowledgeContentStatuses.Normalize(request.ContentStatus ?? content.ContentStatus);
        content.SubjectId = request.SubjectId;
        content.TopicId = request.TopicId;
        content.AudienceProfileId = request.AudienceProfileId;
        content.ConceptNodeId = request.ConceptNodeId;
        content.BrandId = request.BrandId;
        content.ProductId = request.ProductId;
        content.CampaignId = request.CampaignId;
        content.SegmentId = request.SegmentId;
        content.LanguageCode = request.LanguageCode.Trim();
        content.Summary = KnowledgeValidation.Trim(request.Summary);
        content.ContentBodyRef = KnowledgeValidation.Trim(request.ContentBodyRef);
        content.ContentAssetRef = KnowledgeValidation.Trim(request.ContentAssetRef);
        content.FileRef = KnowledgeValidation.Trim(request.FileRef);
        content.Url = KnowledgeValidation.Trim(request.Url);
        content.ContentVersion = request.ContentVersion.Trim();
        content.EffectiveFrom = request.EffectiveFrom;
        content.EffectiveTo = request.EffectiveTo;
        content.Source = KnowledgeContentSources.Normalize(request.Source ?? content.Source);
        content.Tags = KnowledgeMapper.CleanTags(request.Tags);
        content.ExternalReferences = KnowledgeMapper.ToEntities(request.ExternalReferences, now);
        content.UpdatedAt = now;
        content.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(content, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveKnowledgeContentHandler : IRequestHandler<ArchiveKnowledgeContentCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IKnowledgeContentRepository _repository;

    public ArchiveKnowledgeContentHandler(
        ITenantContext tenant, IActorContext actor, IKnowledgeContentRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(ArchiveKnowledgeContentCommand request, CancellationToken cancellationToken)
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

        if (content.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        var now = DateTimeOffset.UtcNow;
        content.ContentStatus = KnowledgeContentStatuses.Archived;
        content.ArchivedAt = now;
        content.ArchivedBy = _actor.ActorName;
        content.UpdatedAt = now;
        content.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(content, cancellationToken);
        return Response<bool>.Success(true);
    }
}
