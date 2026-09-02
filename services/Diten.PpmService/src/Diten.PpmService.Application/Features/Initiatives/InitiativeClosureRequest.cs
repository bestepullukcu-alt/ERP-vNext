using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record InitiativeClosureRequest(string OutcomeCode, string ClosureReasonCode,
    string CompletionSummary, IReadOnlyList<InitiativeTypedReference>? EvidenceReferences,
    IReadOnlyList<InitiativeTypedReference>? FollowUpTaskReferences, string BenefitDisposition);
