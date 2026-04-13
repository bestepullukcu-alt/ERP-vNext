using System.Collections.Generic;
using System.Threading.Tasks;
using Diten.Domain.Aggregates.Task;

namespace Diten.Application.Repositories;

public interface ITaskRepository
{
    Task<TaskAggregate> AddAsync(TaskAggregate task);
    Task<List<TaskAggregate>> GetAllAsync();
    Task<TaskAggregate> GetByIdAsync(string id);
    Task UpdateAsync(TaskAggregate task);
    Task DeleteAsync(TaskAggregate task);
    Task<bool> AnyAsync();
}
