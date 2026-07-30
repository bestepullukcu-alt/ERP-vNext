using Diten.PpmService.Domain.Entities;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence.Mongo;

public sealed class PpmMongoIndexInitializer(IMongoDatabase database) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var portfolios = database.GetCollection<Portfolio>(PpmCollectionNames.Portfolios);
        var initiatives = database.GetCollection<Initiative>(PpmCollectionNames.Initiatives);
        var programs = database.GetCollection<Program>(PpmCollectionNames.Programs);
        var projects = database.GetCollection<Project>(PpmCollectionNames.Projects);
        var auditIntents = database.GetCollection<AuditIntentDocument>(PpmCollectionNames.AuditIntents);

        await CreateEntityIndexes(portfolios, cancellationToken);
        await CreateEntityIndexes(initiatives, cancellationToken);
        await CreateEntityIndexes(programs, cancellationToken);
        await CreateEntityIndexes(projects, cancellationToken);

        await initiatives.Indexes.CreateOneAsync(
            new CreateIndexModel<Initiative>(
                Builders<Initiative>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PortfolioId)
                    .Ascending(x => x.IsDeleted)),
            cancellationToken: cancellationToken);

        await programs.Indexes.CreateOneAsync(
            new CreateIndexModel<Program>(
                Builders<Program>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PortfolioId)
                    .Ascending(x => x.IsDeleted)),
            cancellationToken: cancellationToken);

        await projects.Indexes.CreateOneAsync(
            new CreateIndexModel<Project>(
                Builders<Project>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ParentType)
                    .Ascending(x => x.ParentId)
                    .Ascending(x => x.IsDeleted)),
            cancellationToken: cancellationToken);

        await auditIntents.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<AuditIntentDocument>(
                Builders<AuditIntentDocument>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.EntityType)
                    .Ascending(x => x.EntityId)
                    .Descending(x => x.OccurredAtUtc)),
            new CreateIndexModel<AuditIntentDocument>(
                Builders<AuditIntentDocument>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.OccurredAtUtc))
        ], cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task CreateEntityIndexes<T>(
        IMongoCollection<T> collection,
        CancellationToken cancellationToken)
        where T : EntityBase
    {
        var activeOnly = new BsonDocument(nameof(EntityBase.IsDeleted), false);
        await collection.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<T>(
                Builders<T>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(nameof(Portfolio.Code)),
                new CreateIndexOptions<T>
                {
                    Unique = true,
                    PartialFilterExpression = activeOnly,
                    Name = "ux_tenant_code_active"
                }),
            new CreateIndexModel<T>(
                Builders<T>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IsDeleted)
                    .Ascending(nameof(Portfolio.Code)),
                new CreateIndexOptions { Name = "ix_tenant_active_code" })
        ], cancellationToken);
    }
}
