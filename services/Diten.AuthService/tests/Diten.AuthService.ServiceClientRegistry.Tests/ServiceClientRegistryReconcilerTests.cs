using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Registry;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.ServiceClientRegistry.Tests;

/// <summary>
/// MOD-0033-FU02 §F.1 — the reconciler's own gate: it must not even ATTEMPT a write for something that already
/// exists. The repositories here are deliberately permissive (a create on an existing key OVERWRITES), so these
/// tests stand on the reconciler alone and do not lean on the insert-only repositories or the unique indexes —
/// those have their own tests against a real mongod (<see cref="ServiceClientRegistryMongoTests"/>).
/// </summary>
public sealed class ServiceClientRegistryReconcilerTests
{
    private const string Audience = "TRUSTED_WORKFLOW_CONSUMER";
    private static readonly Guid Tenant = Guid.Parse("7a1f0c52-6f0e-4a55-9c11-0d6b8c3e9a01");

    [Fact]
    public async Task Apply_WhenIdentityExistsWithOtherCredential_AttemptsNoWriteAndReportsFieldNamesOnly()
    {
        var world = new World();
        var existing = world.SeedIdentity("client-a", hash: "persisted-hash", version: "persisted-version");
        var declaration = new ServiceClientDeclaration("client-a", "Diten.MDM", Audience, "declared-hash", "declared-version", []);

        var notices = await world.Reconciler.ApplyAsync([declaration], CancellationToken.None);

        Assert.Equal(0, world.Identities.CreateAttempts);
        Assert.Equal("persisted-hash", world.Identities.Documents["client-a"].ActiveCredentialHash);
        Assert.Equal("persisted-version", world.Identities.Documents["client-a"].ActiveCredentialVersion);
        Assert.Same(existing, world.Identities.Documents["client-a"]);
        var notice = Assert.Single(notices);
        Assert.Equal(ServiceClientRegistryOutcome.IdentityDiffers, notice.Outcome);
        Assert.Equal(["CredentialHash", "CredentialVersion"], notice.Fields);
        Assert.Empty(world.Audit.Events);
    }

    [Fact]
    public async Task Apply_WhenGrantIsDisabled_AttemptsNoWriteAndLeavesItDisabled()
    {
        var world = new World();
        var identity = world.SeedIdentity("client-b", hash: "same-hash", version: "v1");
        var grant = world.SeedGrant(identity, Tenant, enabled: false);
        var declaration = new ServiceClientDeclaration("client-b", "Diten.MDM", Audience, "same-hash", "v1", [Tenant]);

        var notices = await world.Reconciler.ApplyAsync([declaration], CancellationToken.None);

        Assert.Equal(0, world.Grants.CreateAttempts);
        Assert.False(world.Grants.Documents.Single().IsEnabled);
        Assert.Same(grant, world.Grants.Documents.Single());
        Assert.Contains(notices, notice => notice.Outcome == ServiceClientRegistryOutcome.GrantDisabled && notice.TenantId == Tenant);
        Assert.Empty(world.Audit.Events);
    }

    [Theory]
    [InlineData("Diten.Other", Audience, "ServiceName")]
    [InlineData("Diten.MDM", "TRUSTED_AUDIT_SOURCE_INGEST", "AllowedAudience")]
    public async Task Apply_WhenPersistedClientHasAnotherPurpose_CreatesNoGrantForIt(
        string persistedService, string persistedAudience, string differingField)
    {
        var world = new World();
        world.SeedIdentity("client-c", hash: "same-hash", version: "v1", service: persistedService, audience: persistedAudience);
        var declaration = new ServiceClientDeclaration("client-c", "Diten.MDM", Audience, "same-hash", "v1", [Tenant]);

        var notices = await world.Reconciler.ApplyAsync([declaration], CancellationToken.None);

        Assert.Empty(world.Grants.Documents);
        Assert.Equal(0, world.Grants.CreateAttempts);
        Assert.Contains(notices, notice => notice.Outcome == ServiceClientRegistryOutcome.IdentityDiffers
            && notice.Fields.SequenceEqual([differingField]));
        Assert.Contains(notices, notice => notice.Outcome == ServiceClientRegistryOutcome.GrantsSkipped);
    }

    [Theory]
    [InlineData(true, false, ServiceClientRegistryOutcome.IdentityRevoked)]
    [InlineData(false, true, ServiceClientRegistryOutcome.IdentityDeleted)]
    public async Task Apply_WhenIdentityIsRevokedOrDeleted_NeitherRestoresItNorGrantsIt(
        bool revoked, bool deleted, ServiceClientRegistryOutcome expected)
    {
        var world = new World();
        var identity = world.SeedIdentity("client-d", hash: "same-hash", version: "v1");
        identity.IsRevoked = revoked;
        identity.IsDeleted = deleted;
        var declaration = new ServiceClientDeclaration("client-d", "Diten.MDM", Audience, "same-hash", "v1", [Tenant]);

        var notices = await world.Reconciler.ApplyAsync([declaration], CancellationToken.None);

        Assert.Equal(0, world.Identities.CreateAttempts);
        Assert.Equal(0, world.Grants.CreateAttempts);
        Assert.Equal(revoked, world.Identities.Documents["client-d"].IsRevoked);
        Assert.Equal(deleted, world.Identities.Documents["client-d"].IsDeleted);
        Assert.Contains(notices, notice => notice.Outcome == expected);
    }

    [Fact]
    public async Task Apply_WhenNothingExists_CreatesIdentityWithTheCodeDerivedIdAndAuditsWithoutTheHash()
    {
        var world = new World();
        var declaration = new ServiceClientDeclaration("client-e", "Diten.MDM", Audience, "declared-hash", "v7", [Tenant]);

        await world.Reconciler.ApplyAsync([declaration], CancellationToken.None);

        var identity = world.Identities.Documents["client-e"];
        Assert.Equal(ServiceClientIdentityIds.FromClientCode("client-e"), identity.Id);
        Assert.Equal(ServiceClientRegistryReconciler.SystemActor, identity.CreatedBy);
        Assert.Equal("declared-hash", identity.ActiveCredentialHash);
        Assert.Null(identity.PreviousCredentialHash);
        Assert.False(identity.IsRevoked);
        var grant = Assert.Single(world.Grants.Documents);
        Assert.True(grant.IsEnabled);
        Assert.Equal((Tenant, identity.Id, Audience), (grant.TenantId, grant.ServiceClientIdentityId, grant.Audience));
        Assert.Equal(
            [ServiceClientRegistryReconciler.IdentityRegisteredEvent, ServiceClientRegistryReconciler.TenantGrantedEvent],
            world.Audit.Events.Select(entry => entry.Event));
        Assert.All(world.Audit.Events, entry =>
        {
            Assert.Null(entry.UserId);
            Assert.Contains("client-e", entry.Metadata, StringComparison.Ordinal);
            Assert.DoesNotContain("declared-hash", entry.Metadata, StringComparison.Ordinal);
        });
        Assert.Equal(Tenant, world.Audit.Events[1].TenantId);
    }

    private sealed class World
    {
        public World() => Reconciler = new ServiceClientRegistryReconciler(
            Identities, Grants, Audit, TimeProvider.System, NullLogger<ServiceClientRegistryReconciler>.Instance);

        public PermissiveIdentities Identities { get; } = new();
        public PermissiveGrants Grants { get; } = new();
        public RecordingAudit Audit { get; } = new();
        public ServiceClientRegistryReconciler Reconciler { get; }

        public ServiceClientIdentity SeedIdentity(
            string code, string hash, string version, string service = "Diten.MDM", string? audience = Audience)
        {
            var identity = new ServiceClientIdentity
            {
                ClientCode = code, ServiceName = service, AllowedAudience = audience,
                ActiveCredentialHash = hash, ActiveCredentialVersion = version
            };
            Identities.Documents[code] = identity;
            return identity;
        }

        public ServiceClientTenantGrant SeedGrant(ServiceClientIdentity identity, Guid tenantId, bool enabled)
        {
            var grant = new ServiceClientTenantGrant
            {
                TenantId = tenantId, ServiceClientIdentityId = identity.Id, Audience = Audience, IsEnabled = enabled
            };
            Grants.Documents.Add(grant);
            return grant;
        }
    }

    /// <summary>Overwrites on create — so only the reconciler's own gate can keep an existing identity intact.</summary>
    private sealed class PermissiveIdentities : IServiceClientIdentityRepository
    {
        public Dictionary<string, ServiceClientIdentity> Documents { get; } = new(StringComparer.Ordinal);
        public int CreateAttempts { get; private set; }

        public Task<ServiceClientIdentity?> GetByClientCodeAsync(string clientCode, CancellationToken ct) =>
            FindByClientCodeIncludingDeletedAsync(clientCode, ct);

        public Task<ServiceClientIdentity?> FindByClientCodeIncludingDeletedAsync(string clientCode, CancellationToken ct) =>
            Task.FromResult(Documents.GetValueOrDefault(clientCode));

        public Task<bool> CreateIfAbsentAsync(ServiceClientIdentity identity, CancellationToken ct)
        {
            CreateAttempts++;
            Documents[identity.ClientCode] = identity;
            return Task.FromResult(true);
        }
    }

    private sealed class PermissiveGrants : IServiceClientTenantGrantRepository
    {
        public List<ServiceClientTenantGrant> Documents { get; } = [];
        public int CreateAttempts { get; private set; }

        public Task<bool> HasEnabledGrantAsync(Guid tenantId, Guid serviceClientIdentityId, string audience, CancellationToken ct) =>
            Task.FromResult(Documents.Any(grant => Same(grant, tenantId, serviceClientIdentityId, audience) && grant.IsEnabled));

        public Task<ServiceClientTenantGrant?> FindIncludingDeletedAsync(
            Guid tenantId, Guid serviceClientIdentityId, string audience, CancellationToken ct) =>
            Task.FromResult(Documents.FirstOrDefault(grant => Same(grant, tenantId, serviceClientIdentityId, audience)));

        public Task<bool> CreateIfAbsentAsync(ServiceClientTenantGrant grant, CancellationToken ct)
        {
            CreateAttempts++;
            Documents.RemoveAll(existing => Same(existing, grant.TenantId, grant.ServiceClientIdentityId, grant.Audience));
            Documents.Add(grant);
            return Task.FromResult(true);
        }

        private static bool Same(ServiceClientTenantGrant grant, Guid tenantId, Guid identityId, string audience) =>
            grant.TenantId == tenantId && grant.ServiceClientIdentityId == identityId
            && string.Equals(grant.Audience, audience, StringComparison.Ordinal);
    }

    private sealed class RecordingAudit : IAuthAuditService
    {
        public List<(string Event, Guid? UserId, Guid TenantId, string Metadata)> Events { get; } = [];

        public Task WriteEmptyRoleLoginAsync(Guid userId, Guid tenantId, string email, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task WriteAsync(string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default)
        {
            Events.Add((eventName, userId, tenantId, metadata));
            return Task.CompletedTask;
        }
    }
}
