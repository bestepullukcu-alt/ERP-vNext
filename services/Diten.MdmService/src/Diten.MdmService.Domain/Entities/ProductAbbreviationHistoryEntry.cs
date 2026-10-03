using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

public sealed class ProductAbbreviationHistoryEntry : EntityBase
{
    public Guid RegisterEntryId { get; set; }
    public Guid GlobalProductId { get; set; }
    public string NormalizedAbbreviation { get; set; } = string.Empty;
    public ProductAbbreviationHistoryEventType EventType { get; set; }
    public ProductAbbreviationLifecycleStatus? BeforeStatus { get; set; }
    public ProductAbbreviationLifecycleStatus? AfterStatus { get; set; }
    public string CanonicalHumanSubjectId { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string EvidenceHash { get; set; } = string.Empty;
    public DateTimeOffset OccurredAtUtc { get; set; }
    /// <summary>
    /// The register entry's version right after the decision this row records (FIX1 S1). The audit intent is built
    /// from it, so a row whose intent was lost can still be turned into the SAME intent after the entry moved on.
    /// Null on rows written before the field existed: the entry's current version is used for those.
    /// </summary>
    public int? PostVersion { get; set; }
}
