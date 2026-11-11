namespace Diten.Platform.API.Security;

public interface ITrustedRecordCodeCredentialAuthenticator
{
    TrustedRecordCodeCredentialResult Authenticate(string? identifier, string? secret, string? audience);
}

/// <summary>
/// <see cref="CallerService"/> is the closed-list service name of the binding that authenticated (what the audit record
/// names as its source service); <see cref="AllowedRecordTypes"/> the configured subset of that binding's record types.
/// </summary>
public sealed record TrustedRecordCodeCredentialResult(
    bool Authenticated,
    bool Forbidden,
    string? ConsumerService,
    string? CallerService,
    IReadOnlySet<string> AllowedRecordTypes)
{
    private static readonly IReadOnlySet<string> NoTypes = new HashSet<string>(StringComparer.Ordinal);

    public static TrustedRecordCodeCredentialResult Unauthenticated { get; } = new(false, false, null, null, NoTypes);
    public static TrustedRecordCodeCredentialResult Denied { get; } = new(false, true, null, null, NoTypes);
}
