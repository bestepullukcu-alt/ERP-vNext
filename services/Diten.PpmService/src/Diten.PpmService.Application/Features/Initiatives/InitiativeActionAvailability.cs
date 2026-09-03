using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Application.Features.Initiatives;

public sealed record InitiativeActionAvailability(
    InitiativeLifecycleState TargetState,
    string Availability,
    string ReasonCode,
    string RequiredCompanionDataKind)
{
    public const string Available = "available";
    public const string Forbidden = "forbidden";
    public const string DependencyUnavailable = "dependency-unavailable";
    public const string RecordNotReady = "record-not-ready";
    public const string LifecyclePermissionDeniedReason = "lifecycle-permission-denied";
    public const string EntitlementAuthorityUnavailableReason = "entitlement-authority-unavailable";
    public const string ActivationDataIncompleteReason = "activation-data-incomplete";
    public const string ApprovalAuthorityUnavailableReason = "approval-authority-unavailable";
}
