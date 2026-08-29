using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using ChainTemplateEntity = Diten.CrmService.Domain.Entities.ConceptChainTemplate;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.ChainTemplate;

/// <summary>Shared chain-template logic: same-subject membership check for the ordered types, and the published
/// effective-window overlap guard (two published versions of one ChainCode may not overlap).</summary>
internal static class ConceptChainTemplateRules
{
    public static async Task<string?> ValidateOrderedTypesBelongToSubjectAsync(
        IConceptTypeRepository types,
        Guid tenantId,
        Guid subjectId,
        IReadOnlyList<Guid> orderedConceptTypes,
        CancellationToken cancellationToken)
    {
        var subjectTypeIds = (await types.ListBySubjectAsync(tenantId, subjectId, cancellationToken))
            .Select(t => t.Id)
            .ToHashSet();

        return orderedConceptTypes.All(subjectTypeIds.Contains)
            ? null
            : "OrderedConceptTypes must all be concept types of the same subject.";
    }

    /// <summary>Two effective windows overlap when each starts on or before the other ends (open-ended = MaxValue).</summary>
    public static bool WindowsOverlap(
        DateTimeOffset aFrom, DateTimeOffset? aTo, DateTimeOffset bFrom, DateTimeOffset? bTo)
        => aFrom <= (bTo ?? DateTimeOffset.MaxValue) && bFrom <= (aTo ?? DateTimeOffset.MaxValue);

    public static ChainTemplateEntity? FindPublishedOverlap(
        IReadOnlyList<ChainTemplateEntity> sameCode,
        Guid? selfId,
        DateTimeOffset from,
        DateTimeOffset? to)
        => sameCode.FirstOrDefault(t =>
            (selfId is null || t.Id != selfId)
            && !t.IsArchived()
            && t.IsPublished()
            && WindowsOverlap(from, to, t.EffectiveFrom, t.EffectiveTo));
}

public sealed class CreateConceptChainTemplateHandler
    : IRequestHandler<CreateConceptChainTemplateCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConceptChainTemplateRepository _templates;
    private readonly IConceptTypeRepository _types;

    public CreateConceptChainTemplateHandler(
        ITenantContext tenant,
        IActorContext actor,
        IConceptChainTemplateRepository templates,
        IConceptTypeRepository types)
    {
        _tenant = tenant;
        _actor = actor;
        _templates = templates;
        _types = types;
    }

    public async Task<Response<Guid>> Handle(
        CreateConceptChainTemplateCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var error = KnowledgeValidation.ValidateCode(request.ChainCode, "ChainCode")
            ?? KnowledgeValidation.ValidateName(request.ChainName, "ChainName")
            ?? ConceptGraphValidation.ValidateChainStatus(request.Status)
            ?? KnowledgeValidation.ValidateRequiredSubject(request.SubjectId)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo)
            ?? ConceptGraphValidation.ValidateOrderedTypesShape(request.OrderedConceptTypes);
        if (error is not null)
        {
            return Response<Guid>.Fail(error, 400);
        }

        // V12 — every ordered type must belong to the subject.
        var membershipError = await ConceptChainTemplateRules.ValidateOrderedTypesBelongToSubjectAsync(
            _types, tenantId, request.SubjectId, request.OrderedConceptTypes, cancellationToken);
        if (membershipError is not null)
        {
            return Response<Guid>.Fail(membershipError, 400);
        }

        // V13 — a published version must not overlap another published version of the same code.
        var status = ConceptChainStatuses.Normalize(request.Status);
        if (string.Equals(status, ConceptChainStatuses.Published, StringComparison.OrdinalIgnoreCase))
        {
            var sameCode = await _templates.ListByCodeAsync(
                tenantId, request.SubjectId, request.ChainCode.Trim(), cancellationToken);
            if (ConceptChainTemplateRules.FindPublishedOverlap(
                    sameCode, null, request.EffectiveFrom, request.EffectiveTo) is { } clash)
            {
                return Response<Guid>.Fail(
                    $"Another published version of ChainCode '{request.ChainCode.Trim()}' overlaps this effective " +
                    $"window (templateId={clash.Id}).", 409);
            }
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new ChainTemplateEntity
        {
            TenantId = tenantId,
            SubjectId = request.SubjectId,
            ChainCode = request.ChainCode.Trim(),
            ChainName = request.ChainName.Trim(),
            Description = KnowledgeValidation.Trim(request.Description),
            OrderedConceptTypes = request.OrderedConceptTypes.ToList(),
            Status = status,
            ChainVersion = string.IsNullOrWhiteSpace(request.ChainVersion) ? "1.0" : request.ChainVersion.Trim(),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            CreatedAt = now,
            CreatedBy = _actor.ActorName
        };

        await _templates.InsertAsync(entity, cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }
}

public sealed class UpdateConceptChainTemplateHandler
    : IRequestHandler<UpdateConceptChainTemplateCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConceptChainTemplateRepository _templates;
    private readonly IConceptTypeRepository _types;

    public UpdateConceptChainTemplateHandler(
        ITenantContext tenant,
        IActorContext actor,
        IConceptChainTemplateRepository templates,
        IConceptTypeRepository types)
    {
        _tenant = tenant;
        _actor = actor;
        _templates = templates;
        _types = types;
    }

    public async Task<Response<bool>> Handle(
        UpdateConceptChainTemplateCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _templates.GetByIdAsync(tenantId, request.ConceptChainTemplateId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Concept chain template not found.", 404);
        }

        if (entity.IsArchived())
        {
            return Response<bool>.Fail("An archived concept chain template cannot be updated.", 409);
        }

        if (string.Equals(request.Status?.Trim(), ConceptChainStatuses.Archived, StringComparison.OrdinalIgnoreCase))
        {
            return Response<bool>.Fail("Use the archive endpoint to archive a concept chain template.", 400);
        }

        var error = KnowledgeValidation.ValidateName(request.ChainName, "ChainName")
            ?? ConceptGraphValidation.ValidateChainStatus(request.Status)
            ?? KnowledgeValidation.ValidateEffectiveFrom(request.EffectiveFrom)
            ?? KnowledgeValidation.ValidateEffectiveRange(request.EffectiveFrom, request.EffectiveTo)
            ?? ConceptGraphValidation.ValidateOrderedTypesShape(request.OrderedConceptTypes);
        if (error is not null)
        {
            return Response<bool>.Fail(error, 400);
        }

        // A published version freezes its sequence — changing it needs a new version.
        var sequenceChanged = !entity.OrderedConceptTypes.SequenceEqual(request.OrderedConceptTypes);
        if (entity.IsPublished() && sequenceChanged)
        {
            return Response<bool>.Fail(
                "OrderedConceptTypes is frozen on a published template; create a new version to change the sequence.",
                409);
        }

        if (sequenceChanged)
        {
            var membershipError = await ConceptChainTemplateRules.ValidateOrderedTypesBelongToSubjectAsync(
                _types, tenantId, entity.SubjectId, request.OrderedConceptTypes, cancellationToken);
            if (membershipError is not null)
            {
                return Response<bool>.Fail(membershipError, 400);
            }
        }

        // V13 — publishing must not overlap another published version of the same code.
        var status = ConceptChainStatuses.Normalize(request.Status ?? entity.Status);
        if (string.Equals(status, ConceptChainStatuses.Published, StringComparison.OrdinalIgnoreCase))
        {
            var sameCode = await _templates.ListByCodeAsync(
                tenantId, entity.SubjectId, entity.ChainCode, cancellationToken);
            if (ConceptChainTemplateRules.FindPublishedOverlap(
                    sameCode, entity.Id, request.EffectiveFrom, request.EffectiveTo) is { } clash)
            {
                return Response<bool>.Fail(
                    $"Another published version of ChainCode '{entity.ChainCode}' overlaps this effective window " +
                    $"(templateId={clash.Id}).", 409);
            }
        }

        var now = DateTimeOffset.UtcNow;
        entity.ChainName = request.ChainName.Trim();
        entity.Description = KnowledgeValidation.Trim(request.Description);
        entity.OrderedConceptTypes = request.OrderedConceptTypes.ToList();
        entity.Status = status;
        if (!string.IsNullOrWhiteSpace(request.ChainVersion))
        {
            entity.ChainVersion = request.ChainVersion.Trim();
        }

        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.UpdatedAt = now;
        entity.UpdatedBy = _actor.ActorName;

        await _templates.UpdateAsync(entity, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ArchiveConceptChainTemplateHandler
    : IRequestHandler<ArchiveConceptChainTemplateCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IConceptChainTemplateRepository _templates;

    public ArchiveConceptChainTemplateHandler(
        ITenantContext tenant, IActorContext actor, IConceptChainTemplateRepository templates)
    {
        _tenant = tenant;
        _actor = actor;
        _templates = templates;
    }

    public async Task<Response<bool>> Handle(
        ArchiveConceptChainTemplateCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var entity = await _templates.GetByIdAsync(tenantId, request.ConceptChainTemplateId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("Concept chain template not found.", 404);
        }

        if (entity.IsArchived())
        {
            return Response<bool>.Success(true); // idempotent
        }

        var now = DateTimeOffset.UtcNow;
        entity.Status = ConceptChainStatuses.Archived;
        entity.ArchivedAt = now;
        entity.ArchivedBy = _actor.ActorName;
        entity.UpdatedAt = now;
        entity.UpdatedBy = _actor.ActorName;

        await _templates.UpdateAsync(entity, cancellationToken);
        return Response<bool>.Success(true);
    }
}

public sealed class ListConceptChainTemplatesHandler
    : IRequestHandler<ListConceptChainTemplatesQuery, Response<ConceptChainTemplateListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IConceptChainTemplateRepository _templates;

    public ListConceptChainTemplatesHandler(ITenantContext tenant, IConceptChainTemplateRepository templates)
    {
        _tenant = tenant;
        _templates = templates;
    }

    public async Task<Response<ConceptChainTemplateListDto>> Handle(
        ListConceptChainTemplatesQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConceptChainTemplateListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<ChainTemplateEntity> rows = request.SubjectId is { } subjectId && subjectId != Guid.Empty
            ? await _templates.ListBySubjectAsync(tenantId, subjectId, cancellationToken)
            : await _templates.ListAsync(tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = ConceptChainStatuses.Normalize(request.Status);
            rows = rows.Where(x => x.Status == status);
        }

        if (request.EffectiveAt is { } at)
        {
            rows = rows.Where(x => x.EffectiveFrom <= at && (x.EffectiveTo is null || at <= x.EffectiveTo));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(x =>
                x.ChainName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.ChainCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(x => !x.IsArchived());
        }

        var items = rows.Select(ConceptGraphMapper.ToDto).ToList();
        return Response<ConceptChainTemplateListDto>.Success(new ConceptChainTemplateListDto(items, items.Count));
    }
}

public sealed class GetConceptChainTemplateHandler
    : IRequestHandler<GetConceptChainTemplateQuery, Response<ConceptChainTemplateDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IConceptChainTemplateRepository _templates;

    public GetConceptChainTemplateHandler(ITenantContext tenant, IConceptChainTemplateRepository templates)
    {
        _tenant = tenant;
        _templates = templates;
    }

    public async Task<Response<ConceptChainTemplateDto>> Handle(
        GetConceptChainTemplateQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConceptChainTemplateDto>.Fail("Tenant context is required.", 400);
        }

        var entity = await _templates.GetByIdAsync(tenantId, request.ConceptChainTemplateId, cancellationToken);
        return entity is null
            ? Response<ConceptChainTemplateDto>.Fail("Concept chain template not found.", 404)
            : Response<ConceptChainTemplateDto>.Success(ConceptGraphMapper.ToDto(entity));
    }
}
