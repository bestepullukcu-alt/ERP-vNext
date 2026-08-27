namespace Diten.AuthService.Application.S2S;

/// <summary>
/// Internal, non-transport binding used by Gate-I module enforcement. It carries no credential or key material.
/// </summary>
public sealed record GateIModuleAuthorizationBindingV1(
    Guid TenantId,
    string OwnerModuleId,
    string ModuleEntitlementCode,
    string ServiceIdentity,
    string ClientId,
    string Audience,
    string ProtocolScope,
    string OperationId,
    string PermissionKey,
    string RequestHash);
