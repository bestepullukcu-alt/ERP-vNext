using Diten.Domain.Aggregates.EnterpriseStrategy;
using Diten.Persistence.Context;
using MongoDB.Driver;

namespace Diten.Persistence.EnterpriseStrategy;

internal static class PlanningCycleMongoMigration
{
    public static async Task EnsureAppliedAsync(MongoDbContext context, CancellationToken cancellationToken = default)
    {
        var planningCycles = context.GetCollection<PlanningCycleAggregate>(nameof(PlanningCycleAggregate));
        var strategyPeriods = context.GetCollection<StrategyPeriodAggregate>(nameof(StrategyPeriodAggregate));

        await planningCycles.Indexes.CreateManyAsync(
            new[]
            {
                new CreateIndexModel<PlanningCycleAggregate>(
                    Builders<PlanningCycleAggregate>.IndexKeys.Ascending(x => x.Code),
                    new CreateIndexOptions { Name = "ux_planning_cycle_code", Unique = true }),
                new CreateIndexModel<PlanningCycleAggregate>(
                    Builders<PlanningCycleAggregate>.IndexKeys.Ascending(x => x.Status),
                    new CreateIndexOptions { Name = "ix_planning_cycle_status" })
            },
            cancellationToken);

        await strategyPeriods.Indexes.CreateManyAsync(
            new[]
            {
                new CreateIndexModel<StrategyPeriodAggregate>(
                    Builders<StrategyPeriodAggregate>.IndexKeys.Ascending(x => x.Code),
                    new CreateIndexOptions { Name = "ux_strategy_period_code", Unique = true }),
                new CreateIndexModel<StrategyPeriodAggregate>(
                    Builders<StrategyPeriodAggregate>.IndexKeys.Ascending(x => x.PlanningCycleId),
                    new CreateIndexOptions { Name = "ix_strategy_period_planning_cycle_id" }),
                new CreateIndexModel<StrategyPeriodAggregate>(
                    Builders<StrategyPeriodAggregate>.IndexKeys
                        .Ascending(x => x.CompanyId)
                        .Ascending(x => x.BusinessUnitId)
                        .Ascending(x => x.RegionId),
                    new CreateIndexOptions { Name = "ix_strategy_period_scope" })
            },
            cancellationToken);
    }
}
