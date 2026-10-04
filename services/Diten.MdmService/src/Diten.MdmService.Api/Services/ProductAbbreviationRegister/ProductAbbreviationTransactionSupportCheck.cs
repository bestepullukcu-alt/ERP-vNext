using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Api.Services.ProductAbbreviationRegister;

/// <summary>
/// ABB FIX3 item 6 — every product-abbreviation write runs in a MongoDB transaction, which a standalone mongod cannot
/// run. At startup the server is asked once: when it cannot run transactions, ONE warning line says so (each ABB write
/// then answers 503 PRODUCT_ABBREVIATION_TRANSACTIONS_UNAVAILABLE). The check never stops the service from starting —
/// every other MDM feature works on a standalone server.
/// FIX4 item 1 — and never holds the start up: the question gets <see cref="DefaultTimeout"/>, not the driver's server
/// selection time (30 s by default). No answer in time is one "unchecked" line saying why.
/// </summary>
public sealed class ProductAbbreviationTransactionSupportCheck : IHostedService
{
    public const string UnavailableEvent = "mdm.product_abbreviation.transactions_unavailable";
    public const string UncheckedEvent = "mdm.product_abbreviation.transaction_support_unchecked";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProductAbbreviationTransactionSupportCheck> _logger;
    private readonly TimeSpan _timeout;

    public ProductAbbreviationTransactionSupportCheck(
        IServiceScopeFactory scopeFactory,
        ILogger<ProductAbbreviationTransactionSupportCheck> logger)
        : this(scopeFactory, logger, DefaultTimeout)
    {
    }

    public ProductAbbreviationTransactionSupportCheck(
        IServiceScopeFactory scopeFactory,
        ILogger<ProductAbbreviationTransactionSupportCheck> logger,
        TimeSpan timeout)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeout = timeout;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(_timeout);
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
            var hello = await database.RunCommandAsync<BsonDocument>(
                new BsonDocument("hello", 1),
                cancellationToken: bounded.Token);
            if (!SupportsTransactions(hello))
            {
                _logger.LogWarning(
                    "{Event}: the MongoDB server is not a replica set member or mongos, so it cannot run transactions; "
                    + "product abbreviation requests and decisions will answer 503 until it can.",
                    UnavailableEvent);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "{Event}: the MongoDB server did not answer whether it runs transactions within {TimeoutSeconds} s.",
                UncheckedEvent,
                _timeout.TotalSeconds);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
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
