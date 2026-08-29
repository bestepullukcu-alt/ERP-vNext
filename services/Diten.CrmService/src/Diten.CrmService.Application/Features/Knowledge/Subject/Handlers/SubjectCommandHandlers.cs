using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.Subject.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using SubjectEntity = Diten.CrmService.Domain.Entities.Subject;

namespace Diten.CrmService.Application.Features.Knowledge.Subject.Handlers;

/// <summary>Shared parent-subject guards for create and update — the same rules the topic hierarchy enforces, so the
/// subject tree can never hold a self-parent, an archived parent or a cycle.</summary>
internal static class SubjectParentGuard
{
    public static async Task<(string? Error, int Status)> ValidateAsync(
        ISubjectRepository subjects,
        Guid tenantId,
        Guid? selfId,
        Guid? parentSubjectId,
        CancellationToken cancellationToken)
    {
        if (parentSubjectId is not { } parentId || parentId == Guid.Empty)
        {
            return (null, 0);
        }

        if (selfId is { } id && parentId == id)
        {
            return ("A subject cannot be its own parent.", 400);
        }

        var parent = await subjects.GetByIdAsync(tenantId, parentId, cancellationToken);
        if (parent is null)
        {
            return ("ParentSubjectId does not reference an existing subject.", 400);
        }

        if (parent.IsArchived())
        {
            return ("ParentSubjectId cannot reference an archived subject.", 400);
        }

        // Walk up from the proposed parent. If we reach selfId, this parent would close a cycle.
        var all = (await subjects.ListAsync(tenantId, cancellationToken)).ToDictionary(s => s.Id);
        var cursor = parent.ParentSubjectId;
        var guard = 0;
        while (cursor is { } current && current != Guid.Empty)
        {
            if (selfId is { } sid && current == sid)
            {
                return ("The parent assignment would create a cycle in the subject hierarchy.", 400);
            }

            if (!all.TryGetValue(current, out var node))
            {
                break;
            }

            cursor = node.ParentSubjectId;
            if (++guard > 1000)
            {
                return ("The subject hierarchy is too deep to validate.", 400);
            }
        }

        return (null, 0);
    }
}

public sealed class CreateSubjectHandler : IRequestHandler<CreateSubjectCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ISubjectRepository _repository;

    public CreateSubjectHandler(ITenantContext tenant, IActorContext actor, ISubjectRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<Guid>> Handle(CreateSubjectCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var error = KnowledgeValidation.ValidateCode(request.SubjectCode, "SubjectCode")
            ?? KnowledgeValidation.ValidateName(request.SubjectName, "SubjectName")
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

        var code = request.SubjectCode.Trim();
        if (await _repository.GetActiveByCodeAsync(tenantId, code, cancellationToken) is { } duplicate)
        {
            return Response<Guid>.Fail(
                $"A non-archived subject already uses SubjectCode '{code}' (subjectId={duplicate.Id}).", 409);
        }

        var (parentError, parentStatus) = await SubjectParentGuard.ValidateAsync(
            _repository, tenantId, null, request.ParentSubjectId, cancellationToken);
        if (parentError is not null)
        {
            return Response<Guid>.Fail(parentError, parentStatus);
        }

        var now = DateTimeOffset.UtcNow;
        var subject = new SubjectEntity
        {
            TenantId = tenantId,
            SubjectCode = code,
            SubjectName = request.SubjectName.Trim(),
            ParentSubjectId = request.ParentSubjectId == Guid.Empty ? null : request.ParentSubjectId,
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

        await _repository.InsertAsync(subject, cancellationToken);
        return Response<Guid>.Success(subject.Id, 201);
    }
}

public sealed class UpdateSubjectHandler : IRequestHandler<UpdateSubjectCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ISubjectRepository _repository;

    public UpdateSubjectHandler(ITenantContext tenant, IActorContext actor, ISubjectRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(UpdateSubjectCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var subject = await _repository.GetByIdAsync(tenantId, request.SubjectId, cancellationToken);
        if (subject is null)
        {
            return Response<bool>.Fail("Subject not found.", 404);
        }

        if (subject.IsArchived())
        {
            return Response<bool>.Fail("An archived subject cannot be updated.", 409);
        }

        if (string.Equals(request.Status?.Trim(), TaxonomyStatuses.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail("Use the archive endpoint to archive a subject.", 400);
        }

        var error = KnowledgeValidation.ValidateName(request.SubjectName, "SubjectName")
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

        var (parentError, parentStatus) = await SubjectParentGuard.ValidateAsync(
            _repository, tenantId, subject.Id, request.ParentSubjectId, cancellationToken);
        if (parentError is not null)
        {
            return Response<bool>.Fail(parentError, parentStatus);
        }

        var now = DateTimeOffset.UtcNow;
        subject.SubjectName = request.SubjectName.Trim();
        subject.ParentSubjectId = request.ParentSubjectId == Guid.Empty ? null : request.ParentSubjectId;
        subject.Description = KnowledgeValidation.Trim(request.Description);
        subject.Status = TaxonomyStatuses.Normalize(request.Status ?? subject.Status);
        subject.SortOrder = request.SortOrder;
        subject.EffectiveFrom = request.EffectiveFrom;
        subject.EffectiveTo = request.EffectiveTo;
        subject.Alias = KnowledgeMapper.CleanAlias(request.Alias);
        subject.ExternalReferences = KnowledgeMapper.ToEntities(request.ExternalReferences, now);
        subject.UpdatedAt = now;
        subject.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(subject, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveSubjectHandler : IRequestHandler<ArchiveSubjectCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ISubjectRepository _repository;

    public ArchiveSubjectHandler(ITenantContext tenant, IActorContext actor, ISubjectRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(ArchiveSubjectCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var subject = await _repository.GetByIdAsync(tenantId, request.SubjectId, cancellationToken);
        if (subject is null)
        {
            return Response<bool>.Fail("Subject not found.", 404);
        }

        if (subject.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        // No cascade: existing content keeps its classification and stays readable; only NEW attachment is blocked.
        var now = DateTimeOffset.UtcNow;
        subject.Status = TaxonomyStatuses.Archived;
        subject.ArchivedAt = now;
        subject.ArchivedBy = _actor.ActorName;
        subject.UpdatedAt = now;
        subject.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(subject, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class UnarchiveSubjectHandler : IRequestHandler<UnarchiveSubjectCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly ISubjectRepository _repository;

    public UnarchiveSubjectHandler(ITenantContext tenant, IActorContext actor, ISubjectRepository repository)
    {
        _tenant = tenant;
        _actor = actor;
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(UnarchiveSubjectCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var subject = await _repository.GetByIdAsync(tenantId, request.SubjectId, cancellationToken);
        if (subject is null)
        {
            return Response<bool>.Fail("Subject not found.", 404);
        }

        if (!subject.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        // Restoring must not produce a row the create path would have rejected.
        if (subject.ParentSubjectId is { } parentId && parentId != Guid.Empty)
        {
            var parent = await _repository.GetByIdAsync(tenantId, parentId, cancellationToken);
            if (parent is null || parent.IsArchived())
            {
                return Response<bool>.Fail("The parent subject is archived; restore the parent first.", 409);
            }
        }

        // SubjectCode is unique only among non-archived rows, so the code may have been reused while this subject was
        // archived. Restoring it would produce two live rows on the same business key — refuse instead.
        if (await _repository.GetActiveByCodeAsync(tenantId, subject.SubjectCode, cancellationToken) is { } holder)
        {
            return Response<bool>.Fail(
                $"SubjectCode '{subject.SubjectCode}' is now used by a non-archived subject (subjectId={holder.Id}).",
                409);
        }

        // Comes back as inactive, never straight to active: restoring is not the same decision as putting back in use.
        var now = DateTimeOffset.UtcNow;
        subject.Status = TaxonomyStatuses.Inactive;
        subject.ArchivedAt = null;
        subject.ArchivedBy = null;
        subject.UpdatedAt = now;
        subject.UpdatedBy = _actor.ActorName;

        await _repository.UpdateAsync(subject, cancellationToken);
        return Response<bool>.Success(true);
    }
}
