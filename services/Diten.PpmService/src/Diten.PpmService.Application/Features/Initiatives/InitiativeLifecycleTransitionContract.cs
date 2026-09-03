using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record InitiativeLifecycleTransitionContract(
    InitiativeLifecycleState SourceState,
    InitiativeLifecycleState TargetState,
    string RequiredCompanionDataKind,
    string ApprovalDependencyDisposition)
{
    public const string NoCompanionData = "none";
    public const string CancellationReasonCompanionData = "cancellation-reason";
    public const string HoldReasonCompanionData = "hold-reason";
    public const string ClosureCompanionData = "closure";
    public const string DirectApprovalDisposition = "direct";
    public const string ApprovalAuthorityRequiredDisposition = "approval-authority-required";
}
