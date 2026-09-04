namespace Diten.PpmService.Domain.Repositories;

public interface IPpmUnitOfWork
{
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
