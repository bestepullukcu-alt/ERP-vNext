using System.Security.Cryptography;
using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.PpmService.Application.Events;
using Diten.PpmService.Domain.Repositories;
using Diten.PpmService.Infrastructure.Audit;
using Xunit;

namespace Diten.PpmService.Tests;

public sealed class PpmAuditCanonicalContractTests
{
    private static readonly Guid AuditIntentId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TenantId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid EntityId =
        Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid CorrelationId =
        Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTime OccurredAtUtc =
        DateTime.Parse(
            "2026-07-30T10:20:30.0000000Z",
            null,
            System.Globalization.DateTimeStyles.RoundtripKind);

    private const string ExpectedPayload =
        "{\"actorId\":\"22222222-2222-2222-2222-222222222222\"," +
        "\"auditIntentId\":\"11111111-1111-1111-1111-111111111111\"," +
        "\"entityId\":\"44444444-4444-4444-4444-444444444444\"," +
        "\"entityType\":\"Project\",\"mutation\":\"created\"," +
        "\"occurredAtUtc\":\"2026-07-30T10:20:30.0000000Z\"}";

    [Fact]
    public void Fixture_emits_exact_six_property_bytes_and_sha256()
    {
        var @event = CreateEvent();
        var payload = ((ICanonicalIntegrationEvent)@event).CanonicalPayloadUtf8.ToArray();

        Assert.Equal(ExpectedPayload, Encoding.UTF8.GetString(payload));
        Assert.Equal(
            "fd82d7c05ae88372bab689fd38975fb7f0a839b5b56693bb0d7ba8e304633445",
            Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant());
        Assert.False(payload.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.Equal(AuditIntentId, @event.AuditIntentId);
    }

    [Fact]
    public void Fixture_emits_exact_hmac_signing_contract()
    {
        var payload = ((ICanonicalIntegrationEvent)CreateEvent()).CanonicalPayloadUtf8;
        var metadata = new EventMetadata(
            AuditIntentId,
            PpmAuditIntentSubmittedV1.CanonicalEventName,
            PpmAuditIntentSubmittedV1.CanonicalEventVersion,
            CorrelationId,
            null,
            TenantId,
            "Diten.PpmService",
            new DateTimeOffset(OccurredAtUtc));
        var secret = Convert.FromBase64String(
            "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=");
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            secret,
            PpmAuditTrustedTransportMetadataProvider.BuildSigningInput(
                metadata,
                payload.Span))).ToLowerInvariant();

        Assert.Equal(
            "ea31a1822130953463f4705493aa1e4de6a09752801dd52d810dfbc461e9d40e",
            signature);
    }

    [Fact]
    public void Producer_options_are_fail_closed_and_disabled_without_secret_is_valid()
    {
        var validator = new PpmAuditProducerOptionsValidator();
        Assert.True(validator.Validate(null, new PpmAuditProducerOptions()).Succeeded);
        Assert.False(validator.Validate(null, new PpmAuditProducerOptions
        {
            Enabled = true,
            WorkerEnabled = true
        }).Succeeded);
        Assert.False(validator.Validate(null, EnabledOptions(secret: "not-base64")).Succeeded);
        Assert.False(validator.Validate(null, EnabledOptions(
            secret: Convert.ToBase64String(new byte[16]))).Succeeded);
        Assert.False(validator.Validate(null, EnabledOptions(
            keyId: "bad\r\nkey")).Succeeded);
    }

    [Theory]
    [InlineData("portfolio")]
    [InlineData("Task")]
    [InlineData("Project ")]
    [InlineData("")]
    public void Entity_type_is_closed_and_case_sensitive(string entityType)
    {
        Assert.Throws<EventValidationException>(
            () => CreateEvent(entityType: entityType));
    }

    [Theory]
    [InlineData("Created")]
    [InlineData("approved")]
    [InlineData("scheduled")]
    [InlineData("")]
    public void Mutation_is_closed_and_case_sensitive(string mutation)
    {
        Assert.Throws<EventValidationException>(
            () => CreateEvent(mutation: mutation));
    }

    [Fact]
    public void Non_utc_timestamp_fails_closed()
    {
        Assert.Throws<EventValidationException>(
            () => CreateEvent(occurredAtUtc: DateTime.SpecifyKind(
                OccurredAtUtc,
                DateTimeKind.Unspecified)));
    }

    [Fact]
    public void Correlation_is_not_part_of_payload_but_remains_dispatch_candidate_metadata()
    {
        var candidate = CreateCandidate();
        var @event = new PpmAuditIntentSubmittedV1(candidate);

        Assert.Equal(CorrelationId, candidate.CorrelationId);
        Assert.DoesNotContain(
            CorrelationId.ToString("D"),
            Encoding.UTF8.GetString(
                ((ICanonicalIntegrationEvent)@event).CanonicalPayloadUtf8.Span),
            StringComparison.Ordinal);
    }

    private static PpmAuditIntentSubmittedV1 CreateEvent(
        string entityType = "Project",
        string mutation = "created",
        DateTime? occurredAtUtc = null) =>
        new(CreateCandidate(entityType, mutation, occurredAtUtc));

    private static AuditIntentDispatchCandidate CreateCandidate(
        string entityType = "Project",
        string mutation = "created",
        DateTime? occurredAtUtc = null) =>
        new(
            AuditIntentId,
            TenantId,
            ActorId,
            CorrelationId,
            entityType,
            EntityId,
            mutation,
            occurredAtUtc ?? OccurredAtUtc,
            null);

    private static PpmAuditProducerOptions EnabledOptions(
        string? secret = null,
        string keyId = "ppm-current") =>
        new()
        {
            Enabled = true,
            WorkerEnabled = true,
            KeyId = keyId,
            SecretBase64 = secret ?? Convert.ToBase64String(
                Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()),
            RabbitMqHost = "localhost",
            RabbitMqUsername = "ppm",
            RabbitMqPassword = "runtime-only"
        };
}
