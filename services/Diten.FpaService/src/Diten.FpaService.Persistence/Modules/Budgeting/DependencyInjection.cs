using Microsoft.Extensions.DependencyInjection;

namespace Diten.FpaService.Persistence.Modules.Budgeting;

public static class DependencyInjection
{
    public static IServiceCollection AddBudgetingPersistenceContracts(this IServiceCollection services) => services;
}
