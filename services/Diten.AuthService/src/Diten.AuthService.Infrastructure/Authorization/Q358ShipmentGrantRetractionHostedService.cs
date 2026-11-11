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
/// Runs the Admin-baseline clean-ups (<see cref="Q358ShipmentGrantRetractionRunner"/>) once at start, each only when its
/// own setting is <c>DryRun</c> or <c>Apply</c>: Q358 (<c>Q358Retraction:Mode</c>) and G7 (<c>G7Retraction:Mode</c>).
/// Absent or anything else is Off: nothing is read. In the background, so a slow Platform never delays the start; a failure
/// is logged with its TYPE only and does not stop the other clean-up. The entitlement read is the AUTHORITATIVE one: an
/// unavailable Platform makes the tenant unknown (skipped), never "entitled to nothing".
/// </summary>
public sealed class Q358ShipmentGrantRetractionHostedService(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<Q358ShipmentGrantRetractionHostedService> logger) : BackgroundService
{
    internal static readonly IReadOnlyList<BaselineGrantRetraction.Definition> Definitions =
        [Q358ShipmentGrantRetraction.Definition, G7SandopCapacityGrantRetraction.Definition];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var definition in Definitions)
        {
            var mode = BaselineGrantRetraction.ParseMode(configuration[definition.ConfigurationKey]);
            if (mode == BaselineGrantRetraction.Mode.Off) continue;

            try
            {
                using var scope = scopes.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
                var client = scope.ServiceProvider.GetRequiredService<ITenantEntitlementClient>();
                var result = await Q358ShipmentGrantRetractionRunner.RunAsync(database, definition, mode, async (tenantId, ct) =>
                {
                    var read = await client.ReadEntitledModulesWithPermissionKeysAsync(tenantId, ct);
                    return read.IsAuthoritative ? read.Modules.Select(m => m.ModuleCode).ToList() : null;
                }, logger, stoppingToken);

                logger.LogInformation(
                    "{Retraction} retraction {Mode} finished: {Roles} Admin role(s), {Planned} grant(s) planned, {Converted} converted to Module, {SoftDeleted} soft-deleted, {NotApplied} not applied, {Unknown} tenant(s) unknown, {Failed} failed.",
                    definition.Name, result.Mode, result.AdminRolesScanned, result.Planned.Count, result.Converted, result.SoftDeleted,
                    result.NotApplied, result.UnknownTenants.Count, result.FailedTenants.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError("{Retraction} retraction could not run: {ExceptionType}. Nothing further was changed by it.",
                    definition.Name, ex.GetType().FullName);
            }
        }
    }
}
