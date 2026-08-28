using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.StrategyTemplate.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using TemplateEntity = Diten.CrmService.Domain.Entities.StrategyTemplate;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Handlers.CommandHandlers;

/// <summary>
/// Clones a template into a new DRAFT version of the same lineage. The clone gets fresh child ids for every binding,
/// line and allocation, so the two versions share no identity and an id from one can never address the other.
/// <para>The source stays untouched and live; it is superseded only when the new version is activated. Read then insert
/// is two independent single-document writes and can never leave half a template behind.</para>
/// </summary>
public sealed class CreateStrategyTemplateVersionHandler
    : IRequestHandler<CreateStrategyTemplateVersionCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IStrategyTemplateRepository _templates;

    public CreateStrategyTemplateVersionHandler(
        ITenantContext tenant, IActorContext actor, IStrategyTemplateRepository templates)
    {
        _tenant = tenant;
        _actor = actor;
        _templates = templates;
    }

    public async Task<Response<Guid>> Handle(
        CreateStrategyTemplateVersionCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var source = await _templates.GetByIdAsync(tenantId, request.TemplateId, cancellationToken);
        if (source is null)
        {
            return Response<Guid>.Fail("Strategy template not found.", 404);
        }

        if (source.IsArchived())
        {
            return Response<Guid>.Fail("An archived strategy template cannot be versioned.", 409);
        }

        var lineage = await _templates.ListByLineageAsync(tenantId, source.VersionLineageId, cancellationToken);
        if (lineage.Any(t => !t.IsArchived()
                             && string.Equals(
                                 t.TemplateStatus, StrategyTemplateStatuses.Draft, StringComparison.Ordinal)))
        {
            return Response<Guid>.Fail(
                "This lineage already has an open draft version. Finish or archive it first.", 409);
        }

        var (segments, frequency, products, contents) = StrategyTemplateMapper.CloneBindings(source);
        var nextVersion = lineage.Count == 0 ? source.TemplateVersion + 1 : lineage.Max(t => t.TemplateVersion) + 1;

        var clone = new TemplateEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateCode = source.TemplateCode,
            TemplateName = source.TemplateName,
            SubjectType = source.SubjectType,
            TemplateStatus = StrategyTemplateStatuses.Draft,
            TemplateVersion = nextVersion,
            VersionLineageId = source.VersionLineageId,
            BusinessUnitId = source.BusinessUnitId,
            Description = source.Description,
            Notes = source.Notes,
            EffectiveFrom = source.EffectiveFrom,
            EffectiveTo = source.EffectiveTo,
            SegmentBindings = segments,
            FrequencyIntent = frequency,
            ProductLines = products,
            ContentBindings = contents,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _actor.ActorName
            // BindingsFrozenAt / ActivatedAt stay null: a clone is a draft, and a draft is not frozen.
        };

        await _templates.InsertAsync(clone, cancellationToken);
        return Response<Guid>.Success(clone.Id, 201);
    }
}
