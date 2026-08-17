using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities;

public sealed class TenantModuleEntitlement : GlobalEntity
{
    public Guid TenantId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public EntitlementSource Source { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset? ExpiryDateUtc { get; set; }
    public string? Reason { get; set; }
    public byte[] RowVersion { get; set; } = Guid.NewGuid().ToByteArray();
}
