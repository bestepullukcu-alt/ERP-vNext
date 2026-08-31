using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Infrastructure.Eventing;
using Diten.PpmService.Contracts.Events;
using Xunit;

namespace Diten.Platform.Application.Tests.Eventing;

public sealed class PpmAuditIntentV1TransportShapeValidatorTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Accepts_only_canonical_v1_payload_bound_to_verified_context()
    {
        var intent = CreateIntent();
        var message = CreateMessage(intent);

        var result = PpmAuditIntentV1TransportShapeValidator.Validate(
            message,
            new PpmAuditIntentV1VerifiedTransportContext(TenantId, ActorId));

        Assert.Same(message, result.Message);
        Assert.Equal(intent.AuditIntentId, result.AuditIntent.AuditIntentId);
        Assert.Equal(ActorId, result.VerifiedContext.ActorId);
    }

    [Fact]
    public void Rejects_payload_with_unknown_property_before_audit_mapping()
    {
        var intent = CreateIntent();
        var payload = Encoding.UTF8.GetString(((ICanonicalIntegrationEvent)intent).CanonicalPayloadUtf8.Span)
            .Replace("}", ",\"targetState\":\"Active\"}", StringComparison.Ordinal);
        var message = CreateMessage(intent, payload);

        Assert.Throws<EventValidationException>(() => PpmAuditIntentV1TransportShapeValidator.Validate(
            message,
            new PpmAuditIntentV1VerifiedTransportContext(TenantId, ActorId)));
    }

    [Fact]
    public void Rejects_unbound_actor_or_event_identity()
    {
        var intent = CreateIntent();
        var message = CreateMessage(intent, eventId: Guid.NewGuid());

        Assert.Throws<EventSecurityException>(() => PpmAuditIntentV1TransportShapeValidator.Validate(
            message,
            new PpmAuditIntentV1VerifiedTransportContext(TenantId, Guid.NewGuid())));
    }

    [Fact]
    public void Rejects_missing_signed_metadata()
    {
        var intent = CreateIntent();
        var message = new Diten.BuildingBlocks.Eventing.EventTransportMessage(
            intent.AuditIntentId,
            PpmAuditIntentSubmittedV1.CanonicalEventName,
            PpmAuditIntentSubmittedV1.CanonicalEventVersion,
            Guid.NewGuid(),
            null,
            TenantId,
            PpmAuditIntentV1AuditMapping.SourceService,
            DateTimeOffset.UtcNow,
            ((ICanonicalIntegrationEvent)intent).CanonicalPayloadUtf8);

        Assert.Throws<EventSecurityException>(() => PpmAuditIntentV1TransportShapeValidator.Validate(
            message,
            new PpmAuditIntentV1VerifiedTransportContext(TenantId, ActorId)));
    }

    private static PpmAuditIntentSubmittedV1 CreateIntent() => new(
        Guid.NewGuid(),
        ActorId,
        "Portfolio",
        Guid.NewGuid(),
        "lifecycle-changed",
        DateTime.UtcNow);

    private static Diten.BuildingBlocks.Eventing.EventTransportMessage CreateMessage(
        PpmAuditIntentSubmittedV1 intent,
        string? payloadJson = null,
        Guid? eventId = null) => new Diten.BuildingBlocks.Eventing.EventTransportMessage(
        eventId ?? intent.AuditIntentId,
        PpmAuditIntentSubmittedV1.CanonicalEventName,
        PpmAuditIntentSubmittedV1.CanonicalEventVersion,
        Guid.NewGuid(),
        null,
        TenantId,
        PpmAuditIntentV1AuditMapping.SourceService,
        DateTimeOffset.UtcNow,
        payloadJson is null
            ? ((ICanonicalIntegrationEvent)intent).CanonicalPayloadUtf8
            : Encoding.UTF8.GetBytes(payloadJson),
        new TrustedTransportMetadata(
        [
            new KeyValuePair<string, string>(TrustedTransportMetadata.SignatureSchemeHeader, "HMAC-SHA256"),
            new KeyValuePair<string, string>(TrustedTransportMetadata.KeyIdHeader, "ppm-audit-v1"),
            new KeyValuePair<string, string>(TrustedTransportMetadata.SignatureHeader, "AAECAwQFBgcICQ")
        ]));
}
