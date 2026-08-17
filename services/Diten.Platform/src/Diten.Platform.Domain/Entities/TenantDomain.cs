using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities;

public sealed class TenantDomain : GlobalEntity
{
    public required Guid TenantId { get; init; }
    public required string DomainName { get; init; }
    public required DomainType Type { get; init; }
    public bool IsPrimary { get; set; }
    public bool IsLoginDomain { get; set; }
    public bool IsVerified { get; set; }
    public TenantDomainStatus Status { get; set; } = TenantDomainStatus.Active;
    public DateTimeOffset? VerifiedAt { get; set; }
}

public enum DomainType
{
    Platform = 1,
    Custom = 2
}

public enum TenantDomainStatus
{
    Active = 1,
    Inactive = 2,
    PendingVerification = 3
}
