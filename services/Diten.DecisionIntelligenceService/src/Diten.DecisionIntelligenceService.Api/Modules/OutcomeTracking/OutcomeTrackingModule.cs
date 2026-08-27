using Diten.DecisionIntelligenceService.Application.Modules.OutcomeTracking.GateI;
using Microsoft.Extensions.DependencyInjection;
namespace Diten.DecisionIntelligenceService.Api.Modules.OutcomeTracking;
public static class OutcomeTrackingModule
{
 public static IReadOnlyList<string> DescribeCompileTimeComposition()=>[Application.Modules.OutcomeTracking.DependencyInjection.ModuleName,Persistence.Modules.OutcomeTracking.DependencyInjection.ModuleName,Infrastructure.Modules.OutcomeTracking.DependencyInjection.ModuleName];
 public static IServiceCollection AddOutcomeTrackingGateILocalProvider(this IServiceCollection services)=>services
  .AddSingleton(OutcomeProviderOptions.Disabled)
  .AddSingleton<IOutcomeReferenceSnapshotReader,UnavailableOutcomeReferenceSnapshotReader>()
  .AddSingleton<IOutcomeGateIContextAccessor,UnavailableOutcomeGateIContextAccessor>()
  .AddSingleton<OutcomeReferenceValidationProvider>();
}
