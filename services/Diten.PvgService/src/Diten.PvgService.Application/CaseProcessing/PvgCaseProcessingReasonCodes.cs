namespace Diten.PvgService.Application.CaseProcessing;

public static class PvgCaseProcessingReasonCodes
{
    public const string TenantContextRequired = "PVG_CASE_PROCESSING_TENANT_CONTEXT_REQUIRED";
    public const string ActorContextRequired = "PVG_CASE_PROCESSING_ACTOR_CONTEXT_REQUIRED";
    public const string PermissionContextRequired = "PVG_CASE_PROCESSING_PERMISSION_CONTEXT_REQUIRED";
    public const string PermissionDenied = "PVG_CASE_PROCESSING_PERMISSION_DENIED";
    public const string CorrelationContextRequired = "PVG_CASE_PROCESSING_CORRELATION_CONTEXT_REQUIRED";
    public const string CorrelationContextInvalid = "PVG_CASE_PROCESSING_CORRELATION_CONTEXT_INVALID";
    public const string Mod0230HandoffRequired = "PVG_CASE_PROCESSING_MOD0230_HANDOFF_REQUIRED";
    public const string HandoffReferenceInvalid = "PVG_CASE_PROCESSING_HANDOFF_REFERENCE_INVALID";
    public const string FieldPolicyRequired = "PVG_CASE_PROCESSING_FIELD_POLICY_REQUIRED";
    public const string FieldPolicyDenied = "PVG_CASE_PROCESSING_FIELD_POLICY_DENIED";
    public const string WorkflowGateRequired = "PVG_CASE_PROCESSING_WORKFLOW_GATE_REQUIRED";
    public const string WorkflowGateDenied = "PVG_CASE_PROCESSING_WORKFLOW_GATE_DENIED";
    public const string EvidenceCompletenessRequired = "PVG_CASE_PROCESSING_EVIDENCE_COMPLETENESS_REQUIRED";
    public const string EvidenceCompletenessDenied = "PVG_CASE_PROCESSING_EVIDENCE_COMPLETENESS_DENIED";
    public const string AssessmentRequired = "PVG_CASE_PROCESSING_ASSESSMENT_REQUIRED";
    public const string CaseProcessingIdRequired = "PVG_CASE_PROCESSING_ID_REQUIRED";
    public const string RequiredFieldMissing = "PVG_CASE_PROCESSING_REQUIRED_FIELD_MISSING";
}
