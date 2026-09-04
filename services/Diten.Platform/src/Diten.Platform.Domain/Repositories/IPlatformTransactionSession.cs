namespace Diten.Platform.Domain.Repositories;

/// <summary>
/// Opaque, explicitly propagated handle for one Platform Mongo transaction.
/// Domain/Application code cannot manufacture or unwrap it.
/// </summary>
public interface IPlatformTransactionSession
{
    Guid TransactionId { get; }
}

public interface IPlatformTransactionExecutor
{
    Task<T> ExecuteAsync<T>(
        Func<IPlatformTransactionSession, CancellationToken, Task<T>> body,
        CancellationToken cancellationToken = default);
}

public sealed class PlatformTransactionUnavailableException : Exception
{
    public const int ServiceUnavailableStatusCode = 503;
    public int StatusCode => ServiceUnavailableStatusCode;

    public PlatformTransactionUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
