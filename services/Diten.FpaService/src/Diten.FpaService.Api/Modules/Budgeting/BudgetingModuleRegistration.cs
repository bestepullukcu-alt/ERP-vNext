using Diten.FpaService.Application.Modules.Budgeting;
using Diten.FpaService.Application.Modules.Budgeting.GateI;
using Diten.FpaService.Infrastructure.Modules.Budgeting;
using Diten.FpaService.Persistence.Modules.Budgeting;
using Diten.Platform.Common.Authorization.Fu16;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.FpaService.Api.Modules.Budgeting;

public static class BudgetingModuleRegistration
{
    public static IServiceCollection AddBudgetingModuleForContractDiscovery(this IServiceCollection services) =>
        services.AddBudgetingApplication().AddBudgetingPersistenceContracts().AddBudgetingInfrastructureContracts()
            .AddSingleton(BudgetProviderOptions.Disabled)
            .AddSingleton<IBudgetVersionSnapshotReader, UnavailableBudgetVersionSnapshotReader>()
            .AddSingleton<IBudgetGateITrustedContextAccessor, UnavailableBudgetGateITrustedContextAccessor>()
            .AddSingleton<BudgetVersionReferenceValidationProvider>();

    public static IServiceCollection AddBudgetingGateIFu16Evidence(
        this IServiceCollection services,
        Action<Fu16AuthenticationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddAuthentication().AddFu16DelegatedActor(configure);
        services.AddScoped<IBudgetGateITrustedContextAccessor, Fu16BudgetGateITrustedContextAccessor>();
        return services;
    }
}
