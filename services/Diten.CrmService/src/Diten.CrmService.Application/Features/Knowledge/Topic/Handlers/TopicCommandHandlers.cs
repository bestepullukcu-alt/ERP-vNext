using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.Topic.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using TopicEntity = Diten.CrmService.Domain.Entities.Topic;

namespace Diten.CrmService.Application.Features.Knowledge.Topic.Handlers;

/// <summary>Shared topic hierarchy guard: a parent must live in the SAME subject, must not be the topic itself, and the
/// resulting parent chain must not form a cycle. Runs read-time over the subject's topics.</summary>
internal static class TopicHierarchy
{
    /// <summary>Validates a proposed parent for a topic (<paramref name="selfId"/> is null on create). Returns
    /// (error, statusCode) or (null, 0).</summary>
    public static async Task<(string? Error, int StatusCode)> ValidateParentAsync(
        ITopicRepository topics,
        Guid tenantId,
        Guid subjectId,
        Guid? selfId,
        Guid? parentTopicId,
        CancellationToken cancellationToken)
    {
        if (parentTopicId is not { } parentId || parentId == Guid.Empty)
        {
            return (null, 0);
        }

        if (selfId is { } id && parentId == id)
        {
            return ("A topic cannot be its own parent.", 400);
        }

        var parent = await topics.GetByIdAsync(tenantId, parentId, cancellationToken);
        if (parent is null)
        {
            return ("ParentTopicId does not reference an existing topic.", 400);
        }

        if (parent.SubjectId != subjectId)
        {
            return ("ParentTopicId must belong to the same SubjectId (cross-subject parent is not allowed).", 400);
        }

        if (parent.IsArchived())
        {
            return ("ParentTopicId cannot reference an archived topic.", 400);
        }

        // Walk up from the proposed parent. If we reach selfId, this parent would close a cycle.
        var subjectTopics = (await topics.ListBySubjectAsync(tenantId, subjectId, cancellationToken))
            .ToDictionary(t => t.Id);
        var cursor = parent.ParentTopicId;
        var guard = 0;
        while (cursor is { } current && current != Guid.Empty)
        {
            if (selfId is { } sid && current == sid)
            {
                return ("The parent assignment would create a cycle in the topic hierarchy.", 400);
            }

            if (!subjectTopics.TryGetValue(current, out var node))
            {
                break;
            }

            cursor = node.ParentTopicId;
            if (++guard > 1000)
            {
                break; // defensive: a pre-existing cycle should never spin forever
            }
        }

        return (null, 0);
    }
}

public sealed class CreateTopicHandler : IRequestHandler<CreateTopicCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ITopicRepository _repository;
    private readonly ISubjectRepository _subjects;

    public CreateTopicHandler(
        ITenantContext tenant, IActorContext actor, ITopicRepository repository, ISubjectRepository subjects)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
        _subjects = subjects;
    }

    public async Task<Response<Guid>> Handle(CreateTopicCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var error = KnowledgeValidation.ValidateRequiredSubject(request.SubjectId)
            ?? KnowledgeValidation.ValidateCode(request.TopicCode, "TopicCode")
            ?? KnowledgeValidation.ValidateName(request.TopicName, "TopicName")
            ?? KnowledgeValidation.ValidateTaxonomyStatus(request.Status)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo);
        if (error is not null)
        {
            return Response<Guid>.Fail(error, 400);
        }

        var (refError, isConflict) = KnowledgeValidation.ValidateExternalReferences(request.ExternalReferences);
        if (refError is not null)
        {
            return Response<Guid>.Fail(refError, isConflict ? 409 : 400);
        }

        var subject = await _subjects.GetByIdAsync(tenantId, request.SubjectId, cancellationToken);
        if (subject is null)
        {
            return Response<Guid>.Fail("SubjectId does not reference an existing subject.", 400);
        }

        if (subject.IsArchived())
        {
            return Response<Guid>.Fail("A topic cannot be created under an archived subject.", 409);
        }

        var code = request.TopicCode.Trim();
        if (await _repository.GetActiveByCodeAsync(tenantId, request.SubjectId, code, cancellationToken) is { } dup)
        {
            return Response<Guid>.Fail(
                $"A non-archived topic already uses TopicCode '{code}' in this subject (topicId={dup.Id}).", 409);
        }

        var (parentError, parentStatus) = await TopicHierarchy.ValidateParentAsync(
            _repository, tenantId, request.SubjectId, null, request.ParentTopicId, cancellationToken);
        if (parentError is not null)
        {
            return Response<Guid>.Fail(parentError, parentStatus);
        }

        var now = DateTimeOffset.UtcNow;
        var topic = new TopicEntity
        {
            TenantId = tenantId,
            SubjectId = request.SubjectId,
            TopicCode = code,
            ParentTopicId = request.ParentTopicId,
            TopicName = request.TopicName.Trim(),
            Description = KnowledgeValidation.Trim(request.Description),
            Status = TaxonomyStatuses.Normalize(request.Status),
            SortOrder = request.SortOrder,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Alias = KnowledgeMapper.CleanAlias(request.Alias),
            ExternalReferences = KnowledgeMapper.ToEntities(request.ExternalReferences, now),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _repository.InsertAsync(topic, cancellationToken);
        return Response<Guid>.Success(topic.Id, 201);
    }
}

public sealed class UpdateTopicHandler : IRequestHandler<UpdateTopicCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ITopicRepository _repository;

    public UpdateTopicHandler(ITenantContext tenant, IActorContext actor, ITopicRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(UpdateTopicCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var topic = await _repository.GetByIdAsync(tenantId, request.TopicId, cancellationToken);
        if (topic is null)
        {
            return Response<bool>.Fail("Topic not found.", 404);
        }

        if (topic.IsArchived())
        {
            return Response<bool>.Fail("An archived topic cannot be updated.", 409);
        }

        if (string.Equals(request.Status?.Trim(), TaxonomyStatuses.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail("Use the archive endpoint to archive a topic.", 400);
        }

        var error = KnowledgeValidation.ValidateName(request.TopicName, "TopicName")
            ?? KnowledgeValidation.ValidateTaxonomyStatus(request.Status)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo);
        if (error is not null)
        {
            return Response<bool>.Fail(error, 400);
        }

        var (refError, isConflict) = KnowledgeValidation.ValidateExternalReferences(request.ExternalReferences);
        if (refError is not null)
        {
            return Response<bool>.Fail(refError, isConflict ? 409 : 400);
        }

        // SubjectId is immutable; the parent is re-validated within the topic's own subject.
        var (parentError, parentStatus) = await TopicHierarchy.ValidateParentAsync(
            _repository, tenantId, topic.SubjectId, topic.Id, request.ParentTopicId, cancellationToken);
        if (parentError is not null)
        {
            return Response<bool>.Fail(parentError, parentStatus);
        }

        var now = DateTimeOffset.UtcNow;
        topic.ParentTopicId = request.ParentTopicId;
        topic.TopicName = request.TopicName.Trim();
        topic.Description = KnowledgeValidation.Trim(request.Description);
        topic.Status = TaxonomyStatuses.Normalize(request.Status ?? topic.Status);
        topic.SortOrder = request.SortOrder;
        topic.EffectiveFrom = request.EffectiveFrom;
        topic.EffectiveTo = request.EffectiveTo;
        topic.Alias = KnowledgeMapper.CleanAlias(request.Alias);
        topic.ExternalReferences = KnowledgeMapper.ToEntities(request.ExternalReferences, now);
        topic.UpdatedAt = now;
        topic.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(topic, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveTopicHandler : IRequestHandler<ArchiveTopicCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ITopicRepository _repository;

    public ArchiveTopicHandler(ITenantContext tenant, IActorContext actor, ITopicRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(ArchiveTopicCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var topic = await _repository.GetByIdAsync(tenantId, request.TopicId, cancellationToken);
        if (topic is null)
        {
            return Response<bool>.Fail("Topic not found.", 404);
        }

        if (topic.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        // No cascade to child topics or content: existing links stay readable; only NEW attachment is blocked.
        var now = DateTimeOffset.UtcNow;
        topic.Status = TaxonomyStatuses.Archived;
        topic.ArchivedAt = now;
        topic.ArchivedBy = _actor.ActorName;
        topic.UpdatedAt = now;
        topic.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(topic, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class UnarchiveTopicHandler : IRequestHandler<UnarchiveTopicCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ITopicRepository _repository;
    private readonly ISubjectRepository _subjects;

    public UnarchiveTopicHandler(
        ITenantContext tenant, IActorContext actor, ITopicRepository repository, ISubjectRepository subjects)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
        _subjects = subjects;
    }

    public async Task<Response<bool>> Handle(UnarchiveTopicCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var topic = await _repository.GetByIdAsync(tenantId, request.TopicId, cancellationToken);
        if (topic is null)
        {
            return Response<bool>.Fail("Topic not found.", 404);
        }

        if (!topic.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        // Restoring must not produce a row the create path would have rejected.
        var subject = await _subjects.GetByIdAsync(tenantId, topic.SubjectId, cancellationToken);
        if (subject is null || subject.IsArchived())
        {
            return Response<bool>.Fail("The owning subject is archived; restore the subject first.", 409);
        }

        if (topic.ParentTopicId is { } parentId && parentId != Guid.Empty)
        {
            var parent = await _repository.GetByIdAsync(tenantId, parentId, cancellationToken);
            if (parent is null || parent.IsArchived())
            {
                return Response<bool>.Fail("The parent topic is archived; restore the parent first.", 409);
            }
        }

        // TopicCode is unique only among non-archived topics of the subject, so it may have been reused meanwhile.
        var holder = await _repository.GetActiveByCodeAsync(tenantId, topic.SubjectId, topic.TopicCode, cancellationToken);
        if (holder is not null)
        {
            return Response<bool>.Fail(
                $"TopicCode '{topic.TopicCode}' is now used by a non-archived topic in this subject (topicId={holder.Id}).",
                409);
        }

        // Comes back as inactive, never straight to active.
        var now = DateTimeOffset.UtcNow;
        topic.Status = TaxonomyStatuses.Inactive;
        topic.ArchivedAt = null;
        topic.ArchivedBy = null;
        topic.UpdatedAt = now;
        topic.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(topic, cancellationToken);
        return Response<bool>.Success(true);
    }
}
