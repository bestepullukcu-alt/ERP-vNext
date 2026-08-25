namespace Diten.PvgService.Domain.RegPvBase;

public sealed class SafetyCaseIntake
{
    public SafetyCaseIntake(
        string tenantId,
        PvgIntakeStatus status,
        string intakeChannel,
        string sourceType,
        DateTimeOffset receivedAtUtc,
        string reporterType,
        string adverseEventNarrative,
        string seriousness,
        string intakePriority,
        PvgTriageOutcome? triageOutcome = null,
        string? triageReason = null,
        string? routeTargetQueue = null)
    {
        TenantId = tenantId;
        Status = status;
        IntakeChannel = intakeChannel;
        SourceType = sourceType;
        ReceivedAtUtc = receivedAtUtc;
        ReporterType = reporterType;
        AdverseEventNarrative = adverseEventNarrative;
        Seriousness = seriousness;
        IntakePriority = intakePriority;
        TriageOutcome = triageOutcome;
        TriageReason = triageReason;
        RouteTargetQueue = routeTargetQueue;
    }

    public string TenantId { get; }
    public PvgIntakeStatus Status { get; private set; }
    public string IntakeChannel { get; }
    public string SourceType { get; }
    public string? SourceReference { get; init; }
    public DateTimeOffset ReceivedAtUtc { get; }
    public string ReporterType { get; }
    public string? ReporterContactSummary { get; init; }
    public string? PatientSubjectCode { get; init; }
    public DateOnly? EventOnsetDate { get; init; }
    public string AdverseEventNarrative { get; }
    public string? SuspectProductText { get; init; }
    public string Seriousness { get; }
    public string IntakePriority { get; }
    public PvgTriageOutcome? TriageOutcome { get; private set; }
    public string? TriageReason { get; private set; }
    public string? RouteTargetQueue { get; private set; }
    public IReadOnlyList<string> EvidenceLinkReferences { get; init; } = [];

    public void MarkUpdated() => Status = PvgIntakeStatus.IntakeUpdated;

    public void MarkTriaged(PvgTriageOutcome outcome, string? triageReason)
    {
        Status = PvgIntakeStatus.Triaged;
        TriageOutcome = outcome;
        TriageReason = triageReason;
    }

    public void MarkRoutePending(string routeTargetQueue)
    {
        Status = PvgIntakeStatus.RoutePending;
        RouteTargetQueue = routeTargetQueue;
    }
}
