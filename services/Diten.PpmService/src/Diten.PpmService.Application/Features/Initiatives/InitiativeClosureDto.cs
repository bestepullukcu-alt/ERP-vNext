using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record InitiativeClosureDto(Guid Id, Guid InitiativeId, string OutcomeCode,
    string ClosureReasonCode, DateTime CompletedAt, string CompletionSummary,
    IReadOnlyList<InitiativeTypedReference> EvidenceReferences,
    IReadOnlyList<InitiativeTypedReference> FollowUpTaskReferences, string BenefitDisposition);
