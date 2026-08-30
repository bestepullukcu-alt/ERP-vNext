using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Infrastructure.Audit;
using Diten.PpmService.Persistence;
using Diten.PpmService.Persistence.Mongo;
using Diten.PpmService.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Xunit;

namespace Diten.PpmService.IntegrationTests;

[Collection(PpmMongoCollection.CollectionName)]
public sealed class PpmAuditDispatcherMongoIntegrationTests
{
    private const string EventName = "ppm.audit-intent.submitted.v1";
    private const string Producer = "Diten.PpmService";
    private static string _replicaSetConnection = string.Empty;

    public PpmAuditDispatcherMongoIntegrationTests(PpmDisposableMongo mongo) =>
        _replicaSetConnection = mongo.ReplicaSetConnectionString;

    [Fact]
    public async Task Normal_dispatch_writes_exact_signed_pending_outbox_and_marker()
    {
        var fixture = await Fixture.Create();
        var intent = fixture.Intent();
        await fixture.InsertTransactionally(intent);

        Assert.Equal(1, await fixture.Dispatcher.DispatchPendingAsync(default));

        var persistedIntent = await fixture.Context.AuditIntents.Find(x => x.Id == intent.Id).SingleAsync();
        var outbox = await fixture.Context.EventOutbox.Find(x => x.EventId == intent.Id).SingleAsync();
        Assert.Equal(1, await fixture.Context.AuditIntents.CountDocumentsAsync(_ => true));
        Assert.Equal(1, await fixture.Context.EventOutbox.CountDocumentsAsync(_ => true));
        Assert.NotNull(persistedIntent.OutboxEnqueuedAtUtc);
        Assert.Null(persistedIntent.DispatchFailureCode);
        Assert.Equal(intent.Id, outbox.EventId);
        Assert.Equal(intent.CorrelationId, outbox.CorrelationId);
        Assert.Equal(intent.TenantId, outbox.TenantId);
        Assert.Equal(Producer, outbox.Producer);
        Assert.Equal(EventName, outbox.EventName);
        Assert.Equal(1, outbox.EventVersion);
        Assert.Equal(EventOutboxDeliveryStatus.Pending, outbox.Status);

        var expectedPayload = CanonicalPayload(intent);
        Assert.Equal(expectedPayload, outbox.CanonicalPayloadUtf8);
        Assert.Equal(3, outbox.TransportHeaders.Count);
        Assert.Equal(
            PpmAuditTrustedTransportMetadataProvider.SignatureScheme,
            outbox.TransportHeaders[TrustedTransportMetadata.SignatureSchemeHeader]);
        Assert.Equal(fixture.Options.KeyId, outbox.TransportHeaders[TrustedTransportMetadata.KeyIdHeader]);
        var metadata = Metadata(intent);
        var expectedSignature = Convert.ToHexString(HMACSHA256.HashData(
            fixture.Secret,
            PpmAuditTrustedTransportMetadataProvider.BuildSigningInput(metadata, expectedPayload)))
            .ToLowerInvariant();
        Assert.Equal(
            expectedSignature,
            outbox.TransportHeaders[TrustedTransportMetadata.SignatureHeader]);
    }

    [Fact]
    public async Task Repeated_dispatch_is_no_op_and_marker_is_unchanged()
    {
        var fixture = await Fixture.Create();
        var intent = fixture.Intent();
        await fixture.InsertTransactionally(intent);
        Assert.Equal(1, await fixture.Dispatcher.DispatchPendingAsync(default));
        var first = await fixture.Context.AuditIntents.Find(x => x.Id == intent.Id).SingleAsync();

        Assert.Equal(0, await fixture.Dispatcher.DispatchPendingAsync(default));

        var second = await fixture.Context.AuditIntents.Find(x => x.Id == intent.Id).SingleAsync();
        Assert.Equal(first.OutboxEnqueuedAtUtc, second.OutboxEnqueuedAtUtc);
        Assert.Equal(1, await fixture.Context.EventOutbox.CountDocumentsAsync(_ => true));
        Assert.Empty(await fixture.Audit.GetDispatchCandidatesAsync(10, default));
    }

    [Fact]
    public async Task Enqueue_before_marker_failure_replays_duplicate_and_then_marks()
    {
        var fixture = await Fixture.Create(failMarkerOnce: true);
        var intent = fixture.Intent();
        await fixture.InsertTransactionally(intent);

        await Assert.ThrowsAsync<MarkerCrashException>(
            () => fixture.Dispatcher.DispatchPendingAsync(default));
        var beforeRetry = await fixture.Context.EventOutbox.Find(x => x.EventId == intent.Id).SingleAsync();
        Assert.Null((await fixture.Context.AuditIntents.Find(x => x.Id == intent.Id).SingleAsync()).OutboxEnqueuedAtUtc);

        Assert.Equal(1, await fixture.Dispatcher.DispatchPendingAsync(default));

        var afterRetry = await fixture.Context.EventOutbox.Find(x => x.EventId == intent.Id).SingleAsync();
        Assert.Equal(1, await fixture.Context.EventOutbox.CountDocumentsAsync(_ => true));
        Assert.NotNull((await fixture.Context.AuditIntents.Find(x => x.Id == intent.Id).SingleAsync()).OutboxEnqueuedAtUtc);
        AssertImmutableEqual(beforeRetry, afterRetry);
    }

    [Fact]
    public async Task Two_dispatchers_racing_create_one_event_without_conflict()
    {
        var fixture = await Fixture.Create();
        var intent = fixture.Intent();
        await fixture.InsertTransactionally(intent);
        var secondDispatcher = fixture.CreateDispatcher(fixture.Repository);

        var results = await Task.WhenAll(
            fixture.Dispatcher.DispatchPendingAsync(default),
            secondDispatcher.DispatchPendingAsync(default));

        Assert.Equal(2, results.Sum());
        Assert.Equal(1, await fixture.Context.EventOutbox.CountDocumentsAsync(_ => true));
        Assert.NotNull((await fixture.Context.AuditIntents.Find(x => x.Id == intent.Id).SingleAsync()).OutboxEnqueuedAtUtc);
    }

    [Fact]
    public async Task Missing_correlation_is_quarantined_without_outbox()
    {
        var fixture = await Fixture.Create();
        var intent = fixture.Intent();
        await fixture.Context.AuditIntents.InsertOneAsync(Document(intent, omitCorrelation: true));

        Assert.Equal(0, await fixture.Dispatcher.DispatchPendingAsync(default));

        var persisted = await fixture.Context.AuditIntents.Find(x => x.Id == intent.Id).SingleAsync();
        Assert.Equal(0, await fixture.Context.EventOutbox.CountDocumentsAsync(_ => true));
        Assert.Null(persisted.OutboxEnqueuedAtUtc);
        Assert.Equal("ppm.audit-intent.correlation-missing", persisted.DispatchFailureCode);
        Assert.Null(persisted.CorrelationId);
    }

    [Theory]
    [InlineData(PpmAuditTrustedTransportMetadataProvider.SignatureScheme, null, null)]
    [InlineData(" ", "integration-random", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData(PpmAuditTrustedTransportMetadataProvider.SignatureScheme, " ", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData(PpmAuditTrustedTransportMetadataProvider.SignatureScheme, "integration-random", "not-a-valid-signature")]
    [InlineData("legacy-signature-scheme", "integration-random", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Poison_signing_metadata_is_quarantined_and_next_valid_candidate_dispatches(
        string? signatureScheme,
        string? keyId,
        string? signature)
    {
        var fixture = await Fixture.Create();
        var invalid = fixture.Intent(DateTime.UtcNow.AddMinutes(-1));
        var valid = fixture.Intent(DateTime.UtcNow);
        await fixture.Context.AuditIntents.InsertManyAsync(
        [
            Document(
                invalid,
                dispatchSignatureScheme: signatureScheme,
                dispatchKeyId: keyId,
                dispatchSignature: signature),
            Document(valid)
        ]);

        Assert.Equal(1, await fixture.Dispatcher.DispatchPendingAsync(default));

        Assert.Equal(1, await fixture.Context.EventOutbox.CountDocumentsAsync(_ => true));
        Assert.Equal(0, await fixture.Context.EventOutbox.CountDocumentsAsync(x => x.EventId == invalid.Id));
        Assert.Equal(1, await fixture.Context.EventOutbox.CountDocumentsAsync(x => x.EventId == valid.Id));
        var invalidDocument = await fixture.Context.AuditIntents.Find(x => x.Id == invalid.Id).SingleAsync();
        var validDocument = await fixture.Context.AuditIntents.Find(x => x.Id == valid.Id).SingleAsync();
        Assert.Null(invalidDocument.OutboxEnqueuedAtUtc);
        Assert.Equal(
            "ppm.audit-intent.signing-metadata-invalid",
            invalidDocument.DispatchFailureCode);
        Assert.NotNull(validDocument.OutboxEnqueuedAtUtc);
        Assert.Null(validDocument.DispatchFailureCode);
        Assert.Empty(await fixture.Audit.GetDispatchCandidatesAsync(10, default));
    }

    [Fact]
    public async Task Systemic_signing_secret_failure_is_not_quarantined_or_marked()
    {
        var fixture = await Fixture.Create();
        var intent = fixture.Intent();
        await fixture.InsertTransactionally(intent);
        var invalidOptions = new PpmAuditProducerOptions
        {
            Enabled = true,
            WorkerEnabled = true,
            BatchSize = 25,
            KeyId = "integration-random",
            SecretBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(8))
        };
        var dispatcher = fixture.CreateDispatcher(fixture.Repository, invalidOptions);

        await Assert.ThrowsAsync<EventValidationException>(
            () => dispatcher.DispatchPendingAsync(default));

        var persisted = await fixture.Context.AuditIntents.Find(x => x.Id == intent.Id).SingleAsync();
        Assert.Null(persisted.OutboxEnqueuedAtUtc);
        Assert.Null(persisted.DispatchFailureCode);
        Assert.Equal(0, await fixture.Context.EventOutbox.CountDocumentsAsync(_ => true));
        Assert.Single(await fixture.Audit.GetDispatchCandidatesAsync(10, default));
    }

    [Fact]
    public async Task Cancellation_is_propagated_without_quarantine_marker_or_outbox()
    {
        var fixture = await Fixture.Create();
        var intent = fixture.Intent();
        await fixture.InsertTransactionally(intent);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Dispatcher.DispatchPendingAsync(cancellation.Token));

        var persisted = await fixture.Context.AuditIntents.Find(x => x.Id == intent.Id).SingleAsync();
        Assert.Null(persisted.OutboxEnqueuedAtUtc);
        Assert.Null(persisted.DispatchFailureCode);
        Assert.Equal(0, await fixture.Context.EventOutbox.CountDocumentsAsync(_ => true));
    }

    private static byte[] CanonicalPayload(AuditIntent intent) =>
        Encoding.UTF8.GetBytes(
            $"{{\"actorId\":\"{intent.ActorId:D}\",\"auditIntentId\":\"{intent.Id:D}\"," +
            $"\"entityId\":\"{intent.EntityId:D}\",\"entityType\":\"{intent.EntityType}\"," +
            $"\"mutation\":\"{intent.Mutation}\",\"occurredAtUtc\":\"" +
            $"{intent.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture)}\"}}");

    private static EventMetadata Metadata(AuditIntent intent) =>
        new(
            intent.Id,
            EventName,
            1,
            intent.CorrelationId,
            null,
            intent.TenantId,
            Producer,
            new DateTimeOffset(intent.OccurredAtUtc));

    private static AuditIntentDocument Document(
        AuditIntent intent,
        bool omitCorrelation = false,
        string? dispatchSignatureScheme = null,
        string? dispatchKeyId = null,
        string? dispatchSignature = null) =>
        new()
        {
            Id = intent.Id,
            TenantId = intent.TenantId,
            ActorId = intent.ActorId,
            CorrelationId = omitCorrelation ? null : intent.CorrelationId,
            EntityType = intent.EntityType,
            EntityId = intent.EntityId,
            Mutation = intent.Mutation,
            OccurredAtUtc = intent.OccurredAtUtc,
            DispatchSignatureScheme = dispatchSignatureScheme,
            DispatchKeyId = dispatchKeyId,
            DispatchSignature = dispatchSignature
        };

    private static void AssertImmutableEqual(PpmEventOutboxDocument left, PpmEventOutboxDocument right)
    {
        Assert.Equal(left.EventId, right.EventId);
        Assert.Equal(left.EventName, right.EventName);
        Assert.Equal(left.EventVersion, right.EventVersion);
        Assert.Equal(left.CorrelationId, right.CorrelationId);
        Assert.Equal(left.CausationId, right.CausationId);
        Assert.Equal(left.TenantId, right.TenantId);
        Assert.Equal(left.Producer, right.Producer);
        Assert.Equal(left.OccurredAtUtcTicks, right.OccurredAtUtcTicks);
        Assert.Equal(left.CanonicalPayloadUtf8, right.CanonicalPayloadUtf8);
        Assert.Equal(left.TransportHeaders.Count, right.TransportHeaders.Count);
        Assert.All(left.TransportHeaders, pair =>
            Assert.Equal(pair.Value, right.TransportHeaders[pair.Key]));
    }

    private sealed class Fixture
    {
        private Fixture(PpmMongoContext context, bool failMarkerOnce)
        {
            Context = context;
            Repository = new AuditIntentRepository(context);
            Audit = failMarkerOnce
                ? new FailMarkerOnceAuditIntentRepository(Repository)
                : Repository;
            Store = new PpmEventOutboxStore(context);
            Secret = RandomNumberGenerator.GetBytes(32);
            Options = new PpmAuditProducerOptions
            {
                Enabled = true,
                WorkerEnabled = true,
                BatchSize = 25,
                KeyId = "integration-random",
                SecretBase64 = Convert.ToBase64String(Secret)
            };
            Dispatcher = CreateDispatcher(Audit);
        }

        public PpmMongoContext Context { get; }
        public AuditIntentRepository Repository { get; }
        public IAuditIntentRepository Audit { get; }
        public PpmEventOutboxStore Store { get; }
        public byte[] Secret { get; }
        public PpmAuditProducerOptions Options { get; }
        public PpmAuditIntentDispatcher Dispatcher { get; }

        public static async Task<Fixture> Create(bool failMarkerOnce = false)
        {
            var database = PpmMongoTestDatabase.Open(_replicaSetConnection);
            await PpmMongoTestDatabase.ResetAsync(database);
            await new PpmMongoIndexInitializer(database).StartAsync(default);
            return new Fixture(new PpmMongoContext(database.Client, database), failMarkerOnce);
        }

        public AuditIntent Intent(DateTime? occurredAtUtc = null)
        {
            var occurred = occurredAtUtc ?? DateTime.UtcNow;
            occurred = new DateTime(
                occurred.Ticks - occurred.Ticks % TimeSpan.TicksPerMillisecond,
                DateTimeKind.Utc);
            return new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Project",
                Guid.NewGuid(),
                "created",
                occurred);
        }

        public async Task InsertTransactionally(AuditIntent intent)
        {
            var unitOfWork = new PpmUnitOfWork(Context);
            await unitOfWork.ExecuteInTransactionAsync(
                async cancellationToken =>
                {
                    await Repository.AddAsync(intent, cancellationToken);
                    return true;
                },
                default);
        }

        public PpmAuditIntentDispatcher CreateDispatcher(
            IAuditIntentRepository audit,
            PpmAuditProducerOptions? options = null)
        {
            var selectedOptions = options ?? Options;
            var metadata = new PpmAuditTrustedTransportMetadataProvider(
                audit,
                Microsoft.Extensions.Options.Options.Create(selectedOptions));
            var bus = new OutboxEventBus(
                Store,
                new EventPayloadContractValidator(),
                metadata,
                Producer,
                2048);
            return new PpmAuditIntentDispatcher(
                audit,
                bus,
                Microsoft.Extensions.Options.Options.Create(selectedOptions),
                NullLogger<PpmAuditIntentDispatcher>.Instance);
        }
    }

    private sealed class FailMarkerOnceAuditIntentRepository(IAuditIntentRepository inner)
        : IAuditIntentRepository
    {
        private int _failuresRemaining = 1;

        public Task AddAsync(AuditIntent intent, CancellationToken cancellationToken) =>
            inner.AddAsync(intent, cancellationToken);

        public Task<IReadOnlyList<AuditIntentDispatchCandidate>> GetDispatchCandidatesAsync(
            int batchSize,
            CancellationToken cancellationToken) =>
            inner.GetDispatchCandidatesAsync(batchSize, cancellationToken);

        public Task<AuditIntentDispatchMetadata> EnsureDispatchMetadataAsync(
            Guid intentId,
            AuditIntentDispatchMetadata proposed,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken) =>
            inner.EnsureDispatchMetadataAsync(intentId, proposed, updatedAtUtc, cancellationToken);

        public Task<bool> MarkOutboxEnqueuedAsync(
            Guid intentId,
            DateTime enqueuedAtUtc,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _failuresRemaining, 0) == 1)
            {
                throw new MarkerCrashException();
            }

            return inner.MarkOutboxEnqueuedAsync(intentId, enqueuedAtUtc, cancellationToken);
        }

        public Task<bool> MarkDispatchQuarantinedAsync(
            Guid intentId,
            string failureCode,
            DateTime updatedAtUtc,
            CancellationToken cancellationToken) =>
            inner.MarkDispatchQuarantinedAsync(intentId, failureCode, updatedAtUtc, cancellationToken);
    }

    private sealed class MarkerCrashException : Exception;
}
