namespace Diten.Platform.Common.Authorization.Fu16;

public sealed record Fu16TrustedRequestContext(
    Guid TenantId,
    Guid EffectiveActorId,
    Guid DelegatedActorId,
    Guid DelegationId,
    Guid ServicePrincipalId,
    Guid CredentialId,
    string ClientId,
    string Issuer,
    string Audience,
    string TokenType,
    string ProtocolScope,
    string OperationId,
    IReadOnlyList<string> Permissions,
    string RequestHash,
    long TenantGrantVersion,
    long ServicePrincipalVersion,
    long CredentialGeneration,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset ExpiresAtUtc)
{
    public Fu16TrustedRequestContext Validate()
    {
        if (TenantId == Guid.Empty || EffectiveActorId == Guid.Empty || DelegatedActorId == Guid.Empty
            || DelegationId == Guid.Empty || ServicePrincipalId == Guid.Empty || CredentialId == Guid.Empty)
            throw new ArgumentException("FU16 trusted identity is incomplete.");
        if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(Issuer)
            || string.IsNullOrWhiteSpace(Audience) || string.IsNullOrWhiteSpace(TokenType)
            || string.IsNullOrWhiteSpace(ProtocolScope) || string.IsNullOrWhiteSpace(OperationId)
            || RequestHash.Length != 64 || RequestHash.Any(static c => !char.IsAsciiHexDigit(c) || char.IsUpper(c)))
            throw new ArgumentException("FU16 trusted strings are invalid.");
        if (Permissions.Count == 0 || Permissions.Any(string.IsNullOrWhiteSpace)
            || Permissions.Distinct(StringComparer.Ordinal).Count() != Permissions.Count)
            throw new ArgumentException("FU16 permissions must be non-empty and ordinally unique.");
        if (TenantGrantVersion < 0 || ServicePrincipalVersion < 0 || CredentialGeneration < 0
            || IssuedAtUtc > NotBeforeUtc || NotBeforeUtc >= ExpiresAtUtc
            || ExpiresAtUtc - IssuedAtUtc > TimeSpan.FromMinutes(5))
            throw new ArgumentException("FU16 freshness fields are invalid.");
        return this;
    }
}
