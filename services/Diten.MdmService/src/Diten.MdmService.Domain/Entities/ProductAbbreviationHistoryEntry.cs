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
}
