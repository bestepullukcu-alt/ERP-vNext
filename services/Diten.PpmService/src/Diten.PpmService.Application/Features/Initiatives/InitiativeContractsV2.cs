namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record InitiativeContractsV2(IReadOnlyList<InitiativeContractOption> InitiativeTypes,
    IReadOnlyList<InitiativeContractOption> Priorities, IReadOnlyList<string> CancellationReasons,
    IReadOnlyList<string> HoldReasons, IReadOnlyList<string> CompletionOutcomes,
    IReadOnlyList<string> ClosureReasons, IReadOnlyList<string> BenefitDispositions);
