using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Node;

/// <summary>
/// SCMM-09 (②) — legacy "New UCLN List" ergonomics: create a NEW <c>ConceptNode</c> (the value) AND connect it to an
/// EXISTING counterpart node with a <c>ConceptRelationship</c> in ONE atomic operation. The separate create-node /
/// create-relationship commands remain; this is an authoring convenience, not a new model. <c>TenantId</c> is
/// server-resolved. <c>NewNodeIsSource</c> decides the edge direction: <c>true</c> ⇒ newNode → counterpart, <c>false</c>
/// ⇒ counterpart → newNode. The SAME relationship rules are applied (type/direction/priority/duplicate-active/cycle/
/// conformance); consistency is atomic (transaction when supported, else compensation).
/// </summary>
public sealed record CreateConceptNodeWithRelationshipCommand(
    // new node
    Guid SubjectId,
    Guid ConceptTypeId,
    string ConceptNodeCode,
    string ConceptNodeName,
    DateTimeOffset NodeEffectiveFrom,
    // edge
    Guid CounterpartConceptNodeId,
    string RelationshipType,
    string RelationshipCode,
    string RelationshipName,
    DateTimeOffset RelationshipEffectiveFrom,
    bool NewNodeIsSource = true,
    string? NodeDescription = null,
    string? NodeStatus = null,
    DateTimeOffset? NodeEffectiveTo = null,
    string? ExternalRefType = null,
    string? ExternalRefId = null,
    string? MetadataJson = null,
    string? Direction = null,
    int Priority = 0,
    string? RelationshipStatus = null,
    DateTimeOffset? RelationshipEffectiveTo = null)
    : IRequest<Response<ConceptNodeWithRelationshipResult>>;

/// <summary>Ids of the two aggregates the combined-write created.</summary>
public sealed record ConceptNodeWithRelationshipResult(Guid ConceptNodeId, Guid ConceptRelationshipId);
