namespace Diten.MdmService.Application.Contracts.Audit;

public sealed record TrustedSourceAuditServiceIdentity(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc);
