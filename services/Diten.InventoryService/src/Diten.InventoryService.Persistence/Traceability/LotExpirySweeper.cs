using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Domain.Traceability.Rules;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Traceability;

/// <summary>
/// B-3 / CT-D8 — the ONLY writer of <c>Expired</c>. Finds every lot (any tenant, any status but Expired) whose expiry date
/// is before the UTC day of <c>now</c> and expires each in its own transaction: re-read inside the transaction (a
/// concurrent edit / status change wins and the lot is re-checked on the next tick), version + 1, and one
/// <c>SystemExpired</c> history row with the system actor (Guid.Empty), source <c>system-expiry</c>. Postings check the
/// expiry date themselves (slice P2), so a lagging sweeper never lets an expired lot through.
/// </summary>
public sealed class LotExpirySweeper : ILotExpirySweeper
{
    public const string SystemReason = "Expiry date passed (system).";
    public const string SystemSource = "system-expiry";

    private readonly IMongoClient _client;
    private readonly IMongoCollection<Lot> _lots;
    private readonly IMongoCollection<LotChangeEntry> _history;
    private readonly ILogger<LotExpirySweeper> _logger;

    public LotExpirySweeper(IMongoClient client, IMongoDatabase database, ILogger<LotExpirySweeper> logger)
    {
        _client = client;
        _lots = database.GetCollection<Lot>(TraceCollections.Lots);
        _history = database.GetCollection<LotChangeEntry>(TraceCollections.LotHistory);
        _logger = logger;
    }

    public async Task<int> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var startOfToday = new DateTimeOffset(today.Year, today.Month, today.Day, 0, 0, 0, TimeSpan.Zero);
        var f = Builders<Lot>.Filter;
        var due = await _lots.Find(f.Lt(x => x.ExpiryDate, startOfToday)
                                   & f.Ne(x => x.Status, LotStatus.Expired)
                                   & f.Eq(x => x.IsDeleted, false))
            .Project(x => x.Id)
            .ToListAsync(cancellationToken);

        var expired = 0;
        foreach (var lotId in due)
        {
            if (await ExpireAsync(lotId, today, now, cancellationToken)) expired++;
        }

        return expired;
    }

    private async Task<bool> ExpireAsync(Guid lotId, DateOnly today, DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        return await session.WithTransactionAsync(async (s, ct) =>
        {
            var lot = await _lots.Find(s, Builders<Lot>.Filter.Eq(x => x.Id, lotId)).FirstOrDefaultAsync(ct);
            if (lot is null || !LotDateRules.ShouldExpire(lot.Status, lot.ExpiryDate is { } e ? DateOnly.FromDateTime(e.UtcDateTime) : null, today))
            {
                return false;
            }

            var expectedVersion = lot.Version;
            var entry = new LotChangeEntry
            {
                Id = Guid.NewGuid(),
                TenantId = lot.TenantId,
                LegalEntityId = lot.LegalEntityId,
                CreatedAt = now,
                LotId = lot.Id,
                Action = LotChangeAction.SystemExpired,
                Field = "status",
                OldValue = lot.Status.ToString(),
                NewValue = nameof(LotStatus.Expired),
                Reason = SystemReason,
                ActorUserId = Guid.Empty,
                Source = SystemSource,
                CorrelationId = Guid.NewGuid(),
                OccurredAt = now
            };

            lot.Status = LotStatus.Expired;
            lot.UpdatedAt = now;
            lot.Version = expectedVersion + 1;
            var result = await _lots.ReplaceOneAsync(s,
                Builders<Lot>.Filter.Eq(x => x.Id, lot.Id) & Builders<Lot>.Filter.Eq(x => x.Version, expectedVersion), lot, cancellationToken: ct);
            if (result.MatchedCount != 1) return false;

            await _history.InsertOneAsync(s, entry, cancellationToken: ct);
            _logger.LogInformation("MOD-0174 lot {LotId} expired by the system (expiry {Expiry}).", lot.Id, lot.ExpiryDate);
            return true;
        }, new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority), cancellationToken);
    }
}
