using System.Text.RegularExpressions;
using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Enums;

namespace Diten.TalentEcosystemService.Application.Features.IndustryKnowledgeNetwork;

public static class IndustryKnowledgeNetworkGuard
{
    public const string OwnerKey = "tep.industry-knowledge-network";
    public const string ReadPermission = "tep.industry-knowledge-network.read";
    public const string ManagePermission = "tep.industry-knowledge-network.manage";
    public const string EvaluatePermission = "tep.industry-knowledge-network.evaluate";
    public const string AuditReadPermission = "tep.industry-knowledge-network.audit.read";

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
    // "industry-knowledge-network" are NOT falsely rejected, while real markers ("tax", "salary", "ssn",
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

    public static IReadOnlyList<string> Validate(IndustryKnowledgeNetworkReadinessCreateRequest request)
    {
        var errors = new List<string>();
        RequireText(request.Code, nameof(request.Code), 64, errors);
        RequireText(request.DisplayName, nameof(request.DisplayName), 128, errors);
        RequireText(request.SourceContractVersion, nameof(request.SourceContractVersion), 32, errors);

        if (request.IndustryKnowledgeNetworkReadinessVersion < 1)
        {
            errors.Add("IndustryKnowledgeNetworkReadinessVersion must be greater than zero.");
        }

        ValidateState(request.IndustryKnowledgeNetworkReadinessState, nameof(request.IndustryKnowledgeNetworkReadinessState), errors);
        ValidateState(request.KnowledgeCatalogBoundaryState, nameof(request.KnowledgeCatalogBoundaryState), errors);
        ValidateState(request.ContentBindingIntakeBoundaryState, nameof(request.ContentBindingIntakeBoundaryState), errors);
        ValidateState(request.NetworkScopeBoundaryState, nameof(request.NetworkScopeBoundaryState), errors);
        ValidateState(request.VisibilityControlBoundaryState, nameof(request.VisibilityControlBoundaryState), errors);
        ValidateState(request.KnowledgeReviewBoundaryState, nameof(request.KnowledgeReviewBoundaryState), errors);
        ValidateState(request.AutomatedDecisionBoundaryState, nameof(request.AutomatedDecisionBoundaryState), errors);
        ValidateState(request.KnowledgeSourceRegistryDependencyState, nameof(request.KnowledgeSourceRegistryDependencyState), errors);
        ValidateState(request.SectorTrendSourceDependencyState, nameof(request.SectorTrendSourceDependencyState), errors);
        ValidateState(request.DataGovernancePolicyDependencyState, nameof(request.DataGovernancePolicyDependencyState), errors);
        ValidateState(request.AssociationOperationsSourceDependencyState, nameof(request.AssociationOperationsSourceDependencyState), errors);
        ValidateState(request.ConsentPreconditionState, nameof(request.ConsentPreconditionState), errors);
        ValidateState(request.DataMinimizationState, nameof(request.DataMinimizationState), errors);
        ValidateState(request.RetentionPolicyState, nameof(request.RetentionPolicyState), errors);
        ValidateState(request.PublicationPolicyState, nameof(request.PublicationPolicyState), errors);

        if (request.IndustryKnowledgeNetworkReadinessState == IndustryKnowledgeNetworkReadinessState.Archived)
        {
            errors.Add("Archived state is controlled by delete operation.");
        }

        if (request.KnowledgeCatalogBoundaryState == IndustryKnowledgeNetworkReadinessState.Ready)
        {
            errors.Add("Knowledge catalog cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.ContentBindingIntakeBoundaryState == IndustryKnowledgeNetworkReadinessState.Ready)
        {
            errors.Add("Content binding intake cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.NetworkScopeBoundaryState == IndustryKnowledgeNetworkReadinessState.Ready)
        {
            errors.Add("Network scope cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.VisibilityControlBoundaryState == IndustryKnowledgeNetworkReadinessState.Ready)
        {
            errors.Add("Visibility control cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.KnowledgeReviewBoundaryState == IndustryKnowledgeNetworkReadinessState.Ready)
        {
            errors.Add("Knowledge review cannot be marked Ready in the first metadata-only slice.");
        }

        if (request.AutomatedDecisionBoundaryState == IndustryKnowledgeNetworkReadinessState.Ready)
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
            errors.Add("Industry knowledge network readiness metadata cannot contain real knowledge content or article bodies, content/document payloads, per-item knowledge data, network graph or edge data, query results, individual/participant PII or contact details, workforce or company rosters, free-text notes, narrative, attachments, ratings, scores, model output, automated decision outputs, compensation, benefits, payroll, bank, tax, raw provider payload, credential, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers.");
        }

        return errors;
    }

    public static IndustryKnowledgeNetworkReadinessState ResolveFailClosedReadinessState(IndustryKnowledgeNetworkReadinessCreateRequest request)
    {
        if (request.IndustryKnowledgeNetworkReadinessState != IndustryKnowledgeNetworkReadinessState.Ready)
        {
            return request.IndustryKnowledgeNetworkReadinessState;
        }

        return ArePreconditionsReady(
            request.KnowledgeCatalogBoundaryState,
            request.ContentBindingIntakeBoundaryState,
            request.NetworkScopeBoundaryState,
            request.VisibilityControlBoundaryState,
            request.KnowledgeReviewBoundaryState,
            request.AutomatedDecisionBoundaryState,
            request.KnowledgeSourceRegistryDependencyState,
            request.SectorTrendSourceDependencyState,
            request.DataGovernancePolicyDependencyState,
            request.AssociationOperationsSourceDependencyState,
            request.ConsentPreconditionState,
            request.DataMinimizationState,
            request.RetentionPolicyState,
            request.PublicationPolicyState,
            request.DependencyStates)
            ? IndustryKnowledgeNetworkReadinessState.Ready
            : IndustryKnowledgeNetworkReadinessState.Deferred;
    }

    public static void ApplyEvaluation(IndustryKnowledgeNetworkReadinessMetadata entity, DateTimeOffset evaluatedAt)
    {
        entity.LastEvaluatedAt = evaluatedAt;
        entity.UpdatedAt = evaluatedAt;
        entity.IndustryKnowledgeNetworkReadinessState = ArePreconditionsReady(
            entity.KnowledgeCatalogBoundaryState,
            entity.ContentBindingIntakeBoundaryState,
            entity.NetworkScopeBoundaryState,
            entity.VisibilityControlBoundaryState,
            entity.KnowledgeReviewBoundaryState,
            entity.AutomatedDecisionBoundaryState,
            entity.KnowledgeSourceRegistryDependencyState,
            entity.SectorTrendSourceDependencyState,
            entity.DataGovernancePolicyDependencyState,
            entity.AssociationOperationsSourceDependencyState,
            entity.ConsentPreconditionState,
            entity.DataMinimizationState,
            entity.RetentionPolicyState,
            entity.PublicationPolicyState,
            entity.DependencyStates)
            ? IndustryKnowledgeNetworkReadinessState.Ready
            : IndustryKnowledgeNetworkReadinessState.Deferred;

        entity.DeferredReason = entity.IndustryKnowledgeNetworkReadinessState == IndustryKnowledgeNetworkReadinessState.Ready
            ? NormalizeOptional(entity.DeferredReason)
            : MergeDeferredReason(entity.DeferredReason, "Industry knowledge network readiness remains deferred until metadata preconditions are Ready or NotRequired.");
    }

    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string MergeDeferredReason(params string?[] reasons) =>
        string.Join(" ", reasons.Where(reason => !string.IsNullOrWhiteSpace(reason))).Trim();

    private static bool ArePreconditionsReady(
        IndustryKnowledgeNetworkReadinessState knowledgeCatalog,
        IndustryKnowledgeNetworkReadinessState contentBindingIntake,
        IndustryKnowledgeNetworkReadinessState networkScope,
        IndustryKnowledgeNetworkReadinessState visibilityControl,
        IndustryKnowledgeNetworkReadinessState knowledgeReview,
        IndustryKnowledgeNetworkReadinessState automatedDecision,
        IndustryKnowledgeNetworkReadinessState knowledgeSourceRegistryDependency,
        IndustryKnowledgeNetworkReadinessState sectorTrendSourceDependency,
        IndustryKnowledgeNetworkReadinessState dataGovernancePolicyDependency,
        IndustryKnowledgeNetworkReadinessState associationOperationsSourceDependency,
        IndustryKnowledgeNetworkReadinessState consent,
        IndustryKnowledgeNetworkReadinessState dataMinimization,
        IndustryKnowledgeNetworkReadinessState retention,
        IndustryKnowledgeNetworkReadinessState evidence,
        IReadOnlyDictionary<string, IndustryKnowledgeNetworkReadinessState> dependencyStates)
    {
        var required = new[]
        {
            knowledgeCatalog,
            contentBindingIntake,
            networkScope,
            visibilityControl,
            knowledgeReview,
            automatedDecision,
            knowledgeSourceRegistryDependency,
            sectorTrendSourceDependency,
            dataGovernancePolicyDependency,
            associationOperationsSourceDependency,
            consent,
            dataMinimization,
            retention,
            evidence
        };

        return required.All(IsSatisfied) && dependencyStates.Values.All(IsSatisfied);
    }

    private static bool IsSatisfied(IndustryKnowledgeNetworkReadinessState state) =>
        state is IndustryKnowledgeNetworkReadinessState.Ready or IndustryKnowledgeNetworkReadinessState.NotRequired;

    private static void ValidateState(IndustryKnowledgeNetworkReadinessState state, string fieldName, List<string> errors)
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

    private static IEnumerable<string> ForbiddenValues(IndustryKnowledgeNetworkReadinessCreateRequest request)
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
