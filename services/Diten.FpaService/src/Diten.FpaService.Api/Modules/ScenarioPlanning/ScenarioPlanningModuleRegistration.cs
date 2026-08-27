using Diten.FpaService.Application.Modules.ScenarioPlanning.GateI;
using Microsoft.Extensions.DependencyInjection;
namespace Diten.FpaService.Api.Modules.ScenarioPlanning;
public static class ScenarioPlanningModuleRegistration
{
 public static IServiceCollection AddScenarioPlanningModuleForContractDiscovery(this IServiceCollection services)=>services
  .AddSingleton(ScenarioProviderOptions.Disabled)
  .AddSingleton<IScenarioReferenceSnapshotReader,UnavailableScenarioReferenceSnapshotReader>()
  .AddSingleton<IScenarioGateIContextAccessor,UnavailableScenarioGateIContextAccessor>()
  .AddSingleton<ScenarioReferenceValidationProvider>();
}
