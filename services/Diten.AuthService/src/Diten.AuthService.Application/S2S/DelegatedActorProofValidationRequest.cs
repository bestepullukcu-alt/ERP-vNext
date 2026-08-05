namespace Diten.AuthService.Application.S2S;

public sealed record DelegatedActorProofValidationRequest(
    string Token,
    string Method,
    string Path,
    ReadOnlyMemory<byte> Body,
    Guid ExpectedTenantId,
    string ExpectedOperationId);
