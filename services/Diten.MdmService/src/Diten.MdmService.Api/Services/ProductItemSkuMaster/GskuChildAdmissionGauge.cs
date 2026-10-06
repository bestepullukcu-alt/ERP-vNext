using Diten.MdmService.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

/*
 * CT 2026-10-06 (LSKU FIX1 B1, reused by FG FIX1): GSKU child-creation admissions — of every child kind — held longer
 * than a creation ever takes. Each is either a leak the operator repair sweeps (no live row, no live reservation) or
 * one a live reservation still holds. Counted on the shared gauge under its own flow, said once per run when any.
 * LSKU FIX2 item 1: counted by a tenant-free census (FIX1 asked a tenant-scoped repository without a tenant and never
 * counted), on its OWN worker — not on the LSKU retirement worker, which may be switched off.
 */
public sealed class GskuChildAdmissionGauge(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<GskuChildAdmissionGauge> logger, ProductIdentityRecoveryMetrics? metrics = null)
{
    public const string Flow = "GSKU_CHILD_ADMISSION";
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    public async Task<long> RecordAsync(CancellationToken ct)
    {
        long stale;
        await using (var scope = scopes.CreateAsyncScope())
            stale = await scope.ServiceProvider.GetRequiredService<IChildCreationAdmissionCensus>()
                .CountStaleAsync(clock.GetUtcNow().ToUniversalTime() - StaleAfter, ct);
        metrics?.Record(Flow, stale);
        if (stale > 0)
            logger.LogWarning("{Flow}: {Count} child-creation admissions are held longer than {Minutes} minutes (swept by the operator repair, or held by a live reservation).",
                Flow, stale, (int)StaleAfter.TotalMinutes);
        return stale;
    }
}

public sealed class GskuChildAdmissionGaugeOptions
{
    public const string SectionName = "GskuChildAdmissionGauge";
    // A read of one count: on by default, so the gauge does not depend on any recovery worker being switched on.
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 300;
}

public sealed class GskuChildAdmissionGaugeWorker(GskuChildAdmissionGauge gauge,
    IOptions<GskuChildAdmissionGaugeOptions> options, ILogger<GskuChildAdmissionGaugeWorker> logger) : BackgroundService
{
    public async Task RunOnceAsync(CancellationToken ct)
    {
        try { await gauge.RecordAsync(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError("{Flow}: the admission count failed with {ExceptionType}.", GskuChildAdmissionGauge.Flow,
                exception.GetType().Name);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        var interval = TimeSpan.FromSeconds(Math.Clamp(settings.IntervalSeconds, 30, 3600));
        while (!ct.IsCancellationRequested)
        {
            await RunOnceAsync(ct);
            await Task.Delay(interval, ct);
        }
    }
}
