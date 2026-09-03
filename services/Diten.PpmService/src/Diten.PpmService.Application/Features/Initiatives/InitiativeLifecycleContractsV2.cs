using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record InitiativeLifecycleContractsV2(
    string ContractVersion,
    IReadOnlyDictionary<InitiativeLifecycleState, IReadOnlyList<InitiativeLifecycleState>> AllowedTargetStatesBySource,
    IReadOnlyList<InitiativeLifecycleTransitionContract> Transitions,
    IReadOnlyList<string> CancellationReasons,
    IReadOnlyList<string> HoldReasons,
    IReadOnlyList<string> CompletionOutcomes,
    IReadOnlyList<string> ClosureReasons,
    IReadOnlyList<string> BenefitDispositions)
{
    public const string CurrentContractVersion = "2";
}
