using Diten.BuildingBlocks.Eventing;
using Xunit;

namespace Diten.BuildingBlocks.Eventing.Tests;

public sealed class EventNameContractTests
{
    [Theory]
    [InlineData("tenant.activated.v1")]
    [InlineData("ppm.audit-intent.submitted.v1")]
    [InlineData("portfolio-delivery.project-created.confirmed.v2")]
    [InlineData("a.b.v1")]
    public void IsValid_AcceptsDotSeparatedSegmentsWithInternalLowercaseKebabCase(string eventName)
    {
        Assert.True(EventName.IsValid(eventName));
    }

    [Theory]
    [InlineData("Ppm.audit-intent.submitted.v1")]
    [InlineData("ppm.Audit-intent.submitted.v1")]
    [InlineData("ppm.audit--intent.submitted.v1")]
    [InlineData("ppm.-audit.submitted.v1")]
    [InlineData("ppm.audit-.submitted.v1")]
    [InlineData("ppm.audit_intent.submitted.v1")]
    [InlineData("ppm.audit intent.submitted.v1")]
    [InlineData("ppm.audit-intent.submitted")]
    [InlineData("ppm.audit-intent.submitted.v0")]
    [InlineData("ppm.audit-intent.submitted.v01")]
    [InlineData("ppm..submitted.v1")]
    [InlineData("ppm.audit-1intent.submitted.v1")]
    [InlineData(" ppm.audit-intent.submitted.v1")]
    [InlineData("ppm.audit-intent.submitted.v1 ")]
    public void IsValid_RejectsInvalidGrammar(string eventName)
    {
        Assert.False(EventName.IsValid(eventName));
    }

    [Fact]
    public void EnsureMatchesVersion_AcceptsCanonicalPpmEventAtVersionOne()
    {
        EventName.EnsureMatchesVersion("ppm.audit-intent.submitted.v1", 1);
    }

    [Fact]
    public void EnsureMatchesVersion_RejectsCanonicalPpmEventAtVersionTwo()
    {
        Assert.Throws<EventValidationException>(
            () => EventName.EnsureMatchesVersion("ppm.audit-intent.submitted.v1", 2));
    }

    [Fact]
    public void PayloadValidator_AcceptsCanonicalPpmEquivalentEvent()
    {
        new EventPayloadContractValidator().Validate(new PpmAuditIntentSubmittedV1(Guid.NewGuid()));
    }

    [Fact]
    public async Task OutboxEventBus_PassesCanonicalPpmEventToWriterAfterValidation()
    {
        var writer = new RecordingWriter();
        var eventBus = CreateEventBus(writer);
        var auditIntentId = Guid.NewGuid();

        await eventBus.PublishAsync(new PpmAuditIntentSubmittedV1(auditIntentId));

        Assert.NotNull(writer.Request);
        var request = writer.Request;
        Assert.Equal("ppm.audit-intent.submitted.v1", request.Metadata.EventName);
        Assert.Equal(1, request.Metadata.EventVersion);
        Assert.Contains(auditIntentId.ToString(), System.Text.Encoding.UTF8.GetString(request.CanonicalPayloadUtf8.Span));
    }

    [Fact]
    public async Task OutboxEventBus_RejectsInvalidEventNameBeforeCallingWriter()
    {
        var writer = new RecordingWriter();
        var eventBus = CreateEventBus(writer);

        await Assert.ThrowsAsync<EventValidationException>(
            () => eventBus.PublishAsync(new InvalidPpmAuditIntentEvent(Guid.NewGuid())));

        Assert.Null(writer.Request);
        Assert.Equal(0, writer.Calls);
    }

    private static OutboxEventBus CreateEventBus(IEventOutboxWriter writer) =>
        new(
            writer,
            new EventPayloadContractValidator(),
            new EmptyTrustedTransportMetadataProvider(),
            "Diten.BuildingBlocks.Eventing.Tests",
            64 * 1024);

    private sealed record PpmAuditIntentSubmittedV1(Guid AuditIntentId) : IIntegrationEvent
    {
        public string EventName => "ppm.audit-intent.submitted.v1";
        public int EventVersion => 1;
    }

    private sealed record InvalidPpmAuditIntentEvent(Guid AuditIntentId) : IIntegrationEvent
    {
        public string EventName => "ppm.audit--intent.submitted.v1";
        public int EventVersion => 1;
    }

    private sealed class RecordingWriter : IEventOutboxWriter
    {
        public EventOutboxWriteRequest? Request { get; private set; }
        public int Calls { get; private set; }

        public Task<EventOutboxWriteResult> EnqueueAsync(
            EventOutboxWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Request = request;
            return Task.FromResult(EventOutboxWriteResult.Inserted);
        }
    }
}
