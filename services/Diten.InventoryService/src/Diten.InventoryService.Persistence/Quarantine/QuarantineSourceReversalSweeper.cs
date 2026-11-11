using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine;
using Diten.InventoryService.Application.Features.Quarantine.Commands;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Quarantine;

/// <summary>The three feeds of the source-reversal sweep (pack MOD-0175-FU01 §8.2).</summary>
public enum SourceReversalFeedKind
{
    /// <summary>Open records (close).</summary>
    Open,

    /// <summary>Unmarked terminal records (the mark's safety net).</summary>
    Terminal,

    /// <summary>Unmarked voided records with a voided decision, watched for a line posted after the close (r4).</summary>
    Voided
}

/// <summary>One record of a feed page — the scope it lives in and what the sweep needs of it.</summary>
public sealed record SourceReversalFeedRecord(
    Guid Id, Guid TenantId, Guid LegalEntityId, string SourceMovementId, string? VoidedReversalTransactionId, Guid? VoidedDecisionId);

/// <summary>
/// MOD-0175-FU01 §8.2 — READ-ONLY, record based (no time window on the open / terminal feeds): one page of a feed, cross-tenant on the
/// record collections, ordered by <c>Id</c> after a cursor. Every feed skips records without a source movement and marked records
/// (a mark is written once). Only <c>Find</c>.
/// </summary>
public sealed class QuarantineSourceReversalFeed
{
    private readonly IMongoCollection<QuarantineHold> _holds;
    private readonly IMongoCollection<BlockedStock> _blocked;

    public QuarantineSourceReversalFeed(IMongoDatabase database)
    {
        _holds = database.GetCollection<QuarantineHold>(QuarantineCollections.Holds);
        _blocked = database.GetCollection<BlockedStock>(QuarantineCollections.BlockedStock);
    }

    public async Task<IReadOnlyList<SourceReversalFeedRecord>> PageAsync(
        SourceReversalFeedKind feed, HistorySubject subject, Guid? after, int size, DateTimeOffset now, QuarantineSweeperOptions options,
        Guid? onlyTenant, CancellationToken cancellationToken)
    {
        var watchFrom = now.AddHours(-options.SourceReversalVoidedWatchHours);
        var settled = now.AddSeconds(-options.SourceReversalSettleSeconds);
        if (subject == HistorySubject.Hold)
        {
            var f = Builders<QuarantineHold>.Filter;
            var filter = f.Eq(x => x.IsDeleted, false) & f.Type(x => x.SourceMovementId, MongoDB.Bson.BsonType.String)
                         & f.Eq(x => x.SourceReversalConflict, null) & f.Ne(x => x.TenantId, null) & f.Ne(x => x.LegalEntityId, null)
                         & feed switch
                         {
                             SourceReversalFeedKind.Open => f.In(x => x.Status, QuarantineRecordStates.OpenHoldStatuses),
                             SourceReversalFeedKind.Terminal => f.Eq(x => x.Status, HoldStatus.Decided),
                             _ => f.Eq(x => x.Status, HoldStatus.Voided) & f.Ne(x => x.Voided!.DecisionId, null)
                                  & f.Gte(x => x.Voided!.AtUtc, watchFrom) & f.Lt(x => x.Voided!.AtUtc, settled)
                         };
            if (after is { } cursor) filter &= f.Gt(x => x.Id, cursor);
            if (onlyTenant is { } tenant) filter &= f.Eq(x => x.TenantId, tenant);
            var rows = await _holds.Find(filter).Sort(Builders<QuarantineHold>.Sort.Ascending(x => x.Id)).Limit(size).ToListAsync(cancellationToken);
            return rows.Select(h => new SourceReversalFeedRecord(h.Id, h.TenantId!.Value, h.LegalEntityId!.Value, h.SourceMovementId!,
                h.Voided?.ReversalTransactionId, h.Voided?.DecisionId)).ToList();
        }
        else
        {
            var f = Builders<BlockedStock>.Filter;
            var filter = f.Eq(x => x.IsDeleted, false) & f.Type(x => x.SourceMovementId, MongoDB.Bson.BsonType.String)
                         & f.Eq(x => x.SourceReversalConflict, null) & f.Ne(x => x.TenantId, null) & f.Ne(x => x.LegalEntityId, null)
                         & feed switch
                         {
                             SourceReversalFeedKind.Open => f.Eq(x => x.Status, BlockedStatus.Open),
                             SourceReversalFeedKind.Terminal => f.In(x => x.Status, [BlockedStatus.Closed, BlockedStatus.SampleRetained]),
                             _ => f.Eq(x => x.Status, BlockedStatus.Voided) & f.Ne(x => x.Voided!.DecisionId, null)
                                  & f.Gte(x => x.Voided!.AtUtc, watchFrom) & f.Lt(x => x.Voided!.AtUtc, settled)
                         };
            if (after is { } cursor) filter &= f.Gt(x => x.Id, cursor);
            if (onlyTenant is { } tenant) filter &= f.Eq(x => x.TenantId, tenant);
            var rows = await _blocked.Find(filter).Sort(Builders<BlockedStock>.Sort.Ascending(x => x.Id)).Limit(size).ToListAsync(cancellationToken);
            return rows.Select(b => new SourceReversalFeedRecord(b.Id, b.TenantId!.Value, b.LegalEntityId!.Value, b.SourceMovementId!,
                b.Voided?.ReversalTransactionId, b.Voided?.DecisionId)).ToList();
        }
    }
}

/// <summary>
/// MOD-0175-FU01 §8.2 — the sweep that closes / marks what the post-commit observer missed, and old data (REVERSALs committed before
/// this FU). Each round reads ONE page of every feed × collection after its cursor (six in-memory cursors; a short page resets the cursor,
/// a restarted process starts over — the command is idempotent). Open and terminal pages are grouped by Tenant + company and their
/// REVERSALs read in one indexed ledger query per group; the void feed asks the ledger for the voided decision's line keys. Every command
/// is sent inside a scope set to the RECORD's tenant + company, with the REVERSAL's correlation id. A failing record is logged and the
/// round goes on.
/// </summary>
public sealed class QuarantineSourceReversalSweeper
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly QuarantineSweeperOptions _options;
    private readonly ILogger<QuarantineSourceReversalSweeper> _logger;
    private readonly Dictionary<(SourceReversalFeedKind, HistorySubject), Guid?> _cursors = [];
    private readonly SemaphoreSlim _round = new(1, 1);

    public QuarantineSourceReversalSweeper(
        IServiceScopeFactory scopeFactory, IOptions<QuarantineSweeperOptions> options, ILogger<QuarantineSourceReversalSweeper> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>What a round did: closes, marks, records the table left unchanged (the command's NoChange), and failures.</summary>
    public sealed record SweepResult(int Voided, int Conflicts, int NoChange, int Failed, IReadOnlyList<SourceReversalResultDto> Results)
    {
        public int Changes => Voided + Conflicts;
    }

    public async Task<SweepResult> SweepAsync(DateTimeOffset now, CancellationToken ct, Guid? onlyTenant = null)
    {
        await _round.WaitAsync(ct);
        try
        {
            var results = new List<SourceReversalResultDto>();
            var failed = 0;
            foreach (var feed in new[] { SourceReversalFeedKind.Open, SourceReversalFeedKind.Terminal, SourceReversalFeedKind.Voided })
            {
                foreach (var subject in new[] { HistorySubject.Hold, HistorySubject.Blocked })
                {
                    failed += await SweepPageAsync(feed, subject, now, onlyTenant, results, ct);
                }
            }

            return new SweepResult(results.Count(r => r.Outcome == SourceReversalVerdict.Voided), results.Count(r => r.Outcome == SourceReversalVerdict.Conflict),
                results.Count(r => r.Outcome == SourceReversalVerdict.NoChange), failed, results);
        }
        finally
        {
            _round.Release();
        }
    }

    private async Task<int> SweepPageAsync(
        SourceReversalFeedKind feed, HistorySubject subject, DateTimeOffset now, Guid? onlyTenant, List<SourceReversalResultDto> results, CancellationToken ct)
    {
        var size = Math.Max(1, _options.SourceReversalPageSize);
        IReadOnlyList<SourceReversalFeedRecord> page;
        using (var feedScope = _scopeFactory.CreateScope())
        {
            page = await feedScope.ServiceProvider.GetRequiredService<QuarantineSourceReversalFeed>()
                .PageAsync(feed, subject, _cursors.GetValueOrDefault((feed, subject)), size, now, _options, onlyTenant, ct);
        }

        _cursors[(feed, subject)] = page.Count < size ? null : page[^1].Id;
        var failed = 0;
        if (feed == SourceReversalFeedKind.Voided)
        {
            foreach (var record in page)
            {
                failed += await TryAsync(record, () => VoidedRecordAsync(record, subject, results, ct));
            }

            return failed;
        }

        var settled = now.AddSeconds(-_options.SourceReversalSettleSeconds);
        foreach (var group in page.GroupBy(r => (r.TenantId, r.LegalEntityId)))
        {
            var bySource = group.Where(r => Guid.TryParse(r.SourceMovementId, out _)).ToDictionary(r => Guid.Parse(r.SourceMovementId));
            IReadOnlyList<Domain.Entities.InventoryTransaction> reversals;
            using (var readScope = _scopeFactory.CreateScope())
            {
                reversals = await readScope.ServiceProvider.GetRequiredService<QuarantineReversalReads>()
                    .ReversalsOfAsync(group.Key.TenantId, group.Key.LegalEntityId, bySource.Keys.ToList(), settled, ct);
            }

            foreach (var reversal in reversals)
            {
                var record = bySource[reversal.ReversesTransactionId!.Value];
                failed += await TryAsync(record, () => DispatchAsync(record, subject, reversal.Id.ToString("D"), reversal.CorrelationId, results, ct));
            }
        }

        return failed;
    }

    /// <summary>The void feed: only a record whose voided decision has a posted line key is sent (R0a); the rest write nothing.</summary>
    private async Task VoidedRecordAsync(SourceReversalFeedRecord record, HistorySubject subject, List<SourceReversalResultDto> results, CancellationToken ct)
    {
        Guid correlation;
        using (var scope = Scoped(record, Guid.NewGuid()))
        {
            var sp = scope.ServiceProvider;
            var decision = await sp.GetRequiredService<IQuarantineDecisionRepository>().FindAsync(record.VoidedDecisionId!.Value, ct);
            var ledger = sp.GetRequiredService<IInventoryLedgerRepository>();
            var posted = false;
            foreach (var lineNo in decision is null ? [] : QuarantineStockEffects.Plan(decision).Select(m => m.LineNo).Distinct())
            {
                posted |= await ledger.FindByIdempotencyKeyAsync(QuarantineMovementPoster.DecisionKey(decision!.Id, lineNo), ct) is not null;
            }

            if (!posted || record.VoidedReversalTransactionId is null || !Guid.TryParse(record.VoidedReversalTransactionId, out var reversalId))
            {
                return;
            }

            correlation = (await ledger.FindTransactionAsync(reversalId, ct))?.CorrelationId ?? Guid.Empty;
        }

        await DispatchAsync(record, subject, record.VoidedReversalTransactionId, correlation, results, ct);
    }

    private async Task DispatchAsync(
        SourceReversalFeedRecord record, HistorySubject subject, string reversalId, Guid correlation, List<SourceReversalResultDto> results, CancellationToken ct)
    {
        using var scope = Scoped(record, correlation);
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var response = subject == HistorySubject.Hold
            ? await sender.Send(new ApplySourceReversalToHoldCommand(reversalId), ct)
            : await sender.Send(new ApplySourceReversalToBlockedCommand(reversalId), ct);
        if (!response.IsSuccessful)
        {
            throw new InvalidOperationException($"{response.ErrorCode}: {string.Join(" ", response.Errors)}");
        }

        results.Add(response.Data!);
    }

    /// <summary>A scope resolved to the record's tenant + company (§8.1); the actor is MOD-0175's system actor (Q-03 = A).</summary>
    private IServiceScope Scoped(SourceReversalFeedRecord record, Guid correlation)
    {
        var scope = _scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(record.TenantId, record.LegalEntityId, QuarantineSystemActor.ActorId);
        scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(correlation != Guid.Empty ? correlation : Guid.NewGuid());
        return scope;
    }

    private async Task<int> TryAsync(SourceReversalFeedRecord record, Func<Task> work)
    {
        try
        {
            await work();
            return 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "quarantine.source_reversal.sweep_failed Record={Record} Tenant={Tenant}; retried next round.", record.Id, record.TenantId);
            return 1;
        }
    }
}

public sealed class QuarantineSourceReversalSweeperHostedService : BackgroundService
{
    private readonly QuarantineSourceReversalSweeper _sweeper;
    private readonly QuarantineSweeperOptions _options;
    private readonly ILogger<QuarantineSourceReversalSweeperHostedService> _logger;

    public QuarantineSourceReversalSweeperHostedService(
        QuarantineSourceReversalSweeper sweeper, IOptions<QuarantineSweeperOptions> options, ILogger<QuarantineSourceReversalSweeperHostedService> logger)
    {
        _sweeper = sweeper;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.SourceReversalSweeperEnabled)
        {
            _logger.LogWarning("MOD-0175-FU01 source-reversal sweeper is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _options.SourceReversalSweepIntervalSeconds)));
        try
        {
            do
            {
                try
                {
                    var result = await _sweeper.SweepAsync(DateTimeOffset.UtcNow, stoppingToken);
                    if (result.Changes > 0 || result.Failed > 0)
                    {
                        _logger.LogWarning("MOD-0175-FU01 source-reversal sweep: {Voided} voided, {Conflicts} marked, {Failed} failed.",
                            result.Voided, result.Conflicts, result.Failed);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MOD-0175-FU01 source-reversal sweep failed; retrying on the next tick.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
