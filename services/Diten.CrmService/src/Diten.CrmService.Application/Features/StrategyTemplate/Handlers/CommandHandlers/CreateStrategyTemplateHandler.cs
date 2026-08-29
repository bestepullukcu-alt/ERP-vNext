using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.StrategyTemplate.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using TemplateEntity = Diten.CrmService.Domain.Entities.StrategyTemplate;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Handlers.CommandHandlers;

/// <summary>
/// Creates a strategy template. It is always born <c>draft</c>, at business version 1, as the root of its own lineage:
/// a play is never born live, because putting one live is a separate act with a separate permission.
/// <para><b>Order matters.</b> Shape is validated in-domain, then the in-service bindings (segment / policy / content)
/// are proven, then every MDM reference is proven cross-service, and only then is anything written — so a 503 from the
/// product master leaves no half-authored play behind.</para>
/// <para>This handler writes to exactly one collection. It creates no <c>VisitFrequencyPolicy</c>, no
/// <c>CampaignTarget</c>, no MicroTarget row and no membership: a template binds, it does not produce.</para>
/// </summary>
public sealed class CreateStrategyTemplateHandler : IRequestHandler<CreateStrategyTemplateCommand, Response<Guid>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IStrategyTemplateRepository _templates;
    private readonly StrategyTemplateBindingValidator _bindings;
    private readonly IStrategyTemplateProductReferenceValidator _references;

    public CreateStrategyTemplateHandler(
        ITenantContext tenant,
        IActorContext actor,
        IStrategyTemplateRepository templates,
        StrategyTemplateBindingValidator bindings,
        IStrategyTemplateProductReferenceValidator references)
    {
        _tenant = tenant;
        _actor = actor;
        _templates = templates;
        _bindings = bindings;
        _references = references;
    }

    public async Task<Response<Guid>> Handle(
        CreateStrategyTemplateCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<Guid>.Fail("Tenant context is required.", 400);
        }

        var codeFailure = StrategyTemplateValidation.ValidateTemplateCode(request.TemplateCode);
        if (codeFailure is not null)
        {
            return Response<Guid>.Fail(StrategyTemplateWriteGuards.ToErrors(codeFailure), codeFailure.StatusCode);
        }

        var segmentBindings = StrategyTemplateMapper.ToSegmentBindings(request.SegmentBindings);
        var frequencyIntent = StrategyTemplateMapper.ToFrequencyIntent(request.FrequencyIntent);
        var productLines = StrategyTemplateMapper.ToProductLines(request.ProductLines);
        var contentBindings = StrategyTemplateMapper.ToContentBindings(request.ContentBindings);

        var shapeFailure = StrategyTemplateWriteGuards.ValidateShape(
            request.TemplateName, request.SubjectType, request.EffectiveFrom, request.EffectiveTo,
            request.BusinessUnitId, request.Description, request.Notes,
            segmentBindings, frequencyIntent, productLines, contentBindings);
        if (shapeFailure is not null)
        {
            return Response<Guid>.Fail(StrategyTemplateWriteGuards.ToErrors(shapeFailure), shapeFailure.StatusCode);
        }

        var code = request.TemplateCode.Trim().ToLowerInvariant();
        var existing = await _templates.ListByCodeAsync(tenantId, code, cancellationToken);
        if (existing.Any(t => !t.IsArchived()))
        {
            return Response<Guid>.Fail($"A live strategy template already uses TemplateCode '{code}'.", 409);
        }

        var id = Guid.NewGuid();
        var entity = new TemplateEntity
        {
            Id = id,
            TenantId = tenantId,
            TemplateCode = code,
            TemplateName = request.TemplateName.Trim(),
            SubjectType = StrategyTemplateSubjectTypes.Normalize(request.SubjectType),
            TemplateStatus = StrategyTemplateStatuses.Draft,
            TemplateVersion = 1,
            VersionLineageId = id,
            BusinessUnitId = StrategyTemplateValidation.Trim(request.BusinessUnitId),
            Description = StrategyTemplateValidation.Trim(request.Description),
            Notes = StrategyTemplateValidation.Trim(request.Notes),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            SegmentBindings = segmentBindings,
            FrequencyIntent = frequencyIntent,
            ProductLines = productLines,
            ContentBindings = contentBindings,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _actor.ActorName
        };

        // In-service proof (segment / policy / content) — read-only, and it stamps the provenance fields.
        var bindingFailure = await _bindings.ValidateAsync(
            tenantId, entity, requireActiveSegments: false, cancellationToken);
        if (bindingFailure is not null)
        {
            return Response<Guid>.Fail(
                StrategyTemplateWriteGuards.ToErrors(bindingFailure), bindingFailure.StatusCode);
        }

        // Cross-service proof BEFORE the insert: on 503 nothing is persisted at all.
        var referenceFailure = await StrategyTemplateWriteGuards.ValidateCrossServiceReferencesAsync(
            _references, productLines, cancellationToken);
        if (referenceFailure is not null)
        {
            return Response<Guid>.Fail(
                StrategyTemplateWriteGuards.ToErrors(referenceFailure), referenceFailure.StatusCode);
        }

        await _templates.InsertAsync(entity, cancellationToken);
        return Response<Guid>.Success(entity.Id, 201);
    }
}
