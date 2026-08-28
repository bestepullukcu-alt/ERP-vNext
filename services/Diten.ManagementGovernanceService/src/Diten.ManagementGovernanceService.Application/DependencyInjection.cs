using Microsoft.Extensions.DependencyInjection;
using MediatR;
using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using Diten.ManagementGovernanceService.Application.Features.Dws.Queries;
using Diten.ManagementGovernanceService.Application.Features.Dws.Validators;

namespace Diten.ManagementGovernanceService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddDwsApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.ExceptionBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Behaviors.PerformanceBehavior<,>));
        services.AddTransient<IDwsFunctionalValidator<CreateStructureCommand>, CreateStructureValidator>();
        services.AddTransient<IDwsFunctionalValidator<UpdateStructureMetadataCommand>, UpdateStructureMetadataValidator>();
        services.AddTransient<IDwsFunctionalValidator<AddStructureNodeCommand>, AddStructureNodeValidator>();
        services.AddTransient<IDwsFunctionalValidator<MoveStructureNodeCommand>, MoveStructureNodeValidator>();
        services.AddTransient<IDwsFunctionalValidator<ReorderStructureNodeCommand>, ReorderStructureNodeValidator>();
        services.AddTransient<IDwsFunctionalValidator<RemoveStructureNodeCommand>, RemoveStructureNodeValidator>();
        services.AddTransient<IDwsFunctionalValidator<AddStructuralDependencyCommand>, AddStructuralDependencyValidator>();
        services.AddTransient<IDwsFunctionalValidator<RemoveStructuralDependencyCommand>, RemoveStructuralDependencyValidator>();
        services.AddTransient<IDwsFunctionalValidator<CreateStructureBaselineCommand>, CreateStructureBaselineValidator>();
        services.AddTransient<IDwsFunctionalValidator<CreateNextStructureRevisionCommand>, CreateNextStructureRevisionValidator>();
        services.AddTransient<IDwsFunctionalValidator<GetStructureByIdQuery>, GetStructureByIdValidator>();
        services.AddTransient<IDwsFunctionalValidator<GetStructureTreeQuery>, GetStructureTreeValidator>();
        services.AddTransient<IDwsFunctionalValidator<ValidateStructureQuery>, ValidateStructureValidator>();
        services.AddTransient<IDwsFunctionalValidator<CompareStructureRevisionsQuery>, CompareStructureRevisionsValidator>();
        services.AddTransient<IDwsFunctionalValidator<CompareStructureBaselinesQuery>, CompareStructureBaselinesValidator>();
        return services;
    }
}
