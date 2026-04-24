namespace Diten.AuthService.Domain.Entities;

public sealed class TenantUserMembership : GlobalEntityBase
{
    private TenantUserMembership() { }

    public TenantUserMembership(Guid userId, Guid tenantId, string email)
    {
        UserId = userId;
        TenantId = tenantId;
        Email = email;
    }

    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Email { get; private set; } = string.Empty;
}
