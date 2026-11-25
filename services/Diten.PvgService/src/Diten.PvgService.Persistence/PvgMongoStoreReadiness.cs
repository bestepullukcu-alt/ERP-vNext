using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.PvgService.Persistence;

// /health/ready asks this when the Mongo store is the selected store (pack "Readiness"). The signature carries no
// Mongo type, so the Api can use it without referencing the driver.
public interface IPvgIntakeStoreReadiness
{
    ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken);
}

// A ping only: it reads no collection and so no tenant data. An unreachable server is "not ready", never an exception.
internal sealed class PvgMongoStoreReadiness : IPvgIntakeStoreReadiness
{
    private static readonly TimeSpan PingBudget = TimeSpan.FromSeconds(8);

    private readonly PvgMongoContext _context;

    public PvgMongoStoreReadiness(PvgMongoContext context)
    {
        _context = context;
    }

    public async ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(PingBudget);

        try
        {
            await _context.Database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1),
                ReadPreference.Primary,
                budget.Token);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
