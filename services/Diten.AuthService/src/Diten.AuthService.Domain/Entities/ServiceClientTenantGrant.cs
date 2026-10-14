namespace Diten.AuthService.Domain.Entities;

public sealed class ServiceClientTenantGrant : EntityBase
{
    public Guid ServiceClientIdentityId { get; init; }
    public string Audience { get; init; } = string.Empty;
    public bool IsEnabled { get; set; }
}
