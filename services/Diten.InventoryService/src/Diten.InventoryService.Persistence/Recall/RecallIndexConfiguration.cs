using Diten.InventoryService.Domain.Recall.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Recall;

/// <summary>
/// MOD-0177 indexes (pack §8.9, R1 part). The unique ones are load-bearing:
/// <list type="bullet">
/// <item><c>ux_recall_settings_company</c> — one settings row per Tenant + LegalEntity (two concurrent first saves: one wins);</item>
/// <item><c>ux_recall_case_number</c> — the case number per tenant;</item>
/// <item><c>ux_recall_case_active_root</c> — one NON-TERMINAL case per company + root lot (cleared at Closed / Cancelled);</item>
/// <item><c>ux_recall_case_create_key</c> — one case per (company, opener, idempotency key);</item>
/// <item><c>ux_recall_event_key</c> — one event per (case, actor, idempotency key);</item>
/// <item><c>ux_recall_scope_node</c> — one item per (case, revision, node key);</item>
/// <item><c>ux_recall_lot_block</c> — one block per (lot, case); <c>ux_recall_*_key</c> — one record per (company, user, key).</item>
/// </list>
/// </summary>
public static class RecallIndexConfiguration
{
    public const string SettingsCompanyIndex = "ux_recall_settings_company";
    public const string CaseNumberIndex = "ux_recall_case_number";
    public const string CaseActiveRootIndex = "ux_recall_case_active_root";
    public const string CaseCreateKeyIndex = "ux_recall_case_create_key";
    public const string EventKeyIndex = "ux_recall_event_key";
    public const string ScopeNodeIndex = "ux_recall_scope_node";
    public const string LotBlockIndex = "ux_recall_lot_block";
    public const string NotificationKeyIndex = "ux_recall_notification_key";
    public const string ConsumptionKeyIndex = "ux_recall_consumption_key";
    public const string SegregationKeyIndex = "ux_recall_segregation_key";

    public static readonly IReadOnlyList<string> UniqueIndexes =
    [
        SettingsCompanyIndex, CaseNumberIndex, CaseActiveRootIndex, CaseCreateKeyIndex, EventKeyIndex, ScopeNodeIndex, LotBlockIndex,
        NotificationKeyIndex, ConsumptionKeyIndex, SegregationKeyIndex
    ];

    private static BsonDocument IsString(string field) => new(field, new BsonDocument("$type", "string"));

    public static async Task ApplyAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        await database.GetCollection<RecallSettings>(RecallCollections.Settings).Indexes.CreateOneAsync(
            new CreateIndexModel<RecallSettings>(
                Builders<RecallSettings>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId),
                new CreateIndexOptions { Name = SettingsCompanyIndex, Unique = true }),
            cancellationToken: ct);

        await database.GetCollection<RecallSettingsHistoryEntry>(RecallCollections.SettingsHistory).Indexes.CreateOneAsync(
            new CreateIndexModel<RecallSettingsHistoryEntry>(
                Builders<RecallSettingsHistoryEntry>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Descending(x => x.ChangedAtUtc),
                new CreateIndexOptions { Name = "ix_recall_settings_history" }),
            cancellationToken: ct);

        var cases = Builders<RecallCase>.IndexKeys;
        await database.GetCollection<RecallCase>(RecallCollections.Cases).Indexes.CreateManyAsync(
        [
            new(cases.Ascending(x => x.TenantId).Ascending(x => x.Number), new CreateIndexOptions { Name = CaseNumberIndex, Unique = true }),
            new(cases.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ActiveRootKey), new CreateIndexOptions<RecallCase>
                { Name = CaseActiveRootIndex, Unique = true, PartialFilterExpression = IsString(nameof(RecallCase.ActiveRootKey)) }),
            new(cases.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.OwnerUserId).Ascending(x => x.CreateIdempotencyKey),
                new CreateIndexOptions<RecallCase>
                    { Name = CaseCreateKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(RecallCase.CreateIdempotencyKey)) }),
            new(cases.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Status).Ascending(x => x.OpenedAtUtc),
                new CreateIndexOptions { Name = "ix_recall_case_list" })
        ], ct);

        var events = Builders<RecallCaseEvent>.IndexKeys;
        await database.GetCollection<RecallCaseEvent>(RecallCollections.CaseEvents).Indexes.CreateManyAsync(
        [
            new(events.Ascending(x => x.TenantId).Ascending(x => x.CaseId).Ascending(x => x.ActorUserId).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<RecallCaseEvent>
                    { Name = EventKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(RecallCaseEvent.IdempotencyKey)) }),
            new(events.Ascending(x => x.TenantId).Ascending(x => x.CaseId).Ascending(x => x.Sequence), new CreateIndexOptions { Name = "ix_recall_events" })
        ], ct);

        await database.GetCollection<RecallScopeItem>(RecallCollections.ScopeItems).Indexes.CreateOneAsync(
            new CreateIndexModel<RecallScopeItem>(
                Builders<RecallScopeItem>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CaseId).Ascending(x => x.Revision).Ascending(x => x.NodeKey),
                new CreateIndexOptions { Name = ScopeNodeIndex, Unique = true }),
            cancellationToken: ct);

        var blocks = Builders<RecallLotBlock>.IndexKeys;
        await database.GetCollection<RecallLotBlock>(RecallCollections.LotBlocks).Indexes.CreateManyAsync(
        [
            new(blocks.Ascending(x => x.TenantId).Ascending(x => x.LotId).Ascending(x => x.CaseId), new CreateIndexOptions { Name = LotBlockIndex, Unique = true }),
            // the posting gate (R3) and getLotRecallStatus read the ACTIVE blocks of a lot, tenant-wide (CT-R7)
            new(blocks.Ascending(x => x.TenantId).Ascending(x => x.LotId), new CreateIndexOptions<RecallLotBlock>
                { Name = "ix_recall_lot_block_active", PartialFilterExpression = new BsonDocument(nameof(RecallLotBlock.Active), true) })
        ], ct);

        await database.GetCollection<RecallCustomerNotification>(RecallCollections.Notifications).Indexes.CreateManyAsync(
        [
            new(Builders<RecallCustomerNotification>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.RecordedBy).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions { Name = NotificationKeyIndex, Unique = true }),
            new(Builders<RecallCustomerNotification>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CaseId), new CreateIndexOptions { Name = "ix_recall_notification_case" })
        ], ct);

        await database.GetCollection<RecallConsumptionDeclaration>(RecallCollections.ConsumptionDeclarations).Indexes.CreateManyAsync(
        [
            new(Builders<RecallConsumptionDeclaration>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.RecordedBy).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions { Name = ConsumptionKeyIndex, Unique = true }),
            new(Builders<RecallConsumptionDeclaration>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CaseId), new CreateIndexOptions { Name = "ix_recall_consumption_case" })
        ], ct);

        await database.GetCollection<RecallSegregationConfirmation>(RecallCollections.Segregations).Indexes.CreateManyAsync(
        [
            new(Builders<RecallSegregationConfirmation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ConfirmedBy).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions { Name = SegregationKeyIndex, Unique = true }),
            new(Builders<RecallSegregationConfirmation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CaseId), new CreateIndexOptions { Name = "ix_recall_segregation_case" })
        ], ct);
    }
}

/// <summary>
/// Startup gate of the MOD-0177 indexes (the MOD-0176 rule: the unique indexes carry idempotency and "one active case per root lot",
/// so the host refuses to start without them — fail-closed). Its own hosted service, so the shared index files stay untouched.
/// </summary>
public sealed class RecallIndexInitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecallIndexInitializerHostedService> _logger;

    public RecallIndexInitializerHostedService(IServiceScopeFactory scopeFactory, ILogger<RecallIndexInitializerHostedService> logger)
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
            await RecallIndexConfiguration.ApplyAsync(database, cancellationToken);
            _logger.LogInformation("MOD-0177 Mongo indexes ensured on database {Database}.", database.DatabaseNamespace.DatabaseName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogCritical(ex, "MOD-0177 index gate failed; refusing to start.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
