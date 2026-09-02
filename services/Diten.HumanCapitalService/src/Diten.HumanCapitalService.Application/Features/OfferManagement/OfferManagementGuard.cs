using Diten.HumanCapitalService.Application.Common;
using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Enums;

namespace Diten.HumanCapitalService.Application.Features.OfferManagement;

public static class OfferManagementGuard
{
    public const string OwnerKey = "hcm.offer-management";
    public const string ReadPermission = "hcm.offer-management.read";
    public const string ManagePermission = "hcm.offer-management.manage";
    public const string EvaluatePermission = "hcm.offer-management.evaluate";
    public const string AuditReadPermission = "hcm.offer-management.audit.read";

    private static readonly string[] ForbiddenMarkers =
    [
        "offerletter",
        "offer_letter",
        "letterbody",
        "letter_body",
        "compensationamount",
        "compensation_amount",
        "salaryamount",
        "salary_amount",
        "salary",
        "wage",
        "bonusamount",
        "bonus_amount",
        "benefitselection",
        "benefits_election",
        "payroll",
        "bank",
        "tax",
        "payslip",
        "acceptancetext",
        "acceptance_text",
        "negotiation",
        "approvalcomment",
        "approval_comment",
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
        "nationalid",
        "national_id",
        "dateofbirth",
        "dob",
        "homeaddress",
        "home_address",
        "biometric",
        "geolocation"
    ];

    public static Response<Guid> RequireTenant(ITenantContext tenantContext) =>
        tenantContext.TenantId is { } tenantId && tenantId != Guid.Empty
            ? Response<Guid>.Success(tenantId)
            : Response<Guid>.Fail("Tenant context is required.", 401);

    public static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    public static IReadOnlyList<string> Validate(OfferReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.OfferReadinessVersion < 1)
        {
            errors.Add("OfferReadinessVersion must be greater than zero.");
        }

        ValidateState(request.OfferReadinessState, nameof(request.OfferReadinessState), errors);
        ValidateState(request.OfferWorkflowBoundaryState, nameof(request.OfferWorkflowBoundaryState), errors);
        ValidateState(request.ApprovalWorkflowBoundaryState, nameof(request.ApprovalWorkflowBoundaryState), errors);
        ValidateState(request.CandidateAcceptanceBoundaryState, nameof(request.CandidateAcceptanceBoundaryState), errors);
        ValidateState(request.OfferDocumentBoundaryState, nameof(request.OfferDocumentBoundaryState), errors);
        ValidateState(request.CompensationDataBoundaryState, nameof(request.CompensationDataBoundaryState), errors);
        ValidateState(request.BenefitsDataBoundaryState, nameof(request.BenefitsDataBoundaryState), errors);
        ValidateState(request.PayrollDataBoundaryState, nameof(request.PayrollDataBoundaryState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.EvidencePolicyState, nameof(request.EvidencePolicyState), errors);
        ValidateState(request.NotificationDependencyState, nameof(request.NotificationDependencyState), errors);
        ValidateState(request.DocumentDependencyState, nameof(request.DocumentDependencyState), errors);

        if (request.OfferReadinessState == OfferReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.OfferWorkflowBoundaryState == OfferReadinessState.Ready)
        {
            errors.Add("Offer workflow execution cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ApprovalWorkflowBoundaryState == OfferReadinessState.Ready)
        {
            errors.Add("Approval workflow execution cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.CandidateAcceptanceBoundaryState == OfferReadinessState.Ready)
        {
            errors.Add("Candidate-facing offer acceptance UX cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.OfferDocumentBoundaryState == OfferReadinessState.Ready)
        {
            errors.Add("Offer letter document generation cannot be marked Ready in the first metadata-only slice.");
        }

        foreach (var pair in request.DependencyStates)
        {
            RequireText(pair.Key, nameof(request.DependencyStates), 80, errors);
            ValidateState(pair.Value, $"{nameof(request.DependencyStates)}.{pair.Key}", errors);
        }

        if (ForbiddenValues(request).Any(ContainsForbiddenMarker))
        {
            errors.Add("Offer readiness metadata cannot contain offer letter, compensation amount, benefits election, payroll, bank, tax, acceptance text, attachment, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static OfferReadinessState ResolveFailClosedReadinessState(OfferReadinessCreateRequest request)
    {
        if (request.OfferReadinessState != OfferReadinessState.Ready)
        {
            return request.OfferReadinessState;
        }

        return ArePreconditionsReady(
            request.OfferWorkflowBoundaryState,
            request.ApprovalWorkflowBoundaryState,
            request.CandidateAcceptanceBoundaryState,
            request.OfferDocumentBoundaryState,
            request.CompensationDataBoundaryState,
            request.BenefitsDataBoundaryState,
            request.PayrollDataBoundaryState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.EvidencePolicyState,
            request.NotificationDependencyState,
            request.DocumentDependencyState,
            request.DependencyStates)
            ? OfferReadinessState.Ready
            : OfferReadinessState.Deferred;
    }

    public static void ApplyEvaluation(OfferReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.OfferReadinessState = ArePreconditionsReady(
            entity.OfferWorkflowBoundaryState,
            entity.ApprovalWorkflowBoundaryState,
            entity.CandidateAcceptanceBoundaryState,
            entity.OfferDocumentBoundaryState,
            entity.CompensationDataBoundaryState,
            entity.BenefitsDataBoundaryState,
            entity.PayrollDataBoundaryState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.EvidencePolicyState,
            entity.NotificationDependencyState,
            entity.DocumentDependencyState,
            entity.DependencyStates)
            ? OfferReadinessState.Ready
            : OfferReadinessState.Deferred;

        entity.DeferredReason = entity.OfferReadinessState == OfferReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Offer readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        OfferReadinessState offerWorkflow,
        OfferReadinessState approvalWorkflow,
        OfferReadinessState candidateAcceptance,
        OfferReadinessState offerDocument,
        OfferReadinessState compensationData,
        OfferReadinessState benefitsData,
        OfferReadinessState payrollData,
        OfferReadinessState consent,
        OfferReadinessState dataMinimization,
        OfferReadinessState retention,
        OfferReadinessState evidence,
        OfferReadinessState notificationDependency,
        OfferReadinessState documentDependency,
        IReadOnlyDictionary<string, OfferReadinessState> dependencyStates)
    {
        var required = new[]
        {
            offerWorkflow,
            approvalWorkflow,
            candidateAcceptance,
            offerDocument,
            compensationData,
            benefitsData,
            payrollData,
            consent,
            dataMinimization,
            retention,
            evidence,
            notificationDependency,
            documentDependency
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(OfferReadinessState state) =>
        state is OfferReadinessState.Ready or OfferReadinessState.NotRequired;

    private static void ValidateState(OfferReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(OfferReadinessCreateRequest request)
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
