namespace Diten.AuthService.Application.S2S;

public sealed record DelegatedActorProofProvenance(
    Guid ServicePrincipalId,
    Guid CredentialId,
    long ServicePrincipalVersion,
    long CredentialGeneration,
    Guid TenantId,
    Guid DelegatedActorId,
    Guid DelegationId,
    string ClientId,
    string Audience,
    string OperationId,
    IReadOnlyList<string> Permissions);
