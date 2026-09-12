using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// SCMM-09 (②) atomic node+edge combined-write. Mirrors <see cref="TerritoryDraftCloneUnitOfWork"/>: a real Mongo
/// transaction when the server supports one, else a sequential insert with compensation for the CRM standalone dev
/// server (guarded by <c>SupportsTransactionsAsync</c>). Collections match the concept-graph repositories.
/// </summary>
public sealed class ConceptNodeWithRelationshipUnitOfWork(IMongoDatabase database) : IConceptNodeWithRelationshipUnitOfWork
{
    public async Task CommitAsync(ConceptNode node, ConceptRelationship relationship, CancellationToken cancellationToken)
    {
        var nodes = database.GetCollection<ConceptNode>(ConceptNodeRepository.CollectionName);
        var relationships = database.GetCollection<ConceptRelationship>(ConceptRelationshipRepository.CollectionName);

        if (!await SupportsTransactionsAsync(cancellationToken))
        {
            await nodes.InsertOneAsync(node, cancellationToken: cancellationToken);
            try
            {
                await relationships.InsertOneAsync(relationship, cancellationToken: cancellationToken);
            }
            catch
            {
                // Compensation: the node was never meant to exist without its edge — remove it so no orphan remains.
                await nodes.DeleteOneAsync(x => x.TenantId == node.TenantId && x.Id == node.Id, cancellationToken);
                throw;
            }

            return;
        }

        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            await nodes.InsertOneAsync(session, node, cancellationToken: cancellationToken);
            await relationships.InsertOneAsync(session, relationship, cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task<bool> SupportsTransactionsAsync(CancellationToken cancellationToken)
    {
        var hello = await database.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: cancellationToken);
        return hello.Contains("setName") || string.Equals(hello.GetValue("msg", "").AsString, "isdbgrid", StringComparison.OrdinalIgnoreCase);
    }
}
