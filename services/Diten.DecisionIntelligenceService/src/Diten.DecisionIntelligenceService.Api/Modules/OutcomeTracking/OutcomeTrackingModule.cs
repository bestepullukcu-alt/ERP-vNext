namespace Diten.DecisionIntelligenceService.Api.Modules.OutcomeTracking;
public static class OutcomeTrackingModule { public static IReadOnlyList<string> DescribeCompileTimeComposition()=>[Application.Modules.OutcomeTracking.DependencyInjection.ModuleName,Persistence.Modules.OutcomeTracking.DependencyInjection.ModuleName,Infrastructure.Modules.OutcomeTracking.DependencyInjection.ModuleName]; }
