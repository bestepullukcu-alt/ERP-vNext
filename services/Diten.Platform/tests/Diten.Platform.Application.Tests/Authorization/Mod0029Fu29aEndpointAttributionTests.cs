using System.Reflection;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Features.DocumentManagementApproval;
using Diten.Platform.Application.Features.DocumentManagementControlledCopy;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Application.Features.DocumentManagementDowntime;
using Diten.Platform.Application.Features.DocumentManagementElectronicSignature;
using Diten.Platform.Application.Features.DocumentManagementExternalDocuments;
using Diten.Platform.Application.Features.DocumentManagementGDocPCorrection;
using Diten.Platform.Application.Features.DocumentManagementIdentifiers;
using Diten.Platform.Application.Features.DocumentManagementLifecycle;
using Diten.Platform.Application.Features.DocumentManagementMasterRegister;
using Diten.Platform.Application.Features.DocumentManagementPeriodicReview;
using Diten.Platform.Application.Features.DocumentManagementQualityEvent;
using Diten.Platform.Application.Features.DocumentManagementReleaseGates;
using Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment;
using Diten.Platform.Application.Features.DocumentManagementRetention;
using Diten.Platform.Application.Features.DocumentManagementSuspension;
using Diten.Platform.Application.Features.DocumentManagementTraining;
using Diten.Platform.Application.Features.DocumentManagementVariantLocalization;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

/// <summary>
/// MOD-0029-FU29A — Runtime Authorization Attribution Hardening. Reflects the <see cref="HasPermissionAttribute"/>
/// declared on each MOD-0029 governance controller action (matched by its route template, so no dependency on C#
/// method names) and asserts every critical write endpoint now enforces its DEDICATED FU29 permission key instead of
/// the generic <c>controlled-documents.view/create</c> reuse. Values reference the real Diten.Platform permission
/// constants (compile-safe, drift-proof) — the same constants the AuthService FU29 seed persists.
/// </summary>
public sealed class Mod0029Fu29aEndpointAttributionTests
{
    private const string GenericView = "platform.document-management.controlled-documents.view";
    private const string GenericCreate = "platform.document-management.controlled-documents.create";

    // The 16 MOD-0029 governance controllers hardened by FU29A (NOT the FU01 controlled-documents/templates ones).
    private static readonly Type[] GovernanceControllers =
    {
        typeof(DocumentManagementRetentionController),
        typeof(DocumentManagementGDocPCorrectionController),
        typeof(DocumentManagementQualityEventController),
        typeof(DocumentManagementSignaturesController),
        typeof(DocumentManagementDowntimeController),
        typeof(DocumentManagementExternalDocumentsController),
        typeof(DocumentManagementVariantLocalizationController),
        typeof(DocumentManagementRepositoryAssessmentController),
        typeof(DocumentManagementControlledCopyController),
        typeof(DocumentManagementReleaseGatesController),
        typeof(DocumentManagementTrainingController),
        typeof(DocumentManagementPeriodicReviewController),
        typeof(DocumentManagementSuspensionController),
        typeof(DocumentManagementMasterRegisterController),
        typeof(DocumentManagementIdentifiersController),
        typeof(DocumentManagementLifecycleController),
        typeof(DocumentManagementApprovalController),
    };

    // ── critical endpoint → dedicated permission key (route-template matched) ────────────────────

    [Fact] // 1
    public void Retention_legal_hold_release_uses_legal_hold_release_permission() =>
        Assert.Equal(DocumentRetentionPermissions.LegalHoldRelease,
            KeyByRoute(typeof(DocumentManagementRetentionController), "legal-holds/{id:guid}/release"));

    [Fact] // 2
    public void Disposition_execute_marker_uses_disposition_manage_permission() =>
        Assert.Equal(DocumentRetentionPermissions.DispositionManage,
            KeyByRoute(typeof(DocumentManagementRetentionController), "disposition-requests/{id:guid}/execute-marker"));

    [Fact] // 3
    public void GDocP_review_uses_gdocp_review_permission() =>
        Assert.Equal(GDocPCorrectionPermissions.Review,
            KeyByRoute(typeof(DocumentManagementGDocPCorrectionController), "gdocp-corrections/{id:guid}/review"));

    [Fact] // 4
    public void GDocP_reject_uses_gdocp_review_permission() =>
        Assert.Equal(GDocPCorrectionPermissions.Review,
            KeyByRoute(typeof(DocumentManagementGDocPCorrectionController), "gdocp-corrections/{id:guid}/reject"));

    [Fact] // 5
    public void Quality_event_close_uses_quality_event_manage_permission() =>
        Assert.Equal(QualityEventPermissions.QualityEventsManage,
            KeyByRoute(typeof(DocumentManagementQualityEventController), "quality-events/{id:guid}/close"));

    [Fact] // 6
    public void Quality_event_cancel_uses_quality_event_manage_permission() =>
        Assert.Equal(QualityEventPermissions.QualityEventsManage,
            KeyByRoute(typeof(DocumentManagementQualityEventController), "quality-events/{id:guid}/cancel"));

    [Fact] // 7
    public void Deviation_close_uses_deviation_manage_permission() =>
        Assert.Equal(QualityEventPermissions.DeviationsManage,
            KeyByRoute(typeof(DocumentManagementQualityEventController), "deviations/{id:guid}/close"));

    [Fact] // 8
    public void Deviation_cancel_uses_deviation_manage_permission() =>
        Assert.Equal(QualityEventPermissions.DeviationsManage,
            KeyByRoute(typeof(DocumentManagementQualityEventController), "deviations/{id:guid}/cancel"));

    [Fact] // 9
    public void CAPA_effectiveness_uses_capa_manage_permission() =>
        Assert.Equal(QualityEventPermissions.CapaManage,
            KeyByRoute(typeof(DocumentManagementQualityEventController), "capa-actions/{id:guid}/effectiveness"));

    [Fact] // 10
    public void CAPA_close_uses_capa_manage_permission() =>
        Assert.Equal(QualityEventPermissions.CapaManage,
            KeyByRoute(typeof(DocumentManagementQualityEventController), "capa-actions/{id:guid}/close"));

    [Fact] // 11
    public void Signature_sign_uses_signature_sign_permission() =>
        Assert.Equal(ElectronicSignaturePermissions.SignaturesSign,
            KeyByRoute(typeof(DocumentManagementSignaturesController), "signatures/sign"));

    [Fact] // 12
    public void Signature_verify_uses_signature_verify_permission() =>
        Assert.Equal(ElectronicSignaturePermissions.SignaturesVerify,
            KeyByRoute(typeof(DocumentManagementSignaturesController), "signatures/{id:guid}/verify"));

    [Fact] // 13
    public void Signature_invalidate_uses_signature_invalidate_permission() =>
        Assert.Equal(ElectronicSignaturePermissions.SignaturesInvalidate,
            KeyByRoute(typeof(DocumentManagementSignaturesController), "signatures/{id:guid}/invalidate"));

    [Fact] // 14
    public void Downtime_temp_issue_approve_uses_temporary_issue_permission() =>
        Assert.Equal(DowntimePermissions.TemporaryIssue,
            KeyByRoute(typeof(DocumentManagementDowntimeController), "{id:guid}/temporary-issues/{issueId:guid}/approve"));

    [Fact] // 15
    public void Downtime_temp_issue_reconcile_uses_reconcile_permission() =>
        Assert.Equal(DowntimePermissions.Reconcile,
            KeyByRoute(typeof(DocumentManagementDowntimeController), "{id:guid}/temporary-issues/{issueId:guid}/reconcile"));

    [Fact] // 16
    public void External_impact_complete_uses_impact_manage_permission() =>
        Assert.Equal(ExternalDocumentPermissions.ImpactManage,
            KeyByRoute(typeof(DocumentManagementExternalDocumentsController),
                "external-documents/{id:guid}/impact-assessments/{assessmentId:guid}/complete"));

    [Fact] // 17
    public void Retention_policy_activate_and_retire_use_retention_manage_permission()
    {
        Assert.Equal(DocumentRetentionPermissions.RetentionManage,
            KeyByRoute(typeof(DocumentManagementRetentionController), "retention-policies/{id:guid}/activate"));
        Assert.Equal(DocumentRetentionPermissions.RetentionManage,
            KeyByRoute(typeof(DocumentManagementRetentionController), "retention-policies/{id:guid}/retire"));
    }

    [Fact] // 18
    public void Variant_bilingual_review_uses_translation_review_record_permission() =>
        Assert.Equal(VariantLocalizationPermissions.TranslationReviewRecord,
            KeyByRoute(typeof(DocumentManagementVariantLocalizationController), "{id:guid}/bilingual-review/complete"));

    [Fact] // 19
    public void Variant_local_approval_uses_local_approval_record_permission() =>
        Assert.Equal(VariantLocalizationPermissions.LocalApprovalRecord,
            KeyByRoute(typeof(DocumentManagementVariantLocalizationController), "{id:guid}/local-approval/complete"));

    [Fact] // 20
    public void Repository_assessment_approve_uses_approve_permission() =>
        Assert.Equal(DocumentRepositoryAssessmentPermissions.Approve,
            KeyByRoute(typeof(DocumentManagementRepositoryAssessmentController), "repository-assessments/{id:guid}/approve"));

    [Fact] // 21
    public void Controlled_copy_reconcile_uses_reconcile_permission() =>
        Assert.Equal(DocumentControlledCopyPermissions.Reconcile,
            KeyByRoute(typeof(DocumentManagementControlledCopyController),
                "document-master-register/{id:guid}/controlled-copies/{copyId:guid}/reconcile"));

    [Fact] // 22
    public void Release_gate_record_evidence_uses_record_evidence_permission() =>
        Assert.Equal(DocumentReleaseGatePermissions.RecordEvidence,
            KeyByRoute(typeof(DocumentManagementReleaseGatesController),
                "document-master-register/{id:guid}/release-gates/{gateKey}/evidence"));

    [Fact] // 23
    public void Training_effectiveness_uses_training_verify_permission() =>
        Assert.Equal(DocumentTrainingPermissions.Verify,
            KeyByRoute(typeof(DocumentManagementTrainingController),
                "document-master-register/{id:guid}/training-assignments/{assignmentId:guid}/effectiveness"));

    [Fact] // 24
    public void Periodic_review_extension_approve_uses_approve_extension_permission() =>
        Assert.Equal(DocumentPeriodicReviewPermissions.ApproveExtension,
            KeyByRoute(typeof(DocumentManagementPeriodicReviewController),
                "document-master-register/{id:guid}/periodic-review/{reviewId:guid}/extension/{extensionId:guid}/approve"));

    [Fact] // 25
    public void Suspension_execute_uses_suspension_manage_permission() =>
        Assert.Equal(DocumentSuspensionPermissions.Manage,
            KeyByRoute(typeof(DocumentManagementSuspensionController),
                "document-master-register/{id:guid}/suspension-cases/{caseId:guid}/execute"));

    [Fact] // 26
    public void Lifecycle_transition_uses_lifecycle_manage_permission() =>
        Assert.Equal(DocumentLifecyclePermissions.Manage,
            KeyByRoute(typeof(DocumentManagementLifecycleController), "document-master-register/{id:guid}/lifecycle/transition"));

    [Fact] // 27
    public void Master_register_update_no_longer_uses_generic_controlled_documents_create()
    {
        // The "{id:guid}" route is shared by GET (Detail → View) and PUT (Update → Manage); assert the set has the
        // dedicated keys and no generic controlled-documents key survives on either verb.
        var keys = EnumerateHasPermission(typeof(DocumentManagementMasterRegisterController))
            .Where(x => string.Equals(x.Route, "document-master-register/{id:guid}", StringComparison.Ordinal))
            .Select(x => x.Key)
            .ToList();

        Assert.Contains(DocumentMasterRegisterPermissions.Manage, keys);
        Assert.DoesNotContain(GenericCreate, keys);
        Assert.DoesNotContain(GenericView, keys);
    }

    [Fact] // 28
    public void No_governance_endpoint_uses_generic_controlled_documents_keys()
    {
        var offenders = new List<string>();
        foreach (var controller in GovernanceControllers)
        {
            foreach (var (route, key) in EnumerateHasPermission(controller))
            {
                if (key is GenericView or GenericCreate)
                {
                    offenders.Add($"{controller.Name}:{route} → {key}");
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "Governance endpoints still on generic controlled-documents keys: " + string.Join(", ", offenders));
    }

    [Fact] // 29
    public void All_governance_endpoint_keys_are_document_management_scoped()
    {
        foreach (var controller in GovernanceControllers)
        {
            foreach (var (_, key) in EnumerateHasPermission(controller))
            {
                // A base-route action may carry an empty template (route inherited from the controller [Route]); the
                // key is what matters — every governance key must be document-management-scoped, none generic.
                Assert.StartsWith("platform.document-management.", key);
                Assert.NotEqual(GenericView, key);
                Assert.NotEqual(GenericCreate, key);
            }
        }
    }

    [Fact]
    public void Every_governance_controller_declares_at_least_one_hardened_key()
    {
        foreach (var controller in GovernanceControllers)
        {
            Assert.NotEmpty(EnumerateHasPermission(controller));
        }
    }

    // ── reflection helpers ───────────────────────────────────────────────────────────────────────

    private static string KeyByRoute(Type controller, string routeTemplate)
    {
        var match = EnumerateHasPermission(controller)
            .Where(x => string.Equals(x.Route, routeTemplate, StringComparison.Ordinal))
            .Select(x => x.Key)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(match.Count == 1,
            $"Expected exactly one action on {controller.Name} with route '{routeTemplate}', found {match.Count}.");
        return match[0];
    }

    private static IEnumerable<(string Route, string Key)> EnumerateHasPermission(Type controller)
    {
        foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var perm = method.GetCustomAttribute<HasPermissionAttribute>();
            if (perm is null)
            {
                continue;
            }

            var route = method.GetCustomAttributes()
                .OfType<HttpMethodAttribute>()
                .Select(a => a.Template)
                .FirstOrDefault(t => !string.IsNullOrEmpty(t)) ?? string.Empty;

            yield return (route, perm.Permission);
        }
    }
}
