using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Recall.Quarantine;
using Diten.InventoryService.Domain.Recall.Entities;
using Diten.InventoryService.Domain.Recall.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Recall;

/// <summary>
/// Pack §8.4 — the retry sweeper of KR-B: every <c>Pending</c> request whose last attempt (or creation) is older than 2 minutes is handed to the SAME
/// <see cref="RecallQuarantineDispatcher"/> inside a scope resolved to its tenant, company and requester (CT-R2: the actor of the 0175 call). Only
/// Pending — never Failed / BlockedByReservation / NothingAvailable (those need "Yeniden dene"). The 0175 key is unchanged, so a call that reached
/// 0175 before a crash replays (200) instead of posting twice. No bus (RabbitMQ is off on this machine).
/// WP-0177-FIX-R3T: one request that throws (outside the dispatcher's own catch — the scope, the re-read, a row without a company) is logged with
/// its tenant, company and id and stays Pending for the next sweep; the rest of the sweep is still sent.
/// </summary>
public sealed class RecallQuarantineSweeper
{
    public static readonly TimeSpan PendingAge = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMongoCollection<RecallQuarantineRequest> _requests;
    private readonly ILogger<RecallQuarantineSweeper> _logger;

    public RecallQuarantineSweeper(IServiceScopeFactory scopeFactory, IMongoClient client, Microsoft.Extensions.Configuration.IConfiguration configuration,
        ILogger<RecallQuarantineSweeper> logger)
    {
        _scopeFactory = scopeFactory;
        _requests = client.GetDatabase(configuration["Mongo:DatabaseName"]).GetCollection<RecallQuarantineRequest>(RecallCollections.QuarantineRequests);
        _logger = logger;
    }

    /// <summary>Returns how many requests left Pending in this sweep.</summary>
    public Task<int> SweepAsync(DateTimeOffset now, CancellationToken ct, Guid? onlyTenant = null)
        => SweepAsync(now, ct, onlyTenant is { } tenant ? [tenant] : null);

    /// <summary>The same sweep limited to these tenants (the tests share one database; the hosted service passes none = every tenant).</summary>
    public async Task<int> SweepAsync(DateTimeOffset now, CancellationToken ct, IReadOnlyCollection<Guid>? onlyTenants)
    {
        var f = Builders<RecallQuarantineRequest>.Filter;
        var cutoff = now - PendingAge;
        var filter = f.Eq(x => x.Status, RecallQuarantineRequestStatus.Pending) & f.Eq(x => x.IsDeleted, false)
                     & f.Or(f.Lt(x => x.LastAttemptAtUtc, cutoff), f.And(f.Eq(x => x.LastAttemptAtUtc, null), f.Lt(x => x.RequestedAtUtc, cutoff)));
        if (onlyTenants is not null) filter &= f.In(x => x.TenantId, onlyTenants.Select(t => (Guid?)t));
        var due = await _requests.Find(filter).Limit(200).ToListAsync(ct);

        var moved = 0;
        foreach (var request in due)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(request.TenantId!.Value, request.LegalEntityId!.Value, request.RequestedBy);
                scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(Guid.NewGuid());
                var after = await scope.ServiceProvider.GetRequiredService<RecallQuarantineDispatcher>().DispatchAsync([request.Id], ct);
                if (after.Any(r => r.Status != RecallQuarantineRequestStatus.Pending)) moved++;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
            {
                // stays Pending (nothing is written here): the next sweep retries it; the others of this sweep are still sent
                _logger.LogError(ex, "recall.quarantine.sweep_request_failed {RequestId} tenant {TenantId} company {LegalEntityId}",
                    request.Id, request.TenantId, request.LegalEntityId);
            }
        }

        return moved;
    }
}

public sealed class RecallQuarantineSweeperHostedService : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private readonly RecallQuarantineSweeper _sweeper;
    private readonly ILogger<RecallQuarantineSweeperHostedService> _logger;

    public RecallQuarantineSweeperHostedService(RecallQuarantineSweeper sweeper, ILogger<RecallQuarantineSweeperHostedService> logger)
    {
        _sweeper = sweeper;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var moved = await _sweeper.SweepAsync(DateTimeOffset.UtcNow, stoppingToken);
                    if (moved > 0) _logger.LogWarning("MOD-0177 quarantine sweep sent {Count} pending recall request(s).", moved);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MOD-0177 quarantine sweep failed; the next tick retries.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
