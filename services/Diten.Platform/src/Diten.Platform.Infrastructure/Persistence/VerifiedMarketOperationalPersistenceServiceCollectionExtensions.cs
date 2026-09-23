using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Events;
using Diten.Platform.Application.Features.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Migrations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Repositories.BusinessReferenceData;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Diten.Platform.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence;

public static class VerifiedMarketOperationalPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddVerifiedMarketOperationalPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration["MongoDbSettings:ConnectionString"];
        var databaseName = configuration["MongoDbSettings:DatabaseName"];
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("VERIFIED_MARKET_OPERATIONAL_MONGO_CONFIGURATION_MISSING");
        }

        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        BsonSerializer.TryRegisterSerializer(new DecimalSerializer(BsonType.Decimal128));

        var mongoSettings = MongoClientSettings.FromConnectionString(connectionString);
        mongoSettings.GuidRepresentation = GuidRepresentation.Standard;
        var mongoClient = new MongoClient(mongoSettings);
        var database = mongoClient.GetDatabase(databaseName.Trim());

        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddSingleton<IMongoClient>(mongoClient);
        services.AddSingleton<IMongoDatabase>(database);
        services.AddSingleton<IPlatformDbContext>(new PlatformDbContext(mongoClient, database));
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();

        services.Configure<BusinessReferenceDataProviderOptions>(
            configuration.GetSection(BusinessReferenceDataProviderOptions.SectionName));
        services.AddScoped<IBusinessReferenceDataStewardshipRepository, BusinessReferenceDataStewardshipRepository>();
        services.AddScoped<IBusinessReferenceDataValidationService, BusinessReferenceDataValidationService>();
        services.AddScoped<IBusinessReferenceDataPublicationEligibility, RuntimeBusinessReferenceDataPublicationEligibility>();
        services.AddScoped<IBusinessReferenceDataPublishCheckpointObserver, NoOpBusinessReferenceDataPublishCheckpointObserver>();
        services.AddScoped<IBusinessReferenceDataEvidenceAdapter, DefaultBusinessReferenceDataEvidenceAdapter>();
        services.AddScoped<VerifiedMarketOperationalGovernanceAuditAdapter>();
        services.AddScoped<IBusinessReferenceDataGovernanceAuditAdapter>(provider =>
            provider.GetRequiredService<VerifiedMarketOperationalGovernanceAuditAdapter>());
        services.AddScoped<IBusinessReferenceDataEventPublisher, VerifiedMarketOperationalNoWriteEventPublisher>();
        services.AddScoped<IBusinessReferenceDataPostPublicationReviewHook, DisabledBusinessReferenceDataPostPublicationReviewHook>();
        services.AddScoped<IBusinessReferenceDataPublishService, BusinessReferenceDataPublishService>();
        services.AddScoped<IBusinessReferenceDataCatalogLoaderService, BusinessReferenceDataCatalogLoaderService>();

        services.AddSingleton<ISensitiveFieldRedactionRegistry, SensitiveFieldRedactionRegistry>();
        services.AddSingleton<ISensitiveFieldRedactor, SensitiveFieldRedactor>();
        services.AddSingleton<IAuditIdempotencyKeyBuilder, AuditIdempotencyKeyBuilder>();
        services.AddSingleton<IAuditRecursionGuard, AuditRecursionGuard>();
        services.AddScoped<AuditOutboxTemporalMigrationRepository>();
        services.AddScoped<AuditOutboxRepository>(provider =>
            new AuditOutboxRepository(
                provider.GetRequiredService<IPlatformDbContext>(),
                provider.GetRequiredService<AuditOutboxTemporalMigrationRepository>()));
        services.AddScoped<IAuditOutboxWriter>(provider => provider.GetRequiredService<AuditOutboxRepository>());
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IVerifiedMarketOperationalPreflight, VerifiedMarketOperationalPreflight>();

        return services;
    }
}

public sealed class VerifiedMarketOperationalNoWriteEventPublisher : IBusinessReferenceDataEventPublisher
{
    private static readonly Guid LockedReferenceTenantId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");
    private bool _accepted;

    public Task PublishAsync(
        EventEnvelope envelope,
        string idempotencyKey,
        Guid versionId,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ct.ThrowIfCancellationRequested();

        if (_accepted
            || envelope.EventId == Guid.Empty
            || !string.Equals(envelope.EventName, "reference_data.version_published.v1", StringComparison.Ordinal)
            || envelope.EventVersion != 1
            || envelope.TenantId != LockedReferenceTenantId
            || envelope.CorrelationId == Guid.Empty
            || envelope.CausationId != envelope.CorrelationId
            || !string.Equals(envelope.Producer, "BusinessReferenceData", StringComparison.Ordinal)
            || envelope.Actor is null
            || string.IsNullOrWhiteSpace(envelope.Actor.UserId)
            || !string.Equals(envelope.Actor.ActorType, "user", StringComparison.Ordinal)
            || envelope.Payload is null
            || versionId == Guid.Empty
            || string.IsNullOrWhiteSpace(idempotencyKey)
            || !idempotencyKey.EndsWith(
                ":businessreferencedata-catalog-vunsd-m49-2026-08-08:market",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("VERIFIED_MARKET_OPERATIONAL_EVENT_SCOPE_VIOLATION");
        }

        _accepted = true;
        return Task.CompletedTask;
    }
}
