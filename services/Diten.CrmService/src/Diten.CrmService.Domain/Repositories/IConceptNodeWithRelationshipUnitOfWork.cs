using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>
/// SCMM-09 (②) — atomic combined-write for the legacy "New UCLN List" ergonomics: a new <see cref="ConceptNode"/> and
/// the <see cref="ConceptRelationship"/> that connects it are persisted together, all-or-nothing. Uses a Mongo
/// transaction when the server supports one (replica set / mongos); on a standalone dev server it falls back to a
/// sequential insert with compensation (the just-inserted node is removed if the edge insert fails), so a half-written
/// pair is never left behind.
/// </summary>
public interface IConceptNodeWithRelationshipUnitOfWork
{
    Task CommitAsync(ConceptNode node, ConceptRelationship relationship, CancellationToken cancellationToken);
}
