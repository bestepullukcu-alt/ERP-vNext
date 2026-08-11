namespace Diten.PvgService.Application.CaseProcessing;

public sealed record PvgCaseProcessingGuardContext(
    PvgCaseProcessingPermissionDecision? PermissionDecision,
    PvgCaseProcessingPortDecision? FieldPolicyDecision,
    PvgCaseProcessingPortDecision? WorkflowGateDecision,
    PvgCaseProcessingPortDecision? EvidenceCompletenessDecision);

public sealed record PvgCaseProcessingPermissionDecision(bool IsAllowed, string ReasonCode)
{
    public static PvgCaseProcessingPermissionDecision Allowed() => new(true, "PVG_CASE_PROCESSING_PERMISSION_ALLOWED");

    public static PvgCaseProcessingPermissionDecision Denied(string? reasonCode = null) =>
        new(false, string.IsNullOrWhiteSpace(reasonCode) ? PvgCaseProcessingReasonCodes.PermissionDenied : reasonCode);
}

public sealed record PvgCaseProcessingPortDecision(bool IsAllowed, string ReasonCode)
{
    public static PvgCaseProcessingPortDecision Allowed(string reasonCode = "PVG_CASE_PROCESSING_PORT_ALLOWED") =>
        new(true, reasonCode);

    public static PvgCaseProcessingPortDecision Denied(string reasonCode) => new(false, reasonCode);
}
