using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.OutboundShipments.Commands;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.InventoryService.Persistence.Warehouse;

/// <summary>What one maintenance tick did (for the log and the tests).</summary>
public sealed record OutboundMaintenanceTick(int Scanned, int CommandsSent, int Failures);

/// <summary>
/// The S2 outbound maintenance (pack §8.17 step 6; AC-71). DL-4 part: the <c>Allocating</c> outbounds whose recovery lock passed — per outbound a
/// fresh DI scope in the outbound's tenant + company with its CREATOR as actor (<c>SetScope(tenant, LE, CreatedBy)</c>, the S1 reconciler
/// pattern) and a new correlation; the internal <see cref="CompleteOutboundAllocationCommand"/> re-sends the persisted batch. (The keyed
/// reservation release — <c>ReleaseOutboundReservationsCommand</c> — is DL-5.) One outbound's failure never stops the others; it opens no
/// transaction itself (the command's repository does).
/// </summary>
public sealed class OutboundMaintenance
{
    private readonly IServiceScopeFactory _scopes;
    private readonly WarehouseOutboundOptions _options;
    private readonly ILogger<OutboundMaintenance> _logger;
    private (Guid TenantId, Guid OutboundId)? _cursor;
    private (Guid TenantId, Guid OutboundId)? _releaseCursor;

    public OutboundMaintenance(IServiceScopeFactory scopes, IOptions<WarehouseOutboundOptions> options, ILogger<OutboundMaintenance> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>One tick at <paramref name="now"/>; <paramref name="onlyTenants"/> narrows the scan (the shared test database).</summary>
    public async Task<OutboundMaintenanceTick> RunOnceAsync(DateTimeOffset now, CancellationToken ct, IReadOnlyCollection<Guid>? onlyTenants = null)
    {
        var budget = _options.MaintenanceBatchSize;
        IReadOnlyList<OutboundMaintenanceRow> rows;
        using (var scanScope = _scopes.CreateScope())
        {
            rows = await scanScope.ServiceProvider.GetRequiredService<IOutboundMaintenanceScan>().ExpiredAllocationsAsync(now, _cursor, budget, onlyTenants, ct);
        }

        _cursor = rows.Count < budget ? null : (rows[^1].TenantId, rows[^1].OutboundId);
        var sent = 0;
        var failures = 0;
        foreach (var row in rows)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(row.TenantId, row.LegalEntityId, row.ActorUserId);
                scope.ServiceProvider.GetService<IPostingActorName>()?.Use(row.ActorName);
                scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(Guid.NewGuid());
                var answer = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new CompleteOutboundAllocationCommand(row.OutboundId), ct);
                sent++;
                if (!answer.IsSuccessful) failures++;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
            {
                failures++;
                _logger.LogError(ex, "wms.outbound.maintenance_failed {OutboundId} tenant {TenantId} company {LegalEntityId}", row.OutboundId, row.TenantId, row.LegalEntityId);
            }
        }

        // DL-5 (§8.17 step 6, §8.21, §8.22): the Pending reservation releases — per outbound, its creator as actor, the same scope rule
        IReadOnlyList<OutboundMaintenanceRow> pending;
        using (var scanScope = _scopes.CreateScope())
        {
            pending = await scanScope.ServiceProvider.GetRequiredService<IOutboundMaintenanceScan>().PendingReleasesAsync(_releaseCursor, budget, onlyTenants, ct);
        }

        _releaseCursor = pending.Count < budget ? null : (pending[^1].TenantId, pending[^1].OutboundId);
        foreach (var row in pending)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetScope(row.TenantId, row.LegalEntityId, row.ActorUserId);
                scope.ServiceProvider.GetService<IPostingActorName>()?.Use(row.ActorName);
                scope.ServiceProvider.GetRequiredService<ICorrelationContext>().Set(Guid.NewGuid());
                var answer = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ReleaseOutboundReservationsCommand(row.OutboundId), ct);
                sent++;
                if (!answer.IsSuccessful) failures++;
            }
            catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
            {
                failures++;
                _logger.LogError(ex, "wms.outbound.release_maintenance_failed {OutboundId} tenant {TenantId} company {LegalEntityId}", row.OutboundId, row.TenantId,
                    row.LegalEntityId);
            }
        }

        return new OutboundMaintenanceTick(rows.Count + pending.Count, sent, failures);
    }
}

/// <summary>The tick of <see cref="OutboundMaintenance"/>; off when <c>Warehouse:Outbound:MaintenanceEnabled</c> is false.</summary>
public sealed class OutboundMaintenanceHostedService : BackgroundService
{
    private readonly OutboundMaintenance _maintenance;
    private readonly WarehouseOutboundOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<OutboundMaintenanceHostedService> _logger;

    public OutboundMaintenanceHostedService(OutboundMaintenance maintenance, IOptions<WarehouseOutboundOptions> options, TimeProvider clock,
        ILogger<OutboundMaintenanceHostedService> logger)
    {
        _maintenance = maintenance;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.MaintenanceEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.MaintenanceIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    var tick = await _maintenance.RunOnceAsync(_clock.GetUtcNow(), stoppingToken);
                    if (tick.CommandsSent > 0 || tick.Failures > 0)
                        _logger.LogWarning("MOD-0178 outbound maintenance sent {Sent} command(s), {Failures} failure(s).", tick.CommandsSent, tick.Failures);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "MOD-0178 outbound maintenance tick failed; the next tick retries.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
