using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.ServiceIdentityTokens;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Commands;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Handlers.CommandHandlers;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.ServiceClientRegistry.Tests.Support;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.AuthService.ServiceClientRegistry.Tests;

/// <summary>
/// WP-GP-AUDIT-TRAIL-01 D3 — the token request's security event against the PRODUCTION writer and a real (test-owned)
/// mongod: one <c>authAuditLogs</c> row per (client, tenant, audience, UTC day), a coded row per refusal, nothing secret
/// in the stored document, and tenants kept apart.
/// </summary>
[Collection(OwnedMongoCollection.Name)]
public sealed class ServiceIdentityTokenAuditMongoTests(OwnedMongo mongo)
{
    private const string Audience = "TRUSTED_AUDIT_SOURCE_INGEST";
    private const string Secret = "token-audit-test-credential";

    private IMongoCollection<AuthAuditLog> Audits => mongo.Database.GetCollection<AuthAuditLog>("authAuditLogs");

    [Fact]
    public async Task Issued_tokens_leave_one_row_per_client_tenant_audience_and_day_with_nothing_secret()
    {
        var (code, identity) = await SeedIdentityAsync();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await SeedGrantAsync(identity, tenantA);
        await SeedGrantAsync(identity, tenantB);
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 3, 8, 0, 0, TimeSpan.Zero));
        var issuer = new DistinctIssuer();
        var handler = Handler(issuer, clock);

        for (var i = 0; i < 5; i++)
            Assert.True((await handler.Handle(new IssueServiceIdentityTokenCommand(code, Secret, tenantA, Audience), CancellationToken.None)).IsSuccessful);
        Assert.True((await handler.Handle(new IssueServiceIdentityTokenCommand(code, Secret, tenantB, Audience), CancellationToken.None)).IsSuccessful);

        var rows = await RowsForAsync(code);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(ServiceIdentityTokenAudit.IssuedEvent, row.EventName));
        Assert.Single(rows, row => row.TenantId == tenantA);
        Assert.Single(rows, row => row.TenantId == tenantB);

        // The stored document, every field: no credential, no hash of it, no issued token.
        var stored = (await Audits.Find(Builders<AuthAuditLog>.Filter.In(x => x.Id, rows.Select(r => r.Id))).ToListAsync())
            .Select(row => row.ToBsonDocument().ToJson()).ToList();
        var hash = new ServiceClientCredentialVerifier().Hash(Secret);
        foreach (var json in stored)
        {
            Assert.DoesNotContain(Secret, json, StringComparison.Ordinal);
            Assert.DoesNotContain(hash, json, StringComparison.Ordinal);
            Assert.All(issuer.Issued, token => Assert.DoesNotContain(token, json, StringComparison.Ordinal));
        }

        clock.Now = clock.Now.AddDays(1);
        Assert.True((await handler.Handle(new IssueServiceIdentityTokenCommand(code, Secret, tenantA, Audience), CancellationToken.None)).IsSuccessful);
        Assert.Equal(3, (await RowsForAsync(code)).Count);
    }

    [Fact]
    public async Task A_refusal_leaves_one_coded_row_per_day()
    {
        var (code, _) = await SeedIdentityAsync();
        var tenant = Guid.NewGuid(); // no grant
        var handler = Handler(new DistinctIssuer(), new FixedClock(new DateTimeOffset(2026, 10, 3, 8, 0, 0, TimeSpan.Zero)));

        for (var i = 0; i < 3; i++)
            Assert.Equal(403, (await handler.Handle(new IssueServiceIdentityTokenCommand(code, Secret, tenant, Audience), CancellationToken.None)).StatusCode);

        var row = Assert.Single(await RowsForAsync(code));
        Assert.Equal(ServiceIdentityTokenAudit.DeniedEvent, row.EventName);
        Assert.Equal(tenant, row.TenantId);
        Assert.Contains($"\"result\":\"{ServiceIdentityTokenAudit.Results.TenantGrantMissing}\"", row.Metadata, StringComparison.Ordinal);
    }

    private IssueServiceIdentityTokenHandler Handler(IServiceIdentityTokenIssuer issuer, TimeProvider clock) => new(
        new ServiceClientIdentityRepository(mongo.Database),
        new ServiceClientTenantGrantRepository(mongo.Database),
        new ServiceClientCredentialVerifier(),
        issuer,
        new AuthAuditService(mongo.Database),
        clock);

    private async Task<(string Code, ServiceClientIdentity Identity)> SeedIdentityAsync()
    {
        var code = "token-audit-" + Guid.NewGuid().ToString("N")[..12];
        var identity = new ServiceClientIdentity
        {
            ClientCode = code,
            ServiceName = "Diten.MDM",
            ActiveCredentialHash = new ServiceClientCredentialVerifier().Hash(Secret),
            ActiveCredentialVersion = "v1"
        };
        await mongo.Database.GetCollection<ServiceClientIdentity>("serviceClientIdentities").InsertOneAsync(identity);
        return (code, identity);
    }

    private Task SeedGrantAsync(ServiceClientIdentity identity, Guid tenantId) =>
        mongo.Database.GetCollection<ServiceClientTenantGrant>("serviceClientTenantGrants").InsertOneAsync(new ServiceClientTenantGrant
        {
            TenantId = tenantId,
            ServiceClientIdentityId = identity.Id,
            Audience = Audience,
            IsEnabled = true
        });

    private async Task<List<AuthAuditLog>> RowsForAsync(string code) =>
        (await Audits.Find(x => x.EventName == ServiceIdentityTokenAudit.IssuedEvent || x.EventName == ServiceIdentityTokenAudit.DeniedEvent)
            .ToListAsync())
        .Where(x => x.Metadata.Contains("\"clientCode\":\"" + code + "\"", StringComparison.Ordinal))
        .ToList();

    private sealed class DistinctIssuer : IServiceIdentityTokenIssuer
    {
        public List<string> Issued { get; } = [];

        public ServiceIdentityTokenIssue Issue(Guid clientId, string serviceName, Guid tenantId, string audience)
        {
            var token = "eyJ" + Guid.NewGuid().ToString("N");
            Issued.Add(token);
            return new ServiceIdentityTokenIssue(token, DateTimeOffset.UtcNow.AddMinutes(5), 300);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
