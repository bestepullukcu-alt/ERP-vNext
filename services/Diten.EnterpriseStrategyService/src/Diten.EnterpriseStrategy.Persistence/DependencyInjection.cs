using Diten.Application.Common.Interfaces;
using Diten.Application.EnterpriseStrategy.Repositories;
using Diten.Domain.Aggregates.DemandIdea;
using Diten.Persistence.Context;
using Diten.Persistence.Repositories;
using Diten.Persistence.EnterpriseStrategy;
using Diten.Application.Repositories;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.EnterpriseStrategy.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddSingleton<MongoDbContext>();
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IGoalRepository, GoalRepository>();
        services.AddScoped<IObjectiveRepository, ObjectiveRepository>();
        services.AddScoped<IStrategyConnectionRepository, StrategyConnectionRepository>();
        services.AddScoped<IInitiativeStrategyLinkRepository, InitiativeStrategyLinkRepository>();
        services.AddScoped<IProjectStrategyLinkRepository, ProjectStrategyLinkRepository>();
        services.AddScoped<IPpmInitiativeCacheRepository, PpmInitiativeCacheRepository>();
        services.AddScoped<IPpmProjectCacheRepository, PpmProjectCacheRepository>();
        services.AddScoped<IPlanningCycleRepository, PlanningCycleRepository>();
        services.AddScoped<IStrategyPeriodRepository, StrategyPeriodRepository>();
        services.AddScoped<IStrategyLibraryRepository, StrategyLibraryRepository>();
        services.AddScoped<IGoalTemplateSnapshotWriter, GoalTemplateSnapshotWriter>();
        services.AddScoped<IKpiScorecardRepository, KpiScorecardRepository>();
        services.AddScoped<MongoEnterpriseStrategyAuditStore>();
        services.AddScoped<IEnterpriseStrategyAuditSink>(sp => sp.GetRequiredService<MongoEnterpriseStrategyAuditStore>());
        services.AddScoped<IEnterpriseStrategyAuditStore>(sp => sp.GetRequiredService<MongoEnterpriseStrategyAuditStore>());

        return services;
    }
}
