using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.ServiceIdentityTokens;
using Diten.AuthService.Application.Features.ServiceIdentityTokens.Operational;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Persistence.Configurations;
using Diten.AuthService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

public sealed class ServiceClientOperationalProvisioningMongoTests
{
    private const string DatabaseName = "diten_auth_service_identity_itest";

    [Fact]
    public async Task Operational_lifecycle_is_atomic_idempotent_and_tenant_isolated()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        var database = client.GetDatabase(DatabaseName);
        await MongoDbIndexConfigurations.EnsureServiceIdentityIndexesAsync(database);

        var identities = new ServiceClientIdentityRepository(database);
        var grants = new ServiceClientTenantGrantRepository(database);
        var operations = new ServiceClientOperationalProvisioningOperationRepository(database);
        var verifier = new ServiceClientCredentialVerifier();
        var sink = new RecordingSink();
        var service = new ServiceClientOperationalProvisioningService(
            identities, grants, operations, verifier, sink, TimeProvider.System);
        var actor = new ServiceClientOperationalActor(Guid.NewGuid(), Guid.Empty, "platform_admin");
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var marker = Guid.NewGuid().ToString("N");
        var clientCode = "MDM-WORKFLOW-" + marker.ToUpperInvariant();
        var createdIdentityId = Guid.Empty;
        var commandIds = new List<Guid>();

        try
        {
            var createCommand = Guid.NewGuid();
            commandIds.Add(createCommand);
            var create = new ServiceClientOperationalProvisioningRequest(
                ServiceClientOperationalOperations.CreateIdentity,
                createCommand,
                0,
                null,
                null,
                clientCode.ToLowerInvariant(),
                ServiceIdentityTokenAudiencePolicy.MdmServiceName,
                ServiceIdentityTokenAudiencePolicy.TrustedWorkflowConsumer,
                "pipe");

            var created = await service.ExecuteAsync(create, actor, CancellationToken.None);
            createdIdentityId = created.ServiceClientIdentityId;
            Assert.False(created.IsReplay);
            Assert.Equal("issued-once", created.SecretDisposition);
            Assert.Equal(clientCode, created.ClientCode);
            Assert.Equal(1, created.OperationalVersion);
            Assert.Single(sink.Deliveries);

            var replay = await service.ExecuteAsync(create, actor, CancellationToken.None);
            Assert.True(replay.IsReplay);
            Assert.Equal("not-reissued", replay.SecretDisposition);
            Assert.Single(sink.Deliveries);

            var drift = create with { Audience = ServiceIdentityTokenAudiencePolicy.TrustedAuditSourceIngest };
            await Assert.ThrowsAsync<ServiceClientOperationalConflictException>(() =>
                service.ExecuteAsync(drift, actor, CancellationToken.None));

            var enableACommand = Guid.NewGuid();
            commandIds.Add(enableACommand);
            var enableA = GrantRequest(ServiceClientOperationalOperations.EnableGrant,
                enableACommand, 0, createdIdentityId, tenantA);
            var enabledA = await service.ExecuteAsync(enableA, actor, CancellationToken.None);
            Assert.Equal(tenantA, enabledA.TenantId);
            Assert.Equal(1, enabledA.OperationalVersion);
            Assert.True(await grants.HasEnabledGrantAsync(tenantA, createdIdentityId,
                ServiceIdentityTokenAudiencePolicy.TrustedWorkflowConsumer, CancellationToken.None));
            Assert.False(await grants.HasEnabledGrantAsync(tenantB, createdIdentityId,
                ServiceIdentityTokenAudiencePolicy.TrustedWorkflowConsumer, CancellationToken.None));

            var rotateCommand = Guid.NewGuid();
            commandIds.Add(rotateCommand);
            var rotate = new ServiceClientOperationalProvisioningRequest(
                ServiceClientOperationalOperations.RotateCredential,
                rotateCommand,
                1,
                createdIdentityId,
                null,
                null,
                ServiceIdentityTokenAudiencePolicy.MdmServiceName,
                ServiceIdentityTokenAudiencePolicy.TrustedWorkflowConsumer,
                "pipe");
            var concurrent = await Task.WhenAll(
                service.ExecuteAsync(rotate, actor, CancellationToken.None),
                service.ExecuteAsync(rotate, actor, CancellationToken.None));
            Assert.Single(concurrent, x => !x.IsReplay);
            Assert.Single(concurrent, x => x.IsReplay);
            Assert.Equal(2, sink.Deliveries.Count);

            var identity = await identities.GetByIdAsync(createdIdentityId, CancellationToken.None);
            Assert.NotNull(identity);
            Assert.Equal(2, identity.OperationalVersion);
            Assert.Equal("op-" + createCommand.ToString("N"), identity.PreviousCredentialVersion);
            Assert.Equal("op-" + rotateCommand.ToString("N"), identity.ActiveCredentialVersion);
            Assert.NotNull(identity.PreviousValidUntilUtc);
            Assert.True(identity.PreviousValidUntilUtc > identity.UpdatedAt);
            Assert.True(verifier.Verify(identity, sink.Deliveries[0].Secret,
                identity.PreviousValidUntilUtc!.Value.AddTicks(-1)));
            Assert.False(verifier.Verify(identity, sink.Deliveries[0].Secret,
                identity.PreviousValidUntilUtc.Value));
            Assert.True(verifier.Verify(identity, sink.Deliveries[1].Secret,
                identity.PreviousValidUntilUtc.Value));

            var lostDeliveryCommand = Guid.NewGuid();
            commandIds.Add(lostDeliveryCommand);
            var lostDeliveryRequest = rotate with
            {
                CommandId = lostDeliveryCommand,
                ExpectedOperationalVersion = 2
            };
            var failingDeliveryService = new ServiceClientOperationalProvisioningService(
                identities, grants, operations, verifier, new CancellingSink(), TimeProvider.System);
            await Assert.ThrowsAsync<ServiceClientOperationalSecretDeliveryException>(() =>
                failingDeliveryService.ExecuteAsync(lostDeliveryRequest, actor, CancellationToken.None));
            var lostDeliveryReplay = await service.ExecuteAsync(
                lostDeliveryRequest, actor, CancellationToken.None);
            Assert.True(lostDeliveryReplay.IsReplay);
            Assert.Equal("not-reissued", lostDeliveryReplay.SecretDisposition);
            Assert.Equal(2, sink.Deliveries.Count);
            identity = await identities.GetByIdAsync(createdIdentityId, CancellationToken.None);
            Assert.NotNull(identity);
            Assert.Equal(3, identity.OperationalVersion);

            var disableCommand = Guid.NewGuid();
            commandIds.Add(disableCommand);
            var disabled = await service.ExecuteAsync(
                GrantRequest(ServiceClientOperationalOperations.DisableGrant,
                    disableCommand, 1, createdIdentityId, tenantA), actor, CancellationToken.None);
            Assert.Equal(2, disabled.OperationalVersion);
            Assert.False(await grants.HasEnabledGrantAsync(tenantA, createdIdentityId,
                ServiceIdentityTokenAudiencePolicy.TrustedWorkflowConsumer, CancellationToken.None));

            var enableBCommand = Guid.NewGuid();
            commandIds.Add(enableBCommand);
            await service.ExecuteAsync(
                GrantRequest(ServiceClientOperationalOperations.EnableGrant,
                    enableBCommand, 0, createdIdentityId, tenantB), actor, CancellationToken.None);
            Assert.True(await grants.HasEnabledGrantAsync(tenantB, createdIdentityId,
                ServiceIdentityTokenAudiencePolicy.TrustedWorkflowConsumer, CancellationToken.None));

            var revokeCommand = Guid.NewGuid();
            commandIds.Add(revokeCommand);
            var revoked = await service.ExecuteAsync(new ServiceClientOperationalProvisioningRequest(
                ServiceClientOperationalOperations.RevokeIdentity,
                revokeCommand,
                3,
                createdIdentityId,
                null,
                null,
                ServiceIdentityTokenAudiencePolicy.MdmServiceName,
                ServiceIdentityTokenAudiencePolicy.TrustedWorkflowConsumer,
                null), actor, CancellationToken.None);
            Assert.Equal(4, revoked.OperationalVersion);
            identity = await identities.GetByIdAsync(createdIdentityId, CancellationToken.None);
            Assert.NotNull(identity);
            Assert.True(identity.IsRevoked);
            Assert.Null(identity.PreviousCredentialHash);
            Assert.False(verifier.Verify(identity, sink.Deliveries[1].Secret, DateTimeOffset.UtcNow));

            var persistedOperations = database.GetCollection<ServiceClientOperationalProvisioningOperation>(
                "serviceClientOperationalProvisioningOperations");
            var completed = await persistedOperations.Find(x => commandIds.Contains(x.CommandId)).ToListAsync();
            Assert.Equal(commandIds.Count, completed.Count);
            Assert.All(completed, x =>
            {
                Assert.Equal(ServiceClientOperationalProvisioningState.Completed, x.State);
                Assert.Equal(ServiceClientOperationalProvisioningCheckpoint.EvidenceRecorded, x.Checkpoint);
                Assert.Equal(3, x.Evidence.Count);
                Assert.DoesNotContain(sink.Deliveries, d =>
                    x.ToBsonDocument().ToJson().Contains(d.Secret, StringComparison.Ordinal));
            });
        }
        finally
        {
            if (createdIdentityId != Guid.Empty)
            {
                await database.GetCollection<ServiceClientIdentity>("serviceClientIdentities")
                    .DeleteManyAsync(x => x.Id == createdIdentityId);
                await database.GetCollection<ServiceClientTenantGrant>("serviceClientTenantGrants")
                    .DeleteManyAsync(x => x.ServiceClientIdentityId == createdIdentityId);
            }

            await database.GetCollection<ServiceClientOperationalProvisioningOperation>(
                    "serviceClientOperationalProvisioningOperations")
                .DeleteManyAsync(x => commandIds.Contains(x.CommandId));
        }
    }

    private static ServiceClientOperationalProvisioningRequest GrantRequest(
        string operation, Guid commandId, long expectedVersion, Guid identityId, Guid tenantId) =>
        new(operation, commandId, expectedVersion, identityId, tenantId, null,
            ServiceIdentityTokenAudiencePolicy.MdmServiceName,
            ServiceIdentityTokenAudiencePolicy.TrustedWorkflowConsumer, null);

    private sealed class RecordingSink : IServiceClientSecretOutputSink
    {
        public List<Delivery> Deliveries { get; } = [];

        public Task PreflightAsync(string inheritedPipeHandle, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeliverOnceAsync(
            Guid serviceClientIdentityId, string clientCode, string rawSecret,
            CancellationToken cancellationToken)
        {
            lock (Deliveries)
                Deliveries.Add(new Delivery(serviceClientIdentityId, clientCode, rawSecret));
            return Task.CompletedTask;
        }
    }

    private sealed class CancellingSink : IServiceClientSecretOutputSink
    {
        public Task PreflightAsync(string inheritedPipeHandle, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeliverOnceAsync(
            Guid serviceClientIdentityId, string clientCode, string rawSecret,
            CancellationToken cancellationToken) => Task.FromCanceled(new CancellationToken(true));
    }

    private sealed record Delivery(Guid IdentityId, string ClientCode, string Secret);
}
