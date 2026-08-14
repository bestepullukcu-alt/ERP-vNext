using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence;

public sealed class PlatformTransactionExecutor : IPlatformTransactionExecutor
{
    private const int MaximumBodyAttempts = 3;
    private const int MaximumCommitAttempts = 3;
    private readonly IPlatformDbContext _dbContext;
    private readonly IPlatformTransactionFaultProbe _faultProbe;

    public PlatformTransactionExecutor(IPlatformDbContext dbContext, IPlatformTransactionFaultProbe? faultProbe = null)
    {
        _dbContext = dbContext;
        _faultProbe = faultProbe ?? new NoOpPlatformTransactionFaultProbe();
    }

    public async Task<T> ExecuteAsync<T>(
        Func<IPlatformTransactionSession, CancellationToken, Task<T>> body,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);

        for (var bodyAttempt = 1; bodyAttempt <= MaximumBodyAttempts; bodyAttempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var handle = await _dbContext.Client.StartSessionAsync(cancellationToken: cancellationToken);
            var session = new PlatformMongoTransactionSession(_dbContext.Client, handle);
            try
            {
                handle.StartTransaction();
            }
            catch (NotSupportedException exception)
            {
                throw new PlatformTransactionUnavailableException(
                    "MongoDB deployment does not support Platform transactions.", exception);
            }

            T result;
            try
            {
                result = await body(session, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await AbortIfActiveAsync(handle, CancellationToken.None);
                throw;
            }
            catch (MongoException exception) when (
                IsTransientBodyFailure(exception)
                && bodyAttempt < MaximumBodyAttempts
                && !cancellationToken.IsCancellationRequested)
            {
                await AbortIfActiveAsync(handle, cancellationToken);
                continue;
            }
            catch (MongoException exception) when (!cancellationToken.IsCancellationRequested)
            {
                await AbortIfActiveAsync(handle, CancellationToken.None);
                throw new PlatformTransactionUnavailableException(
                    "MongoDB could not execute the Platform transaction body.", exception);
            }
            catch
            {
                await AbortIfActiveAsync(handle, cancellationToken);
                throw;
            }

            for (var commitAttempt = 1; ; commitAttempt++)
            {
                try
                {
                    await _faultProbe.BeforeCommitAsync(session, commitAttempt, cancellationToken);
                    await handle.CommitTransactionAsync(cancellationToken);
                    await _faultProbe.AfterCommitAsync(session, commitAttempt, cancellationToken);
                    return result;
                }
                catch (MongoException exception) when (
                    exception.HasErrorLabel("UnknownTransactionCommitResult")
                    && commitAttempt < MaximumCommitAttempts
                    && !cancellationToken.IsCancellationRequested)
                {
                    // Commit-only retry on the same session. The body is never replayed here.
                }
                catch (MongoException exception) when (exception.HasErrorLabel("UnknownTransactionCommitResult"))
                {
                    throw new PlatformTransactionUnavailableException(
                        "MongoDB could not establish the transaction commit result.",
                        exception);
                }
            }
        }

        throw new PlatformTransactionUnavailableException(
            "MongoDB exhausted transient transaction body retries before commit began.");
    }

    private static async Task AbortIfActiveAsync(
        IClientSessionHandle session,
        CancellationToken cancellationToken)
    {
        if (session.IsInTransaction)
        {
            await session.AbortTransactionAsync(cancellationToken);
        }
    }

    private static bool IsTransientBodyFailure(MongoException exception) =>
        exception.HasErrorLabel("TransientTransactionError")
        // Single-node replica sets can surface a transactional write conflict
        // without attaching the label. Code 112 is MongoDB's WriteConflict and
        // is safe for a fully aborted pre-commit body retry.
        || exception is MongoCommandException { Code: 112 };
}
