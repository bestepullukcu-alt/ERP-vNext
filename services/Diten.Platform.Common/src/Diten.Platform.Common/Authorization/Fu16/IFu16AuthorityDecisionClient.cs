namespace Diten.Platform.Common.Authorization.Fu16;

public enum Fu16AuthorityDisposition { Allowed, Denied, Conflict, Unavailable }

public sealed record Fu16AuthorityRequest(
    Guid TenantId,
    Guid EffectiveActorId,
    Guid DelegatedActorId,
    Guid DelegationId,
    Guid ServicePrincipalId,
    Guid CredentialId,
    string ClientId,
    string Issuer,
    string Audience,
    string ProtocolScope,
    string OperationId,
    IReadOnlyList<string> Permissions,
    string RequestHash,
    string Jti,
    string Nonce,
    long TenantGrantVersion,
    long ServicePrincipalVersion,
    long CredentialGeneration,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record Fu16AuthorityDecision(
    Fu16AuthorityDisposition Disposition,
    string StableCode,
    long TenantGrantVersion,
    long ServicePrincipalVersion,
    long CredentialGeneration);

public interface IFu16AuthorityDecisionClient
{
    Task<Fu16AuthorityDecision> AuthorizeAndConsumeReplayAsync(
        Fu16AuthorityRequest request,
        CancellationToken cancellationToken);
}
