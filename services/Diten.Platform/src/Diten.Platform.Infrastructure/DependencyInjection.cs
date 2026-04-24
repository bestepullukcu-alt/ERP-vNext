using System.Text;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Settings;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Services.Http;
using Diten.Platform.Common.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSecret = configuration["JwtSettings:Secret"]
            ?? throw new InvalidOperationException("Configuration error: 'JwtSettings:Secret' is missing in appsettings.json.");
        var jwtIssuer = configuration["JwtSettings:Issuer"]
            ?? throw new InvalidOperationException("Configuration error: 'JwtSettings:Issuer' is missing in appsettings.json.");
        var jwtAudience = configuration["JwtSettings:Audience"]
            ?? throw new InvalidOperationException("Configuration error: 'JwtSettings:Audience' is missing in appsettings.json.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("PlatformActor", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("actor_type", "platform_admin", "partner_admin");
            });
        });
        services.AddHttpContextAccessor();
        services.Configure<TenantManagementOptions>(configuration.GetSection(TenantManagementOptions.SectionName));

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddScoped<ITenantDefaultsProvider, TenantDefaultsProvider>();
        services.AddTransient<TenantPropagationHandler>();
        services.AddHttpClient("TenantAwareClient").AddHttpMessageHandler<TenantPropagationHandler>();

        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        var connectionString = configuration["MongoDbSettings:ConnectionString"]
            ?? throw new InvalidOperationException("Configuration error: 'MongoDbSettings:ConnectionString' is missing in appsettings.json.");
        var databaseName = configuration["MongoDbSettings:DatabaseName"]
            ?? throw new InvalidOperationException("Configuration error: 'MongoDbSettings:DatabaseName' is missing in appsettings.json.");

        var mongoSettings = new MongoDbSettings
        {
            ConnectionString = connectionString,
            DatabaseName = databaseName
        };

        services.AddSingleton(mongoSettings);
        var mongoClientSettings = MongoClientSettings.FromConnectionString(mongoSettings.ConnectionString);
        mongoClientSettings.GuidRepresentation = GuidRepresentation.Standard;
        var mongoClient = new MongoClient(mongoClientSettings);
        var database = mongoClient.GetDatabase(mongoSettings.DatabaseName);

        services.AddSingleton<IMongoClient>(mongoClient);
        services.AddSingleton<IPlatformDbContext>(new PlatformDbContext(mongoClient, database));
        services.AddScoped<IMongoDatabase>(_ => database);
        services.AddScoped<ISavedViewRepository, SavedViewRepository>();
        services.AddScoped<ITenantRegistryRepository, TenantRegistryRepository>();

        LegacySavedViewMigration.MigrateAsync(database).GetAwaiter().GetResult();
        MongoDbIndexConfigurations.EnsureIndexesAsync(database).GetAwaiter().GetResult();

        return services;
    }
}
