namespace Diten.Platform.API.Security;

public sealed record TrustedWorkflowDelegatedUserIdentity(
    Guid UserId,
    Guid TenantId);
