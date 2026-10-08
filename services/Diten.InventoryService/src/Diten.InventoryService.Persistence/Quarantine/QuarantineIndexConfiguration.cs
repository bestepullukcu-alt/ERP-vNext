using Diten.InventoryService.Domain.Quarantine.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Quarantine;

/// <summary>
/// MOD-0175 indexes (pack §4), applied by the MOD-0173 startup index gate. The unique ones are load-bearing:
/// <list type="bullet">
/// <item><c>ux_quarantine_hold_source_movement</c> / <c>ux_quarantine_blocked_source_movement</c> — one record per intake
/// ledger transaction (observer + sweep never duplicate, §16 Q1);</item>
/// <item><c>ux_quarantine_hold_create_key</c> / <c>ux_quarantine_blocked_create_key</c> — one record per bucket of a
/// "Karantinaya Al" / "Stoğu bloke et" request;</item>
/// <item><c>ux_quarantine_decision_key</c> — one decision per (decider, idempotency key);</item>
/// <item><c>ux_quarantine_decision_event_once</c> — the once-only events (Approved, Applied, …) happen once per decision,
/// so two concurrent appliers can never both record Applied;</item>
/// <item>the record numbers and the block-reason code per tenant.</item>
/// </list>
/// </summary>
public static class QuarantineIndexConfiguration
{
    public const string HoldNumberIndex = "ux_quarantine_hold_number";
    public const string HoldSourceMovementIndex = "ux_quarantine_hold_source_movement";
    public const string HoldCreateKeyIndex = "ux_quarantine_hold_create_key";
    public const string DecisionNumberIndex = "ux_quarantine_decision_number";
    public const string DecisionKeyIndex = "ux_quarantine_decision_key";
    public const string DecisionEventOnceIndex = "ux_quarantine_decision_event_once";
    public const string BlockedNumberIndex = "ux_quarantine_blocked_number";
    public const string BlockedSourceMovementIndex = "ux_quarantine_blocked_source_movement";
    public const string BlockedCreateKeyIndex = "ux_quarantine_blocked_create_key";
    public const string SettingsIndex = "ux_quarantine_settings_company";
    public const string BlockReasonIndex = "ux_quarantine_block_reason_code";

    public static readonly IReadOnlyList<string> UniqueIndexes =
    [
        HoldNumberIndex, HoldSourceMovementIndex, HoldCreateKeyIndex, DecisionNumberIndex, DecisionKeyIndex, DecisionEventOnceIndex,
        BlockedNumberIndex, BlockedSourceMovementIndex, BlockedCreateKeyIndex, SettingsIndex, BlockReasonIndex
    ];

    private static BsonDocument IsString(string field) => new(field, new BsonDocument("$type", "string"));

    public static async Task ApplyAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var holds = Builders<QuarantineHold>.IndexKeys;
        await database.GetCollection<QuarantineHold>(QuarantineCollections.Holds).Indexes.CreateManyAsync(
        [
            new(holds.Ascending(x => x.TenantId).Ascending(x => x.HoldNumber), new CreateIndexOptions { Name = HoldNumberIndex, Unique = true }),
            new(holds.Ascending(x => x.TenantId).Ascending(x => x.SourceMovementId), new CreateIndexOptions<QuarantineHold>
                { Name = HoldSourceMovementIndex, Unique = true, PartialFilterExpression = IsString(nameof(QuarantineHold.SourceMovementId)) }),
            new(holds.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CreatedBy).Ascending(x => x.CreateIdempotencyKey),
                new CreateIndexOptions<QuarantineHold>
                    { Name = HoldCreateKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(QuarantineHold.CreateIdempotencyKey)) }),
            new(holds.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Status).Ascending(x => x.EnteredAtUtc),
                new CreateIndexOptions { Name = "ix_quarantine_hold_queue" }),
            new(holds.Ascending(x => x.TenantId).Ascending(x => x.LotId), new CreateIndexOptions { Name = "ix_quarantine_hold_lot" })
        ], ct);

        var decisions = Builders<QuarantineDecision>.IndexKeys;
        await database.GetCollection<QuarantineDecision>(QuarantineCollections.Decisions).Indexes.CreateManyAsync(
        [
            new(decisions.Ascending(x => x.TenantId).Ascending(x => x.DecisionNumber), new CreateIndexOptions { Name = DecisionNumberIndex, Unique = true }),
            new(decisions.Ascending(x => x.TenantId).Ascending(x => x.DeciderUserId).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions { Name = DecisionKeyIndex, Unique = true }),
            new(decisions.Ascending(x => x.TenantId).Ascending(x => x.HoldId), new CreateIndexOptions { Name = "ix_quarantine_decision_hold" }),
            new(decisions.Ascending(x => x.TenantId).Ascending(x => x.BlockedStockId), new CreateIndexOptions { Name = "ix_quarantine_decision_blocked" })
        ], ct);

        var events = Builders<QuarantineDecisionEvent>.IndexKeys;
        await database.GetCollection<QuarantineDecisionEvent>(QuarantineCollections.DecisionEvents).Indexes.CreateManyAsync(
        [
            new(events.Ascending(x => x.TenantId).Ascending(x => x.DecisionId).Ascending(x => x.OnceKey), new CreateIndexOptions<QuarantineDecisionEvent>
                { Name = DecisionEventOnceIndex, Unique = true, PartialFilterExpression = IsString(nameof(QuarantineDecisionEvent.OnceKey)) }),
            new(events.Ascending(x => x.TenantId).Ascending(x => x.DecisionId).Ascending(x => x.Sequence), new CreateIndexOptions { Name = "ix_quarantine_decision_events" })
        ], ct);

        var blocked = Builders<BlockedStock>.IndexKeys;
        await database.GetCollection<BlockedStock>(QuarantineCollections.BlockedStock).Indexes.CreateManyAsync(
        [
            new(blocked.Ascending(x => x.TenantId).Ascending(x => x.BlockedNumber), new CreateIndexOptions { Name = BlockedNumberIndex, Unique = true }),
            new(blocked.Ascending(x => x.TenantId).Ascending(x => x.SourceMovementId), new CreateIndexOptions<BlockedStock>
                { Name = BlockedSourceMovementIndex, Unique = true, PartialFilterExpression = IsString(nameof(BlockedStock.SourceMovementId)) }),
            new(blocked.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CreatedBy).Ascending(x => x.CreateIdempotencyKey),
                new CreateIndexOptions<BlockedStock>
                    { Name = BlockedCreateKeyIndex, Unique = true, PartialFilterExpression = IsString(nameof(BlockedStock.CreateIdempotencyKey)) }),
            new(blocked.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Status).Ascending(x => x.BlockedAtUtc),
                new CreateIndexOptions { Name = "ix_quarantine_blocked_list" })
        ], ct);

        var history = Builders<QuarantineHistoryEntry>.IndexKeys;
        await database.GetCollection<QuarantineHistoryEntry>(QuarantineCollections.History).Indexes.CreateOneAsync(
            new CreateIndexModel<QuarantineHistoryEntry>(
                history.Ascending(x => x.TenantId).Ascending(x => x.SubjectId).Ascending(x => x.AtUtc), new CreateIndexOptions { Name = "ix_quarantine_history_subject" }),
            cancellationToken: ct);

        await database.GetCollection<QuarantineSettings>(QuarantineCollections.Settings).Indexes.CreateOneAsync(
            new CreateIndexModel<QuarantineSettings>(
                Builders<QuarantineSettings>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId),
                new CreateIndexOptions { Name = SettingsIndex, Unique = true }),
            cancellationToken: ct);

        await database.GetCollection<BlockReason>(QuarantineCollections.BlockReasons).Indexes.CreateOneAsync(
            new CreateIndexModel<BlockReason>(
                Builders<BlockReason>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.Code),
                new CreateIndexOptions { Name = BlockReasonIndex, Unique = true }),
            cancellationToken: ct);
    }
}
