using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster;
using Diten.HcmService.Domain.Entities;
using Diten.HcmService.Domain.Repositories;
using Diten.HcmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.HcmService.Persistence;

public static class DependencyInjection
{
    private static bool _guidSerializerRegistered;
    private static bool _employeeDraftSessionMapRegistered;
    private static bool _employeeMapRegistered;
    private static bool _employmentRecordMapRegistered;

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        RegisterGuidSerializer();
        RegisterEmployeeDraftSessionMap();
        RegisterEmployeeMap();
        RegisterEmploymentRecordMap();

        var connectionString = configuration["Mongo:ConnectionString"]
            ?? throw new InvalidOperationException("Configuration error: 'Mongo:ConnectionString' is missing.");
        var databaseName = configuration["Mongo:DatabaseName"]
            ?? throw new InvalidOperationException("Configuration error: 'Mongo:DatabaseName' is missing.");

        var client = new MongoClient(MongoClientSettings.FromConnectionString(connectionString));

        services.AddSingleton<IMongoClient>(client);
        services.AddScoped<IMongoDatabase>(_ => client.GetDatabase(databaseName));
        services.AddScoped<IEmployeeDraftSessionRepository, EmployeeDraftSessionRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IEmployeeSmokeFixtureRepository, EmployeeSmokeFixtureRepository>();

        return services;
    }

    private static void RegisterGuidSerializer()
    {
        if (_guidSerializerRegistered)
        {
            return;
        }

        try
        {
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        }
        catch (BsonSerializationException)
        {
            // Another service/test host may have already registered the serializer in-process.
        }

        _guidSerializerRegistered = true;
    }

    private static void RegisterEmployeeDraftSessionMap()
    {
        if (_employeeDraftSessionMapRegistered)
        {
            return;
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(EmployeeDraftSession)))
        {
            BsonClassMap.RegisterClassMap<EmployeeDraftSession>(map =>
            {
                map.AutoMap();
                map.MapIdMember(session => session.Id)
                    .SetSerializer(new GuidSerializer(BsonType.String));
                map.GetMemberMap(session => session.TenantId)
                    .SetSerializer(new GuidSerializer(BsonType.String));
            });
        }

        _employeeDraftSessionMapRegistered = true;
    }

    private static void RegisterEmployeeMap()
    {
        if (_employeeMapRegistered)
        {
            return;
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(Employee)))
        {
            BsonClassMap.RegisterClassMap<Employee>(map =>
            {
                map.AutoMap();
                map.MapIdMember(employee => employee.Id)
                    .SetSerializer(new GuidSerializer(BsonType.String));
                map.GetMemberMap(employee => employee.TenantId)
                    .SetSerializer(new GuidSerializer(BsonType.String));
                map.GetMemberMap(employee => employee.PersonId)
                    .SetSerializer(new GuidSerializer(BsonType.String));
            });
        }

        _employeeMapRegistered = true;
    }

    private static void RegisterEmploymentRecordMap()
    {
        if (_employmentRecordMapRegistered)
        {
            return;
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(EmploymentRecord)))
        {
            BsonClassMap.RegisterClassMap<EmploymentRecord>(map =>
            {
                map.AutoMap();
                map.MapIdMember(record => record.Id)
                    .SetSerializer(new GuidSerializer(BsonType.String));
                map.GetMemberMap(record => record.TenantId)
                    .SetSerializer(new GuidSerializer(BsonType.String));
                map.GetMemberMap(record => record.EmployeeId)
                    .SetSerializer(new GuidSerializer(BsonType.String));
                map.GetMemberMap(record => record.LegalEntityId)
                    .SetSerializer(new GuidSerializer(BsonType.String));
                map.GetMemberMap(record => record.OrganizationUnitId)
                    .SetSerializer(new GuidSerializer(BsonType.String));
                map.GetMemberMap(record => record.PositionId)
                    .SetSerializer(new GuidSerializer(BsonType.String));
            });
        }

        _employmentRecordMapRegistered = true;
    }

}
