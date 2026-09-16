using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.HumanCapital.SelfRegistration;

/// <summary>
/// Human Capital (HCM) — the DitenHumanCapitalService HR module self-registration manifest (nav wiring gap #4).
/// <para>Mirror of <see cref="Crm.SelfRegistration.CrmManifestProvider"/>: it reconciles the code-owned identity of
/// the <c>HUMAN-CAPITAL</c> catalog entry and its page/permission descriptors so the MOD-0285 data-driven sidebar
/// (<c>GET /api/platform/navigation/menu</c>) can render the 23 Human Capital pages. Before this provider existed the
/// HR pages were URL-reachable in Diten.Web but carried NO catalog / page-descriptor / permission entries — so they
/// never appeared in the tenant menu.</para>
/// <para>Every route below is the verbatim <c>Route("HumanCapital/…")</c> of the Diten.Web HR controller, and every
/// <see cref="ModuleManifestPage.RequiredPermission"/> is the verbatim key the owning <c>DitenHumanCapitalService</c>
/// controller <c>[HasPermission]</c> enforces (the Diten.Web controllers themselves carry only <c>[Authorize]</c> —
/// the granular permission gate lives on the backend service, so the backend <c>*Guard</c> constants are the source of
/// truth). Page actions carry the granular write/operational permissions (manage / evaluate / archive / review /
/// reference-link / source-link / handoff) so the module catalogue surfaces descriptors an RBAC admin can grant.</para>
/// <para>Like CRM this provider lives in Platform.Application (not inside DitenHumanCapitalService) and declares no HR
/// business capability. <b>Entitlement + RBAC grant (WP-C) and the seven-language <c>Nav.Page.*</c> labels (WP-D) are
/// separate work packages</b> — until a tenant is entitled to HUMAN-CAPITAL and granted the read permission, the
/// descriptor exists but no sidebar entry renders. <c>HCM/Employees</c> (MOD-0251, DitenHcmService) is a DISTINCT
/// module and is registered by <see cref="HcmEmployeeMasterManifestProvider"/> under its own ModuleCode.</para>
/// </summary>
public sealed class HumanCapitalManifestProvider : IModuleManifestProvider
{
    // Verbatim read-permission keys enforced by the DitenHumanCapitalService controllers ([HasPermission] via each
    // feature's *Guard.ReadPermission; EmployeeProjections gates on inline literals of the same shape).
    private const string ApplicantIntakeRead = "hcm.applicant-intake.read";
    private const string CandidatePipelineRead = "hcm.candidate-pipeline.read";
    private const string CompensationBenefitsRead = "hcm.compensation-benefits.read";
    private const string CompetencySkillsRead = "hcm.competency-skills.read";
    private const string DevelopmentPlansRead = "hcm.development-plans.read";
    private const string EmployeeOnboardingRead = "hcm.employee-onboarding.read";
    private const string EmployeeProjectionsRead = "hcm.employee-projections.read";
    private const string EmploymentChangesRead = "hcm.employment-changes.read";
    private const string HeadcountBudgetRead = "hcm.headcount-budget.read";
    private const string HrCaseManagementRead = "hcm.hr-case-management.read";
    private const string HrComplianceRead = "hcm.hr-compliance.read";
    private const string HrDocumentationRead = "hcm.hr-documentation.read";
    private const string HrKpiAnalyticsRead = "hcm.hr-kpi-analytics.read";
    private const string LearningTrainingRead = "hcm.learning-training.read";
    private const string OffboardingRead = "hcm.offboarding.read";
    private const string OfferManagementRead = "hcm.offer-management.read";
    private const string PerformanceReviewsRead = "hcm.performance-reviews.read";
    private const string PositionAssignmentsRead = "hcm.position-assignments.read";
    private const string SelfServiceRead = "hcm.self-service.read";
    private const string SensitiveAccessRead = "hcm.sensitive-access.read";
    private const string SuccessionRead = "hcm.succession.read";
    private const string TimeAttendanceLeaveRead = "hcm.time-attendance-leave.read";
    private const string WorkforcePlanningRead = "hcm.workforce-planning.read";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "HUMAN-CAPITAL",
            ModuleName: "Human Capital",
            DisplayName: "Human Capital",
            Domain: "Human Capital", // SOFT: only seeds on first-register; operator-owned thereafter.
            Service: "DitenHumanCapitalService", // SOFT.
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true, // SOFT.
            SortOrder: 70,
            Pages:
            [
                // Standard readiness/authoring modules — read gate on the page; MANAGE (create+update) + EVALUATE are
                // the granular write/operational permissions the backend controller enforces.
                new ModuleManifestPage("APPLICANT_INTAKE", "Applicant Intake", "/HumanCapital/ApplicantIntake", ApplicantIntakeRead, null, true, "List", 10,
                [
                    new ModuleManifestAction("MANAGE", "New Applicant Intake", "hcm.applicant-intake.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.applicant-intake.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("CANDIDATE_PIPELINE", "Candidate Pipeline", "/HumanCapital/CandidatePipeline", CandidatePipelineRead, null, true, "List", 20,
                [
                    new ModuleManifestAction("MANAGE", "New Candidate Pipeline", "hcm.candidate-pipeline.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.candidate-pipeline.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("COMPENSATION_BENEFITS", "Compensation & Benefits", "/HumanCapital/CompensationBenefits", CompensationBenefitsRead, null, true, "List", 30,
                [
                    new ModuleManifestAction("MANAGE", "New Compensation & Benefits", "hcm.compensation-benefits.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.compensation-benefits.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("COMPETENCY_SKILLS", "Competency & Skills", "/HumanCapital/CompetencySkills", CompetencySkillsRead, null, true, "List", 40,
                [
                    new ModuleManifestAction("MANAGE", "New Competency & Skills", "hcm.competency-skills.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.competency-skills.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("DEVELOPMENT_PLANS", "Development Plans", "/HumanCapital/DevelopmentPlans", DevelopmentPlansRead, null, true, "List", 50,
                [
                    new ModuleManifestAction("MANAGE", "New Development Plan", "hcm.development-plans.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.development-plans.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("EMPLOYEE_ONBOARDING", "Employee Onboarding", "/HumanCapital/EmployeeOnboarding", EmployeeOnboardingRead, null, true, "List", 60,
                [
                    new ModuleManifestAction("MANAGE", "New Employee Onboarding", "hcm.employee-onboarding.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.employee-onboarding.evaluate", "RowAction", 20, false, false, true)
                ]),
                // EmployeeProjections gates on inline "hcm.employee-projections.*" literals (no *Guard class); it exposes
                // source-link + archive rather than an evaluate endpoint.
                new ModuleManifestPage("EMPLOYEE_PROJECTIONS", "Employee Projections", "/HumanCapital/EmployeeProjections", EmployeeProjectionsRead, null, true, "List", 70,
                [
                    new ModuleManifestAction("MANAGE", "New Employee Projection", "hcm.employee-projections.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("MANAGE_SOURCE_LINK", "Manage Source Link", "hcm.employee-projections.source-link.manage", "RowAction", 20, false, false, true),
                    new ModuleManifestAction("ARCHIVE", "Archive", "hcm.employee-projections.archive", "RowAction", 30, false, false, true)
                ]),
                new ModuleManifestPage("EMPLOYMENT_CHANGES", "Employment Changes", "/HumanCapital/EmploymentChanges", EmploymentChangesRead, null, true, "List", 80,
                [
                    new ModuleManifestAction("MANAGE", "New Employment Change", "hcm.employment-changes.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.employment-changes.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("HEADCOUNT_BUDGET", "Headcount Budget", "/HumanCapital/HeadcountBudget", HeadcountBudgetRead, null, true, "List", 90,
                [
                    new ModuleManifestAction("MANAGE", "New Headcount Budget", "hcm.headcount-budget.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.headcount-budget.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("HR_CASE_MANAGEMENT", "HR Case Management", "/HumanCapital/HrCaseManagement", HrCaseManagementRead, null, true, "List", 100,
                [
                    new ModuleManifestAction("MANAGE", "New HR Case", "hcm.hr-case-management.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.hr-case-management.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("HR_COMPLIANCE", "HR Compliance", "/HumanCapital/HrCompliance", HrComplianceRead, null, true, "List", 110,
                [
                    new ModuleManifestAction("MANAGE", "New HR Compliance", "hcm.hr-compliance.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.hr-compliance.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("HR_DOCUMENTATION", "HR Documentation", "/HumanCapital/HrDocumentation", HrDocumentationRead, null, true, "List", 120,
                [
                    new ModuleManifestAction("MANAGE", "New HR Documentation", "hcm.hr-documentation.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.hr-documentation.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("HR_KPI_ANALYTICS", "HR KPI Analytics", "/HumanCapital/HrKpiAnalytics", HrKpiAnalyticsRead, null, true, "List", 130,
                [
                    new ModuleManifestAction("MANAGE", "New HR KPI Analytics", "hcm.hr-kpi-analytics.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.hr-kpi-analytics.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("LEARNING_TRAINING", "Learning & Training", "/HumanCapital/LearningTraining", LearningTrainingRead, null, true, "List", 140,
                [
                    new ModuleManifestAction("MANAGE", "New Learning & Training", "hcm.learning-training.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.learning-training.evaluate", "RowAction", 20, false, false, true)
                ]),
                // OffboardingCases exposes review + handoff + archive (no evaluate).
                new ModuleManifestPage("OFFBOARDING_CASES", "Offboarding Cases", "/HumanCapital/OffboardingCases", OffboardingRead, null, true, "List", 150,
                [
                    new ModuleManifestAction("MANAGE", "New Offboarding Case", "hcm.offboarding.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("REVIEW", "Review", "hcm.offboarding.review", "RowAction", 20, false, false, true),
                    new ModuleManifestAction("MANAGE_HANDOFF", "Manage Handoff", "hcm.offboarding.handoff.manage", "RowAction", 30, false, false, true),
                    new ModuleManifestAction("ARCHIVE", "Archive", "hcm.offboarding.archive", "RowAction", 40, false, false, true)
                ]),
                new ModuleManifestPage("OFFER_MANAGEMENT", "Offer Management", "/HumanCapital/OfferManagement", OfferManagementRead, null, true, "List", 160,
                [
                    new ModuleManifestAction("MANAGE", "New Offer", "hcm.offer-management.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.offer-management.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("PERFORMANCE_REVIEWS", "Performance Reviews", "/HumanCapital/PerformanceReviews", PerformanceReviewsRead, null, true, "List", 170,
                [
                    new ModuleManifestAction("MANAGE", "New Performance Review", "hcm.performance-reviews.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.performance-reviews.evaluate", "RowAction", 20, false, false, true)
                ]),
                // PositionAssignments exposes reference-link + archive (no evaluate).
                new ModuleManifestPage("POSITION_ASSIGNMENTS", "Position Assignments", "/HumanCapital/PositionAssignments", PositionAssignmentsRead, null, true, "List", 180,
                [
                    new ModuleManifestAction("MANAGE", "New Position Assignment", "hcm.position-assignments.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("MANAGE_REFERENCE_LINK", "Manage Reference Link", "hcm.position-assignments.reference-link.manage", "RowAction", 20, false, false, true),
                    new ModuleManifestAction("ARCHIVE", "Archive", "hcm.position-assignments.archive", "RowAction", 30, false, false, true)
                ]),
                new ModuleManifestPage("SELF_SERVICE", "Self Service", "/HumanCapital/SelfService", SelfServiceRead, null, true, "List", 190,
                [
                    new ModuleManifestAction("MANAGE", "New Self Service", "hcm.self-service.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.self-service.evaluate", "RowAction", 20, false, false, true)
                ]),
                // SensitiveAccess exposes review (no evaluate); audit.read is a read gate, not surfaced as an action.
                new ModuleManifestPage("SENSITIVE_ACCESS", "Sensitive Access", "/HumanCapital/SensitiveAccess", SensitiveAccessRead, null, true, "List", 200,
                [
                    new ModuleManifestAction("MANAGE", "Manage Sensitive Access", "hcm.sensitive-access.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("REVIEW", "Review", "hcm.sensitive-access.review", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("SUCCESSION", "Succession", "/HumanCapital/Succession", SuccessionRead, null, true, "List", 210,
                [
                    new ModuleManifestAction("MANAGE", "New Succession", "hcm.succession.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.succession.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("TIME_ATTENDANCE_LEAVE", "Time, Attendance & Leave", "/HumanCapital/TimeAttendanceLeave", TimeAttendanceLeaveRead, null, true, "List", 220,
                [
                    new ModuleManifestAction("MANAGE", "New Time, Attendance & Leave", "hcm.time-attendance-leave.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.time-attendance-leave.evaluate", "RowAction", 20, false, false, true)
                ]),
                new ModuleManifestPage("WORKFORCE_PLANNING", "Workforce Planning", "/HumanCapital/WorkforcePlanning", WorkforcePlanningRead, null, true, "List", 230,
                [
                    new ModuleManifestAction("MANAGE", "New Workforce Planning", "hcm.workforce-planning.manage", "Toolbar", 10, false, true, false),
                    new ModuleManifestAction("EVALUATE", "Evaluate", "hcm.workforce-planning.evaluate", "RowAction", 20, false, false, true)
                ])
            ],
            Icon: "bx-group",
            IsBaseline: false); // HARD: HR is a licensed module — tenant entitlement required (never entitlement-free).
}
