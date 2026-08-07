using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Infrastructure.Eventing;

internal sealed class GateIAuditConsumerProcessor
{
    private readonly GateIAuditAdapterRegistry _registry;
    private readonly IEventSignatureVerifier _signatureVerifier;
    private readonly IGateIAuditAcceptanceRepository _repository;

    public GateIAuditConsumerProcessor(
        GateIAuditAdapterRegistry registry,
        IEventSignatureVerifier signatureVerifier,
        IGateIAuditAcceptanceRepository repository)
    {
        _registry = registry;
        _signatureVerifier = signatureVerifier;
        _repository = repository;
    }

    public async Task<GateIAuditAcceptanceResult> ProcessAsync(
        EventTransportMessage untrustedMessage,
        IEnumerable<KeyValuePair<string, string>> rawSignatureHeaders,
        string signingIdentity,
        CancellationToken cancellationToken = default)
    {
        var trustedHeaders = RawSignatureHeaderExtractor.Extract(rawSignatureHeaders);
        var message = new EventTransportMessage(
            untrustedMessage.EventId,
            untrustedMessage.EventName,
            untrustedMessage.EventVersion,
            untrustedMessage.CorrelationId,
            untrustedMessage.CausationId,
            untrustedMessage.TenantId,
            untrustedMessage.Producer,
            untrustedMessage.OccurredAtUtc,
            untrustedMessage.CanonicalPayloadUtf8,
            trustedHeaders);

        var candidates = _registry.ForProducer(message.Producer, message.EventName, message.EventVersion);
        if (candidates.Count == 0)
        {
            throw new EventUnsupportedException("The Gate I producer/event/version is unsupported.",
                "gate-i.profile.unsupported");
        }

        var profile = candidates[0];
        if (candidates.Any(candidate =>
                !string.Equals(candidate.Scheme, profile.Scheme, StringComparison.Ordinal)
                || !string.Equals(candidate.ModuleCode, profile.ModuleCode, StringComparison.Ordinal)))
        {
            throw new EventTerminalValidationException("The Gate I adapter profile is ambiguous.",
                "gate-i.profile.ambiguous");
        }

        var authorization = new SignedEventAuthorizationTuple(
            profile.Producer, profile.EventName, profile.EventVersion, profile.Scheme, signingIdentity);
        await _signatureVerifier.VerifyAsync(message, authorization, signingIdentity, cancellationToken);
        var intent = GateIAuditIntentParser.Parse(message, _registry, signingIdentity);
        var mapping = _registry.Resolve(
            message.Producer, message.EventName, message.EventVersion, intent.EntityType, intent.Mutation);
        return await _repository.AcceptAsync(message, intent, mapping, cancellationToken);
    }
}
