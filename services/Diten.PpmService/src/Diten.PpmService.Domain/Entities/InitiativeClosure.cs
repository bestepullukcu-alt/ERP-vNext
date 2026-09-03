namespace Diten.PpmService.Domain.Entities;

public sealed class InitiativeClosure : EntityBase
{
    public Guid InitiativeId { get; private set; }
    public string OutcomeCode { get; private set; } = "";
    public string ClosureReasonCode { get; private set; } = "";
    public DateTime CompletedAt { get; private set; }
    public string CompletionSummary { get; private set; } = "";
    public IReadOnlyList<InitiativeTypedReference> EvidenceReferences { get; private set; } = [];
    public IReadOnlyList<InitiativeTypedReference> FollowUpTaskReferences { get; private set; } = [];
    public string BenefitDisposition { get; private set; } = "";

    private InitiativeClosure() { }

    public InitiativeClosure(Guid tenantId, Guid actorId, Guid initiativeId, string outcomeCode,
        string closureReasonCode, DateTime completedAt, string completionSummary,
        IReadOnlyList<InitiativeTypedReference>? evidenceReferences,
        IReadOnlyList<InitiativeTypedReference>? followUpTaskReferences, string benefitDisposition,
        DateTime initiativeCreatedAtUtc) : base(tenantId, actorId)
    {
        if (initiativeId == Guid.Empty) throw new ArgumentException("InitiativeId is required.", nameof(initiativeId));
        if (completedAt.Kind != DateTimeKind.Utc || completedAt < initiativeCreatedAtUtc)
            throw new ArgumentException("CompletedAt must be UTC and cannot precede Initiative creation.", nameof(completedAt));
        InitiativeId = initiativeId;
        OutcomeCode = InitiativeVocabularies.RequireCompletionOutcome(outcomeCode);
        ClosureReasonCode = InitiativeVocabularies.RequireClosureReason(closureReasonCode);
        CompletedAt = completedAt;
        CompletionSummary = Required(completionSummary, 4000, nameof(CompletionSummary));
        EvidenceReferences = evidenceReferences?.ToArray() ?? [];
        FollowUpTaskReferences = followUpTaskReferences?.ToArray() ?? [];
        BenefitDisposition = InitiativeVocabularies.RequireBenefitDisposition(benefitDisposition);
    }
}
