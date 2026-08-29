namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU11 — document training matrix + assignment enums (GMG-QMS-SOP-0001 §7.3, §17, §19 gate 5). Kept in a
// dedicated file so FU11 ownership never edits earlier FU enum surfaces. Roles reuse ApprovalRequiredRole for
// consistent naming across approval (FU09) and training (FU11).

public enum TrainingAudienceType
{
    Role = 0,
    User = 1,
    Department = 2,
    ThirdParty = 3
}

public enum DocumentTrainingType
{
    FullSopCompetencyAssessment = 0,
    ScenarioAssessment = 1,
    ReadAndUnderstand = 2,
    ThirdPartyVerifiedTraining = 3
}

public enum TrainingRequirementStatus
{
    Pending = 0,
    Assigned = 1,
    Completed = 2,
    Restricted = 3,

    /// <summary>A waiver was attempted — never permitted (SOP §19 non-waivable). Recorded, never honoured.</summary>
    WaivedNotAllowed = 4,
    Overdue = 5,
    Blocked = 6
}

public enum TrainingSourceRule
{
    Criticality = 0,
    DocumentClass = 1,
    Manual = 2,
    ImpactAssessment = 3,
    ThirdPartyAgreement = 4
}

public enum TrainingAssignmentStatus
{
    Assigned = 0,
    Completed = 1,
    Failed = 2,
    Restricted = 3,
    Cancelled = 4
}

public enum TrainingEffectivenessCheckStatus
{
    NotRequired = 0,
    Pending = 1,
    Passed = 2,
    Failed = 3
}
