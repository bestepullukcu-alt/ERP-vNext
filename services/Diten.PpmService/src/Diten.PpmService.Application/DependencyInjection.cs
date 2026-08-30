using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Diten.PpmService.Application.Features.Initiatives;
using Diten.PpmService.Application.Features.Portfolios;
using Diten.PpmService.Application.Features.Programs;
using Diten.PpmService.Application.Features.Projects;
using Diten.PpmService.Application.Behaviors;
using Diten.PpmService.Application.Common;
using Diten.PpmService.Application.Features.InvestmentCases;
using Diten.PpmService.Application.Features.BenefitCommitments;

namespace Diten.PpmService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<PortfolioService>();
        services.AddScoped<InitiativeService>();
        services.AddScoped<ProgramService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<InvestmentCaseService>();
        services.AddScoped<BenefitCommitmentService>();
        services.AddScoped<IPpmAccessAuthorizer, PpmAccessAuthorizer>();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        return services;
    }
}
