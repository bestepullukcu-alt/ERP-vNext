using Diten.BuildingBlocks.Eventing;
using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Application.Features.ExternalContextReferences;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Persistence.Mongo;
using Diten.PpmService.Persistence.Repositories;
using Diten.PpmService.Persistence.GateI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.PpmService.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPpmPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        PpmBsonConfiguration.Configure();

        services.AddOptions<PpmMongoOptions>()
            .Bind(configuration.GetSection(PpmMongoOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "Mongo:ConnectionString is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.DatabaseName),
                "Mongo:DatabaseName is required.")
            .ValidateOnStart();

        services.AddSingleton<IMongoClient>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<PpmMongoOptions>>().Value;
            var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
            return new MongoClient(settings);
        });
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IOptions<PpmMongoOptions>>().Value;
            return provider.GetRequiredService<IMongoClient>().GetDatabase(options.DatabaseName);
        });

        services.AddScoped<PpmMongoContext>();
        services.AddScoped<IPortfolioRepository, PortfolioRepository>();
        services.AddScoped<IInitiativeRepository, InitiativeRepository>();
        services.AddScoped<IProgramRepository, ProgramRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IInvestmentCaseRepository, InvestmentCaseRepository>();
        services.AddScoped<IBenefitCommitmentRepository, BenefitCommitmentRepository>();
        services.AddScoped<IExternalContextReferenceLookup, ExternalContextReferenceLookup>();
        services.AddScoped<IAuditIntentRepository, AuditIntentRepository>();
        services.AddScoped<PpmEventOutboxStore>();
        services.AddScoped<IEventOutboxWriter>(provider =>
            provider.GetRequiredService<PpmEventOutboxStore>());
        services.AddScoped<IEventOutboxStore>(provider =>
            provider.GetRequiredService<PpmEventOutboxStore>());
        services.AddScoped<IPpmUnitOfWork, PpmUnitOfWork>();
        services.AddScoped<IGateICompositionPersistenceBoundary, GateICompositionPersistenceBoundary>();
        services.AddScoped<IGateIRelationshipMutationPersistence, GateIRelationshipMutationPersistence>();
        services.AddSingleton<IGateIRelationshipMutationFaultProbe, NullGateIRelationshipMutationFaultProbe>();
        services.AddSingleton<IGateIRelationshipTransportMetadataProvider, UnavailableGateIRelationshipTransportMetadataProvider>();
        services.AddHostedService<GateIMutationReceiptIndexInitializer>();
        services.AddHostedService<PpmMongoIndexInitializer>();
        services.AddHealthChecks()
            .AddCheck<PpmMongoTransactionHealthCheck>("ppm-mongo-transactions");

        return services;
    }
}
