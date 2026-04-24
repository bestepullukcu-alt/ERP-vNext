using Diten.Application.Repositories;
using Diten.Domain.Aggregates.Task;
using Diten.Persistence.Context;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Diten.Persistence.Repositories;

public class TaskRepository : GenericRepository<TaskAggregate>, ITaskRepository
{
    public TaskRepository(MongoDbContext context) : base(context)
    {
    }

    public new async Task<TaskAggregate> AddAsync(TaskAggregate task)
    {
        return await base.AddAsync(task);
    }

    public new async Task<List<TaskAggregate>> GetAllAsync()
    {
        var result = await base.GetAllAsync();
        return new List<TaskAggregate>(result);
    }

    public async Task<bool> AnyAsync()
    {
        return await _collection.Find(_ => true).AnyAsync();
    }
}
