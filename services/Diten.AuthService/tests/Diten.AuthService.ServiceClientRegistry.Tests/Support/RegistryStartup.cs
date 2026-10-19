using System.Collections.Concurrent;
using Diten.AuthService.Api.Services.ServiceIdentityTokens;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Repositories;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.ServiceClientRegistry.Tests.Support;

/// <summary>
/// Runs the PRODUCTION registry wiring (<see cref="ServiceClientRegistryStartup.AddServiceClientRegistry"/> + its
/// hosted service) over the production repositories and audit writer, against the owned mongod. Nothing of the
/// registry is re-implemented here: one "Auth startup" is exactly what Program.cs registers for it.
/// </summary>
public static class RegistryStartup
{
    public sealed record Declared(
        string ClientCode,
        string CredentialHash,
        Guid[] Tenants,
        string CredentialVersion = "v1",
        string ServiceName = "Diten.MDM",
        string AllowedAudience = "TRUSTED_WORKFLOW_CONSUMER");

    public static IConfiguration Configuration(params Declared[] clients)
    {
        var values = new Dictionary<string, string?>();
        for (var index = 0; index < clients.Length; index++)
        {
            var prefix = $"ServiceClientRegistry:Clients:{index}:";
            values[prefix + "ClientCode"] = clients[index].ClientCode;
            values[prefix + "ServiceName"] = clients[index].ServiceName;
            values[prefix + "AllowedAudience"] = clients[index].AllowedAudience;
            values[prefix + "CredentialHash"] = clients[index].CredentialHash;
            values[prefix + "CredentialVersion"] = clients[index].CredentialVersion;
            for (var tenant = 0; tenant < clients[index].Tenants.Length; tenant++)
                values[prefix + $"Tenants:{tenant}"] = clients[index].Tenants[tenant].ToString("D");
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    /// <summary>One Auth startup's worth of registry work. Returns what was logged (level + rendered text).</summary>
    public static async Task<IReadOnlyList<(LogLevel Level, string Text)>> RunAsync(
        IMongoDatabase database, IConfiguration configuration)
    {
        var log = new CapturingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(log));
        services.AddSingleton(database);
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IServiceClientIdentityRepository, ServiceClientIdentityRepository>();
        services.AddScoped<IServiceClientTenantGrantRepository, ServiceClientTenantGrantRepository>();
        services.AddScoped<IAuthAuditService, AuthAuditService>();
        services.AddServiceClientRegistry(configuration);
        await using var provider = services.BuildServiceProvider(validateScopes: true);
        foreach (var hosted in provider.GetServices<IHostedService>())
        {
            await hosted.StartAsync(CancellationToken.None);
        }

        return log.Entries.ToArray();
    }

    public static string NewHash() => new ServiceClientCredentialVerifier().Hash("test-only-" + Guid.NewGuid().ToString("N"));

    public static string NewClientCode() => "registry-test-" + Guid.NewGuid().ToString("N");

    /// <summary>Every document the registry could have written for these client codes and tenants, as canonical JSON.</summary>
    public static async Task<string[]> SnapshotAsync(IMongoDatabase database, string[] clientCodes, Guid[] tenantIds)
    {
        var identities = await database.GetCollection<BsonDocument>("serviceClientIdentities")
            .Find(Builders<BsonDocument>.Filter.In("ClientCode", clientCodes)).ToListAsync();
        var identityIds = identities.Select(document => document["_id"]).ToArray();
        var grants = await database.GetCollection<BsonDocument>("serviceClientTenantGrants")
            .Find(Builders<BsonDocument>.Filter.In("ServiceClientIdentityId", identityIds)).ToListAsync();
        var audits = (await database.GetCollection<AuthAuditLog>("authAuditLogs")
                .Find(Builders<AuthAuditLog>.Filter.Empty).ToListAsync())
            .Where(entry => clientCodes.Any(code => entry.Metadata.Contains(code, StringComparison.Ordinal))
                || tenantIds.Contains(entry.TenantId))
            .Select(entry => entry.ToBsonDocument());
        return identities.Select(document => "identity " + document.ToJson())
            .Concat(grants.Select(document => "grant " + document.ToJson()))
            .Concat(audits.Select(document => "audit " + document.ToJson()))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, string Text)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Capture(Entries);

        public void Dispose()
        {
        }

        private sealed class Capture(ConcurrentQueue<(LogLevel, string)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                entries.Enqueue((logLevel, formatter(state, exception) + (exception is null ? "" : " " + exception)));
        }
    }
}
