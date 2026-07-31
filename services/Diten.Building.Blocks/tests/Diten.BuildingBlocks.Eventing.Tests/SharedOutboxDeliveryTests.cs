using System.Text;
using Diten.BuildingBlocks.Eventing;
using Xunit;

namespace Diten.BuildingBlocks.Eventing.Tests;

public sealed class SharedOutboxDeliveryTests
{
    private static readonly EventMetadata Metadata = new(
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        "ppm.audit-intent.submitted.v1", 1,
        Guid.Parse("20000000-0000-0000-0000-000000000002"), null,
        Guid.Parse("30000000-0000-0000-0000-000000000003"),
        "Diten.PpmService", DateTimeOffset.Parse("2026-07-31T00:00:00Z"));

    [Fact]
    public void Message_PreservesDefensiveExactCanonicalBytes_AndSharedIdentity()
    {
        var source = Encoding.UTF8.GetBytes("{\"z\":\"é\",\"a\":1 }");
        var expected = source.ToArray();
        var message = CreateMessage(source);
        source[0] = (byte)'X';
        Assert.Equal(expected, message.CanonicalPayloadUtf8.ToArray());
        Assert.Equal("{\"z\":\"é\",\"a\":1 }", message.PayloadJson);
        Assert.Equal("Diten.BuildingBlocks.Eventing.EventTransportMessage", message.GetType().FullName);
    }

    [Fact]
    public async Task Success_PreservesExactBytesAndHeaders_ThenCompletes()
    {
        var store = new FakeStore(Item());
        var publisher = new FakePublisher();
        var result = await Processor(store, publisher).PublishPendingAsync();
        Assert.Equal(EventOutboxPublishOutcome.Published, Assert.Single(result).Outcome);
        Assert.Equal(Item().CanonicalPayloadUtf8.ToArray(), publisher.Message!.CanonicalPayloadUtf8.ToArray());
        Assert.Equal(Item().TransportMetadata.Headers, publisher.Message.TransportMetadata.Headers);
        Assert.Equal((1, 0, 0), (store.CompleteCalls, store.FailCalls, store.DeadLetterCalls));
    }

    [Theory]
    [InlineData(EventOutboxTerminalFailureKind.Contract)]
    [InlineData(EventOutboxTerminalFailureKind.Security)]
    [InlineData(EventOutboxTerminalFailureKind.Validation)]
    [InlineData(EventOutboxTerminalFailureKind.Unsupported)]
    public async Task TerminalFailure_DeadLettersOnce_WithoutRetry(EventOutboxTerminalFailureKind kind)
    {
        var store = new FakeStore(Item());
        var publisher = new FakePublisher
        {
            Failure = new EventTransportTerminalException(
                new EventOutboxTerminalFailure(kind, "event.contract.rejected"))
        };
        var result = await Processor(store, publisher).PublishPendingAsync();
        Assert.Equal(EventOutboxPublishOutcome.DeadLettered, Assert.Single(result).Outcome);
        Assert.Equal((1, 1, 0), (publisher.Calls, store.DeadLetterCalls, store.FailCalls));
    }

    [Fact]
    public async Task TransientFailure_SchedulesExistingRetry_WithoutPersistingRawMessage()
    {
        var store = new FakeStore(Item());
        var publisher = new FakePublisher { Failure = new IOException("credential=do-not-store") };
        var result = await Processor(store, publisher).PublishPendingAsync();
        Assert.Equal(EventOutboxPublishOutcome.RetryScheduled, Assert.Single(result).Outcome);
        Assert.Equal((1, 0, nameof(IOException)), (store.FailCalls, store.DeadLetterCalls, store.Error));
    }

    [Fact]
    public async Task CallerCancellation_PropagatesWithoutStateMutation()
    {
        using var source = new CancellationTokenSource();
        var store = new FakeStore(Item());
        var publisher = new FakePublisher { CancellationSource = source };
        var exception = await Assert.ThrowsAsync<OperationCanceledException>(
            () => Processor(store, publisher).PublishPendingAsync(source.Token));
        Assert.Equal(source.Token, exception.CancellationToken);
        Assert.Equal(0, store.CompleteCalls + store.FailCalls + store.DeadLetterCalls);
        Assert.Equal(0, store.Item!.AttemptCount);
    }

    [Fact]
    public void PublicAssembly_HasNoPlatformMassTransitMongoOrHostingDependency()
    {
        var forbidden = new[] { "Diten.Platform", "MassTransit", "MongoDB", "Microsoft.Extensions.Hosting" };
        var references = typeof(EventOutboxPublisherProcessor).Assembly
            .GetReferencedAssemblies().Select(reference => reference.Name!).ToArray();
        Assert.DoesNotContain(references, reference =>
            forbidden.Any(prefix => reference.StartsWith(prefix, StringComparison.Ordinal)));
    }

    [Fact]
    public void TrustedMetadata_RejectsPartialWhitespaceCrlfUnknownAndCaseDuplicate()
    {
        Assert.Throws<EventValidationException>(() => new TrustedTransportMetadata([
            KeyValuePair.Create(TrustedTransportMetadata.SignatureHeader, " ")]));
        Assert.Throws<EventValidationException>(() => new TrustedTransportMetadata([
            KeyValuePair.Create(TrustedTransportMetadata.SignatureHeader, "abc\rdef")]));
        Assert.Throws<EventValidationException>(() => new TrustedTransportMetadata([
            KeyValuePair.Create("X-Business-Header", "value")]));
        Assert.Throws<EventValidationException>(() => new TrustedTransportMetadata([
            KeyValuePair.Create(TrustedTransportMetadata.SignatureHeader, "abc"),
            KeyValuePair.Create(TrustedTransportMetadata.SignatureHeader.ToLowerInvariant(), "def"),
            KeyValuePair.Create(TrustedTransportMetadata.KeyIdHeader, "key")]));
    }

    private static EventTransportMessage CreateMessage(byte[] payload) => new(
        Metadata.EventId, Metadata.EventName, Metadata.EventVersion, Metadata.CorrelationId,
        Metadata.CausationId, Metadata.TenantId, Metadata.Producer, Metadata.OccurredAtUtc,
        payload, SignedMetadata());

    private static EventOutboxPublishItem Item() => new(
        Metadata, Encoding.UTF8.GetBytes("{\"z\":\"é\",\"a\":1 }"), SignedMetadata(),
        EventOutboxDeliveryStatus.Publishing, 0, null);

    private static TrustedTransportMetadata SignedMetadata() => new([
        KeyValuePair.Create(TrustedTransportMetadata.SignatureSchemeHeader, "hmac-sha256"),
        KeyValuePair.Create(TrustedTransportMetadata.KeyIdHeader, "ppm-key-v1"),
        KeyValuePair.Create(TrustedTransportMetadata.SignatureHeader, new string('a', 64))]);

    private static EventOutboxPublisherProcessor Processor(IEventOutboxStore store, IEventTransportPublisher publisher) =>
        new(store, publisher, new EventOutboxPublisherOptions(
            1, 5, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)));

    private sealed class FakePublisher : IEventTransportPublisher
    {
        public Exception? Failure { get; init; }
        public CancellationTokenSource? CancellationSource { get; init; }
        public EventTransportMessage? Message { get; private set; }
        public int Calls { get; private set; }
        public Task PublishAsync(EventTransportMessage message, CancellationToken cancellationToken = default)
        {
            Calls++;
            Message = message;
            if (CancellationSource is not null)
            {
                CancellationSource.Cancel();
                throw new OperationCanceledException(cancellationToken);
            }
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }

    private sealed class FakeStore(EventOutboxPublishItem item) : IEventOutboxStore
    {
        private bool _claimed;
        public EventOutboxPublishItem? Item { get; } = item;
        public int CompleteCalls { get; private set; }
        public int FailCalls { get; private set; }
        public int DeadLetterCalls { get; private set; }
        public string? Error { get; private set; }
        public Task<EventOutboxWriteResult> EnqueueAsync(EventOutboxWriteRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(EventOutboxWriteResult.Inserted);
        public Task<EventOutboxPublishItem?> ClaimForPublishAsync(
            DateTimeOffset nowUtc, DateTimeOffset stalePublishingCutoffUtc, CancellationToken cancellationToken = default)
        {
            if (_claimed) return Task.FromResult<EventOutboxPublishItem?>(null);
            _claimed = true;
            return Task.FromResult(Item);
        }
        public Task CompletePublishAsync(Guid eventId, CancellationToken cancellationToken = default)
        { CompleteCalls++; return Task.CompletedTask; }
        public Task FailPublishAsync(Guid eventId, string error, DateTimeOffset nextAttemptAtUtc, int maxAttempts,
            CancellationToken cancellationToken = default)
        { FailCalls++; Error = error; return Task.CompletedTask; }
        public Task DeadLetterPublishAsync(Guid eventId, EventOutboxTerminalFailure failure,
            CancellationToken cancellationToken = default)
        { DeadLetterCalls++; return Task.CompletedTask; }
    }
}
