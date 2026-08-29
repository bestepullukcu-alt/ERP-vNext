using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Domain.Entities;
using MediatR;

namespace Diten.CrmService.Application.Features.Segmentation.Handlers.QueryHandlers;

/// <summary>
/// Publishes the closed attribute catalog EXACTLY as the runtime enforces it — same codes, same operators, same
/// required parameters, same subject-type applicability. There is no second, UI-only list to drift out of sync with,
/// which is the whole point of a declared catalog.
/// <para>The published class matters: <c>concept.affinity</c> is declared <b>D</b> (derived in-service, so an empty
/// graph is an empty answer) with a <b>+X</b> marker meaning only its VALUE is proven cross-service. A consumer can
/// therefore predict the fail-closed behaviour of every attribute before it uses one.</para>
/// </summary>
public sealed class GetSegmentAttributeCatalogHandler
    : IRequestHandler<GetSegmentAttributeCatalogQuery, Response<SegmentAttributeCatalogDto>>
{
    private readonly ITenantContext _tenant;

    public GetSegmentAttributeCatalogHandler(ITenantContext tenant) => _tenant = tenant;

    public Task<Response<SegmentAttributeCatalogDto>> Handle(
        GetSegmentAttributeCatalogQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is null)
        {
            return Task.FromResult(
                Response<SegmentAttributeCatalogDto>.Fail("Tenant context is required.", 400));
        }

        var attributes = SegmentAttributeCatalog.All
            .Select(a => new SegmentAttributeDto(
                a.AttributeCode,
                a.AttributeClass,
                a.DeclaredClass,
                a.Source,
                a.ValueType,
                a.Operators,
                a.RequiredParameters,
                a.OptionalParameters,
                a.AllowedSubjectTypes,
                a.RequiresCrossServiceValueValidation,
                a.CrossServiceReferenceKind,
                new SegmentAttributeValueSourceDto(
                    a.ValueSource.Kind,
                    a.ValueSource.ReferenceSetCode,
                    a.ValueSource.AllowedValues,
                    a.ValueSource.EntityKind)))
            .OrderBy(a => a.AttributeCode, StringComparer.Ordinal)
            .ToList();

        var dto = new SegmentAttributeCatalogDto(
            attributes,
            SegmentAttributeValueSource.AllKinds,
            new[]
            {
                SegmentAttributeCatalog.ClassNative,
                SegmentAttributeCatalog.ClassJoin,
                SegmentAttributeCatalog.ClassDerived,
                SegmentAttributeCatalog.ClassCrossService
            },
            SegmentOperators.All,
            SegmentValueTypes.All,
            SegmentLimits.MaxValuesPerInOperator,
            SegmentLimits.MaxCriteriaDepth,
            SegmentLimits.MaxCriteriaNodes,
            SegmentLimits.MaxChildrenPerGroup,
            SegmentLimits.DefaultConceptAffinityDepth,
            SegmentLimits.MaxConceptAffinityDepth,
            ConceptAffinityRelationshipTypes.All);

        return Task.FromResult(Response<SegmentAttributeCatalogDto>.Success(dto));
    }
}
