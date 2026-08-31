namespace Diten.BuildingBlocks.Eventing;

public sealed record SignedEventAuthorizationTuple
{
    public SignedEventAuthorizationTuple(
        string producer,
        string eventName,
        int eventVersion,
        string scheme,
        string signingIdentity)
    {
        Producer = RequireExact(producer, nameof(producer));
        EventName = RequireExact(eventName, nameof(eventName));
        if (eventVersion < 1)
        {
            throw new EventContractException("Event version must be positive.", "event.version.invalid");
        }

        EventVersion = eventVersion;
        Scheme = RequireExact(scheme, nameof(scheme));
        SigningIdentity = RequireExact(signingIdentity, nameof(signingIdentity));
    }

    public string Producer { get; }
    public string EventName { get; }
    public int EventVersion { get; }
    public string Scheme { get; }
    public string SigningIdentity { get; }

    public bool Matches(SignedEventAuthorizationTuple other) =>
        other is not null
        && EventVersion == other.EventVersion
        && string.Equals(Producer, other.Producer, StringComparison.Ordinal)
        && string.Equals(EventName, other.EventName, StringComparison.Ordinal)
        && string.Equals(Scheme, other.Scheme, StringComparison.Ordinal)
        && string.Equals(SigningIdentity, other.SigningIdentity, StringComparison.Ordinal);

    private static string RequireExact(string value, string name)
    {
        if (string.IsNullOrEmpty(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(character => character is '\r' or '\n' or '\t'))
        {
            throw new EventContractException($"{name} is invalid.", "event.authorization-tuple.invalid");
        }

        return value;
    }
}

public sealed record EventSigningKey(string KeyId, ReadOnlyMemory<byte> Secret);

public sealed record EventVerificationKey(
    string KeyId,
    ReadOnlyMemory<byte> Secret,
    bool IsPrevious = false,
    DateTimeOffset? OverlapDeadlineUtc = null,
    DateTimeOffset? RevokedAtUtc = null)
{
    public void EnsureUsable(DateTimeOffset nowUtc)
    {
        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new EventValidationException("Verification time must be UTC.");
        }

        if (RevokedAtUtc is not null)
        {
            throw new EventSecurityException("The signing key generation is revoked.", "event.key.revoked");
        }

        if (IsPrevious
            && (OverlapDeadlineUtc is null
                || OverlapDeadlineUtc.Value.Offset != TimeSpan.Zero
                || nowUtc >= OverlapDeadlineUtc.Value))
        {
            throw new EventSecurityException("The previous signing key overlap has expired.", "event.key.overlap-expired");
        }

        if (!IsPrevious && OverlapDeadlineUtc is not null)
        {
            throw new EventValidationException("An active key cannot have a previous-key overlap deadline.");
        }
    }
}

public sealed class EventVerificationKeySet
{
    public EventVerificationKeySet(EventVerificationKey active, EventVerificationKey? previous = null)
    {
        ArgumentNullException.ThrowIfNull(active);
        if (active.IsPrevious || active.RevokedAtUtc is not null || active.OverlapDeadlineUtc is not null)
        {
            throw new EventValidationException("The active verification key state is invalid.");
        }

        if (string.IsNullOrEmpty(active.KeyId) || active.Secret.IsEmpty)
        {
            throw new EventValidationException("The active verification key is incomplete.");
        }

        if (previous is not null
            && (!previous.IsPrevious
                || previous.OverlapDeadlineUtc is null
                || previous.OverlapDeadlineUtc.Value.Offset != TimeSpan.Zero
                || string.IsNullOrEmpty(previous.KeyId)
                || previous.Secret.IsEmpty
                || string.Equals(active.KeyId, previous.KeyId, StringComparison.Ordinal)))
        {
            throw new EventValidationException("The previous verification key state is invalid.");
        }

        Active = active;
        Previous = previous;
    }

    public EventVerificationKey Active { get; }
    public EventVerificationKey? Previous { get; }

    public EventVerificationKey Resolve(string keyId, DateTimeOffset nowUtc)
    {
        var key = string.Equals(Active.KeyId, keyId, StringComparison.Ordinal)
            ? Active
            : Previous is not null && string.Equals(Previous.KeyId, keyId, StringComparison.Ordinal)
                ? Previous
                : throw new EventSecurityException("The signing key is not trusted.", "event.key.untrusted");
        key.EnsureUsable(nowUtc);
        return key;
    }
}

public interface IEventSigningKeyProvider
{
    ValueTask<EventSigningKey> GetSigningKeyAsync(
        SignedEventAuthorizationTuple authorization,
        CancellationToken cancellationToken = default);
}

public interface IEventVerificationKeyProvider
{
    ValueTask<EventVerificationKey> GetVerificationKeyAsync(
        SignedEventAuthorizationTuple authorization,
        string keyId,
        CancellationToken cancellationToken = default);
}

public interface IEventSigningInputBuilder
{
    byte[] Build(EventTransportMessage message, string scheme);
}

public interface IEventSignatureVerifier
{
    ValueTask VerifyAsync(
        EventTransportMessage message,
        SignedEventAuthorizationTuple authorization,
        string signingIdentity,
        CancellationToken cancellationToken = default);
}
