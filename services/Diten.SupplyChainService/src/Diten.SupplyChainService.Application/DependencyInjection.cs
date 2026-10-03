using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Behaviors;
namespace Diten.SupplyChainService.Application;
public static class DependencyInjection
{
    // Q236 (2026-10-03): MediatR must not register handlers of feature modules that Program.cs does not compose.
    // Their repositories and reference readers are unregistered, so in Development the container validation at
    // builder.Build() fails for them and takes Shipments, Carriers and Loads down too (Q217, Q232).
    // Excluded today: Returns (MOD-0186), Claims (MOD-0187), SandopPlans (MOD-0190), CapacityPlans (MOD-0192).
    // Remove an entry only when Program.cs registers that module's persistence AND its readers (Q209).
    // Validators are not filtered: they have no constructor dependencies and resolve on their own.
    private static readonly string[] MediatRExcludedFeatureNamespaces =
    [
        // Returns and Claims were removed from this list on 2026-10-03 (Q271/Q272): Program.cs now
        // calls AddReturnPersistence() / AddClaimPersistence() and registers their reference readers,
        // which is the condition this comment sets for removing an entry. CapacityPlans was
        // removed and PUT BACK the same day: K3 caught that CapacityRepository depends on
        // IDemandFixtureReader, the same test-only seam that blocks SandopPlans. One missing production
        // reader blocks both modules — see ledger Q273.
        "Diten.SupplyChainService.Application.Features.SandopPlans",
        "Diten.SupplyChainService.Application.Features.CapacityPlans",
    ];

    private static bool IsComposedFeatureType(Type type) =>
        type.Namespace is not { } ns || !MediatRExcludedFeatureNamespaces.Any(excluded =>
            ns == excluded || ns.StartsWith(excluded + ".", StringComparison.Ordinal));

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<RequestContext>(); services.AddMediatR(c => { c.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly); c.TypeEvaluator = IsComposedFeatureType; });
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>)); return services;
    }
}
