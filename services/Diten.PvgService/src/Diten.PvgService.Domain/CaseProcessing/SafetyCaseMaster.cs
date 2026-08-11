namespace Diten.PvgService.Domain.CaseProcessing;

public sealed record SafetyCaseMaster(
    string CaseProcessingId,
    string TenantId,
    Mod0230HandoffReference HandoffReference,
    SafetyCaseMasterStatus Status,
    SignalMinimumLifecycleState LifecycleState,
    SignalMinimumAssessment? Assessment,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static SafetyCaseMaster AcceptHandoff(
        string caseProcessingId,
        string serverTenantId,
        Mod0230HandoffReference handoffReference,
        DateTimeOffset acceptedAtUtc) =>
        new(
            caseProcessingId,
            serverTenantId,
            handoffReference,
            SafetyCaseMasterStatus.HandoffAccepted,
            SignalMinimumLifecycleState.IntakeAccepted,
            null,
            acceptedAtUtc,
            acceptedAtUtc);

    public SafetyCaseMaster WithAssessment(SignalMinimumAssessment assessment, DateTimeOffset updatedAtUtc) =>
        this with
        {
            Assessment = assessment,
            Status = SafetyCaseMasterStatus.AssessmentUpdated,
            LifecycleState = SignalMinimumLifecycleState.AssessmentPending,
            UpdatedAtUtc = updatedAtUtc
        };

    public SafetyCaseMaster MarkSignalMinimumReady(DateTimeOffset updatedAtUtc) =>
        this with
        {
            Status = SafetyCaseMasterStatus.SignalMinimumReady,
            LifecycleState = SignalMinimumLifecycleState.SignalMinimumReady,
            UpdatedAtUtc = updatedAtUtc
        };
}
