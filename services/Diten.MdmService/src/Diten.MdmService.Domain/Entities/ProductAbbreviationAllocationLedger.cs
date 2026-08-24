using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

public sealed class ProductAbbreviationAllocationLedger : EntityBase
{
    public string NormalizedAbbreviation { get; set; } = string.Empty;
    public Guid GlobalProductId { get; set; }
    public Guid RegisterEntryId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public ProductAbbreviationAllocationState AllocationState { get; set; }
        = ProductAbbreviationAllocationState.DURABLY_ALLOCATED;
    public string AllocatedByCanonicalSubjectId { get; set; } = string.Empty;
    public DateTimeOffset AllocatedAtUtc { get; set; }
}
