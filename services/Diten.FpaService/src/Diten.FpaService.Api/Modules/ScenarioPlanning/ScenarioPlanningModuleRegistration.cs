using Diten.FpaService.Application.Modules.ScenarioPlanning.GateI;
using Diten.Platform.Common.Authorization.Fu16;
using Microsoft.Extensions.DependencyInjection;
namespace Diten.FpaService.Api.Modules.ScenarioPlanning;
public static class ScenarioPlanningModuleRegistration
{
 public static IServiceCollection AddScenarioPlanningModuleForContractDiscovery(this IServiceCollection services)=>services
  .AddSingleton(ScenarioProviderOptions.Disabled)
  .AddSingleton<IScenarioReferenceSnapshotReader,UnavailableScenarioReferenceSnapshotReader>()
  .AddSingleton<IScenarioGateIContextAccessor,UnavailableScenarioGateIContextAccessor>()
  .AddSingleton<ScenarioReferenceValidationProvider>();

 public static IServiceCollection AddScenarioPlanningGateIFu16Evidence(
  this IServiceCollection services,Action<Fu16AuthenticationOptions> configure)
 {
  ArgumentNullException.ThrowIfNull(services);ArgumentNullException.ThrowIfNull(configure);
  services.AddAuthentication().AddFu16DelegatedActor(configure);
  services.AddScoped<IScenarioGateIContextAccessor,Fu16ScenarioGateIContextAccessor>();
  return services;
 }
}
