namespace Diten.MdmService.Domain.Entities;

/// <summary>
/// Durable, tenant-scoped binding for a Finished Good creation command before identity allocation.
/// </summary>
public sealed class FinishedGoodCreationAttempt : EntityBase
{
    public Guid GskuId { get; set; }
    public string CreationCommandId { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
}
