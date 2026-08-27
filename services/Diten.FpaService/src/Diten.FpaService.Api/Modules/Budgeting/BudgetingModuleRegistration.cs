using Diten.FpaService.Application.Modules.Budgeting;
using Diten.FpaService.Application.Modules.Budgeting.GateI;
using Diten.FpaService.Infrastructure.Modules.Budgeting;
using Diten.FpaService.Persistence.Modules.Budgeting;
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
}
