using System.Reflection;
using Diten.Application.DeliveryExecutionManagement.Shared;
using Diten.Application.EnterpriseStrategy.Adapters.Ppm;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Application.Services.Decomposition;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using FluentValidation;

namespace Diten.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddScoped<IDecompositionService, DecompositionService>();
        services.AddScoped<IEnterpriseStrategyAuthorizationService, DefaultEnterpriseStrategyAuthorizationService>();
        services.AddScoped<IDeliveryExecutionManagementAuthorizationService, DefaultDeliveryExecutionManagementAuthorizationService>();
        services.AddScoped<NoOpEnterpriseStrategyAuditStore>();
        services.AddScoped<IEnterpriseStrategyAuditSink>(sp => sp.GetRequiredService<NoOpEnterpriseStrategyAuditStore>());
        services.AddScoped<IEnterpriseStrategyAuditStore>(sp => sp.GetRequiredService<NoOpEnterpriseStrategyAuditStore>());
        services.AddScoped<IEnterpriseStrategyObservability, LoggerEnterpriseStrategyObservability>();
        services.AddScoped<ICorrelationContextAccessor, InMemoryCorrelationContextAccessor>();
        services.AddScoped<MockPpmInitiativeReadAdapter>();
        services.AddScoped<IPpmInitiativeReadAdapter, ResilientPpmInitiativeReadAdapter>();
        services.AddScoped<MockPpmProjectReadAdapter>();
        services.AddScoped<IPpmProjectReadAdapter, ResilientPpmProjectReadAdapter>();
        services.AddScoped<IMetricCatalogService, MockMetricCatalogService>();
        services.AddScoped<IAuditEvidenceService, MockAuditEvidenceService>();
        services.AddScoped<IEnterpriseStrategyNormalizationService, EnterpriseStrategyNormalizationService>();
        services.AddScoped<IGoalService, GoalService>();
        services.AddScoped<IObjectiveService, ObjectiveService>();
        services.AddScoped<IConnectionService, ConnectionService>();
        services.AddScoped<IPlanningCycleService, PlanningCycleService>();
        services.AddScoped<IInitiativeOrchestrationService, InitiativeOrchestrationService>();
        services.AddScoped<IProjectOrchestrationService, ProjectOrchestrationService>();
        services.AddScoped<KpiScorecardService>();
        services.AddScoped<IKpiRuntimeService>(sp => sp.GetRequiredService<KpiScorecardService>());
        services.AddScoped<IKpiLibraryService>(sp => sp.GetRequiredService<KpiScorecardService>());
        services.AddScoped<StrategyLibraryService>();
        services.AddScoped<IStrategyLibraryImportService>(sp => sp.GetRequiredService<StrategyLibraryService>());
        services.AddScoped<IStrategyLibraryQueryService>(sp => sp.GetRequiredService<StrategyLibraryService>());
        services.AddScoped<IStrategyLibraryGovernanceService>(sp => sp.GetRequiredService<StrategyLibraryService>());
        services.AddScoped<IStrategyInstantiationService>(sp => sp.GetRequiredService<StrategyLibraryService>());
        services.AddScoped<IStrategyLibraryUsageService>(sp => sp.GetRequiredService<StrategyLibraryService>());
        return services;
    }
}
