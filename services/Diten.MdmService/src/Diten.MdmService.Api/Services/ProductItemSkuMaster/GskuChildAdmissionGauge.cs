using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

/*
 * CT 2026-10-06 (LSKU FIX1 B1, reused by FG FIX1): GSKU child-creation admissions — of every child kind — held longer
 * than a creation ever takes. Each is either a leak the operator repair sweeps (no live row, no live reservation) or
 * one a live reservation still holds (never released; the command's repeat or a person settles it). Counted per cycle
 * on the shared gauge under its own flow, and said once per cycle when there are any.
 */
public sealed class GskuChildAdmissionGauge(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<GskuChildAdmissionGauge> logger, ProductIdentityRecoveryMetrics? metrics = null)
{
    public const string Flow = "GSKU_CHILD_ADMISSION";
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    public async Task<long> RecordAsync(CancellationToken ct)
    {
        long stale = 0;
        Guid? after = null;
        var cutoff = clock.GetUtcNow().ToUniversalTime() - StaleAfter;
        while (true)
        {
            IReadOnlyList<Guid> tenants;
            await using (var scope = scopes.CreateAsyncScope())
                tenants = await scope.ServiceProvider.GetRequiredService<IGskuRepository>()
                    .DiscoverTenantsWithChildAdmissionsAsync(after, 100, ct);
            foreach (var tenantId in tenants)
            {
                await using var scope = scopes.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(tenantId);
                stale += await scope.ServiceProvider.GetRequiredService<IGskuRepository>()
                    .CountStaleChildCreationAdmissionsAsync(cutoff, ct);
            }
            if (tenants.Count < 100) break;
            after = tenants[^1];
        }
        metrics?.Record(Flow, stale);
        if (stale > 0)
            logger.LogWarning("{Flow}: {Count} child-creation admissions are held longer than {Minutes} minutes (swept by the operator repair, or held by a live reservation).",
                Flow, stale, (int)StaleAfter.TotalMinutes);
        return stale;
    }
}
