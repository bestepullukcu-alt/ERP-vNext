using System.Security.Cryptography;
using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Infrastructure.Eventing;
using Xunit;

namespace Diten.Platform.Application.Tests.Eventing;

public sealed class GateIAuditContractTests
{
    internal const string MechanicsSigningIdentity = "gate-i-mechanics-fixture-identity";
    [Fact]
    public void Registry_ContainsTheFiveExplicitGovernanceProfilesAndExactMappings()
    {
        var registry = new GateIAuditAdapterRegistry();
        Assert.NotEqual(PpmAuditAcceptanceRepository.ConsumerName, GateIAuditAcceptanceRepository.ConsumerName);
        Assert.Equal(6, registry.ForProducer("Diten.ManagementGovernanceService",
            "management-governance.decision-registry-audit-intent.submitted.v1", 1).Count);
        Assert.Equal(14, registry.ForProducer("Diten.FpaService",
            "fpa.budgeting-audit-intent.submitted.v1", 1).Count);
        Assert.Equal(14, registry.ForProducer("Diten.FpaService",
            "fpa.scenario-planning-audit-intent.submitted.v1", 1).Count);
        Assert.Equal(5, registry.ForProducer("Diten.DecisionIntelligenceService",
            "decision-intelligence.outcome-tracking-audit-intent.submitted.v1", 1).Count);
        Assert.Equal(20, registry.ForProducer("Diten.ManagementGovernanceService",
            "management-governance.process-modeling-audit-intent.submitted.v1", 1).Count);

        var processModelPublished = registry.Resolve("Diten.ManagementGovernanceService",
            "management-governance.process-modeling-audit-intent.submitted.v1", 1,
            "ProcessModelVersion", "Published");
        Assert.Equal(("MOD-0355", "ProcessModeling.ProcessModelVersion", AuditOperation.Activate),
            (processModelPublished.ModuleCode, processModelPublished.AuditEntityType, processModelPublished.Operation));

        var failed = registry.Resolve("Diten.FpaService",
            "fpa.scenario-planning-audit-intent.submitted.v1", 1, "ComparatorRun", "Failed");
        Assert.Equal(("MOD-0138", "ScenarioPlanning.ComparatorRun", AuditOperation.Execute, AuditOutcome.Failed),
            (failed.ModuleCode, failed.AuditEntityType, failed.Operation, failed.Outcome));
        Assert.Throws<EventUnsupportedException>(() => registry.Resolve("Diten.FpaService",
            "fpa.budgeting-audit-intent.submitted.v1", 1, "Budget", "Created"));
    }

    [Fact]
    public async Task Processor_ValidatesRawHeadersThenSignatureBeforeParsingAndAcceptance()
    {
        var profile = Profiles.Budgeting;
        var message = SyntheticMessage(profile);
        var verifier = new RecordingVerifier();
        var repository = new RecordingRepository();
        var processor = new GateIAuditConsumerProcessor(new GateIAuditAdapterRegistry(), verifier, repository);

        Assert.Equal(GateIAuditAcceptanceResult.Accepted,
            await processor.ProcessAsync(message, Headers(profile), MechanicsSigningIdentity));
        Assert.True(verifier.Called);
        Assert.True(repository.Called);

        await Assert.ThrowsAsync<EventContractException>(() => processor.ProcessAsync(
            message,
            Headers(profile).Concat([KeyValuePair.Create(
                TrustedTransportMetadata.SignatureHeader.ToLowerInvariant(), new string('b', 64))]),
            MechanicsSigningIdentity));
        Assert.Equal(1, repository.CallCount);
    }

    [Fact]
    public void Parser_RejectsNonCanonicalOrderUnknownFieldsAndEnvelopeDrift()
    {
        var profile = Profiles.Budgeting;
        var registry = new GateIAuditAdapterRegistry();
        Assert.Throws<EventContractException>(() => GateIAuditIntentParser.Parse(
            SyntheticMessage(profile, payloadOverride:
                "{\"actorId\":\"22222222-2222-2222-2222-222222222222\",\"auditIntentId\":\"11111111-1111-1111-1111-111111111111\",\"entityType\":\"Budget\",\"entityId\":\"44444444-4444-4444-4444-444444444444\",\"mutation\":\"created\",\"occurredAtUtc\":\"2026-08-06T12:00:00.0000000Z\"}"), registry, MechanicsSigningIdentity));
        Assert.Throws<EventContractException>(() => GateIAuditIntentParser.Parse(
            SyntheticMessage(profile, payloadOverride:
                Payload("Budget", "created")[..^1] + ",\"extra\":true}"), registry, MechanicsSigningIdentity));
        Assert.Throws<EventSecurityException>(() => GateIAuditIntentParser.Parse(
            SyntheticMessage(profile, eventId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")), registry,
            MechanicsSigningIdentity));
    }

    internal static EventTransportMessage SyntheticMessage(
        GateIAuditMapping profile,
        string? payloadOverride = null,
        Guid? eventId = null,
        Guid? correlationId = null,
        TrustedTransportMetadata? metadata = null) => new(
            eventId ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
            profile.EventName,
            profile.EventVersion,
            correlationId ?? Guid.Parse("55555555-5555-5555-5555-555555555555"),
            null,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            profile.Producer,
            DateTimeOffset.Parse("2026-08-06T12:00:00.0000000Z"),
            payloadOverride ?? Payload(profile.EntityType, profile.Mutation),
            metadata ?? new TrustedTransportMetadata(Headers(profile)));

    internal static string Payload(string entityType, string mutation) =>
        $"{{\"auditIntentId\":\"11111111-1111-1111-1111-111111111111\",\"actorId\":\"22222222-2222-2222-2222-222222222222\",\"entityType\":\"{entityType}\",\"entityId\":\"44444444-4444-4444-4444-444444444444\",\"mutation\":\"{mutation}\",\"occurredAtUtc\":\"2026-08-06T12:00:00.0000000Z\"}}";

    internal static KeyValuePair<string, string>[] Headers(GateIAuditMapping profile, string? signature = null) =>
    [
        KeyValuePair.Create(TrustedTransportMetadata.SignatureSchemeHeader, profile.Scheme),
        KeyValuePair.Create(TrustedTransportMetadata.KeyIdHeader, "gate-i-mechanics-fixture-k1"),
        KeyValuePair.Create(TrustedTransportMetadata.SignatureHeader, signature ?? new string('a', 64))
    ];

    internal static class Profiles
    {
        internal static GateIAuditMapping Budgeting => new GateIAuditAdapterRegistry().Resolve(
            "Diten.FpaService", "fpa.budgeting-audit-intent.submitted.v1", 1, "Budget", "created");
    }

    private sealed class RecordingVerifier : IEventSignatureVerifier
    {
        public bool Called { get; private set; }
        public ValueTask VerifyAsync(EventTransportMessage message, SignedEventAuthorizationTuple authorization,
            string signingIdentity, CancellationToken cancellationToken = default)
        {
            Called = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingRepository : IGateIAuditAcceptanceRepository
    {
        public bool Called { get; private set; }
        public int CallCount { get; private set; }
        public Task<GateIAuditAcceptanceResult> AcceptAsync(EventTransportMessage message, GateIAuditIntent intent,
            GateIAuditMapping mapping, CancellationToken cancellationToken)
        {
            Called = true;
            CallCount++;
            return Task.FromResult(GateIAuditAcceptanceResult.Accepted);
        }
    }
}
