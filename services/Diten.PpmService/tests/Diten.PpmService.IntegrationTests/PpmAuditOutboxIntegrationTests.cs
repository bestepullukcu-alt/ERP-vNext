using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Persistence.Mongo;
using Diten.PpmService.Persistence.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.PpmService.IntegrationTests;

[Collection(PpmMongoCollection.CollectionName)]
public sealed class PpmAuditOutboxIntegrationTests
{
    private static string _replicaSetConnection = string.Empty;

    public PpmAuditOutboxIntegrationTests(PpmDisposableMongo mongo) =>
        _replicaSetConnection = mongo.ReplicaSetConnectionString;

    [Fact]
    public async Task Duplicate_is_no_op_but_changed_immutable_content_conflicts()
    {
        var fixture = await Fixture.Create();
        var request = fixture.Request();

        Assert.Equal(
            EventOutboxWriteResult.Inserted,
            await fixture.Store.EnqueueAsync(request));
        Assert.Equal(
            EventOutboxWriteResult.Duplicate,
            await fixture.Store.EnqueueAsync(request));

        var changed = request with
        {
            CanonicalPayloadUtf8 = Encoding.UTF8.GetBytes("{\"changed\":true}")
        };
        await Assert.ThrowsAsync<EventOutboxConflictException>(
            () => fixture.Store.EnqueueAsync(changed));
        Assert.Equal(1, await fixture.Context.EventOutbox.CountDocumentsAsync(_ => true));
    }

    [Fact]
    public async Task Concurrent_enqueue_and_claim_produce_one_record_and_one_winner()
    {
        var fixture = await Fixture.Create();
        var request = fixture.Request();

        var writes = await Task.WhenAll(
            fixture.Store.EnqueueAsync(request),
            fixture.Store.EnqueueAsync(request));
        Assert.Contains(EventOutboxWriteResult.Inserted, writes);
        Assert.Contains(EventOutboxWriteResult.Duplicate, writes);

        var now = DateTimeOffset.UtcNow;
        var claims = await Task.WhenAll(
            fixture.Store.ClaimForPublishAsync(now, now.AddMinutes(-5)),
            fixture.Store.ClaimForPublishAsync(now, now.AddMinutes(-5)));
        Assert.Single(claims, item => item is not null);
        Assert.Equal(1, await fixture.Context.EventOutbox.CountDocumentsAsync(_ => true));
    }

    [Fact]
    public async Task Stale_publish_is_reclaimed_and_published_cannot_dead_letter()
    {
        var fixture = await Fixture.Create();
        var request = fixture.Request();
        await fixture.Store.EnqueueAsync(request);
        var first = await fixture.Store.ClaimForPublishAsync(
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddMinutes(-20));
        Assert.NotNull(first);

        var reclaimed = await fixture.Store.ClaimForPublishAsync(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.NotNull(reclaimed);
        Assert.Equal(request.Metadata.EventId, reclaimed.Metadata.EventId);

        await fixture.Store.CompletePublishAsync(request.Metadata.EventId);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Store.DeadLetterPublishAsync(
                request.Metadata.EventId,
                new EventOutboxTerminalFailure(
                    EventOutboxTerminalFailureKind.Contract,
                    "ppm.audit.contract-invalid")));
    }

    [Fact]
    public async Task Dispatch_metadata_has_one_CAS_winner_and_survives_key_rotation_retry()
    {
        var fixture = await Fixture.Create();
        var intent = fixture.Intent();
        await fixture.Context.AuditIntents.InsertOneAsync(new AuditIntentDocument
        {
            Id = intent.Id,
            TenantId = intent.TenantId,
            ActorId = intent.ActorId,
            CorrelationId = intent.CorrelationId,
            EntityType = intent.EntityType,
            EntityId = intent.EntityId,
            Mutation = intent.Mutation,
            OccurredAtUtc = intent.OccurredAtUtc
        });
        var current = new AuditIntentDispatchMetadata("scheme", "current", new string('a', 64));
        var rotated = new AuditIntentDispatchMetadata("scheme", "rotated", new string('b', 64));

        var selected = await Task.WhenAll(
            fixture.Audit.EnsureDispatchMetadataAsync(
                intent.Id, current, DateTime.UtcNow, default),
            fixture.Audit.EnsureDispatchMetadataAsync(
                intent.Id, rotated, DateTime.UtcNow, default));

        Assert.Equal(selected[0], selected[1]);
        var replay = await fixture.Audit.EnsureDispatchMetadataAsync(
            intent.Id,
            selected[0] == current ? rotated : current,
            DateTime.UtcNow,
            default);
        Assert.Equal(selected[0], replay);
    }

    [Fact]
    public async Task Enqueue_before_marker_crash_replays_duplicate_then_marks_once()
    {
        var fixture = await Fixture.Create();
        var intent = fixture.Intent();
        await fixture.Context.AuditIntents.InsertOneAsync(new AuditIntentDocument
        {
            Id = intent.Id,
            TenantId = intent.TenantId,
            ActorId = intent.ActorId,
            CorrelationId = intent.CorrelationId,
            EntityType = intent.EntityType,
            EntityId = intent.EntityId,
            Mutation = intent.Mutation,
            OccurredAtUtc = intent.OccurredAtUtc
        });
        var request = fixture.Request(intent.Id, intent.CorrelationId, intent.TenantId);

        Assert.Equal(EventOutboxWriteResult.Inserted, await fixture.Store.EnqueueAsync(request));
        Assert.Equal(EventOutboxWriteResult.Duplicate, await fixture.Store.EnqueueAsync(request));
        var markedAt = DateTime.UtcNow;
        Assert.True(await fixture.Audit.MarkOutboxEnqueuedAsync(intent.Id, markedAt, default));
        Assert.False(await fixture.Audit.MarkOutboxEnqueuedAsync(intent.Id, markedAt, default));
        Assert.Empty(await fixture.Audit.GetDispatchCandidatesAsync(10, default));
    }

    private sealed class Fixture
    {
        private Fixture(PpmMongoContext context)
        {
            Context = context;
            Store = new PpmEventOutboxStore(context);
            Audit = new AuditIntentRepository(context);
        }

        public PpmMongoContext Context { get; }
        public PpmEventOutboxStore Store { get; }
        public AuditIntentRepository Audit { get; }

        public static async Task<Fixture> Create()
        {
            var database = PpmMongoTestDatabase.Open(_replicaSetConnection);
            await PpmMongoTestDatabase.ResetAsync(database);
            await new PpmMongoIndexInitializer(database).StartAsync(default);
            return new Fixture(new PpmMongoContext(database.Client, database));
        }

        public AuditIntentDispatchCandidate Intent() =>
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Project",
                Guid.NewGuid(),
                "created",
                DateTime.UtcNow,
                null);

        public EventOutboxWriteRequest Request(
            Guid? eventId = null,
            Guid? correlationId = null,
            Guid? tenantId = null)
        {
            var metadata = new EventMetadata(
                eventId ?? Guid.NewGuid(),
                "ppm.audit-intent.submitted.v1",
                1,
                correlationId ?? Guid.NewGuid(),
                null,
                tenantId ?? Guid.NewGuid(),
                "Diten.PpmService",
                DateTimeOffset.UtcNow);
            var headers = new TrustedTransportMetadata(
            [
                new(TrustedTransportMetadata.SignatureSchemeHeader, "ppm-event-hmac-sha256.v1"),
                new(TrustedTransportMetadata.KeyIdHeader, "current"),
                new(TrustedTransportMetadata.SignatureHeader, new string('a', 64))
            ]);
            return new EventOutboxWriteRequest(
                metadata,
                Encoding.UTF8.GetBytes("{\"event\":\"fixture\"}"),
                headers);
        }
    }
}
