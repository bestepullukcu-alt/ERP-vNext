using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Persistence.Seed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.AuthService.Infrastructure.Authorization;

/// <summary>
/// Runs the Q358 clean-up (<see cref="Q358ShipmentGrantRetractionRunner"/>) once at start when
/// <c>Q358Retraction:Mode</c> is <c>DryRun</c> or <c>Apply</c>. Absent or anything else is Off: nothing is read.
/// In the background, so a slow Platform never delays the start; a failure is logged with its TYPE only.
/// The entitlement read is the AUTHORITATIVE one: an unavailable Platform makes the tenant unknown (skipped), never
/// "entitled to nothing".
/// </summary>
public sealed class Q358ShipmentGrantRetractionHostedService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<Q358ShipmentGrantRetractionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var mode = Q358ShipmentGrantRetraction.ParseMode(configuration[Q358ShipmentGrantRetraction.ConfigurationKey]);
        if (mode == Q358ShipmentGrantRetraction.Mode.Off) return;

        try
        {
            using var scope = scopes.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
            var client = scope.ServiceProvider.GetRequiredService<ITenantEntitlementClient>();
            var result = await Q358ShipmentGrantRetractionRunner.RunAsync(database, mode, async (tenantId, ct) =>
            {
                var read = await client.ReadEntitledModulesWithPermissionKeysAsync(tenantId, ct);
                return read.IsAuthoritative ? read.Modules.Select(m => m.ModuleCode).ToList() : null;
            }, logger, stoppingToken);

            logger.LogInformation(
                "Q358 retraction {Mode} finished: {Roles} Admin role(s), {Planned} grant(s) planned, {Converted} converted to Module, {SoftDeleted} soft-deleted, {NotApplied} not applied, {Unknown} tenant(s) unknown, {Failed} failed.",
                result.Mode, result.AdminRolesScanned, result.Planned.Count, result.Converted, result.SoftDeleted, result.NotApplied,
                result.UnknownTenants.Count, result.FailedTenants.Count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError("Q358 retraction could not run: {ExceptionType}. Nothing further was changed.", ex.GetType().FullName);
        }
    }
}
