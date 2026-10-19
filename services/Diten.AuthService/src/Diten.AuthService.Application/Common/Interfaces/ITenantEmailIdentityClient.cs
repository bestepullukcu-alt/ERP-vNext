namespace Diten.AuthService.Application.Common.Interfaces;

/// <summary>
/// BL-454 — who a tenant's e-mail is from, read from Platform (the tenant record and its messaging settings live
/// there): display name, language, the sender name the tenant chose for itself, and the reply address.
/// </summary>
public sealed record TenantEmailIdentity(
    string? DisplayName,
    string Language,
    string? SenderName,
    string? ReplyToEmail);

public interface ITenantEmailIdentityClient
{
    /// <summary>
    /// Never throws and never waits long. Null when Platform cannot answer in time or does not know the tenant —
    /// the caller then sends under the product's name in English. An e-mail that sets a password must not depend on
    /// a second service being up.
    /// </summary>
    Task<TenantEmailIdentity?> GetAsync(Guid tenantId, CancellationToken ct);
}
