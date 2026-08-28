using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.StrategyTemplate.Commands;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Handlers.CommandHandlers;

/// <summary>
/// Updates a strategy template.
/// <para><b>The freeze guard.</b> Once a play is active its bindings are frozen: they may not change without a new
/// version. The guard compares the SIGNATURE of what is bound, not the ids the payload arrived with, so a UI that
/// round-trips the whole document can still rename a live play — and a real binding change on a frozen play is a 409
/// pointing at <c>new-version</c>.</para>
/// <para>An omitted (null) binding list means "leave it alone". An archived template accepts no update at all.</para>
/// </summary>
public sealed class UpdateStrategyTemplateHandler : IRequestHandler<UpdateStrategyTemplateCommand, Response<bool>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IStrategyTemplateRepository _templates;
    private readonly StrategyTemplateBindingValidator _bindings;
    private readonly IStrategyTemplateProductReferenceValidator _references;

    public UpdateStrategyTemplateHandler(
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

    public async Task<Response<bool>> Handle(
        UpdateStrategyTemplateCommand request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<bool>.Fail("Tenant context is required.", 400);
        }

        var template = await _templates.GetByIdAsync(tenantId, request.TemplateId, cancellationToken);
        if (template is null)
        {
            return Response<bool>.Fail("Strategy template not found.", 404);
        }

        if (template.IsArchived())
        {
            return Response<bool>.Fail("An archived strategy template cannot be updated.", 409);
        }

        var expectedVersion = request.ExpectedVersion ?? template.Version;
        if (expectedVersion != template.Version)
        {
            return Response<bool>.Fail(
                "The strategy template changed since it was loaded. Reload and try again.", 409);
        }

        // Null = "leave this binding alone"; that is what makes a metadata edit possible on a frozen play.
        var segmentBindings = request.SegmentBindings is null
            ? template.SegmentBindings
            : StrategyTemplateMapper.ToSegmentBindings(request.SegmentBindings);
        var frequencyIntent = request.FrequencyIntent is null
            ? template.FrequencyIntent
            : StrategyTemplateMapper.ToFrequencyIntent(request.FrequencyIntent);
        var productLines = request.ProductLines is null
            ? template.ProductLines
            : StrategyTemplateMapper.ToProductLines(request.ProductLines);
        var contentBindings = request.ContentBindings is null
            ? template.ContentBindings
            : StrategyTemplateMapper.ToContentBindings(request.ContentBindings);

        // The freeze guard runs BEFORE the shape validation on purpose: on a frozen play a binding change is refused
        // outright, so the caller must hear "these bindings are frozen" (409) rather than a shape complaint about a
        // payload that was never going to be accepted anyway.
        var incomingSignature = StrategyTemplateWriteGuards.BindingSignature(
            segmentBindings, frequencyIntent, productLines, contentBindings);
        var bindingsChanged = !string.Equals(
            incomingSignature, StrategyTemplateWriteGuards.BindingSignature(template), StringComparison.Ordinal);

        if (template.AreBindingsFrozen() && bindingsChanged)
        {
            return Response<bool>.Fail(
                new[]
                {
                    StrategyTemplateErrorCodes.BindingsFrozen,
                    "The bindings of an active strategy template are frozen. Create a new version to change them."
                },
                409);
        }

        var shapeFailure = StrategyTemplateWriteGuards.ValidateShape(
            request.TemplateName, template.SubjectType, request.EffectiveFrom, request.EffectiveTo,
            request.BusinessUnitId, request.Description, request.Notes,
            segmentBindings, frequencyIntent, productLines, contentBindings);
        if (shapeFailure is not null)
        {
            return Response<bool>.Fail(StrategyTemplateWriteGuards.ToErrors(shapeFailure), shapeFailure.StatusCode);
        }

        template.TemplateName = request.TemplateName.Trim();
        template.BusinessUnitId = StrategyTemplateValidation.Trim(request.BusinessUnitId);
        template.Description = StrategyTemplateValidation.Trim(request.Description);
        template.Notes = StrategyTemplateValidation.Trim(request.Notes);
        template.EffectiveFrom = request.EffectiveFrom;
        template.EffectiveTo = request.EffectiveTo;
        template.SegmentBindings = segmentBindings.ToList();
        template.FrequencyIntent = frequencyIntent;
        template.ProductLines = productLines.ToList();
        template.ContentBindings = contentBindings.ToList();
        template.UpdatedAt = DateTimeOffset.UtcNow;
        template.UpdatedBy = _actor.ActorName;

        if (bindingsChanged)
        {
            var bindingFailure = await _bindings.ValidateAsync(
                tenantId, template, requireActiveSegments: false, cancellationToken);
            if (bindingFailure is not null)
            {
                return Response<bool>.Fail(
                    StrategyTemplateWriteGuards.ToErrors(bindingFailure), bindingFailure.StatusCode);
            }

            // Cross-service proof BEFORE the replace: on 503 the stored template is untouched.
            var referenceFailure = await StrategyTemplateWriteGuards.ValidateCrossServiceReferencesAsync(
                _references, template.ProductLines, cancellationToken);
            if (referenceFailure is not null)
            {
                return Response<bool>.Fail(
                    StrategyTemplateWriteGuards.ToErrors(referenceFailure), referenceFailure.StatusCode);
            }
        }

        var replaced = await _templates.ReplaceAsync(template, expectedVersion, cancellationToken);
        return replaced
            ? Response<bool>.Success(true)
            : Response<bool>.Fail("The strategy template changed since it was loaded. Reload and try again.", 409);
    }
}
