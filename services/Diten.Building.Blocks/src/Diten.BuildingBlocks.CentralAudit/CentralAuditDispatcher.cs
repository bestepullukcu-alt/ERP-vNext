using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.BuildingBlocks.CentralAudit;

/// <summary>
/// Delivers the outbox to Platform. One cycle: release orphaned rows → read the due rows → send each → mark it.
///
/// <list type="bullet">
/// <item>Platform 2xx → <see cref="CentralAuditOutboxStatus.Delivered"/>.</item>
/// <item>Platform 400 / an unknown enum name → <see cref="CentralAuditOutboxStatus.Rejected"/>; the row stays and is logged
/// as an error with tenant + event id.</item>
/// <item>Anything else (down, 5xx, 401, timeout) → the row stays <see cref="CentralAuditOutboxStatus.Pending"/> with a
/// backed-off next attempt; logged as a warning with tenant + event id.</item>
/// <item>Not configured (no address / key) → nothing is sent and nothing is lost: rows accumulate, and a warning with the
/// waiting count is logged every <see cref="CentralAuditOptions.UnconfiguredWarningInterval"/>.</item>
/// </list>
/// No branch deletes a row.
/// </summary>
public sealed class CentralAuditDispatcher
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ICentralAuditPlatformClient _client;
    private readonly IOptionsMonitor<CentralAuditOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<CentralAuditDispatcher> _logger;
    private DateTime _lastUnconfiguredWarningUtc = DateTime.MinValue;

    public CentralAuditDispatcher(
        IServiceScopeFactory scopes,
        ICentralAuditPlatformClient client,
        IOptionsMonitor<CentralAuditOptions> options,
        TimeProvider clock,
        ILogger<CentralAuditDispatcher> logger)
    {
        _scopes = scopes;
        _client = client;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>One delivery cycle. Returns how many rows were delivered.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        var options = _options.CurrentValue;
        var now = _clock.GetUtcNow().UtcDateTime;

        using var scope = _scopes.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ICentralAuditOutboxStore>();

        var released = await store.ReleaseOrphansAsync(now - options.OrphanAfter, ct);
        if (released > 0)
        {
            _logger.LogWarning(
                "Central audit: {Count} outbox row(s) were never confirmed by their command (process stopped after the commit?) and are released for delivery.",
                released);
        }

        if (!options.IsConfigured)
        {
            if (now - _lastUnconfiguredWarningUtc >= options.UnconfiguredWarningInterval)
            {
                _lastUnconfiguredWarningUtc = now;
                var backlog = await store.GetBacklogAsync(ct);
                _logger.LogWarning(
                    "Central audit: NOT DELIVERING — {BaseUrlKey} / {KeyKey} is not configured. {Pending} event(s) are waiting in the outbox; commands keep running and nothing is lost.",
                    $"{CentralAuditOptions.PlatformSection}:BaseUrl",
                    $"{CentralAuditOptions.PlatformSection}:InternalApiKey",
                    backlog.Pending);
            }

            return 0;
        }

        var delivered = 0;
        foreach (var record in await store.GetDueAsync(now, options.BatchSize, ct))
        {
            var result = await _client.SendAsync(record, ct);
            var attempts = record.Attempts + 1;
            switch (result.Outcome)
            {
                case CentralAuditDeliveryOutcome.Accepted:
                    await store.MarkDeliveredAsync(record.Id, _clock.GetUtcNow().UtcDateTime, ct);
                    delivered++;
                    break;

                case CentralAuditDeliveryOutcome.Rejected:
                    await store.MarkRejectedAsync(record.Id, attempts, result.Error ?? "rejected", ct);
                    _logger.LogError(
                        "Central audit: Platform REFUSED the event; the row stays in the outbox as Rejected. TenantId={TenantId} EventId={EventId} RequestType={RequestType} Error={Error}",
                        record.TenantId,
                        record.Id,
                        record.RequestType,
                        result.Error);
                    break;

                default:
                    var next = _clock.GetUtcNow().UtcDateTime + options.BackoffFor(attempts);
                    await store.MarkRetryAsync(record.Id, attempts, next, result.Error ?? "not delivered", ct);
                    _logger.LogWarning(
                        "Central audit: event not delivered, will retry at {NextAttemptAtUtc:O} (attempt {Attempts}). TenantId={TenantId} EventId={EventId} Error={Error}",
                        next,
                        attempts,
                        record.TenantId,
                        record.Id,
                        result.Error);
                    break;
            }
        }

        return delivered;
    }
}

/// <summary>Runs the dispatcher on a timer; creates the outbox indexes and says out loud when delivery is not configured.</summary>
public sealed class CentralAuditDispatcherHostedService : BackgroundService
{
    private readonly CentralAuditDispatcher _dispatcher;
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<CentralAuditOptions> _options;
    private readonly ILogger<CentralAuditDispatcherHostedService> _logger;

    public CentralAuditDispatcherHostedService(
        CentralAuditDispatcher dispatcher,
        IServiceScopeFactory scopes,
        IOptionsMonitor<CentralAuditOptions> options,
        ILogger<CentralAuditDispatcherHostedService> logger)
    {
        _dispatcher = dispatcher;
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ICentralAuditOutboxStore>().CreateIndexesAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Central audit: outbox indexes could not be created; delivery continues without them.");
        }

        if (!_options.CurrentValue.DispatcherEnabled)
        {
            // A test host that drives CentralAuditDispatcher.RunOnceAsync itself.
            return;
        }

        if (!_options.CurrentValue.IsConfigured)
        {
            _logger.LogWarning(
                "Central audit: {Section}:BaseUrl / InternalApiKey not configured — events will accumulate in the outbox and NOT reach Platform's central audit log until they are.",
                CentralAuditOptions.PlatformSection);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _dispatcher.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Central audit: a delivery cycle failed; the outbox is unchanged and the next cycle retries.");
            }

            try
            {
                await Task.Delay(_options.CurrentValue.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
