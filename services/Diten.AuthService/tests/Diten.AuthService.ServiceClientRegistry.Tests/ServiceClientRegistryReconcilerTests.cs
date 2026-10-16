using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Registry;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.ServiceClientRegistry.Tests;

/// <summary>
/// MOD-0033-FU02 §F.1 — the reconciler's own gate: it must not even ATTEMPT a write for something that already
/// exists. The repositories here are deliberately permissive (a create on an existing key OVERWRITES), so these
/// tests stand on the reconciler alone and do not lean on the insert-only repositories or the unique indexes —
/// those have their own tests against a real mongod (<see cref="ServiceClientRegistryMongoTests"/>).
///
/// <para>The audit double keeps one row per id (create-if-absent), which is the contract of
/// <see cref="IAuthAuditService.WriteOnceAsync"/>; that the PRODUCTION writer honours it is proven against the real
/// mongod. What these tests own is the reconciler's part: the audit row comes FIRST, under the derived id.</para>
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
        // Each row sits under the id derived from the event and the thing created — never a random one.
        Assert.Equal(
            [ServiceClientRegistryReconciler.IdentityAuditId(identity.Id),
                ServiceClientRegistryReconciler.GrantAuditId(Tenant, identity.Id, Audience)],
            world.Audit.Events.Select(entry => entry.Id));
        // And each was written BEFORE its insert was attempted.
        Assert.Equal(["audit", "identity", "audit", "grant"], world.Trace);
    }

    // ── Item 1: write-ahead audit ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Apply_WhenTheAuditWriteFails_AttemptsNoInsert_AndALaterStartCreatesWithExactlyOneAuditRowEach()
    {
        var world = new World();
        var declaration = new ServiceClientDeclaration("client-f", "Diten.MDM", Audience, "declared-hash", "v1", [Tenant]);
        world.Audit.Fails = _ => true;

        // However often Auth restarts while the audit log cannot be written, nothing comes into existence.
        for (var restart = 0; restart < 3; restart++)
        {
            await Assert.ThrowsAsync<AuditStoreUnavailable>(() => world.Reconciler.ApplyAsync([declaration], CancellationToken.None));
        }

        Assert.Equal(0, world.Identities.CreateAttempts);
        Assert.Equal(0, world.Grants.CreateAttempts);
        Assert.Empty(world.Identities.Documents);
        Assert.Empty(world.Grants.Documents);
        Assert.Empty(world.Audit.Events);

        world.Audit.Fails = null;
        await world.Reconciler.ApplyAsync([declaration], CancellationToken.None);

        var identity = world.Identities.Documents["client-f"];
        Assert.Single(world.Grants.Documents);
        Assert.Equal(
            [ServiceClientRegistryReconciler.IdentityAuditId(identity.Id),
                ServiceClientRegistryReconciler.GrantAuditId(Tenant, identity.Id, Audience)],
            world.Audit.Events.Select(entry => entry.Id));
    }

    [Fact]
    public async Task Apply_WhenOnlyTheGrantAuditWriteFails_AttemptsNoGrantInsert_AndALaterStartGrantsWithOneAuditRow()
    {
        var world = new World();
        var declaration = new ServiceClientDeclaration("client-g", "Diten.MDM", Audience, "declared-hash", "v1", [Tenant]);
        world.Audit.Fails = eventName => eventName == ServiceClientRegistryReconciler.TenantGrantedEvent;

        await Assert.ThrowsAsync<AuditStoreUnavailable>(() => world.Reconciler.ApplyAsync([declaration], CancellationToken.None));

        var identity = world.Identities.Documents["client-g"]; // created, and audited before it was
        Assert.Equal([ServiceClientRegistryReconciler.IdentityRegisteredEvent], world.Audit.Events.Select(entry => entry.Event));
        Assert.Equal(0, world.Grants.CreateAttempts);
        Assert.Empty(world.Grants.Documents);

        world.Audit.Fails = null;
        var notices = await world.Reconciler.ApplyAsync([declaration], CancellationToken.None);

        Assert.Contains(notices, notice => notice.Outcome == ServiceClientRegistryOutcome.IdentityUnchanged);
        Assert.Contains(notices, notice => notice.Outcome == ServiceClientRegistryOutcome.GrantCreated);
        Assert.Equal(1, world.Identities.CreateAttempts); // the existing identity was not written again
        Assert.Single(world.Grants.Documents);
        Assert.Equal(
            [ServiceClientRegistryReconciler.IdentityAuditId(identity.Id),
                ServiceClientRegistryReconciler.GrantAuditId(Tenant, identity.Id, Audience)],
            world.Audit.Events.Select(entry => entry.Id));
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("grant")]
    public async Task Apply_WhenTheInsertFailsAfterItsAuditRow_TheNextStartCompletesItWithoutASecondAuditRow(string failing)
    {
        var world = new World();
        var declaration = new ServiceClientDeclaration("client-h", "Diten.MDM", Audience, "declared-hash", "v1", [Tenant]);
        if (failing == "identity") world.Identities.FailNextCreate = true;
        else world.Grants.FailNextCreate = true;

        await Assert.ThrowsAsync<TimeoutException>(() => world.Reconciler.ApplyAsync([declaration], CancellationToken.None));

        var identityId = ServiceClientIdentityIds.FromClientCode("client-h");
        var auditId = failing == "identity"
            ? ServiceClientRegistryReconciler.IdentityAuditId(identityId)
            : ServiceClientRegistryReconciler.GrantAuditId(Tenant, identityId, Audience);
        // The row for the creation that did not complete is there (accepted: over-reporting, never under-reporting).
        Assert.Equal(auditId, world.Audit.Events[^1].Id);
        Assert.Equal(failing == "identity", world.Identities.Documents.Count == 0);
        Assert.Empty(world.Grants.Documents);
        var rowsAfterTheFailedStart = world.Audit.Events.Count;

        await world.Reconciler.ApplyAsync([declaration], CancellationToken.None);

        Assert.Single(world.Identities.Documents);
        Assert.Single(world.Grants.Documents);
        Assert.Equal(2, world.Audit.Events.Count); // one per creation — the repeated start added no second row
        Assert.Equal(rowsAfterTheFailedStart + (failing == "identity" ? 1 : 0), world.Audit.Events.Count);
        // It DID write ahead again, under the same id; the store answered "already there".
        Assert.Equal(2, world.Audit.Attempts.Count(id => id == auditId));
    }

    [Fact]
    public async Task Apply_WhenAnotherInstanceCreatedItFirst_WritesNoSecondAuditRow()
    {
        var world = new World();
        var declaration = new ServiceClientDeclaration("client-i", "Diten.MDM", Audience, "declared-hash", "v1", [Tenant]);
        var identityId = ServiceClientIdentityIds.FromClientCode("client-i");
        // The other instance got there between this instance's read and its insert: its audit rows and its
        // documents are already in the store when this instance writes.
        world.Audit.Seed(ServiceClientRegistryReconciler.IdentityAuditId(identityId),
            ServiceClientRegistryReconciler.IdentityRegisteredEvent, "written-by-the-other-instance");
        world.Audit.Seed(ServiceClientRegistryReconciler.GrantAuditId(Tenant, identityId, Audience),
            ServiceClientRegistryReconciler.TenantGrantedEvent, "written-by-the-other-instance");
        world.Identities.LoseTheRace = true;
        world.Grants.LoseTheRace = true;

        var notices = await world.Reconciler.ApplyAsync([declaration], CancellationToken.None);

        Assert.Equal(
            [ServiceClientRegistryOutcome.IdentityUnchanged, ServiceClientRegistryOutcome.GrantUnchanged],
            notices.Select(notice => notice.Outcome));
        Assert.Equal(2, world.Audit.Events.Count);
        Assert.All(world.Audit.Events, entry => Assert.Equal("written-by-the-other-instance", entry.Metadata));
    }

    [Fact]
    public void AuditIds_AreStablePerEventAndSubject_AndDifferAcrossThem()
    {
        var identity = ServiceClientIdentityIds.FromClientCode("client-j");
        var other = ServiceClientIdentityIds.FromClientCode("client-k");
        var otherTenant = Guid.Parse("7a1f0c52-6f0e-4a55-9c11-0d6b8c3e9a02");

        Assert.Equal(ServiceClientRegistryReconciler.IdentityAuditId(identity), ServiceClientRegistryReconciler.IdentityAuditId(identity));
        Assert.Equal(ServiceClientRegistryReconciler.GrantAuditId(Tenant, identity, Audience),
            ServiceClientRegistryReconciler.GrantAuditId(Tenant, identity, Audience));
        Guid[] all =
        [
            identity, other,
            ServiceClientRegistryReconciler.IdentityAuditId(identity),
            ServiceClientRegistryReconciler.IdentityAuditId(other),
            ServiceClientRegistryReconciler.GrantAuditId(Tenant, identity, Audience),
            ServiceClientRegistryReconciler.GrantAuditId(otherTenant, identity, Audience),
            ServiceClientRegistryReconciler.GrantAuditId(Tenant, other, Audience),
            ServiceClientRegistryReconciler.GrantAuditId(Tenant, identity, "TRUSTED_AUDIT_SOURCE_INGEST")
        ];
        Assert.Equal(all.Length, all.Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, all);
    }

    // ── Item 2: an identity under a NON-derived id is not the declared client ─────────────────────────────────

    [Fact]
    public async Task Apply_WhenIdentityExistsUnderAnotherId_ReportsIdByNameOnly_CreatesNoGrant_AndChangesNothing()
    {
        var world = new World();
        var storedId = Guid.Parse("3f2b6c1d-8a4e-4b7f-9d2a-5c6e7f8091a2"); // created by hand or by a fixture
        var existing = world.SeedIdentity("client-l", hash: "same-hash", version: "v1", id: storedId);
        var declaration = new ServiceClientDeclaration("client-l", "Diten.MDM", Audience, "same-hash", "v1", [Tenant]);

        var notices = await world.Reconciler.ApplyAsync([declaration], CancellationToken.None);

        Assert.Equal(
            [ServiceClientRegistryOutcome.IdentityDiffers, ServiceClientRegistryOutcome.GrantsSkipped],
            notices.Select(notice => notice.Outcome));
        Assert.Equal(["Id"], notices[0].Fields);
        Assert.Equal(0, world.Identities.CreateAttempts);
        Assert.Equal(0, world.Grants.CreateAttempts);
        Assert.Empty(world.Grants.Documents);
        Assert.Same(existing, world.Identities.Documents["client-l"]);
        Assert.Equal(storedId, existing.Id);
        Assert.Empty(world.Audit.Events);
        Assert.Empty(world.Audit.Attempts);

        // The existing warning, with the field NAME — and neither id as a value.
        var warning = Assert.Single(world.Log, entry => entry.Level == LogLevel.Warning
            && entry.Text.Contains("differs from configuration", StringComparison.Ordinal));
        Assert.Contains("client-l", warning.Text, StringComparison.Ordinal);
        Assert.Contains(" in Id;", warning.Text, StringComparison.Ordinal);
        Assert.All(world.Log, entry =>
        {
            Assert.DoesNotContain(storedId.ToString("D"), entry.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ServiceClientIdentityIds.FromClientCode("client-l").ToString("D"), entry.Text, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task Apply_WhenIdentityUnderAnotherIdAlsoHasAnotherCredential_ListsIdFirst()
    {
        var world = new World();
        world.SeedIdentity("client-m", hash: "persisted-hash", version: "persisted-version", id: Guid.NewGuid());
        var declaration = new ServiceClientDeclaration("client-m", "Diten.MDM", Audience, "declared-hash", "declared-version", [Tenant]);

        var notices = await world.Reconciler.ApplyAsync([declaration], CancellationToken.None);

        Assert.Equal(["Id", "CredentialHash", "CredentialVersion"], notices[0].Fields);
        Assert.Contains(notices, notice => notice.Outcome == ServiceClientRegistryOutcome.GrantsSkipped);
        Assert.Equal(0, world.Grants.CreateAttempts);
    }

    private sealed class World
    {
        public World()
        {
            Identities = new PermissiveIdentities(Trace);
            Grants = new PermissiveGrants(Trace);
            Audit = new RecordingAudit(Trace);
            Reconciler = new ServiceClientRegistryReconciler(Identities, Grants, Audit, TimeProvider.System, new ListLogger(Log));
        }

        /// <summary>The order in which the reconciler reached the stores: "audit", "identity", "grant".</summary>
        public List<string> Trace { get; } = [];
        public List<(LogLevel Level, string Text)> Log { get; } = [];
        public PermissiveIdentities Identities { get; }
        public PermissiveGrants Grants { get; }
        public RecordingAudit Audit { get; }
        public ServiceClientRegistryReconciler Reconciler { get; }

        /// <summary>
        /// By default the row a DECLARED client has: its id is the one derived from the client code. Pass
        /// <paramref name="id"/> for a row that was created some other way.
        /// </summary>
        public ServiceClientIdentity SeedIdentity(
            string code, string hash, string version, string service = "Diten.MDM", string? audience = Audience, Guid? id = null)
        {
            var identity = new ServiceClientIdentity
            {
                Id = id ?? ServiceClientIdentityIds.FromClientCode(code),
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
    private sealed class PermissiveIdentities(List<string> trace) : IServiceClientIdentityRepository
    {
        public Dictionary<string, ServiceClientIdentity> Documents { get; } = new(StringComparer.Ordinal);
        public int CreateAttempts { get; private set; }

        /// <summary>The next create reaches the store and fails there (nothing is stored).</summary>
        public bool FailNextCreate { get; set; }

        /// <summary>Another instance's identical document is stored instead and the create answers "not by you".</summary>
        public bool LoseTheRace { get; set; }

        public Task<ServiceClientIdentity?> GetByClientCodeAsync(string clientCode, CancellationToken ct) =>
            FindByClientCodeIncludingDeletedAsync(clientCode, ct);

        public Task<ServiceClientIdentity?> FindByClientCodeIncludingDeletedAsync(string clientCode, CancellationToken ct) =>
            Task.FromResult(Documents.GetValueOrDefault(clientCode));

        public Task<bool> CreateIfAbsentAsync(ServiceClientIdentity identity, CancellationToken ct)
        {
            CreateAttempts++;
            trace.Add("identity");
            if (FailNextCreate)
            {
                FailNextCreate = false;
                throw new TimeoutException("identity store unavailable");
            }

            if (LoseTheRace)
            {
                Documents[identity.ClientCode] = new ServiceClientIdentity
                {
                    Id = identity.Id, ClientCode = identity.ClientCode, ServiceName = identity.ServiceName,
                    AllowedAudience = identity.AllowedAudience, ActiveCredentialHash = identity.ActiveCredentialHash,
                    ActiveCredentialVersion = identity.ActiveCredentialVersion, CreatedBy = "the-other-instance"
                };
                return Task.FromResult(false);
            }

            Documents[identity.ClientCode] = identity;
            return Task.FromResult(true);
        }
    }

    private sealed class PermissiveGrants(List<string> trace) : IServiceClientTenantGrantRepository
    {
        public List<ServiceClientTenantGrant> Documents { get; } = [];
        public int CreateAttempts { get; private set; }
        public bool FailNextCreate { get; set; }
        public bool LoseTheRace { get; set; }

        public Task<bool> HasEnabledGrantAsync(Guid tenantId, Guid serviceClientIdentityId, string audience, CancellationToken ct) =>
            Task.FromResult(Documents.Any(grant => Same(grant, tenantId, serviceClientIdentityId, audience) && grant.IsEnabled));

        public Task<ServiceClientTenantGrant?> FindIncludingDeletedAsync(
            Guid tenantId, Guid serviceClientIdentityId, string audience, CancellationToken ct) =>
            Task.FromResult(Documents.FirstOrDefault(grant => Same(grant, tenantId, serviceClientIdentityId, audience)));

        public Task<bool> CreateIfAbsentAsync(ServiceClientTenantGrant grant, CancellationToken ct)
        {
            CreateAttempts++;
            trace.Add("grant");
            if (FailNextCreate)
            {
                FailNextCreate = false;
                throw new TimeoutException("grant store unavailable");
            }

            Documents.RemoveAll(existing => Same(existing, grant.TenantId, grant.ServiceClientIdentityId, grant.Audience));
            if (LoseTheRace)
            {
                Documents.Add(new ServiceClientTenantGrant
                {
                    TenantId = grant.TenantId, ServiceClientIdentityId = grant.ServiceClientIdentityId,
                    Audience = grant.Audience, IsEnabled = true, CreatedBy = "the-other-instance"
                });
                return Task.FromResult(false);
            }

            Documents.Add(grant);
            return Task.FromResult(true);
        }

        private static bool Same(ServiceClientTenantGrant grant, Guid tenantId, Guid identityId, string audience) =>
            grant.TenantId == tenantId && grant.ServiceClientIdentityId == identityId
            && string.Equals(grant.Audience, audience, StringComparison.Ordinal);
    }

    private sealed class AuditStoreUnavailable() : Exception("audit store unavailable");

    /// <summary>One row per id, first writer wins — the create-if-absent contract of the idempotent audit write.</summary>
    private sealed class RecordingAudit(List<string> trace) : IAuthAuditService
    {
        /// <summary>The rows in the store, in the order they were written.</summary>
        public List<(Guid Id, string Event, Guid? UserId, Guid TenantId, string Metadata)> Events { get; } = [];

        /// <summary>The id of every write the reconciler attempted, whether or not it produced a row.</summary>
        public List<Guid> Attempts { get; } = [];

        /// <summary>Event names whose write fails (nothing is stored).</summary>
        public Func<string, bool>? Fails { get; set; }

        public void Seed(Guid id, string eventName, string metadata) => Events.Add((id, eventName, null, Guid.Empty, metadata));

        public Task WriteEmptyRoleLoginAsync(Guid userId, Guid tenantId, string email, CancellationToken ct = default) =>
            throw new NotSupportedException();

        // The registry must not use the plain write: it has no id of the caller's and so cannot be repeated safely.
        public Task WriteAsync(string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool> WriteOnceAsync(
            Guid auditId, string eventName, Guid? userId, Guid tenantId, string metadata, CancellationToken ct = default)
        {
            Attempts.Add(auditId);
            trace.Add("audit");
            if (Fails?.Invoke(eventName) == true) throw new AuditStoreUnavailable();
            if (Events.Any(entry => entry.Id == auditId)) return Task.FromResult(false);
            Events.Add((auditId, eventName, userId, tenantId, metadata));
            return Task.FromResult(true);
        }
    }

    private sealed class ListLogger(List<(LogLevel Level, string Text)> entries) : ILogger<ServiceClientRegistryReconciler>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => entries.Add((logLevel, formatter(state, exception)));
    }
}
