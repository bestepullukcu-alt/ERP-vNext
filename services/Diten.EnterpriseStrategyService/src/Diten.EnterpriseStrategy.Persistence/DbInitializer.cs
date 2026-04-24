using Diten.Application.Common.Interfaces;
using Diten.Application.Repositories;
using Diten.Domain.Aggregates.DemandIdea;
using Diten.Domain.Aggregates.Task;
using Diten.Persistence.Context;
using Diten.Persistence.EnterpriseStrategy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Diten.Persistence;

public static class DbInitializer
{
    public static async Task SeedData(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var mongoContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        var configuration = scope.ServiceProvider.GetService<IConfiguration>();
        await PlanningCycleMongoMigration.EnsureAppliedAsync(mongoContext);
        await StrategyLibraryMongoMigration.EnsureAppliedAsync(mongoContext);
        await StrategicGoalMongoMigration.EnsureAppliedAsync(mongoContext, configuration);

        var repository = scope.ServiceProvider.GetRequiredService<ITaskRepository>();

        if (!await repository.AnyAsync())
        {
            var tasks = new[]
            {
                new TaskAggregate { Title = "Dashboard Integration", Description = "Integrate core KPI cards to the main dashboard view.", Status = "Completed" },
                new TaskAggregate { Title = "Database Optimization", Description = "Optimize MongoDB indexing for report queries.", Status = "InProgress" },
                new TaskAggregate { Title = "User Authentication", Description = "Implement JWT based authentication system.", Status = "Pending" },
                new TaskAggregate { Title = "Report Export Feature", Description = "Enable PDF and Excel export for task reports.", Status = "Completed" },
                new TaskAggregate { Title = "API Documentation", Description = "Update Swagger documentation for v1 endpoints.", Status = "Pending" },
                new TaskAggregate { Title = "Mobile Responsiveness", Description = "Fix layout issues on mobile devices for the report page.", Status = "InProgress" },
                new TaskAggregate { Title = "Unit Testing", Description = "Add unit tests for Task handlers.", Status = "Pending" },
                new TaskAggregate { Title = "Task Filtering", Description = "Add server-side filtering to task table.", Status = "Completed" }
            };

            foreach (var task in tasks)
            {
                await repository.AddAsync(task);
            }
        }

        var demandRepo = scope.ServiceProvider.GetRequiredService<IRepository<DemandIdeaAggregate>>();
        await DemandIdeaSeed.SeedIfEmptyAsync(demandRepo);
    }
}
