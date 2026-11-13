using Diten.InventoryService.Domain.WavePlanning.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.WavePlanning;

/// <summary>
/// MOD-0180 indexes of DL-1 (pack §4.6 — tenant + company first). The unique ones are load-bearing: the number (tenant-wide), the create key
/// (per creating user), the event seq and the key event (per stream, actor, operation, key), one setting per warehouse and — D-8 — the partial
/// unique ACTIVE membership (an outbound in at most one active wave). The exception indexes of §4.6 come with DL-3. MOD-0178's collections
/// get no index from here.
/// </summary>
public static class WaveIndexConfiguration
{
    public const string NumberIndex = "ux_wms_wave_number";
    public const string CreateKeyIndex = "ux_wms_wave_create_key";
    public const string ActiveOrderIndex = "ux_wms_wave_order_active_outbound";
    public const string EventSeqIndex = "ux_wms_wave_event_seq";
    public const string EventKeyIndex = "ux_wms_wave_event_key";
    public const string SettingsWarehouseIndex = "ux_wms_wave_settings_warehouse";

    public static readonly IReadOnlyList<string> UniqueIndexes =
        [NumberIndex, CreateKeyIndex, ActiveOrderIndex, EventSeqIndex, EventKeyIndex, SettingsWarehouseIndex];

    private static BsonDocument IsString(string field) => new(field, new BsonDocument("$type", "string"));

    public static async Task ApplyAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var waves = Builders<Wave>.IndexKeys;
        await database.GetCollection<Wave>(WaveCollections.Waves).Indexes.CreateManyAsync(
        [
            new(waves.Ascending(x => x.TenantId).Ascending(x => x.Number), new CreateIndexOptions { Name = NumberIndex, Unique = true }),
            new(waves.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CreatedBy).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<Wave> { Name = CreateKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(Wave.IdempotencyKey)) }),
            new(waves.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.WarehouseId).Ascending(x => x.Status).Ascending(x => x.CutoffAtUtc),
                new CreateIndexOptions { Name = "ix_wms_wave_warehouse_status_cutoff" })
        ], ct);

        var orders = Builders<WaveOrder>.IndexKeys;
        await database.GetCollection<WaveOrder>(WaveCollections.Orders).Indexes.CreateManyAsync(
        [
            new(orders.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.OutboundId),
                new CreateIndexOptions<WaveOrder>
                    { Name = ActiveOrderIndex, Unique = true, PartialFilterExpression = new BsonDocument(nameof(WaveOrder.Active), true) }),
            new(orders.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.WaveId).Ascending(x => x.State),
                new CreateIndexOptions { Name = "ix_wms_wave_order_wave_state" }),
            new(orders.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.WarehouseId).Ascending(x => x.Active),
                new CreateIndexOptions { Name = "ix_wms_wave_order_warehouse_active" })
        ], ct);

        var events = Builders<WaveEvent>.IndexKeys;
        await database.GetCollection<WaveEvent>(WaveCollections.Events).Indexes.CreateManyAsync(
        [
            new(events.Ascending(x => x.TenantId).Ascending(x => x.WaveId).Ascending(x => x.Seq), new CreateIndexOptions { Name = EventSeqIndex, Unique = true }),
            new(events.Ascending(x => x.TenantId).Ascending(x => x.WaveId).Ascending(x => x.ActorUserId).Ascending(x => x.RequestOp).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<WaveEvent> { Name = EventKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(WaveEvent.IdempotencyKey)) })
        ], ct);

        var settings = Builders<WaveWarehouseSettings>.IndexKeys;
        await database.GetCollection<WaveWarehouseSettings>(WaveCollections.Settings).Indexes.CreateManyAsync(
        [
            new(settings.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.WarehouseId),
                new CreateIndexOptions { Name = SettingsWarehouseIndex, Unique = true })
        ], ct);
    }
}

/// <summary>
/// Startup gate of the MOD-0180 indexes (fail-closed, the MOD-0178 / MOD-0181 rule): the unique indexes carry the number, the request keys and
/// "an order in one active wave", so the host refuses to start without them. Its own hosted service; the shared index files stay untouched.
/// </summary>
public sealed class WaveIndexInitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WaveIndexInitializerHostedService> _logger;

    public WaveIndexInitializerHostedService(IServiceScopeFactory scopeFactory, ILogger<WaveIndexInitializerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        try
        {
            await WaveIndexConfiguration.ApplyAsync(database, cancellationToken);
            _logger.LogInformation("MOD-0180 Mongo indexes ensured on database {Database}.", database.DatabaseNamespace.DatabaseName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogCritical(ex, "MOD-0180 index gate failed; refusing to start.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
