using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Api.Services.ProductAbbreviationRegister;

/// <summary>
/// ABB FIX3 item 6 — every product-abbreviation write runs in a MongoDB transaction, which a standalone mongod cannot
/// run. At startup the server is asked once: when it cannot run transactions, ONE warning line says so (each ABB write
/// then answers 503 PRODUCT_ABBREVIATION_TRANSACTIONS_UNAVAILABLE). The check never stops the service from starting —
/// every other MDM feature works on a standalone server.
/// </summary>
public sealed class ProductAbbreviationTransactionSupportCheck(
    IServiceScopeFactory scopeFactory,
    ILogger<ProductAbbreviationTransactionSupportCheck> logger) : IHostedService
{
    public const string UnavailableEvent = "mdm.product_abbreviation.transactions_unavailable";
    public const string UncheckedEvent = "mdm.product_abbreviation.transaction_support_unchecked";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
            var hello = await database.RunCommandAsync<BsonDocument>(
                new BsonDocument("hello", 1),
                cancellationToken: cancellationToken);
            if (!SupportsTransactions(hello))
            {
                logger.LogWarning(
                    "{Event}: the MongoDB server is not a replica set member or mongos, so it cannot run transactions; "
                    + "product abbreviation requests and decisions will answer 503 until it can.",
                    UnavailableEvent);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "{Event}: the MongoDB server could not be asked whether it runs transactions ({ExceptionType}).",
                UncheckedEvent,
                exception.GetType().Name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>A replica set member (it names its set) or a mongos router (msg "isdbgrid") runs transactions.</summary>
    public static bool SupportsTransactions(BsonDocument hello)
        => hello.TryGetValue("setName", out var setName) && setName.IsString && !string.IsNullOrWhiteSpace(setName.AsString)
           || hello.TryGetValue("msg", out var message) && message.IsString && message.AsString == "isdbgrid";
}
