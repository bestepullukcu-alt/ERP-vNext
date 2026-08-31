using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence;

internal sealed class PlatformMongoTransactionSession : IPlatformTransactionSession
{
    public PlatformMongoTransactionSession(IMongoClient owner, IClientSessionHandle handle)
    {
        Owner = owner;
        Handle = handle;
        TransactionId = Guid.NewGuid();
    }

    public Guid TransactionId { get; }
    internal IMongoClient Owner { get; }
    internal IClientSessionHandle Handle { get; }

    internal static IClientSessionHandle Require(
        IPlatformTransactionSession session,
        IPlatformDbContext dbContext)
    {
        if (session is not PlatformMongoTransactionSession mongoSession
            || !ReferenceEquals(mongoSession.Owner, dbContext.Client)
            || !mongoSession.Handle.IsInTransaction)
        {
            throw new InvalidOperationException(
                "The mutation requires the active Platform transaction session owned by the configured Mongo client.");
        }

        return mongoSession.Handle;
    }
}
