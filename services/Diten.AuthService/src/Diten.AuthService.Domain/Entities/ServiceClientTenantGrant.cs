namespace Diten.AuthService.Domain.Entities;

public sealed class ServiceClientTenantGrant : EntityBase
{
    public Guid ServiceClientIdentityId { get; init; }
    public string Audience { get; init; } = string.Empty;
    public bool IsEnabled { get; set; }
    public long OperationalVersion { get; set; }
    public Guid? LastOperationalCommandId { get; set; }
    public string? LastOperationalCommandFingerprint { get; set; }
}
