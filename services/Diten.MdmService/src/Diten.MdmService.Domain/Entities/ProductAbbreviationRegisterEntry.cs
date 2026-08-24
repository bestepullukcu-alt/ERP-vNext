using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

public sealed class ProductAbbreviationRegisterEntry : EntityBase
{
    public string NormalizedAbbreviation { get; set; } = string.Empty;
    public Guid GlobalProductId { get; set; }
    public Guid AllocationLedgerId { get; set; }
    public string AllocationIdempotencyKey { get; set; } = string.Empty;
    public ProductAbbreviationLifecycleStatus LifecycleStatus { get; set; }
        = ProductAbbreviationLifecycleStatus.REQUESTED;
    public string RequestedByCanonicalSubjectId { get; set; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; set; }
    public Guid? ReplacesEntryId { get; set; }
    public string? RetirementRequestId { get; set; }
    public string? RetirementRequestedByCanonicalSubjectId { get; set; }
    public DateTimeOffset? RetirementRequestedAtUtc { get; set; }
    public string? LastDecisionByCanonicalSubjectId { get; set; }
    public string? LastDecisionIdempotencyKey { get; set; }
    public string? LastDecisionReason { get; set; }
    public DateTimeOffset? LastDecisionAtUtc { get; set; }
}
