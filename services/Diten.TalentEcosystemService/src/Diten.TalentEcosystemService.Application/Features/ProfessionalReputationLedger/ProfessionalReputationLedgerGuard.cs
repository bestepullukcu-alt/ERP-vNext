using System.Text.RegularExpressions;
using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.ProfessionalReputationLedger;

public static class ProfessionalReputationLedgerGuard
{
    public const string OwnerKey = "tep.professional-reputation-ledger";
    public const string ReadPermission = "tep.professional-reputation-ledger.read";
    public const string ManagePermission = "tep.professional-reputation-ledger.manage";
    public const string EvaluatePermission = "tep.professional-reputation-ledger.evaluate";
    public const string AuditReadPermission = "tep.professional-reputation-ledger.audit.read";

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
    // "professional-reputation-ledger" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(ProfessionalReputationLedgerReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.ProfessionalReputationLedgerReadinessVersion < 1)
        {
            errors.Add("ProfessionalReputationLedgerReadinessVersion must be greater than zero.");
        }

        ValidateState(request.ProfessionalReputationLedgerReadinessState, nameof(request.ProfessionalReputationLedgerReadinessState), errors);
        ValidateState(request.ReputationSignalCatalogBoundaryState, nameof(request.ReputationSignalCatalogBoundaryState), errors);
        ValidateState(request.EndorsementIntakeBoundaryState, nameof(request.EndorsementIntakeBoundaryState), errors);
        ValidateState(request.AttributionScopeBoundaryState, nameof(request.AttributionScopeBoundaryState), errors);
        ValidateState(request.VisibilityControlBoundaryState, nameof(request.VisibilityControlBoundaryState), errors);
        ValidateState(request.SignalReviewBoundaryState, nameof(request.SignalReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.TalentDataSourceDependencyState, nameof(request.TalentDataSourceDependencyState), errors);
        ValidateState(request.ConsentPolicyDependencyState, nameof(request.ConsentPolicyDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);

        if (request.ProfessionalReputationLedgerReadinessState == ProfessionalReputationLedgerReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.ReputationSignalCatalogBoundaryState == ProfessionalReputationLedgerReadinessState.Ready)
        {
            errors.Add("Reputation signal catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.EndorsementIntakeBoundaryState == ProfessionalReputationLedgerReadinessState.Ready)
        {
            errors.Add("Endorsement intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AttributionScopeBoundaryState == ProfessionalReputationLedgerReadinessState.Ready)
        {
            errors.Add("Attribution scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.VisibilityControlBoundaryState == ProfessionalReputationLedgerReadinessState.Ready)
        {
            errors.Add("Visibility control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.SignalReviewBoundaryState == ProfessionalReputationLedgerReadinessState.Ready)
        {
            errors.Add("Signal review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == ProfessionalReputationLedgerReadinessState.Ready)
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
            errors.Add("Professional reputation ledger readiness metadata cannot contain reputation scores, endorsement or attestation content, free-text testimonials, individual attributions, candidate/individual PII, ratings, model output, automated decision outputs, free-text notes, narrative, attachments, document payloads, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static ProfessionalReputationLedgerReadinessState ResolveFailClosedReadinessState(ProfessionalReputationLedgerReadinessCreateRequest request)
    {
        if (request.ProfessionalReputationLedgerReadinessState != ProfessionalReputationLedgerReadinessState.Ready)
        {
            return request.ProfessionalReputationLedgerReadinessState;
        }

        return ArePreconditionsReady(
            request.ReputationSignalCatalogBoundaryState,
            request.EndorsementIntakeBoundaryState,
            request.AttributionScopeBoundaryState,
            request.VisibilityControlBoundaryState,
            request.SignalReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.TalentDataSourceDependencyState,
            request.ConsentPolicyDependencyState,
            request.DocumentDependencyState,
            request.NotificationDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.DependencyStates)
            ? ProfessionalReputationLedgerReadinessState.Ready
            : ProfessionalReputationLedgerReadinessState.Deferred;
    }

    public static void ApplyEvaluation(ProfessionalReputationLedgerReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.ProfessionalReputationLedgerReadinessState = ArePreconditionsReady(
            entity.ReputationSignalCatalogBoundaryState,
            entity.EndorsementIntakeBoundaryState,
            entity.AttributionScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.SignalReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.TalentDataSourceDependencyState,
            entity.ConsentPolicyDependencyState,
            entity.DocumentDependencyState,
            entity.NotificationDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.DependencyStates)
            ? ProfessionalReputationLedgerReadinessState.Ready
            : ProfessionalReputationLedgerReadinessState.Deferred;

        entity.DeferredReason = entity.ProfessionalReputationLedgerReadinessState == ProfessionalReputationLedgerReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Professional reputation ledger readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        ProfessionalReputationLedgerReadinessState reputationSignalCatalog,
        ProfessionalReputationLedgerReadinessState endorsementIntake,
        ProfessionalReputationLedgerReadinessState attributionScope,
        ProfessionalReputationLedgerReadinessState visibilityControl,
        ProfessionalReputationLedgerReadinessState signalReview,
        ProfessionalReputationLedgerReadinessState automatedDecision,
        ProfessionalReputationLedgerReadinessState talentDataSourceDependency,
        ProfessionalReputationLedgerReadinessState consentPolicyDependency,
        ProfessionalReputationLedgerReadinessState documentDependency,
        ProfessionalReputationLedgerReadinessState notificationDependency,
        ProfessionalReputationLedgerReadinessState consent,
        ProfessionalReputationLedgerReadinessState dataMinimization,
        ProfessionalReputationLedgerReadinessState retention,
        ProfessionalReputationLedgerReadinessState evidence,
        IReadOnlyDictionary<string, ProfessionalReputationLedgerReadinessState> dependencyStates)
    {
        var required = new[]
        {
            reputationSignalCatalog,
            endorsementIntake,
            attributionScope,
            visibilityControl,
            signalReview,
            automatedDecision,
            talentDataSourceDependency,
            consentPolicyDependency,
            documentDependency,
            notificationDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(ProfessionalReputationLedgerReadinessState state) =>
        state is ProfessionalReputationLedgerReadinessState.Ready or ProfessionalReputationLedgerReadinessState.NotRequired;

    private static void ValidateState(ProfessionalReputationLedgerReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(ProfessionalReputationLedgerReadinessCreateRequest request)
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
