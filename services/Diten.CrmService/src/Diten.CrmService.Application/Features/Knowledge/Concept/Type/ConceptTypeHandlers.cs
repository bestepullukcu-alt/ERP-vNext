using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Type;

public sealed class CreateConceptTypeHandler : IRequestHandler<CreateConceptTypeCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConceptTypeRepository _types;
    private readonly ISubjectRepository _subjects;

    public CreateConceptTypeHandler(
        ITenantContext tenant, IActorContext actor, IConceptTypeRepository types, ISubjectRepository subjects)
    {
        _tenant = tenant;
        _actor = actor;
        _types = types;
        _subjects = subjects;
    }

    public async Task<Response<Guid>> Handle(CreateConceptTypeCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var error = KnowledgeValidation.ValidateCode(request.ConceptTypeCode, "ConceptTypeCode")
            ?? KnowledgeValidation.ValidateName(request.ConceptTypeName, "ConceptTypeName")
            ?? ConceptGraphValidation.ValidateConceptStatus(request.Status)
            ?? KnowledgeValidation.ValidateRequiredSubject(request.SubjectId);
        if (error is not null)
        {
            return Response<Guid>.Fail(error, 400);
        }

        // V03 — the subject must exist and be non-archived.
        var subject = await _subjects.GetByIdAsync(tenantId, request.SubjectId, cancellationToken);
        if (subject is null)
        {
            return Response<Guid>.Fail("SubjectId does not reference an existing subject.", 400);
        }

        if (subject.IsArchived())
        {
            return Response<Guid>.Fail("A concept type cannot be created under an archived subject.", 400);
        }

        // V02 — code unique within (tenant, subject) among non-archived rows.
        var code = request.ConceptTypeCode.Trim();
        if (await _types.GetActiveByCodeAsync(tenantId, request.SubjectId, code, cancellationToken) is { } duplicate)
        {
            return Response<Guid>.Fail(
                $"A non-archived concept type already uses ConceptTypeCode '{code}' (conceptTypeId={duplicate.Id}).", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new ConceptType
        {
            TenantId = tenantId,
            SubjectId = request.SubjectId,
            ConceptTypeCode = code,
            ConceptTypeName = request.ConceptTypeName.Trim(),
            Description = KnowledgeValidation.Trim(request.Description),
            SortOrder = request.SortOrder,
            Status = ConceptStatuses.Normalize(request.Status),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _types.InsertAsync(entity, cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }
}

public sealed class UpdateConceptTypeHandler : IRequestHandler<UpdateConceptTypeCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConceptTypeRepository _types;

    public UpdateConceptTypeHandler(ITenantContext tenant, IActorContext actor, IConceptTypeRepository types)
    {
        _tenant = tenant;
        _actor = actor;
        _types = types;
    }

    public async Task<Response<bool>> Handle(UpdateConceptTypeCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _types.GetByIdAsync(tenantId, request.ConceptTypeId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Concept type not found.", 404);
        }

        if (entity.IsArchived())
        {
            return Response<bool>.Fail("An archived concept type cannot be updated.", 409);
        }

        if (string.Equals(request.Status?.Trim(), ConceptStatuses.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail("Use the archive endpoint to archive a concept type.", 400);
        }

        var error = KnowledgeValidation.ValidateName(request.ConceptTypeName, "ConceptTypeName")
            ?? ConceptGraphValidation.ValidateConceptStatus(request.Status);
        if (error is not null)
        {
            return Response<bool>.Fail(error, 400);
        }

        var now = DateTimeOffset.UtcNow;
        entity.ConceptTypeName = request.ConceptTypeName.Trim();
        entity.Description = KnowledgeValidation.Trim(request.Description);
        entity.SortOrder = request.SortOrder;
        entity.Status = ConceptStatuses.Normalize(request.Status ?? entity.Status);
        entity.UpdatedAt = now;
        entity.UpdatedBy = _actor.ActorName;

        await _types.UpdateAsync(entity, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveConceptTypeHandler : IRequestHandler<ArchiveConceptTypeCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConceptTypeRepository _types;

    public ArchiveConceptTypeHandler(ITenantContext tenant, IActorContext actor, IConceptTypeRepository types)
    {
        _tenant = tenant;
        _actor = actor;
        _types = types;
    }

    public async Task<Response<bool>> Handle(ArchiveConceptTypeCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _types.GetByIdAsync(tenantId, request.ConceptTypeId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Concept type not found.", 404);
        }

        if (entity.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        var now = DateTimeOffset.UtcNow;
        entity.Status = ConceptStatuses.Archived;
        entity.ArchivedAt = now;
        entity.ArchivedBy = _actor.ActorName;
        entity.UpdatedAt = now;
        entity.UpdatedBy = _actor.ActorName;

        await _types.UpdateAsync(entity, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ListConceptTypesHandler : IRequestHandler<ListConceptTypesQuery, Response<ConceptTypeListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IConceptTypeRepository _types;

    public ListConceptTypesHandler(ITenantContext tenant, IConceptTypeRepository types)
    {
        _tenant = tenant;
        _types = types;
    }

    public async Task<Response<ConceptTypeListDto>> Handle(
        ListConceptTypesQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConceptTypeListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<ConceptType> rows = request.SubjectId is { } subjectId && subjectId != Guid.Empty
            ? await _types.ListBySubjectAsync(tenantId, subjectId, cancellationToken)
            : await _types.ListAsync(tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = ConceptStatuses.Normalize(request.Status);
            rows = rows.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(x =>
                x.ConceptTypeName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.ConceptTypeCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(x => !x.IsArchived());
        }

        var items = rows.Select(ConceptGraphMapper.ToDto).ToList();
        return Response<ConceptTypeListDto>.Success(new ConceptTypeListDto(items, items.Count));
    }
}

public sealed class GetConceptTypeHandler : IRequestHandler<GetConceptTypeQuery, Response<ConceptTypeDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IConceptTypeRepository _types;

    public GetConceptTypeHandler(ITenantContext tenant, IConceptTypeRepository types)
    {
        _tenant = tenant;
        _types = types;
    }

    public async Task<Response<ConceptTypeDto>> Handle(GetConceptTypeQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConceptTypeDto>.Fail("Tenant context is required.", 400);
        }

        var entity = await _types.GetByIdAsync(tenantId, request.ConceptTypeId, cancellationToken);
        return entity is null
            ? Response<ConceptTypeDto>.Fail("Concept type not found.", 404)
            : Response<ConceptTypeDto>.Success(ConceptGraphMapper.ToDto(entity));
    }
}
