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
        catch (MongoWriteException exception) when (exception.WriteError?.Code == 112)
        {
            await AbortIfRequired(session);
            throw new OptimisticConcurrencyException(
                "A concurrent write changed the same tenant-scoped resource.");
        }
        catch (MongoCommandException exception) when (exception.Code == 112)
        {
            await AbortIfRequired(session);
            throw new OptimisticConcurrencyException(
                "A concurrent write changed the same tenant-scoped resource.");
        }
        catch (MongoCommandException exception) when (
            exception.Code == 251
            && exception.Message.Contains(
                "Please retry your operation or multi-document transaction",
                StringComparison.OrdinalIgnoreCase))
        {
            await AbortIfRequired(session);
            throw new OptimisticConcurrencyException(
                "A concurrent write changed the same tenant-scoped resource.");
        }
        catch (MongoException exception) when (
            exception.Message.Contains("WriteConflict", StringComparison.OrdinalIgnoreCase))
        {
            await AbortIfRequired(session);
            throw new OptimisticConcurrencyException(
                "A concurrent write changed the same tenant-scoped resource.");
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
        catch (MongoException exception)
        {
            await AbortIfRequired(session);
            throw new TransactionUnavailableException(
                "Mongo transactional persistence is unavailable.", exception);
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
