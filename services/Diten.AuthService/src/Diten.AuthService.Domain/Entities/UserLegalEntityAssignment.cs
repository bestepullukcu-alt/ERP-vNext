namespace Diten.AuthService.Domain.Entities;

/// <summary>
/// F2 legal-entity scoping: assigns a user access to a legal entity (many per user).
/// Global collection (mirrors TenantUserMembership) keyed by UserId; carries its own TenantId.
/// Uniqueness (TenantId, UserId, LegalEntityId) is enforced by a Mongo index.
/// </summary>
public sealed class UserLegalEntityAssignment : GlobalEntityBase
{
    private UserLegalEntityAssignment() { }

    public UserLegalEntityAssignment(Guid userId, Guid tenantId, Guid legalEntityId)
    {
        UserId = userId;
        TenantId = tenantId;
        LegalEntityId = legalEntityId;
    }

    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
}
