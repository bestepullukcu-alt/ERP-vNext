using Diten.FpaService.Api.Modules.Budgeting;
using Diten.FpaService.Api.Modules.ScenarioPlanning;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddBudgetingModuleForContractDiscovery();
services.AddScenarioPlanningModuleForContractDiscovery();

// Bounded scaffold only: no listener, port binding, host build, process start, or runtime activation.
