namespace Diten.AuthService.Domain.S2S;

public sealed record DelegatedActorProofV1(
    string Type,
    string Issuer,
    string Audience,
    Guid ServicePrincipalId,
    string ClientId,
    string AuthorizedParty,
    string Jti,
    string Nonce,
    Guid TenantId,
    Guid DelegatedActorId,
    Guid DelegationId,
    string OperationId,
    IReadOnlyList<string> Permissions,
    string Scope,
    string RequestHash,
    long IssuedAt,
    long NotBefore,
    long ExpiresAt,
    long TenantGrantVersion,
    long ServicePrincipalVersion,
    long CredentialGeneration)
{
    public const string ExactType = "diten-delegated-actor-proof+jwt";
    public const string ExactIssuer = "diten-auth-service";
    public const string ExactActorType = "service";
    public const string ExactDelegatedActorType = "tenant_user";
    public const string ExactScope = "diten.s2s.delegated.invoke";
    public const int MaximumLifetimeSeconds = 300;
    public const int MaximumClockSkewSeconds = 30;
}
