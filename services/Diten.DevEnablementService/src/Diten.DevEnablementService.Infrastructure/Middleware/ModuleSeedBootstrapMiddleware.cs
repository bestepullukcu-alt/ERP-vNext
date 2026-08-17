using System.Collections.Concurrent;
using Diten.DevEnablementService.Application.Common;
using Diten.DevEnablementService.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Diten.DevEnablementService.Infrastructure.Middleware;

/// <summary>
/// Lazily ensures minimum seed data once per tenant for composition/SKU module routes.
/// </summary>
public sealed class ModuleSeedBootstrapMiddleware
{
    private static readonly ConcurrentDictionary<Guid, byte> SeededTenants = new();
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> TenantLocks = new();

    private readonly RequestDelegate _next;
    private readonly ILogger<ModuleSeedBootstrapMiddleware> _logger;

    public ModuleSeedBootstrapMiddleware(
        RequestDelegate next,
        ILogger<ModuleSeedBootstrapMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        IModuleSeedDataInitializer seedDataInitializer)
    {
        if (!tenantContext.IsResolved || !ShouldBootstrap(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var tenantId = tenantContext.TenantId;
        if (!SeededTenants.ContainsKey(tenantId))
        {
            var gate = TenantLocks.GetOrAdd(tenantId, static _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(context.RequestAborted);
            try
            {
                if (!SeededTenants.ContainsKey(tenantId))
                {
                    await seedDataInitializer.EnsureMinimumDataAsync(20, context.RequestAborted);
                    SeededTenants.TryAdd(tenantId, 0);
                    _logger.LogInformation(
                        "Composition/SKU minimum seed data ensured for tenant {TenantId}",
                        tenantId);
                }
            }
            finally
            {
                gate.Release();
            }
        }

        await _next(context);
    }

    private static bool ShouldBootstrap(PathString path)
    {
        return path.StartsWithSegments("/api/compositions", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/skus", StringComparison.OrdinalIgnoreCase);
    }
}
