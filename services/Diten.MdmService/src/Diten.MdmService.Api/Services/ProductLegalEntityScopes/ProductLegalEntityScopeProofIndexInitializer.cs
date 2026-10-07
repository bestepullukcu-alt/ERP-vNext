using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Api.Services.ProductLegalEntityScopes;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX3 (8) — builds the indexes the write proof's queries need (abbreviation ledger / history /
/// register by product) when the service starts, before it serves writes. Before, the first leased write built them, and
/// on a large collection that could outlast its lease. A failure is logged and does not stop the service: the proof stays
/// correct, only slower. Not run by the one-shot operational commands (they return before the host starts).
/// </summary>
public sealed class ProductLegalEntityScopeProofIndexInitializer(
    IServiceScopeFactory scopes,
    ILogger<ProductLegalEntityScopeProofIndexInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            // The database is registered per scope; one scope for the build.
            await using var scope = scopes.CreateAsyncScope();
            await ProductLegalEntityScopeOperationalReadinessRepository.EnsureObjectIndexesAsync(
                scope.ServiceProvider.GetRequiredService<IMongoDatabase>(), cancellationToken);
        }
        catch (MongoException exception)
        {
            logger.LogWarning(exception, "Product legal-entity scope proof indexes could not be built at startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
