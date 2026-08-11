using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public sealed record PvgPortDecision(
    bool IsAllowed,
    bool IsSatisfied,
    string ReasonCode)
{
    public static PvgPortDecision Denied(string reasonCode) => new(false, false, reasonCode);

    public static PvgPortDecision FieldSecurityDenied() =>
        Denied(PvgSafeReasonCodes.FieldSecurityPolicyUnavailable);

    public static PvgPortDecision WorkflowTransitionDenied() =>
        Denied(PvgSafeReasonCodes.WorkflowTransitionGateUnavailable);

    public static PvgPortDecision EvidenceLinkDenied() =>
        Denied(PvgSafeReasonCodes.EvidenceLinkUnavailable);
}
