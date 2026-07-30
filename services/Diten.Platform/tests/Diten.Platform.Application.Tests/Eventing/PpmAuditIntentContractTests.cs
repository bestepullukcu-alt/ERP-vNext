using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Infrastructure.Eventing;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Enums;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Eventing;

public sealed class PpmAuditIntentContractTests
{
    private const string CurrentSecret = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";
    private const string PreviousSecret = "Hh8gISIjJCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0=";
    private static readonly Guid EventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid EntityId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid CorrelationId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTimeOffset OccurredAtUtc = DateTimeOffset.Parse("2026-07-30T10:20:30.0000000Z");

    [Fact]
    public void ImmutableFixtureMatchesCanonicalPayloadHashAndSignature()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Eventing", "ppm-audit-intent-submitted-v1.json");
        using var fixture = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var message = ValidMessage(fixture.RootElement.GetProperty("canonicalPayload").GetString()!);
        var intent = PpmAuditIntentParser.Parse(message);

        Assert.Equal(fixture.RootElement.GetProperty("payloadSha256").GetString(), intent.PayloadSha256);
        Assert.Equal(fixture.RootElement.GetProperty("canonicalPayload").GetString(), Encoding.UTF8.GetString(intent.CanonicalPayload));

        var signature = Convert.ToHexString(HMACSHA256.HashData(
            Convert.FromBase64String(CurrentSecret),
            PpmAuditIntentParser.BuildSigningInput(message, intent.CanonicalPayload))).ToLowerInvariant();
        Assert.Equal(fixture.RootElement.GetProperty("signatureLowerHex").GetString(), signature);
    }

    [Theory]
    [InlineData("Portfolio")]
    [InlineData("Initiative")]
    [InlineData("Program")]
    [InlineData("Project")]
    public void ClosedEntityTypesAreAccepted(string entityType) =>
        Assert.Equal(entityType, PpmAuditIntentParser.Parse(ValidMessage(Payload(entityType: entityType))).EntityType);

    [Theory]
    [InlineData("created")]
    [InlineData("updated")]
    [InlineData("lifecycle-changed")]
    [InlineData("soft-deleted")]
    public void ClosedMutationsAreAccepted(string mutation) =>
        Assert.Equal(mutation, PpmAuditIntentParser.Parse(ValidMessage(Payload(mutation: mutation))).Mutation);

    [Theory]
    [InlineData("created", AuditOperation.Create)]
    [InlineData("updated", AuditOperation.Update)]
    [InlineData("lifecycle-changed", AuditOperation.LifecycleTransition)]
    [InlineData("soft-deleted", AuditOperation.Delete)]
    public void ClosedMutationsMapToExactAuditOperations(string mutation, AuditOperation expected) =>
        Assert.Equal(expected, PpmAuditAcceptanceRepository.MapOperation(mutation));

    [Theory]
    [MemberData(nameof(InvalidPayloads))]
    public void InvalidPayloadsFailClosed(string payload) =>
        Assert.Throws<PpmAuditContractException>(() => PpmAuditIntentParser.Parse(ValidMessage(payload)));

    public static IEnumerable<object[]> InvalidPayloads()
    {
        yield return [Payload().Replace("\"mutation\":\"created\"", "\"mutation\":\"created\",\"unknown\":true")];
        yield return [Payload().Replace("\"mutation\":\"created\"", "\"mutation\":\"created\",\"mutation\":\"created\"")];
        yield return [Payload().Replace("\"entityType\":\"Project\"", "\"entityType\":[]")];
        yield return [Payload().Replace("\"entityType\":\"Project\"", "\"entityType\":{}")];
        yield return [Payload().Replace("\"entityType\":\"Project\"", "\"entityType\":\"Unknown\"")];
        yield return [Payload().Replace("\"mutation\":\"created\"", "\"mutation\":\"unknown\"")];
        yield return [Payload().Replace(EventId.ToString("D"), Guid.Empty.ToString("D"))];
        yield return [Payload().Replace("\"occurredAtUtc\":\"2026-07-30T10:20:30.0000000Z\"", "\"occurredAtUtc\":\"2026-07-30T13:20:30.0000000+03:00\"")];
        yield return ["{\"auditIntentId\":{\"nested\":{\"too\":\"deep\"}}}"];
        yield return [Payload(entityType: new string('A', 2100))];
        yield return [Payload() + " true"];
        yield return [Payload() + " "];
        yield return [Payload().Replace("\"actorId\"", "\"actor\\u0049d\"")];
        yield return [Payload().Replace(
            $"\"actorId\":\"{ActorId:D}\",\"auditIntentId\":\"{EventId:D}\"",
            $"\"auditIntentId\":\"{EventId:D}\",\"actorId\":\"{ActorId:D}\"")];
        yield return [Payload()[..^1]];
        yield return ["\ud800"];
    }

    [Fact]
    public void NullPayloadFailsAsContractError()
    {
        var message = ValidMessage(Payload()) with { PayloadJson = null! };
        Assert.Throws<PpmAuditContractException>(() => PpmAuditIntentParser.Parse(message));
    }

    [Fact]
    public void EnvelopePayloadMismatchFailsClosed()
    {
        var message = ValidMessage(Payload()) with { EventId = Guid.NewGuid() };
        Assert.Throws<PpmAuditContractException>(() => PpmAuditIntentParser.Parse(message));
    }

    [Fact]
    public void CurrentAndPreviousKeysVerifyAndWrongOrInternalKeyFails()
    {
        var message = ValidMessage(Payload());
        var intent = PpmAuditIntentParser.Parse(message);
        var verifier = Verifier(enabled: true);

        verifier.Verify(message, intent, PpmAuditIntentParser.SignatureScheme, "current", Sign(message, intent, CurrentSecret));
        verifier.Verify(message, intent, PpmAuditIntentParser.SignatureScheme, "previous", Sign(message, intent, PreviousSecret));
        Assert.Throws<PpmAuditSecurityException>(() =>
            verifier.Verify(message, intent, PpmAuditIntentParser.SignatureScheme, "current", Sign(message, intent, Convert.ToBase64String(Encoding.UTF8.GetBytes("AuthService:InternalApiKey-is-not-valid")))));
        Assert.Throws<PpmAuditSecurityException>(() =>
            verifier.Verify(message, intent, PpmAuditIntentParser.SignatureScheme, "wrong", Sign(message, intent, CurrentSecret)));
        Assert.Throws<PpmAuditSecurityException>(() =>
            verifier.Verify(message, intent, null, null, null));
        Assert.Throws<PpmAuditSecurityException>(() =>
            verifier.Verify(
                message,
                intent,
                PpmAuditIntentParser.SignatureScheme,
                "current",
                Sign(message, intent, CurrentSecret).ToUpperInvariant()));
    }

    [Fact]
    public void DisabledActivationRejectsBeforeAuthentication()
    {
        var message = ValidMessage(Payload());
        var intent = PpmAuditIntentParser.Parse(message);
        Assert.Throws<PpmAuditSecurityException>(() =>
            Verifier(enabled: false).Verify(message, intent, PpmAuditIntentParser.SignatureScheme, "current", Sign(message, intent, CurrentSecret)));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("current", "short")]
    [InlineData("current", "change-me-placeholder")]
    public void EnabledInvalidSecretFailsValidation(string keyId, string secret)
    {
        var result = new PpmAuditConsumerOptionsValidator().Validate(null, new PpmAuditConsumerOptions
        {
            Enabled = true,
            ActiveKeyId = keyId,
            ActiveSecret = secret
        });
        Assert.True(result.Failed);
    }

    [Fact]
    public void ActivationRegistersConsumerOnlyWhenEnabledAndRetryContractIsFiveTotalAttempts()
    {
        static ServiceCollection Build(bool enabled)
        {
            var services = new ServiceCollection();
            services.AddMassTransit(registration =>
                Diten.Platform.Infrastructure.DependencyInjection.AddPlatformEventConsumers(registration, enabled));
            return services;
        }

        Assert.DoesNotContain(Build(false), descriptor =>
            descriptor.ServiceType == typeof(PpmAuditIntentSubmittedV1Consumer));
        Assert.Contains(Build(true), descriptor =>
            descriptor.ServiceType == typeof(PpmAuditIntentSubmittedV1Consumer));
        Assert.Equal(5, PpmAuditIntentSubmittedV1ConsumerDefinition.TotalAttempts);
        Assert.Equal(4, PpmAuditIntentSubmittedV1ConsumerDefinition.RetryCount);
        Assert.True(typeof(PpmAuditContractException).IsAssignableFrom(typeof(PpmAuditPayloadConflictException)));
        Assert.True(typeof(PpmAuditContractException).IsAssignableFrom(typeof(PpmAuditSecurityException)));
    }

    [Fact]
    public void RetryPolicyHasExactFirstDelayExponentialJitterAndMaximum()
    {
        var minimum = PpmAuditRetryPolicy.CreateDelays(() => 0d);
        var maximum = PpmAuditRetryPolicy.CreateDelays(() => 1d);

        Assert.Equal(4, minimum.Length);
        Assert.Equal(TimeSpan.FromSeconds(10), minimum[0]);
        Assert.Equal(TimeSpan.FromSeconds(10), maximum[0]);
        Assert.Equal([20d, 40d, 80d], minimum.Skip(1).Select(delay => delay.TotalSeconds));
        Assert.Equal([24d, 48d, 96d], maximum.Skip(1).Select(delay => delay.TotalSeconds));
        Assert.All(maximum, delay => Assert.InRange(delay, TimeSpan.Zero, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void TenantScopeBindsAndRestoresConsumerTenant()
    {
        var context = new TenantContext();
        var outerTenant = Guid.NewGuid();
        context.SetTenant(outerTenant);
        using (TenantScope.Begin(context, TenantId))
        {
            Assert.True(context.IsResolved);
            Assert.Equal(TenantId, context.TenantId);
        }
        Assert.Equal(outerTenant, context.TenantId);
    }

    private static PpmAuditSignatureVerifier Verifier(bool enabled) =>
        new(Options.Create(new PpmAuditConsumerOptions
        {
            Enabled = enabled,
            ActiveKeyId = "current",
            ActiveSecret = CurrentSecret,
            PreviousKeyId = "previous",
            PreviousSecret = PreviousSecret
        }));

    private static string Sign(EventTransportMessage message, PpmAuditIntent intent, string secret) =>
        Convert.ToHexString(HMACSHA256.HashData(
            Convert.FromBase64String(secret),
            PpmAuditIntentParser.BuildSigningInput(message, intent.CanonicalPayload))).ToLowerInvariant();

    private static EventTransportMessage ValidMessage(string payload) =>
        new(EventId, PpmAuditIntentParser.EventName, 1, CorrelationId, null, TenantId,
            PpmAuditIntentParser.Producer, OccurredAtUtc, payload);

    private static string Payload(string entityType = "Project", string mutation = "created") =>
        $"{{\"actorId\":\"{ActorId:D}\",\"auditIntentId\":\"{EventId:D}\",\"entityId\":\"{EntityId:D}\",\"entityType\":\"{entityType}\",\"mutation\":\"{mutation}\",\"occurredAtUtc\":\"2026-07-30T10:20:30.0000000Z\"}}";
}
