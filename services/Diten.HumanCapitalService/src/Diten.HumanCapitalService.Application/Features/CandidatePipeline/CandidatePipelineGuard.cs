using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.CandidatePipeline;

public static class CandidatePipelineGuard
{
    public const string OwnerKey = "hcm.candidate-pipeline";
    public const string ReadPermission = "hcm.candidate-pipeline.read";
    public const string ManagePermission = "hcm.candidate-pipeline.manage";
    public const string EvaluatePermission = "hcm.candidate-pipeline.evaluate";
    public const string AuditReadPermission = "hcm.candidate-pipeline.audit.read";

    private static readonly string[] ForbiddenMarkers =
    [
        "interviewnote",
        "interview_note",
        "evaluationtext",
        "evaluation_text",
        "freetext",
        "free_text",
        "resume",
        "cv",
        "coverletter",
        "cover_letter",
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
        "payroll",
        "bank",
        "tax",
        "payslip",
        "biometric",
        "geolocation",
        "nationalid",
        "national_id",
        "dateofbirth",
        "dob",
        "homeaddress",
        "home_address",
        "salary",
        "wage",
        "score",
        "rank",
        "modeloutput",
        "model_output",
        "automateddecision",
        "automated_decision"
    ];

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    public static IReadOnlyList<string> Validate(CandidatePipelineCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.PipelineReadinessVersion < 1)
        {
            errors.Add("PipelineReadinessVersion must be greater than zero.");
        }

        ValidateState(request.PipelineReadinessState, nameof(request.PipelineReadinessState), errors);
        ValidateState(request.PipelineStageGovernanceState, nameof(request.PipelineStageGovernanceState), errors);
        ValidateState(request.InterviewSchedulingReadinessState, nameof(request.InterviewSchedulingReadinessState), errors);
        ValidateState(request.InterviewerAssignmentReadinessState, nameof(request.InterviewerAssignmentReadinessState), errors);
        ValidateState(request.EvaluationGovernanceState, nameof(request.EvaluationGovernanceState), errors);
        ValidateState(request.CandidateCommunicationBoundaryState, nameof(request.CandidateCommunicationBoundaryState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);
        ValidateState(request.CalendarDependencyState, nameof(request.CalendarDependencyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);

        if (request.PipelineReadinessState == CandidatePipelineReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.CandidateCommunicationBoundaryState == CandidatePipelineReadinessState.Ready)
        {
            errors.Add("Candidate-facing scheduling/application UX cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == CandidatePipelineReadinessState.Ready)
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
            errors.Add("Candidate pipeline metadata cannot contain interview notes, free-text evaluation, resume/CV, attachment, raw provider payload, credential, payroll, bank, tax, biometric, geolocation, national ID, DOB, home address, scoring, ranking, automated decision, or PII-heavy markers.");
        }

        return errors;
    }

    public static CandidatePipelineReadinessState ResolveFailClosedReadinessState(CandidatePipelineCreateRequest request)
    {
        if (request.PipelineReadinessState != CandidatePipelineReadinessState.Ready)
        {
            return request.PipelineReadinessState;
        }

        return ArePreconditionsReady(
            request.PipelineStageGovernanceState,
            request.InterviewSchedulingReadinessState,
            request.InterviewerAssignmentReadinessState,
            request.EvaluationGovernanceState,
            request.CandidateCommunicationBoundaryState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.CalendarDependencyState,
            request.NotificationDependencyState,
            request.DocumentDependencyState,
            request.AutomatedDecisionBoundaryState,
            request.DependencyStates)
            ? CandidatePipelineReadinessState.Ready
            : CandidatePipelineReadinessState.Deferred;
    }

    public static void ApplyEvaluation(CandidatePipelineReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.PipelineReadinessState = ArePreconditionsReady(
            entity.PipelineStageGovernanceState,
            entity.InterviewSchedulingReadinessState,
            entity.InterviewerAssignmentReadinessState,
            entity.EvaluationGovernanceState,
            entity.CandidateCommunicationBoundaryState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.CalendarDependencyState,
            entity.NotificationDependencyState,
            entity.DocumentDependencyState,
            entity.AutomatedDecisionBoundaryState,
            entity.DependencyStates)
            ? CandidatePipelineReadinessState.Ready
            : CandidatePipelineReadinessState.Deferred;

        entity.DeferredReason = entity.PipelineReadinessState == CandidatePipelineReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Candidate pipeline readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        CandidatePipelineReadinessState pipelineStage,
        CandidatePipelineReadinessState interviewScheduling,
        CandidatePipelineReadinessState interviewerAssignment,
        CandidatePipelineReadinessState evaluationGovernance,
        CandidatePipelineReadinessState candidateCommunication,
        CandidatePipelineReadinessState consent,
        CandidatePipelineReadinessState dataMinimization,
        CandidatePipelineReadinessState retention,
        CandidatePipelineReadinessState evidence,
        CandidatePipelineReadinessState calendarDependency,
        CandidatePipelineReadinessState notificationDependency,
        CandidatePipelineReadinessState documentDependency,
        CandidatePipelineReadinessState automatedDecision,
        IReadOnlyDictionary<string, CandidatePipelineReadinessState> dependencyStates)
    {
        var required = new[]
        {
            pipelineStage,
            interviewScheduling,
            interviewerAssignment,
            evaluationGovernance,
            candidateCommunication,
            consent,
            dataMinimization,
            retention,
            evidence,
            calendarDependency,
            notificationDependency,
            documentDependency,
            automatedDecision
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(CandidatePipelineReadinessState state) =>
        state is CandidatePipelineReadinessState.Ready or CandidatePipelineReadinessState.NotRequired;

    private static void ValidateState(CandidatePipelineReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(CandidatePipelineCreateRequest request)
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

    private static bool ContainsForbiddenMarker(string value)
    {
        var normalized = Normalize(value);
        return ForbiddenMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
    }

    private static string Normalize(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        var index = 0;

        foreach (var current in value)
        {
            if (char.IsLetterOrDigit(current) || current == '_')
            {
                buffer[index++] = char.ToLowerInvariant(current);
            }
        }

        return new string(buffer[..index]);
    }
}
