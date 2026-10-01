using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.Concept.Relationship;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.ChainTemplate;

/// <summary>
/// WP-CT-BE-A — template conformance diagnostics against a SUPPLIED spine. The draft editor sends the ordered type ids it
/// derives live from its branches, without saving, and gets back how every ACTIVE relationship of the subject classifies
/// (<see cref="ConceptChainConformanceResults"/>). Read-only: nothing is persisted (the stored <c>IsTemplateConforming</c>
/// flag is untouched), no traversal, no resolution, no enforcement (D8). An empty spine is valid (every edge is <c>out</c>).
/// </summary>
public sealed record GetChainTemplateConformanceDiagnosticsQuery(
    Guid SubjectId,
    IReadOnlyList<Guid>? OrderedConceptTypeIds) : IRequest<Response<ChainTemplateConformanceDiagnosticsDto>>;

/// <summary>One active relationship classified against the supplied spine. <c>MissingTypeIds</c> is non-empty only for
/// <c>out</c>; <c>IsReversed</c> tells the UI the pair was read against its narrative direction (addresses / evidences).</summary>
public sealed record ChainTemplateConformanceDiagnosticDto(
    Guid ConceptRelationshipId,
    string RelationshipCode,
    string RelationshipName,
    string RelationshipType,
    Guid FromConceptNodeId,
    string? FromConceptNodeName,
    Guid FromConceptTypeId,
    Guid ToConceptNodeId,
    string? ToConceptNodeName,
    Guid ToConceptTypeId,
    bool IsReversed,
    string Result,
    IReadOnlyList<Guid> MissingTypeIds);

public sealed record ChainTemplateConformanceDiagnosticsDto(
    Guid SubjectId,
    IReadOnlyList<Guid> OrderedConceptTypeIds,
    IReadOnlyList<ChainTemplateConformanceDiagnosticDto> Items,
    int Total,
    int ConformingCount,
    int OrderCount,
    int OutCount);

public sealed class GetChainTemplateConformanceDiagnosticsHandler
    : IRequestHandler<GetChainTemplateConformanceDiagnosticsQuery, Response<ChainTemplateConformanceDiagnosticsDto>>
{
    /// <summary>Upper bound on the supplied spine — a chain blueprint, not a bulk payload.</summary>
    public const int MaxSpineLength = 200;

    private readonly ITenantContext _tenant;
    private readonly IConceptRelationshipRepository _relationships;
    private readonly IConceptNodeRepository _nodes;

    public GetChainTemplateConformanceDiagnosticsHandler(
        ITenantContext tenant, IConceptRelationshipRepository relationships, IConceptNodeRepository nodes)
    {
        _tenant = tenant;
        _relationships = relationships;
        _nodes = nodes;
    }

    public async Task<Response<ChainTemplateConformanceDiagnosticsDto>> Handle(
        GetChainTemplateConformanceDiagnosticsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ChainTemplateConformanceDiagnosticsDto>.Fail("Tenant context is required.", 400);
        }

        var error = KnowledgeValidation.ValidateRequiredSubject(request.SubjectId);
        if (error is not null)
        {
            return Response<ChainTemplateConformanceDiagnosticsDto>.Fail(error, 400);
        }

        var spine = request.OrderedConceptTypeIds ?? Array.Empty<Guid>();
        if (spine.Count > MaxSpineLength)
        {
            return Response<ChainTemplateConformanceDiagnosticsDto>.Fail(
                $"OrderedConceptTypeIds may hold at most {MaxSpineLength} type ids.", 400);
        }

        if (spine.Any(id => id == Guid.Empty))
        {
            return Response<ChainTemplateConformanceDiagnosticsDto>.Fail(
                "OrderedConceptTypeIds cannot contain an empty id.", 400);
        }

        var edges = (await _relationships.ListBySubjectAsync(tenantId, request.SubjectId, cancellationToken))
            .Where(e => e.IsActive())
            .OrderBy(e => e.RelationshipCode, StringComparer.Ordinal)
            .ToList();
        var nodes = (await _nodes.ListBySubjectAsync(tenantId, request.SubjectId, cancellationToken))
            .ToDictionary(n => n.Id);

        // An endpoint that cannot be resolved stays visible (Guid.Empty type ⇒ classified out), never silently dropped.
        var items = edges.Select(edge =>
        {
            nodes.TryGetValue(edge.FromConceptNodeId, out var from);
            nodes.TryGetValue(edge.ToConceptNodeId, out var to);
            var fromTypeId = from?.ConceptTypeId ?? Guid.Empty;
            var toTypeId = to?.ConceptTypeId ?? Guid.Empty;
            var classification = ConceptChainConformance.Classify(spine, fromTypeId, toTypeId, edge.RelationshipType);
            return new ChainTemplateConformanceDiagnosticDto(
                edge.Id, edge.RelationshipCode, edge.RelationshipName, edge.RelationshipType,
                edge.FromConceptNodeId, from?.ConceptNodeName, fromTypeId,
                edge.ToConceptNodeId, to?.ConceptNodeName, toTypeId,
                classification.IsReversed, classification.Result, classification.MissingTypeIds);
        }).ToList();

        return Response<ChainTemplateConformanceDiagnosticsDto>.Success(new ChainTemplateConformanceDiagnosticsDto(
            request.SubjectId,
            spine,
            items,
            items.Count,
            items.Count(i => i.Result == ConceptChainConformanceResults.Conforming),
            items.Count(i => i.Result == ConceptChainConformanceResults.Order),
            items.Count(i => i.Result == ConceptChainConformanceResults.Out)));
    }
}
