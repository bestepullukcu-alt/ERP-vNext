using System.Text.RegularExpressions;
using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.TimeAttendanceLeave;

public static class TimeAttendanceLeaveGuard
{
    public const string OwnerKey = "hcm.time-attendance-leave";
    public const string ReadPermission = "hcm.time-attendance-leave.read";
    public const string ManagePermission = "hcm.time-attendance-leave.manage";
    public const string EvaluatePermission = "hcm.time-attendance-leave.evaluate";
    public const string AuditReadPermission = "hcm.time-attendance-leave.audit.read";

    private static readonly string[] ForbiddenMarkers =
    [
        "workflowbody",
        "workflow_body",
        "reviewbody",
        "review_body",
        "appraisalbody",
        "appraisal_body",
        "appraisalnarrative",
        "appraisal_narrative",
        "approvalbody",
        "approval_body",
        "approvaldecision",
        "approval_decision",
        "reviewnote",
        "review_note",
        "reviewnotes",
        "review_notes",
        "score",
        "skillScoring",
        "rating",
        "rank",
        "ranking",
        "calibration",
        "modeloutput",
        "model_output",
        "automateddecision",
        "automated_decision",
        "positionmutation",
        "position_mutation",
        "positionassignmentpayload",
        "position_assignment_payload",
        "actionpayload",
        "action_payload",
        "managernote",
        "manager_note",
        "hrnote",
        "hr_note",
        "employeestatement",
        "employee_statement",
        "freetext",
        "free_text",
        "narrative",
        "attachment",
        "documentpayload",
        "document_payload",
        "rawpayload",
        "raw_payload",
        "providerpayload",
        "provider_payload",
        "credential",
        "access_token",
        "refresh_token",
        "secret",
        "password",
        "compensationamount",
        "compensation_amount",
        "salaryamount",
        "salary_amount",
        "salary",
        "wage",
        "payroll",
        "bank",
        "tax",
        "payslip",
        "benefitselection",
        "benefits_election",
        "nationalid",
        "national_id",
        "dateofbirth",
        "dob",
        "homeaddress",
        "home_address",
        "biometric",
        "geolocation"
    ];

    // Word/token-boundary matcher: markers only match as standalone tokens, so legitimate
    // words such as "taxonomy" (contains "tax"), "scorecard" (contains "score") or
    // "time-attendance-leave" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
    // "password", "national_id", ...) still match. Tested against the RAW value, not a
    // punctuation-stripped form. Underscore is a regex word character, so snake_case
    // markers ("workflow_body") match as whole tokens.
    private static readonly Regex ForbiddenMarkerRegex = new(
        @"\b(" + string.Join("|", ForbiddenMarkers.Select(Regex.Escape)) + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    public static IReadOnlyList<string> Validate(TimeAttendanceLeaveReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.TimeAttendanceLeaveReadinessVersion < 1)
        {
            errors.Add("TimeAttendanceLeaveReadinessVersion must be greater than zero.");
        }

        ValidateState(request.TimeAttendanceLeaveReadinessState, nameof(request.TimeAttendanceLeaveReadinessState), errors);
        ValidateState(request.TimesheetIntakeBoundaryState, nameof(request.TimesheetIntakeBoundaryState), errors);
        ValidateState(request.AttendanceSyncBoundaryState, nameof(request.AttendanceSyncBoundaryState), errors);
        ValidateState(request.LeaveRequestBoundaryState, nameof(request.LeaveRequestBoundaryState), errors);
        ValidateState(request.LeaveBalanceBoundaryState, nameof(request.LeaveBalanceBoundaryState), errors);
        ValidateState(request.ScheduleConsumptionBoundaryState, nameof(request.ScheduleConsumptionBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.TimeAttendanceSourceDependencyState, nameof(request.TimeAttendanceSourceDependencyState), errors);
        ValidateState(request.LeaveSourceDependencyState, nameof(request.LeaveSourceDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.TimeAttendanceLeaveReadinessState == TimeAttendanceLeaveReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.TimesheetIntakeBoundaryState == TimeAttendanceLeaveReadinessState.Ready)
        {
            errors.Add("Timesheet intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AttendanceSyncBoundaryState == TimeAttendanceLeaveReadinessState.Ready)
        {
            errors.Add("Attendance sync cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.LeaveRequestBoundaryState == TimeAttendanceLeaveReadinessState.Ready)
        {
            errors.Add("Leave request cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.LeaveBalanceBoundaryState == TimeAttendanceLeaveReadinessState.Ready)
        {
            errors.Add("Leave balance cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ScheduleConsumptionBoundaryState == TimeAttendanceLeaveReadinessState.Ready)
        {
            errors.Add("Schedule consumption cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == TimeAttendanceLeaveReadinessState.Ready)
        {
            errors.Add("Automated decision behavior cannot be marked Ready in the first metadata-only slice.");
        }

        foreach (var pair in request.DependencyStates)
        {
            RequireText(pair.Key, nameof(request.DependencyStates), 80, errors);
            ValidateState(pair.Value, $"{nameof(request.DependencyStates)}.{pair.Key}", errors);
        }

        if (ForbiddenValues(request).Any(ContainsForbiddenMarker))
        {
            errors.Add("Time, attendance and leave readiness metadata cannot contain timesheet entries, punch/clock records, attendance logs, leave balances, accrual amounts, schedule payloads, scores, ratings, calibration outcomes, rankings, model output, automated decision outputs, free-text notes, narrative, attachments, document payloads, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static TimeAttendanceLeaveReadinessState ResolveFailClosedReadinessState(TimeAttendanceLeaveReadinessCreateRequest request)
    {
        if (request.TimeAttendanceLeaveReadinessState != TimeAttendanceLeaveReadinessState.Ready)
        {
            return request.TimeAttendanceLeaveReadinessState;
        }

        return ArePreconditionsReady(
            request.TimesheetIntakeBoundaryState,
            request.AttendanceSyncBoundaryState,
            request.LeaveRequestBoundaryState,
            request.LeaveBalanceBoundaryState,
            request.ScheduleConsumptionBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.TimeAttendanceSourceDependencyState,
            request.LeaveSourceDependencyState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? TimeAttendanceLeaveReadinessState.Ready
            : TimeAttendanceLeaveReadinessState.Deferred;
    }

    public static void ApplyEvaluation(TimeAttendanceLeaveReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.TimeAttendanceLeaveReadinessState = ArePreconditionsReady(
            entity.TimesheetIntakeBoundaryState,
            entity.AttendanceSyncBoundaryState,
            entity.LeaveRequestBoundaryState,
            entity.LeaveBalanceBoundaryState,
            entity.ScheduleConsumptionBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TimeAttendanceSourceDependencyState,
            entity.LeaveSourceDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? TimeAttendanceLeaveReadinessState.Ready
            : TimeAttendanceLeaveReadinessState.Deferred;

        entity.DeferredReason = entity.TimeAttendanceLeaveReadinessState == TimeAttendanceLeaveReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Time, attendance and leave readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        TimeAttendanceLeaveReadinessState timesheetIntake,
        TimeAttendanceLeaveReadinessState attendanceSync,
        TimeAttendanceLeaveReadinessState leaveRequest,
        TimeAttendanceLeaveReadinessState leaveBalance,
        TimeAttendanceLeaveReadinessState scheduleConsumption,
        TimeAttendanceLeaveReadinessState automatedDecision,
        TimeAttendanceLeaveReadinessState timeAttendanceSourceDependency,
        TimeAttendanceLeaveReadinessState leaveSourceDependency,
        TimeAttendanceLeaveReadinessState documentDependency,
        TimeAttendanceLeaveReadinessState notificationDependency,
        TimeAttendanceLeaveReadinessState consent,
        TimeAttendanceLeaveReadinessState dataMinimization,
        TimeAttendanceLeaveReadinessState retention,
        TimeAttendanceLeaveReadinessState evidence,
        IReadOnlyDictionary<string, TimeAttendanceLeaveReadinessState> dependencyStates)
    {
        var required = new[]
        {
            timesheetIntake,
            attendanceSync,
            leaveRequest,
            leaveBalance,
            scheduleConsumption,
            automatedDecision,
            timeAttendanceSourceDependency,
            leaveSourceDependency,
            documentDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(TimeAttendanceLeaveReadinessState state) =>
        state is TimeAttendanceLeaveReadinessState.Ready or TimeAttendanceLeaveReadinessState.NotRequired;

    private static void ValidateState(TimeAttendanceLeaveReadinessState state, string fieldName, List<string> errors)
    {
        if (!Enum.IsDefined(state))
        {
            errors.Add($"{fieldName} is not supported.");
        }
    }

    private static void RequireText(string value, string fieldName, int maxLength, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required.");
            return;
        }

        if (value.Trim().Length > maxLength)
        {
            errors.Add($"{fieldName} must be {maxLength} characters or fewer.");
        }
    }

    private static IEnumerable<string> ForbiddenValues(TimeAttendanceLeaveReadinessCreateRequest request)
    {
        yield return request.Code;
        yield return request.DisplayName;
        yield return request.SourceContractVersion;
        yield return request.DeferredReason ?? string.Empty;

        foreach (var key in request.DependencyStates.Keys)
        {
            yield return key;
        }
    }

    private static bool ContainsForbiddenMarker(string value) =>
        !string.IsNullOrEmpty(value) && ForbiddenMarkerRegex.IsMatch(value);
}
