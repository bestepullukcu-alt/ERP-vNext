using Diten.PvgService.Infrastructure.RegPvBase;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.PvgService.Persistence;

// The one start-up step of the build/test Mongo store (pack "Build/Test Persistence (Mongo Store)", Schema bootstrap).
// It runs once, inside IHostedService.StartAsync, which the web host completes before it accepts a connection: it
// creates the collection and the catalog indexes outside any transaction (idempotent), then runs a transaction probe
// (start, read, abort). If either fails the host does not start. It is a start-up step, not a background job: no loop,
// no timer. It migrates no data. Every refusal message is a safe code; it never carries a connection value.
public sealed class PvgMongoSchemaBootstrap : IHostedService
{
    public const string StoreUnreachable = "PVG_PERSISTENCE_STORE_UNREACHABLE";
    public const string TransactionsUnavailable = "PVG_PERSISTENCE_TRANSACTIONS_UNAVAILABLE";

    // Not a draft id of any tenant: ids are GUIDs in "D" format. The probe reads no tenant data.
    private const string ProbeId = "pvg-transaction-probe";
    private const int NamespaceExistsCode = 48;

    private readonly PvgMongoContext _context;
    private readonly ILogger _logger;

    internal PvgMongoSchemaBootstrap(PvgMongoContext context, ILogger? logger = null)
    {
        _context = context;
        _logger = logger ?? NullLogger.Instance;
    }

    public async Task PrepareSchemaAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CreateCollectionIfMissingAsync(cancellationToken);
            await CreateCatalogIndexesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Only the exception type is logged: the message of a driver exception can name the server address.
            _logger.LogError("PVG intake store schema setup failed ({ExceptionType}).", exception.GetType().Name);
            throw new InvalidOperationException(StoreUnreachable);
        }

        try
        {
            await ProbeTransactionsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError("PVG intake store transaction probe failed ({ExceptionType}).", exception.GetType().Name);
            throw new InvalidOperationException(TransactionsUnavailable);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => PrepareSchemaAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task CreateCollectionIfMissingAsync(CancellationToken cancellationToken)
    {
        using var existing = await _context.Database.ListCollectionNamesAsync(cancellationToken: cancellationToken);
        var names = await existing.ToListAsync(cancellationToken);
        if (names.Contains(PvgIntakeDraftIndexCatalog.CollectionName, StringComparer.Ordinal))
        {
            return;
        }

        try
        {
            await _context.Database.CreateCollectionAsync(PvgIntakeDraftIndexCatalog.CollectionName, cancellationToken: cancellationToken);
        }
        catch (MongoCommandException exception) when (exception.Code == NamespaceExistsCode)
        {
            // Created by a concurrent start: fine.
        }
    }

    private async Task CreateCatalogIndexesAsync(CancellationToken cancellationToken)
    {
        var models = PvgIntakeDraftIndexCatalog.RequiredIndexes
            .Select(definition =>
            {
                var keys = new BsonDocument();
                foreach (var field in definition.Fields)
                {
                    keys.Add(field, definition.DescendingFields.Contains(field, StringComparer.Ordinal) ? -1 : 1);
                }

                return new CreateIndexModel<PvgIntakeDraftDocument>(
                    new BsonDocumentIndexKeysDefinition<PvgIntakeDraftDocument>(keys),
                    new CreateIndexOptions { Name = definition.Name, Unique = definition.IsUnique });
            })
            .ToArray();

        await _context.Drafts.Indexes.CreateManyAsync(models, cancellationToken);
    }

    private async Task ProbeTransactionsAsync(CancellationToken cancellationToken)
    {
        using var session = await _context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction(PvgMongoContext.WriteTransactionOptions);

        try
        {
            var filter = Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.IsDeleted, false)
                         & Builders<PvgIntakeDraftDocument>.Filter.Eq(draft => draft.Id, ProbeId);
            await _context.Drafts.Find(session, filter).Limit(1).ToListAsync(cancellationToken);
        }
        finally
        {
            try
            {
                if (session.IsInTransaction)
                {
                    await session.AbortTransactionAsync(CancellationToken.None);
                }
            }
            catch (Exception)
            {
                // The probe result is the read above; an abort that cannot be sent changes nothing.
            }
        }
    }
}
