using System.Security.Cryptography;
using System.Text;
using Diten.PpmService.Application.Features.InvestmentCases.GateI.DecisionTrace;
using Diten.PpmService.Domain.Entities;
using Diten.PpmService.Domain.GateI.DecisionTrace;
using Diten.PpmService.Persistence;
using Diten.PpmService.Persistence.GateI;
using Diten.PpmService.Persistence.Mongo;
using Diten.BuildingBlocks.Eventing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.PpmService.IntegrationTests.GateI.DecisionTrace;

public sealed class GateIRelationshipMutationMongoTests
{
    private static string Connection =>
        Environment.GetEnvironmentVariable("PPM_GATE_I_TEST_MONGO_URI")
        ?? throw new InvalidOperationException("PPM_GATE_I_TEST_MONGO_URI is required for Gate I Mongo evidence.");

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    [Fact]
    public async Task Relationship_receipt_audit_and_outbox_commit_once_and_reconcile()
    {
        await using var fixture = await Fixture.Create();
        var entity = fixture.NewInvestmentCase();
        await fixture.Context.InvestmentCases.InsertOneAsync(entity);
        var scope = fixture.Scope("same-key", "request-a");

        var result = await fixture.Persistence.ExecuteInvestmentCaseAsync(
            scope, entity.Id, entity.Version, fixture.GoverningMutation(entity.Id), "governing-decision-set", default);
        var replay = await fixture.Persistence.ReconcileAsync(scope, default);

        Assert.Equal(2, result.Version);
        Assert.Equal(GateIReceiptDisposition.Matching, replay.Disposition);
        Assert.Equal(1, await fixture.Context.GateIMutationReceipts.CountDocumentsAsync(FilterDefinition<GateIMutationReceiptDocument>.Empty));
        Assert.Equal(1, await fixture.Context.AuditIntents.CountDocumentsAsync(FilterDefinition<AuditIntentDocument>.Empty));
        Assert.Equal(1, await fixture.Context.EventOutbox.CountDocumentsAsync(FilterDefinition<PpmEventOutboxDocument>.Empty));
        var audit = await fixture.Context.AuditIntents.Find(FilterDefinition<AuditIntentDocument>.Empty).SingleAsync();
        var outbox = await fixture.Context.EventOutbox.Find(FilterDefinition<PpmEventOutboxDocument>.Empty).SingleAsync();
        Assert.NotNull(audit.OutboxEnqueuedAtUtc);
        Assert.Equal("ppm-event-hmac-sha256.v1", audit.DispatchSignatureScheme);
        Assert.Equal(3, outbox.TransportHeaders.Count);
        Assert.Equal(audit.DispatchKeyId, outbox.TransportHeaders[TrustedTransportMetadata.KeyIdHeader]);
        Assert.Equal(audit.DispatchSignature, outbox.TransportHeaders[TrustedTransportMetadata.SignatureHeader]);

        var indexes = await (await fixture.Context.GateIMutationReceipts.Indexes.ListAsync()).ToListAsync();
        var exact = Assert.Single(indexes, item => item["name"] == "ux_ppm_gate_i_receipt_scope");
        Assert.True(exact["unique"].AsBoolean);
        Assert.False(exact.Contains("expireAfterSeconds"));

        var conflict = await fixture.Persistence.ReconcileAsync(fixture.Scope("same-key", "request-b"), default);
        Assert.Equal(GateIReceiptDisposition.Conflict, conflict.Disposition);

        var provenanceConflict = await fixture.Persistence.ReconcileAsync(
            fixture.Scope("same-key", "request-a") with { ProvenanceHash = Hash("different-provenance") },
            default);
        Assert.Equal(GateIReceiptDisposition.Conflict, provenanceConflict.Disposition);
    }

    [Theory]
    [InlineData("relationship")]
    [InlineData("receipt")]
    [InlineData("audit-intent")]
    [InlineData("event-outbox")]
    public async Task Every_participant_fault_rolls_back_all_state(string participant)
    {
        await using var fixture = await Fixture.Create(participant);
        var entity = fixture.NewInvestmentCase();
        await fixture.Context.InvestmentCases.InsertOneAsync(entity);

        await Assert.ThrowsAsync<GateIRelationshipUnavailableException>(() =>
            fixture.Persistence.ExecuteInvestmentCaseAsync(
                fixture.Scope($"fault-{participant}", "fault-request"), entity.Id, entity.Version,
                fixture.GoverningMutation(entity.Id), "governing-decision-set", default));

        var stored = await fixture.Context.InvestmentCases.Find(item => item.Id == entity.Id).SingleAsync();
        Assert.Equal(1, stored.Version);
        Assert.Null(stored.GoverningDecisionReference);
        Assert.Equal(0, await fixture.Context.GateIMutationReceipts.CountDocumentsAsync(FilterDefinition<GateIMutationReceiptDocument>.Empty));
        Assert.Equal(0, await fixture.Context.AuditIntents.CountDocumentsAsync(FilterDefinition<AuditIntentDocument>.Empty));
        Assert.Equal(0, await fixture.Context.EventOutbox.CountDocumentsAsync(FilterDefinition<PpmEventOutboxDocument>.Empty));
    }

    [Fact]
    public async Task Cross_tenant_is_non_disclosing_and_has_zero_residue()
    {
        await using var fixture = await Fixture.Create();
        var entity = fixture.NewInvestmentCase();
        await fixture.Context.InvestmentCases.InsertOneAsync(entity);
        var other = fixture.Scope("cross-tenant", "cross-request") with { TenantId = Guid.NewGuid() };

        await Assert.ThrowsAsync<GateIRelationshipNotFoundException>(() =>
            fixture.Persistence.ExecuteInvestmentCaseAsync(
                other, entity.Id, entity.Version, fixture.GoverningMutation(entity.Id), "governing-decision-set", default));

        Assert.Equal(0, await fixture.Context.GateIMutationReceipts.CountDocumentsAsync(FilterDefinition<GateIMutationReceiptDocument>.Empty));
        Assert.Equal(0, await fixture.Context.AuditIntents.CountDocumentsAsync(FilterDefinition<AuditIntentDocument>.Empty));
        Assert.Equal(0, await fixture.Context.EventOutbox.CountDocumentsAsync(FilterDefinition<PpmEventOutboxDocument>.Empty));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly string _databaseName;
        private readonly Guid _actor = Guid.NewGuid();
        public Guid TenantId { get; } = Guid.NewGuid();
        public PpmMongoContext Context { get; }
        public IGateIRelationshipMutationPersistence Persistence { get; }

        private Fixture(ServiceProvider provider, string databaseName)
        {
            _provider = provider;
            _databaseName = databaseName;
            Context = provider.GetRequiredService<PpmMongoContext>();
            Persistence = provider.GetRequiredService<IGateIRelationshipMutationPersistence>();
        }

        public static async Task<Fixture> Create(string? failAfter = null)
        {
            var database = $"ppm_gate_i_{Guid.NewGuid():N}";
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = Connection,
                ["Mongo:DatabaseName"] = database
            }).Build();
            var services = new ServiceCollection();
            services.AddPpmPersistence(configuration);
            services.AddSingleton<IGateIRelationshipTransportMetadataProvider, TestTransportMetadataProvider>();
            if (failAfter is not null)
                services.AddSingleton<IGateIRelationshipMutationFaultProbe>(new ThrowingProbe(failAfter));
            var provider = services.BuildServiceProvider();
            var fixture = new Fixture(provider, database);
            await new GateIMutationReceiptIndexInitializer(fixture.Context).StartAsync(default);
            return fixture;
        }

        public InvestmentCase NewInvestmentCase() =>
            new(TenantId, _actor, "IC-GATE-I", "Gate I", null, Guid.NewGuid(), null, null);

        public Action<InvestmentCase> GoverningMutation(Guid investmentCaseId)
        {
            var reference = new GoverningDecisionReferenceV1(
                new InvestmentCaseContextV1(investmentCaseId),
                new DecisionRevisionReferenceV1(Guid.NewGuid(), Guid.NewGuid(), 1));
            return entity => entity.SetGoverningDecision(_actor, reference);
        }

        public GateIMutationScope Scope(string idempotencyKey, string requestSeed) =>
            new(TenantId, _actor, Guid.NewGuid(), "ppm.gate-i.governing-decision.set", idempotencyKey,
                Hash(requestSeed), Hash("trusted-provenance"));

        public async ValueTask DisposeAsync()
        {
            await Context.Client.DropDatabaseAsync(_databaseName);
            await _provider.DisposeAsync();
        }

        private static string Hash(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private sealed class ThrowingProbe(string participant) : IGateIRelationshipMutationFaultProbe
    {
        public Task AfterParticipantAsync(string current, CancellationToken cancellationToken) =>
            current == participant
                ? throw new MongoClientException("test fault")
                : Task.CompletedTask;
    }

    private sealed class TestTransportMetadataProvider : IGateIRelationshipTransportMetadataProvider
    {
        public ValueTask<TrustedTransportMetadata> CreateAsync(
            EventMetadata metadata,
            ReadOnlyMemory<byte> canonicalPayloadUtf8,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(new TrustedTransportMetadata(new Dictionary<string, string>
            {
                [TrustedTransportMetadata.SignatureSchemeHeader] = "ppm-event-hmac-sha256.v1",
                [TrustedTransportMetadata.KeyIdHeader] = "ppm-gate-i-test-only",
                [TrustedTransportMetadata.SignatureHeader] = new string('a', 64)
            }));
    }
}
