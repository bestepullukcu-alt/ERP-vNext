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
        var investmentCases = database.GetCollection<InvestmentCase>(PpmCollectionNames.InvestmentCases);
        var benefitCommitments = database.GetCollection<BenefitCommitment>(PpmCollectionNames.BenefitCommitments);
        var auditIntents = database.GetCollection<AuditIntentDocument>(PpmCollectionNames.AuditIntents);
        var eventOutbox = database.GetCollection<PpmEventOutboxDocument>(PpmCollectionNames.EventOutbox);

        await CreateEntityIndexes(portfolios, cancellationToken);
        await CreateEntityIndexes(initiatives, cancellationToken);
        await CreateEntityIndexes(programs, cancellationToken);
        await CreateEntityIndexes(projects, cancellationToken);
        await CreateEntityIndexes(investmentCases, cancellationToken);
        await CreateEntityIndexes(benefitCommitments, cancellationToken);

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

        await investmentCases.Indexes.CreateOneAsync(
            new CreateIndexModel<InvestmentCase>(
                Builders<InvestmentCase>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.PortfolioId)
                    .Ascending(x => x.IsDeleted)),
            cancellationToken: cancellationToken);

        await benefitCommitments.Indexes.CreateOneAsync(
            new CreateIndexModel<BenefitCommitment>(
                Builders<BenefitCommitment>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.InvestmentCaseId)
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
                    .Ascending(x => x.OccurredAtUtc)),
            new CreateIndexModel<AuditIntentDocument>(
                Builders<AuditIntentDocument>.IndexKeys
                    .Ascending(x => x.OutboxEnqueuedAtUtc)
                    .Ascending(x => x.DispatchFailureCode)
                    .Ascending(x => x.OccurredAtUtc),
                new CreateIndexOptions { Name = "ix_audit_dispatch_pending" })
        ], cancellationToken);

        await eventOutbox.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<PpmEventOutboxDocument>(
                Builders<PpmEventOutboxDocument>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Status)
                    .Ascending(x => x.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_event_outbox_tenant_status_created" }),
            new CreateIndexModel<PpmEventOutboxDocument>(
                Builders<PpmEventOutboxDocument>.IndexKeys.Ascending(x => x.EventId),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_ppm_event_outbox_event_id"
                }),
            new CreateIndexModel<PpmEventOutboxDocument>(
                Builders<PpmEventOutboxDocument>.IndexKeys
                    .Ascending(x => x.Status)
                    .Ascending(x => x.NextAttemptAtUtc)
                    .Ascending(x => x.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_ppm_event_outbox_claim" }),
            new CreateIndexModel<PpmEventOutboxDocument>(
                Builders<PpmEventOutboxDocument>.IndexKeys
                    .Ascending(x => x.Status)
                    .Ascending(x => x.UpdatedAtUtc),
                new CreateIndexOptions { Name = "ix_ppm_event_outbox_stale" })
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
