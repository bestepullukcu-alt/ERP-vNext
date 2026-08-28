using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Node;

public sealed class CreateConceptNodeHandler : IRequestHandler<CreateConceptNodeCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConceptNodeRepository _nodes;
    private readonly IConceptTypeRepository _types;

    public CreateConceptNodeHandler(
        ITenantContext tenant, IActorContext actor, IConceptNodeRepository nodes, IConceptTypeRepository types)
    {
        _tenant = tenant;
        _actor = actor;
        _nodes = nodes;
        _types = types;
    }

    public async Task<Response<Guid>> Handle(CreateConceptNodeCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var error = KnowledgeValidation.ValidateCode(request.ConceptNodeCode, "ConceptNodeCode")
            ?? KnowledgeValidation.ValidateName(request.ConceptNodeName, "ConceptNodeName")
            ?? ConceptGraphValidation.ValidateConceptStatus(request.Status)
            ?? KnowledgeValidation.ValidateRequiredSubject(request.SubjectId)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo)
            ?? ConceptGraphValidation.ValidateExternalRef(request.ExternalRefType, request.ExternalRefId);
        if (error is not null)
        {
            return Response<Guid>.Fail(error, 400);
        }

        if (request.ConceptTypeId == Guid.Empty)
        {
            return Response<Guid>.Fail("ConceptTypeId is required and cannot be empty.", 400);
        }

        // V04 — the type must exist and be non-archived.
        var type = await _types.GetByIdAsync(tenantId, request.ConceptTypeId, cancellationToken);
        if (type is null)
        {
            return Response<Guid>.Fail("ConceptTypeId does not reference an existing concept type.", 400);
        }

        if (type.IsArchived())
        {
            return Response<Guid>.Fail("A concept node cannot be created under an archived concept type.", 400);
        }

        // V05 — the node's subject must equal its type's subject.
        if (type.SubjectId != request.SubjectId)
        {
            return Response<Guid>.Fail("The node's SubjectId must equal the concept type's subject.", 400);
        }

        // V06 — code unique within (subject, type) among non-archived rows.
        var code = request.ConceptNodeCode.Trim();
        if (await _nodes.GetActiveByCodeAsync(tenantId, request.SubjectId, request.ConceptTypeId, code, cancellationToken)
            is { } duplicate)
        {
            return Response<Guid>.Fail(
                $"A non-archived concept node already uses ConceptNodeCode '{code}' (conceptNodeId={duplicate.Id}).", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new ConceptNode
        {
            TenantId = tenantId,
            SubjectId = request.SubjectId,
            ConceptTypeId = request.ConceptTypeId,
            ConceptNodeCode = code,
            ConceptNodeName = request.ConceptNodeName.Trim(),
            Description = KnowledgeValidation.Trim(request.Description),
            Status = ConceptStatuses.Normalize(request.Status),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            ExternalRefType = NormalizeExternalRefType(request.ExternalRefType),
            ExternalRefId = KnowledgeValidation.Trim(request.ExternalRefId),
            MetadataJson = KnowledgeValidation.Trim(request.MetadataJson),
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _nodes.InsertAsync(entity, cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }

    internal static string? NormalizeExternalRefType(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : ConceptExternalRefTypes.Normalize(value);
}

public sealed class UpdateConceptNodeHandler : IRequestHandler<UpdateConceptNodeCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConceptNodeRepository _nodes;

    public UpdateConceptNodeHandler(ITenantContext tenant, IActorContext actor, IConceptNodeRepository nodes)
    {
        _tenant = tenant;
        _actor = actor;
        _nodes = nodes;
    }

    public async Task<Response<bool>> Handle(UpdateConceptNodeCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _nodes.GetByIdAsync(tenantId, request.ConceptNodeId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Concept node not found.", 404);
        }

        if (entity.IsArchived())
        {
            return Response<bool>.Fail("An archived concept node cannot be updated.", 409);
        }

        if (string.Equals(request.Status?.Trim(), ConceptStatuses.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail("Use the archive endpoint to archive a concept node.", 400);
        }

        var error = KnowledgeValidation.ValidateName(request.ConceptNodeName, "ConceptNodeName")
            ?? ConceptGraphValidation.ValidateConceptStatus(request.Status)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo)
            ?? ConceptGraphValidation.ValidateExternalRef(request.ExternalRefType, request.ExternalRefId);
        if (error is not null)
        {
            return Response<bool>.Fail(error, 400);
        }

        var now = DateTimeOffset.UtcNow;
        entity.ConceptNodeName = request.ConceptNodeName.Trim();
        entity.Description = KnowledgeValidation.Trim(request.Description);
        entity.Status = ConceptStatuses.Normalize(request.Status ?? entity.Status);
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.ExternalRefType = CreateConceptNodeHandler.NormalizeExternalRefType(request.ExternalRefType);
        entity.ExternalRefId = KnowledgeValidation.Trim(request.ExternalRefId);
        entity.MetadataJson = KnowledgeValidation.Trim(request.MetadataJson);
        entity.UpdatedAt = now;
        entity.UpdatedBy = _actor.ActorName;

        await _nodes.UpdateAsync(entity, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveConceptNodeHandler : IRequestHandler<ArchiveConceptNodeCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConceptNodeRepository _nodes;

    public ArchiveConceptNodeHandler(ITenantContext tenant, IActorContext actor, IConceptNodeRepository nodes)
    {
        _tenant = tenant;
        _actor = actor;
        _nodes = nodes;
    }

    public async Task<Response<bool>> Handle(ArchiveConceptNodeCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _nodes.GetByIdAsync(tenantId, request.ConceptNodeId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Concept node not found.", 404);
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

        await _nodes.UpdateAsync(entity, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ListConceptNodesHandler : IRequestHandler<ListConceptNodesQuery, Response<ConceptNodeListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IConceptNodeRepository _nodes;

    public ListConceptNodesHandler(ITenantContext tenant, IConceptNodeRepository nodes)
    {
        _tenant = tenant;
        _nodes = nodes;
    }

    public async Task<Response<ConceptNodeListDto>> Handle(
        ListConceptNodesQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConceptNodeListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<ConceptNode> rows = request.SubjectId is { } subjectId && subjectId != Guid.Empty
            ? await _nodes.ListBySubjectAsync(tenantId, subjectId, cancellationToken)
            : await _nodes.ListAsync(tenantId, cancellationToken);

        if (request.ConceptTypeId is { } typeId && typeId != Guid.Empty)
        {
            rows = rows.Where(x => x.ConceptTypeId == typeId);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = ConceptStatuses.Normalize(request.Status);
            rows = rows.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.ExternalRefType))
        {
            var refType = ConceptExternalRefTypes.Normalize(request.ExternalRefType);
            rows = rows.Where(x => string.Equals(x.ExternalRefType, refType, StringComparison.OrdinalIgnoreCase));
        }

        if (request.EffectiveAt is { } at)
        {
            rows = rows.Where(x => x.IsEffectiveAt(at));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(x =>
                x.ConceptNodeName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.ConceptNodeCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(x => !x.IsArchived());
        }

        var items = rows.Select(ConceptGraphMapper.ToDto).ToList();
        return Response<ConceptNodeListDto>.Success(new ConceptNodeListDto(items, items.Count));
    }
}

public sealed class GetConceptNodeHandler : IRequestHandler<GetConceptNodeQuery, Response<ConceptNodeDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IConceptNodeRepository _nodes;

    public GetConceptNodeHandler(ITenantContext tenant, IConceptNodeRepository nodes)
    {
        _tenant = tenant;
        _nodes = nodes;
    }

    public async Task<Response<ConceptNodeDto>> Handle(GetConceptNodeQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConceptNodeDto>.Fail("Tenant context is required.", 400);
        }

        var entity = await _nodes.GetByIdAsync(tenantId, request.ConceptNodeId, cancellationToken);
        return entity is null
            ? Response<ConceptNodeDto>.Fail("Concept node not found.", 404)
            : Response<ConceptNodeDto>.Success(ConceptGraphMapper.ToDto(entity));
    }
}
