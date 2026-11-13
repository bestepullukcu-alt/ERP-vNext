namespace Diten.Platform.API.Security;

/// <summary>
/// MOD-0262-FU01 DL-2 — the verified caller of the trusted document repository surface, read from a service token that
/// passed the <c>TrustedDocumentRepositoryConsumerService</c> scheme: <c>ClientId</c> = <c>sub</c>, <c>TenantId</c> =
/// <c>tenant_id</c> (the ONLY source of the tenant), <c>TokenId</c> = <c>jti</c>. The token itself is never kept.
/// </summary>
public sealed record TrustedDocumentRepositoryConsumerIdentity(
    Guid ClientId,
    Guid TenantId,
    Guid TokenId,
    string ServiceName,
    string Audience);
