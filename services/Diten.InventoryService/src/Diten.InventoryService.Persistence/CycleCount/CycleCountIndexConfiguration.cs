using Diten.InventoryService.Domain.CycleCounting.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.CycleCounting;

/// <summary>
/// MOD-0181 indexes of DL-1 (pack §4.6 — tenant + company first). The unique ones are load-bearing: the number (tenant-wide), the create key
/// (per creating user), the event seq and the key event (per count, actor, operation, key) and — F-6 — the partial unique OPEN bin claim (one
/// open count per bin). The line key keeps one line per (bin, SKU, level, lot, status). The adjustment-key and adjustment-status indexes of
/// §4.6 come with DL-3 (their field is DL-3's).
/// </summary>
public static class CycleCountIndexConfiguration
{
    public const string NumberIndex = "ux_wms_count_number";
    public const string CreateKeyIndex = "ux_wms_count_create_key";
    public const string LineKeyIndex = "ux_wms_count_line_key";
    public const string EventSeqIndex = "ux_wms_count_event_seq";
    public const string EventKeyIndex = "ux_wms_count_event_key";
    public const string BinClaimOpenIndex = "ux_wms_count_bin_claim_open";

    public static readonly IReadOnlyList<string> UniqueIndexes = [NumberIndex, CreateKeyIndex, LineKeyIndex, EventSeqIndex, EventKeyIndex, BinClaimOpenIndex];

    private static BsonDocument IsString(string field) => new(field, new BsonDocument("$type", "string"));

    public static async Task ApplyAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var counts = Builders<CycleCount>.IndexKeys;
        await database.GetCollection<CycleCount>(CycleCountCollections.Counts).Indexes.CreateManyAsync(
        [
            new(counts.Ascending(x => x.TenantId).Ascending(x => x.Number), new CreateIndexOptions { Name = NumberIndex, Unique = true }),
            new(counts.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CreatedBy).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<CycleCount> { Name = CreateKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(CycleCount.IdempotencyKey)) }),
            new(counts.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Status).Ascending(x => x.PlannedDate),
                new CreateIndexOptions { Name = "ix_wms_count_status_planned" }),
            new(counts.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.WarehouseId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_wms_count_warehouse_status" })
        ], ct);

        var lines = Builders<CycleCountLine>.IndexKeys;
        await database.GetCollection<CycleCountLine>(CycleCountCollections.Lines).Indexes.CreateManyAsync(
        [
            new(lines.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CountId), new CreateIndexOptions { Name = "ix_wms_count_line_count" }),
            new(lines.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CountId).Ascending(x => x.LocationId).Ascending(x => x.SkuId)
                    .Ascending(x => x.SkuLevel).Ascending(x => x.LotId).Ascending(x => x.StockStatus),
                new CreateIndexOptions { Name = LineKeyIndex, Unique = true })
        ], ct);

        var events = Builders<CycleCountEvent>.IndexKeys;
        await database.GetCollection<CycleCountEvent>(CycleCountCollections.Events).Indexes.CreateManyAsync(
        [
            new(events.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CountId).Ascending(x => x.Seq),
                new CreateIndexOptions { Name = EventSeqIndex, Unique = true }),
            new(events.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CountId).Ascending(x => x.ActorUserId)
                    .Ascending(x => x.RequestOp).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<CycleCountEvent> { Name = EventKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(CycleCountEvent.IdempotencyKey)) }),
            new(events.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ActorUserId).Ascending(x => x.RequestOp)
                    .Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions { Name = "ix_wms_count_event_bulk_key" })
        ], ct);

        var claims = Builders<CycleCountBinClaim>.IndexKeys;
        await database.GetCollection<CycleCountBinClaim>(CycleCountCollections.BinClaims).Indexes.CreateManyAsync(
        [
            new(claims.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.BinId),
                new CreateIndexOptions<CycleCountBinClaim>
                    { Name = BinClaimOpenIndex, Unique = true, PartialFilterExpression = new BsonDocument(nameof(CycleCountBinClaim.Open), true) }),
            new(claims.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CountId), new CreateIndexOptions { Name = "ix_wms_count_bin_claim_count" })
        ], ct);
    }
}

/// <summary>
/// Startup gate of the MOD-0181 indexes (fail-closed, the MOD-0178 rule): the unique indexes carry the number, idempotency and "one open count
/// per bin", so the host refuses to start without them. Its own hosted service; the shared index files stay untouched.
/// </summary>
public sealed class CycleCountIndexInitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CycleCountIndexInitializerHostedService> _logger;

    public CycleCountIndexInitializerHostedService(IServiceScopeFactory scopeFactory, ILogger<CycleCountIndexInitializerHostedService> logger)
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
            await CycleCountIndexConfiguration.ApplyAsync(database, cancellationToken);
            _logger.LogInformation("MOD-0181 Mongo indexes ensured on database {Database}.", database.DatabaseNamespace.DatabaseName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogCritical(ex, "MOD-0181 index gate failed; refusing to start.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
