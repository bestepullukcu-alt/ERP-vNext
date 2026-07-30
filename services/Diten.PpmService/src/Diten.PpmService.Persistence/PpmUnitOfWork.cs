using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Domain.Exceptions;
using Diten.PpmService.Persistence.Mongo;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence;

public sealed class PpmUnitOfWork(PpmMongoContext context) : IPpmUnitOfWork
{
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        IClientSessionHandle? session = null;

        try
        {
            session = await context.Client.StartSessionAsync(
                cancellationToken: cancellationToken);

            session.StartTransaction(new TransactionOptions(
                readConcern: ReadConcern.Snapshot,
                writeConcern: WriteConcern.WMajority,
                readPreference: ReadPreference.Primary));

            using var scope = context.EnterSession(session);
            var result = await operation(cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch (MongoWriteException exception) when (
            exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            await AbortIfRequired(session);
            throw new OptimisticConcurrencyException(
                "An active entity with the same tenant-scoped code already exists.");
        }
        catch (MongoException exception) when (
            exception.HasErrorLabel("TransientTransactionError")
            || exception.HasErrorLabel("UnknownTransactionCommitResult")
            || exception is MongoClientException
            || exception.Message.Contains("Transaction numbers are only allowed", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("replica set", StringComparison.OrdinalIgnoreCase))
        {
            await AbortIfRequired(session);
            throw new TransactionUnavailableException(
                "Mongo replica-set transaction support is unavailable.",
                exception);
        }
        catch (NotSupportedException exception) when (
            exception.Message.Contains("do not support transactions", StringComparison.OrdinalIgnoreCase))
        {
            await AbortIfRequired(session);
            throw new TransactionUnavailableException(
                "Mongo replica-set transaction support is unavailable.",
                exception);
        }
        catch
        {
            await AbortIfRequired(session);
            throw;
        }
        finally
        {
            session?.Dispose();
        }
    }

    private static async Task AbortIfRequired(IClientSessionHandle? session)
    {
        if (session?.IsInTransaction == true)
            await session.AbortTransactionAsync(CancellationToken.None);
    }
}
