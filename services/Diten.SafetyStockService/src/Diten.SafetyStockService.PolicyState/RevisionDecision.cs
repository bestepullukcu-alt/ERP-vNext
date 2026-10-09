namespace Diten.SafetyStockService.PolicyState;

public sealed record RevisionDecision(
    string ActorId,
    PolicyRevisionState Outcome,
    string? Reason,
    long RevisionVersion);
