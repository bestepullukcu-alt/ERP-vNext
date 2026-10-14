namespace Diten.Platform.API.Security;

public sealed record TrustedWorkflowConsumerServiceIdentity(
    Guid ClientId,
    Guid TenantId,
    Guid TokenId,
    string ServiceName,
    string Audience);
