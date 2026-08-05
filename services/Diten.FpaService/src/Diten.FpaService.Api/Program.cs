using Diten.FpaService.Api.Modules.Budgeting;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddBudgetingModuleForContractDiscovery();

// Bounded scaffold only: no listener, port binding, host build, process start, or runtime activation.
