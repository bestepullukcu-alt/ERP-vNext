using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Schema;

public static partial class PlatformSchemaManifest
{
    /// <summary>
    /// MOD-0029 FU24-FU29 controlled-document runtime collections (signatures, quality events, release
    /// gates, retention, legal holds, periodic reviews, GDocP, external documents, repository assessments,
    /// identifiers, training, variant governance). Ported from the pre-refactor MongoDbIndexConfigurations
    /// monolith on 2026-08-28 (F-WC-DOC-SCHEMA-PORT); belongs to SchemaProfile.DocumentManagement.
    /// </summary>
    private static readonly SchemaCollection[] DocumentManagementFollowUpCollections =
    {
        Collection<DocumentApprovalEvidence>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementApprovalEvidence,
            () => new CreateIndexModel<DocumentApprovalEvidence>[]
{
            new CreateIndexModel<DocumentApprovalEvidence>(
                Builders<DocumentApprovalEvidence>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.PerformedAt),
                new CreateIndexOptions { Name = "ix_dm_approval_evidence_register_entry" }),
            new CreateIndexModel<DocumentApprovalEvidence>(
                Builders<DocumentApprovalEvidence>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RequirementId),
                new CreateIndexOptions { Name = "ix_dm_approval_evidence_requirement" })
        }),
        Collection<DocumentApprovalRequirement>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementApprovalRequirements,
            () => new CreateIndexModel<DocumentApprovalRequirement>[]
{
            new CreateIndexModel<DocumentApprovalRequirement>(
                Builders<DocumentApprovalRequirement>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.RequirementKey),
                new CreateIndexOptions<DocumentApprovalRequirement>
                {
                    Unique = true,
                    Name = "ux_dm_approval_requirements_entry_key_active",
                    PartialFilterExpression = Builders<DocumentApprovalRequirement>.Filter.Eq(x => x.IsDeleted, false)
                })
        }),
        Collection<DocumentCAPAAction>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementCapaActions,
            () => new CreateIndexModel<DocumentCAPAAction>[]
{
            new CreateIndexModel<DocumentCAPAAction>(
                Builders<DocumentCAPAAction>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CAPANumber),
                new CreateIndexOptions<DocumentCAPAAction>
                {
                    Unique = true,
                    Name = "ux_dm_capa_actions_number_active",
                    PartialFilterExpression = Builders<DocumentCAPAAction>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentCAPAAction>(
                Builders<DocumentCAPAAction>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.DeviationId).Ascending(x => x.ActionStatus),
                new CreateIndexOptions { Name = "ix_dm_capa_actions_deviation_status" }),
            new CreateIndexModel<DocumentCAPAAction>(
                Builders<DocumentCAPAAction>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.QualityEventId).Ascending(x => x.ActionStatus),
                new CreateIndexOptions { Name = "ix_dm_capa_actions_event_status" }),
            new CreateIndexModel<DocumentCAPAAction>(
                Builders<DocumentCAPAAction>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ActionStatus).Ascending(x => x.DueDate),
                new CreateIndexOptions { Name = "ix_dm_capa_actions_status_due" })
        }),
        Collection<DocumentControlledCopy>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementControlledCopies,
            () => new CreateIndexModel<DocumentControlledCopy>[]
{
            new CreateIndexModel<DocumentControlledCopy>(
                Builders<DocumentControlledCopy>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.CopyNumber),
                new CreateIndexOptions<DocumentControlledCopy>
                {
                    Unique = true,
                    Name = "ux_dm_controlled_copies_entry_number_active",
                    PartialFilterExpression = Builders<DocumentControlledCopy>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentControlledCopy>(
                Builders<DocumentControlledCopy>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.CopyStatus),
                new CreateIndexOptions { Name = "ix_dm_controlled_copies_entry_status" })
        }),
        Collection<DocumentCopyWithdrawalPlan>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementCopyWithdrawalPlans,
            () => new CreateIndexModel<DocumentCopyWithdrawalPlan>[]
{
            new CreateIndexModel<DocumentCopyWithdrawalPlan>(
                Builders<DocumentCopyWithdrawalPlan>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.PlanStatus),
                new CreateIndexOptions { Name = "ix_dm_copy_withdrawal_plans_entry_status" })
        }),
        Collection<DocumentDispositionRequest>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementDispositionRequests,
            () => new CreateIndexModel<DocumentDispositionRequest>[]
{
            new CreateIndexModel<DocumentDispositionRequest>(
                Builders<DocumentDispositionRequest>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RequestNumber),
                new CreateIndexOptions<DocumentDispositionRequest>
                {
                    Unique = true,
                    Name = "ux_dm_disposition_requests_number_active",
                    PartialFilterExpression = Builders<DocumentDispositionRequest>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentDispositionRequest>(
                Builders<DocumentDispositionRequest>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SubjectType)
                    .Ascending(x => x.SubjectId).Ascending(x => x.RequestStatus),
                new CreateIndexOptions { Name = "ix_dm_disposition_requests_subject_status" })
        }),
        Collection<DocumentDowntimeEscalation>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementDowntimeEscalations,
            () => new CreateIndexModel<DocumentDowntimeEscalation>[]
{
            new CreateIndexModel<DocumentDowntimeEscalation>(
                Builders<DocumentDowntimeEscalation>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.DowntimeEventId).Ascending(x => x.EscalationType).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_dm_downtime_escalations_event_type_status" })
        }),
        Collection<ExternalDocumentImpactAssessment>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementExternalDocumentImpactAssessments,
            () => new CreateIndexModel<ExternalDocumentImpactAssessment>[]
{
            new CreateIndexModel<ExternalDocumentImpactAssessment>(
                Builders<ExternalDocumentImpactAssessment>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ExternalDocumentRegisterEntryId),
                new CreateIndexOptions { Name = "ix_dm_external_document_impact_entry" }),
            new CreateIndexModel<ExternalDocumentImpactAssessment>(
                Builders<ExternalDocumentImpactAssessment>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.AssessmentStatus).Ascending(x => x.DueDate),
                new CreateIndexOptions { Name = "ix_dm_external_document_impact_status_due" })
        }),
        Collection<ExternalDocumentInternalLink>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementExternalDocumentInternalLinks,
            () => new CreateIndexModel<ExternalDocumentInternalLink>[]
{
            // One live link per (external, internal, type) triple — the link command is idempotent.
            new CreateIndexModel<ExternalDocumentInternalLink>(
                Builders<ExternalDocumentInternalLink>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ExternalDocumentRegisterEntryId)
                    .Ascending(x => x.InternalRegisterEntryId).Ascending(x => x.LinkType),
                new CreateIndexOptions<ExternalDocumentInternalLink>
                {
                    Unique = true,
                    Name = "ux_dm_external_document_links_pair_type_active",
                    PartialFilterExpression = Builders<ExternalDocumentInternalLink>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<ExternalDocumentInternalLink>(
                Builders<ExternalDocumentInternalLink>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.InternalRegisterEntryId),
                new CreateIndexOptions { Name = "ix_dm_external_document_links_internal" })
        }),
        Collection<ExternalDocumentMonitoringCheck>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementExternalDocumentMonitoringChecks,
            () => new CreateIndexModel<ExternalDocumentMonitoringCheck>[]
{
            new CreateIndexModel<ExternalDocumentMonitoringCheck>(
                Builders<ExternalDocumentMonitoringCheck>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ExternalDocumentRegisterEntryId).Descending(x => x.CheckDate),
                new CreateIndexOptions { Name = "ix_dm_external_document_checks_entry_date" })
        }),
        Collection<ExternalDocumentRegisterEntry>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementExternalDocuments,
            () => new CreateIndexModel<ExternalDocumentRegisterEntry>[]
{
            new CreateIndexModel<ExternalDocumentRegisterEntry>(
                Builders<ExternalDocumentRegisterEntry>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ExternalDocumentStatus).Ascending(x => x.NextCheckDueDate),
                new CreateIndexOptions { Name = "ix_dm_external_documents_status_next_check" }),
            new CreateIndexModel<ExternalDocumentRegisterEntry>(
                Builders<ExternalDocumentRegisterEntry>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SourceStatus),
                new CreateIndexOptions { Name = "ix_dm_external_documents_source_status" }),
            new CreateIndexModel<ExternalDocumentRegisterEntry>(
                Builders<ExternalDocumentRegisterEntry>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.MonitoringOwnerUserId),
                new CreateIndexOptions { Name = "ix_dm_external_documents_owner" })
        }),
        Collection<DocumentGDocPCorrectionPolicy>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementGdocpCorrectionPolicies,
            () => new CreateIndexModel<DocumentGDocPCorrectionPolicy>[]
{
            new CreateIndexModel<DocumentGDocPCorrectionPolicy>(
                Builders<DocumentGDocPCorrectionPolicy>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.PolicyKey),
                new CreateIndexOptions<DocumentGDocPCorrectionPolicy>
                {
                    Unique = true,
                    Name = "ux_dm_gdocp_correction_policies_key_active",
                    PartialFilterExpression = Builders<DocumentGDocPCorrectionPolicy>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentGDocPCorrectionPolicy>(
                Builders<DocumentGDocPCorrectionPolicy>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SubjectType).Ascending(x => x.PolicyStatus),
                new CreateIndexOptions { Name = "ix_dm_gdocp_correction_policies_subject_status" })
        }),
        Collection<DocumentGDocPCorrectionRecord>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementGdocpCorrectionRecords,
            () => new CreateIndexModel<DocumentGDocPCorrectionRecord>[]
{
            new CreateIndexModel<DocumentGDocPCorrectionRecord>(
                Builders<DocumentGDocPCorrectionRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CorrectionNumber),
                new CreateIndexOptions<DocumentGDocPCorrectionRecord>
                {
                    Unique = true,
                    Name = "ux_dm_gdocp_corrections_number_active",
                    PartialFilterExpression = Builders<DocumentGDocPCorrectionRecord>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentGDocPCorrectionRecord>(
                Builders<DocumentGDocPCorrectionRecord>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SubjectType).Ascending(x => x.SubjectId).Ascending(x => x.CorrectedAt),
                new CreateIndexOptions { Name = "ix_dm_gdocp_corrections_subject_corrected" }),
            new CreateIndexModel<DocumentGDocPCorrectionRecord>(
                Builders<DocumentGDocPCorrectionRecord>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.ReviewStatus).Ascending(x => x.CorrectedAt),
                new CreateIndexOptions { Name = "ix_dm_gdocp_corrections_review_status" }),
            new CreateIndexModel<DocumentGDocPCorrectionRecord>(
                Builders<DocumentGDocPCorrectionRecord>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.IsHighRiskCorrection).Ascending(x => x.CorrectedAt),
                new CreateIndexOptions { Name = "ix_dm_gdocp_corrections_high_risk" })
        }),
        Collection<DocumentGDocPCorrectionReview>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementGdocpCorrectionReviews,
            () => new CreateIndexModel<DocumentGDocPCorrectionReview>[]
{
            new CreateIndexModel<DocumentGDocPCorrectionReview>(
                Builders<DocumentGDocPCorrectionReview>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.CorrectionRecordId).Ascending(x => x.ReviewedAt),
                new CreateIndexOptions { Name = "ix_dm_gdocp_correction_reviews_correction_reviewed" })
        }),
        Collection<DocumentGovernancePolicyPackApplication>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementGovernancePolicyPackApplications,
            () => new CreateIndexModel<DocumentGovernancePolicyPackApplication>[]
{
            new CreateIndexModel<DocumentGovernancePolicyPackApplication>(
                Builders<DocumentGovernancePolicyPackApplication>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.PackKey).Descending(x => x.AppliedAt),
                new CreateIndexOptions { Name = "ix_dm_governance_policy_pack_applications_pack_applied" }),
            new CreateIndexModel<DocumentGovernancePolicyPackApplication>(
                Builders<DocumentGovernancePolicyPackApplication>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.PackVersion),
                new CreateIndexOptions { Name = "ix_dm_governance_policy_pack_applications_version" }),
            new CreateIndexModel<DocumentGovernancePolicyPackApplication>(
                Builders<DocumentGovernancePolicyPackApplication>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.ApplicationStatus),
                new CreateIndexOptions { Name = "ix_dm_governance_policy_pack_applications_status" })
        }),
        Collection<DocumentGovernanceSweepRun>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementGovernanceSweepRuns,
            () => new CreateIndexModel<DocumentGovernanceSweepRun>[]
{
            new CreateIndexModel<DocumentGovernanceSweepRun>(
                Builders<DocumentGovernanceSweepRun>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SweepKey).Descending(x => x.StartedAt),
                new CreateIndexOptions { Name = "ix_dm_governance_sweep_runs_key_started" }),
            new CreateIndexModel<DocumentGovernanceSweepRun>(
                Builders<DocumentGovernanceSweepRun>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_dm_governance_sweep_runs_status" }),
            new CreateIndexModel<DocumentGovernanceSweepRun>(
                Builders<DocumentGovernanceSweepRun>.IndexKeys.Ascending(x => x.TenantId)
                    .Descending(x => x.StartedAt),
                new CreateIndexOptions { Name = "ix_dm_governance_sweep_runs_started" })
        }),
        Collection<DocumentIdentifierAllocation>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementIdentifierAllocations,
            () => new CreateIndexModel<DocumentIdentifierAllocation>[]
{
            new CreateIndexModel<DocumentIdentifierAllocation>(
                Builders<DocumentIdentifierAllocation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.IdentifierType).Ascending(x => x.IdentifierValue),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_dm_identifier_allocations_tenant_type_value_never_reuse"
                }),
            new CreateIndexModel<DocumentIdentifierAllocation>(
                Builders<DocumentIdentifierAllocation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId),
                new CreateIndexOptions { Name = "ix_dm_identifier_allocations_register_entry" })
        }),
        Collection<DocumentIdentifierSequenceCounter>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementIdentifierSequenceCounters,
            () => new CreateIndexModel<DocumentIdentifierSequenceCounter>[]
{
            new CreateIndexModel<DocumentIdentifierSequenceCounter>(
                Builders<DocumentIdentifierSequenceCounter>.IndexKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.IdentifierType)
                    .Ascending(x => x.Prefix).Ascending(x => x.DomainCode).Ascending(x => x.TypeCode),
                new CreateIndexOptions
                {
                    Unique = true,
                    Name = "ux_dm_identifier_sequence_counters_key"
                })
        }),
        Collection<DocumentLegalHoldSubject>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementLegalHoldSubjects,
            () => new CreateIndexModel<DocumentLegalHoldSubject>[]
{
            new CreateIndexModel<DocumentLegalHoldSubject>(
                Builders<DocumentLegalHoldSubject>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.LegalHoldId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_dm_legal_hold_subjects_hold_status" }),
            new CreateIndexModel<DocumentLegalHoldSubject>(
                Builders<DocumentLegalHoldSubject>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SubjectType).Ascending(x => x.SubjectId),
                new CreateIndexOptions { Name = "ix_dm_legal_hold_subjects_subject" })
        }),
        Collection<DocumentLegalHold>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementLegalHolds,
            () => new CreateIndexModel<DocumentLegalHold>[]
{
            new CreateIndexModel<DocumentLegalHold>(
                Builders<DocumentLegalHold>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.HoldKey),
                new CreateIndexOptions<DocumentLegalHold>
                {
                    Unique = true,
                    Name = "ux_dm_legal_holds_key_active",
                    PartialFilterExpression = Builders<DocumentLegalHold>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentLegalHold>(
                Builders<DocumentLegalHold>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.HoldStatus).Ascending(x => x.EffectiveFrom),
                new CreateIndexOptions { Name = "ix_dm_legal_holds_status_effective" })
        }),
        Collection<DocumentLifecycleTransitionRecord>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementLifecycleTransitions,
            () => new CreateIndexModel<DocumentLifecycleTransitionRecord>[]
{
            new CreateIndexModel<DocumentLifecycleTransitionRecord>(
                Builders<DocumentLifecycleTransitionRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.PerformedAt),
                new CreateIndexOptions { Name = "ix_dm_lifecycle_transitions_register_entry" })
        }),
        Collection<DocumentObsoleteCopyFinding>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementObsoleteCopyFindings,
            () => new CreateIndexModel<DocumentObsoleteCopyFinding>[]
{
            new CreateIndexModel<DocumentObsoleteCopyFinding>(
                Builders<DocumentObsoleteCopyFinding>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.FindingKey),
                new CreateIndexOptions<DocumentObsoleteCopyFinding>
                {
                    Unique = true,
                    Name = "ux_dm_obsolete_copy_findings_entry_key_active",
                    PartialFilterExpression = Builders<DocumentObsoleteCopyFinding>.Filter.Eq(x => x.IsDeleted, false)
                })
        }),
        Collection<DocumentPeriodicReviewEscalation>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementPeriodicReviewEscalations,
            () => new CreateIndexModel<DocumentPeriodicReviewEscalation>[]
{
            new CreateIndexModel<DocumentPeriodicReviewEscalation>(
                Builders<DocumentPeriodicReviewEscalation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.PeriodicReviewId).Ascending(x => x.EscalationType),
                new CreateIndexOptions { Name = "ix_dm_periodic_review_escalations_review_type" })
        }),
        Collection<DocumentPeriodicReviewExtension>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementPeriodicReviewExtensions,
            () => new CreateIndexModel<DocumentPeriodicReviewExtension>[]
{
            new CreateIndexModel<DocumentPeriodicReviewExtension>(
                Builders<DocumentPeriodicReviewExtension>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.PeriodicReviewId),
                new CreateIndexOptions { Name = "ix_dm_periodic_review_extensions_review" })
        }),
        Collection<DocumentPeriodicReview>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementPeriodicReviews,
            () => new CreateIndexModel<DocumentPeriodicReview>[]
{
            new CreateIndexModel<DocumentPeriodicReview>(
                Builders<DocumentPeriodicReview>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.ReviewNumber),
                new CreateIndexOptions<DocumentPeriodicReview>
                {
                    Unique = true,
                    Name = "ux_dm_periodic_reviews_entry_number_active",
                    PartialFilterExpression = Builders<DocumentPeriodicReview>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentPeriodicReview>(
                Builders<DocumentPeriodicReview>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ReviewStatus).Ascending(x => x.ReviewDueDate),
                new CreateIndexOptions { Name = "ix_dm_periodic_reviews_status_due" })
        }),
        Collection<DocumentDeviation>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementQualityDeviations,
            () => new CreateIndexModel<DocumentDeviation>[]
{
            new CreateIndexModel<DocumentDeviation>(
                Builders<DocumentDeviation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.DeviationNumber),
                new CreateIndexOptions<DocumentDeviation>
                {
                    Unique = true,
                    Name = "ux_dm_quality_deviations_number_active",
                    PartialFilterExpression = Builders<DocumentDeviation>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentDeviation>(
                Builders<DocumentDeviation>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.QualityEventId).Ascending(x => x.DeviationStatus),
                new CreateIndexOptions { Name = "ix_dm_quality_deviations_event_status" }),
            new CreateIndexModel<DocumentDeviation>(
                Builders<DocumentDeviation>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.DeviationSeverity).Ascending(x => x.DeviationStatus),
                new CreateIndexOptions { Name = "ix_dm_quality_deviations_severity_status" })
        }),
        Collection<DocumentQualityEventSourceLink>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementQualityEventSourceLinks,
            () => new CreateIndexModel<DocumentQualityEventSourceLink>[]
{
            // The bridge idempotency lookup.
            new CreateIndexModel<DocumentQualityEventSourceLink>(
                Builders<DocumentQualityEventSourceLink>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SourceType).Ascending(x => x.SourceId).Ascending(x => x.EventType),
                new CreateIndexOptions { Name = "ix_dm_quality_event_source_links_source_type" }),
            new CreateIndexModel<DocumentQualityEventSourceLink>(
                Builders<DocumentQualityEventSourceLink>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.QualityEventId),
                new CreateIndexOptions { Name = "ix_dm_quality_event_source_links_event" })
        }),
        Collection<DocumentQualityEvent>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementQualityEvents,
            () => new CreateIndexModel<DocumentQualityEvent>[]
{
            new CreateIndexModel<DocumentQualityEvent>(
                Builders<DocumentQualityEvent>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.QualityEventNumber),
                new CreateIndexOptions<DocumentQualityEvent>
                {
                    Unique = true,
                    Name = "ux_dm_quality_events_number_active",
                    PartialFilterExpression = Builders<DocumentQualityEvent>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentQualityEvent>(
                Builders<DocumentQualityEvent>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.EventStatus).Descending(x => x.DetectedAt),
                new CreateIndexOptions { Name = "ix_dm_quality_events_status_detected" }),
            new CreateIndexModel<DocumentQualityEvent>(
                Builders<DocumentQualityEvent>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId),
                new CreateIndexOptions { Name = "ix_dm_quality_events_register_entry" }),
            new CreateIndexModel<DocumentQualityEvent>(
                Builders<DocumentQualityEvent>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SourceType).Ascending(x => x.SourceId),
                new CreateIndexOptions { Name = "ix_dm_quality_events_source" })
        }),
        Collection<DocumentReleaseGateEvaluation>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementReleaseGateEvaluations,
            () => new CreateIndexModel<DocumentReleaseGateEvaluation>[]
{
            new CreateIndexModel<DocumentReleaseGateEvaluation>(
                Builders<DocumentReleaseGateEvaluation>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Descending(x => x.EvaluatedAt),
                new CreateIndexOptions { Name = "ix_dm_release_gate_evaluations_entry_time" })
        }),
        Collection<DocumentReleaseGateEvidence>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementReleaseGateEvidence,
            () => new CreateIndexModel<DocumentReleaseGateEvidence>[]
{
            new CreateIndexModel<DocumentReleaseGateEvidence>(
                Builders<DocumentReleaseGateEvidence>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.GateKey).Descending(x => x.VerificationDate),
                new CreateIndexOptions { Name = "ix_dm_release_gate_evidence_entry_gate_time" })
        }),
        Collection<DocumentReleaseGateResult>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementReleaseGateResults,
            () => new CreateIndexModel<DocumentReleaseGateResult>[]
{
            new CreateIndexModel<DocumentReleaseGateResult>(
                Builders<DocumentReleaseGateResult>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.EvaluationId).Ascending(x => x.GateNumber),
                new CreateIndexOptions { Name = "ix_dm_release_gate_results_evaluation" })
        }),
        Collection<DocumentRepositoryAssessmentFinding>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementRepositoryAssessmentFindings,
            () => new CreateIndexModel<DocumentRepositoryAssessmentFinding>[]
{
            new CreateIndexModel<DocumentRepositoryAssessmentFinding>(
                Builders<DocumentRepositoryAssessmentFinding>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RepositoryAssessmentId).Ascending(x => x.FindingKey),
                new CreateIndexOptions<DocumentRepositoryAssessmentFinding>
                {
                    Unique = true,
                    Name = "ux_dm_repository_assessment_findings_assessment_key_active",
                    PartialFilterExpression = Builders<DocumentRepositoryAssessmentFinding>.Filter.Eq(x => x.IsDeleted, false)
                })
        }),
        Collection<DocumentRepositoryAssessment>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementRepositoryAssessments,
            () => new CreateIndexModel<DocumentRepositoryAssessment>[]
{
            new CreateIndexModel<DocumentRepositoryAssessment>(
                Builders<DocumentRepositoryAssessment>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RepositoryKey),
                new CreateIndexOptions<DocumentRepositoryAssessment>
                {
                    Unique = true,
                    Name = "ux_dm_repository_assessments_tenant_key_active",
                    PartialFilterExpression = Builders<DocumentRepositoryAssessment>.Filter.Eq(x => x.IsDeleted, false)
                })
        }),
        Collection<DocumentRepositoryDowntimeEvent>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementRepositoryDowntimeEvents,
            () => new CreateIndexModel<DocumentRepositoryDowntimeEvent>[]
{
            new CreateIndexModel<DocumentRepositoryDowntimeEvent>(
                Builders<DocumentRepositoryDowntimeEvent>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.DowntimeNumber),
                new CreateIndexOptions<DocumentRepositoryDowntimeEvent>
                {
                    Unique = true,
                    Name = "ux_dm_downtime_events_number_active",
                    PartialFilterExpression = Builders<DocumentRepositoryDowntimeEvent>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentRepositoryDowntimeEvent>(
                Builders<DocumentRepositoryDowntimeEvent>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.DowntimeStatus).Descending(x => x.StartedAt),
                new CreateIndexOptions { Name = "ix_dm_downtime_events_status_started" }),
            new CreateIndexModel<DocumentRepositoryDowntimeEvent>(
                Builders<DocumentRepositoryDowntimeEvent>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RepositoryAssessmentId),
                new CreateIndexOptions { Name = "ix_dm_downtime_events_repository_assessment" })
        }),
        Collection<DocumentRetentionPolicy>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementRetentionPolicies,
            () => new CreateIndexModel<DocumentRetentionPolicy>[]
{
            new CreateIndexModel<DocumentRetentionPolicy>(
                Builders<DocumentRetentionPolicy>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.PolicyKey),
                new CreateIndexOptions<DocumentRetentionPolicy>
                {
                    Unique = true,
                    Name = "ux_dm_retention_policies_key_active",
                    PartialFilterExpression = Builders<DocumentRetentionPolicy>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentRetentionPolicy>(
                Builders<DocumentRetentionPolicy>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SubjectType).Ascending(x => x.PolicyStatus),
                new CreateIndexOptions { Name = "ix_dm_retention_policies_subject_status" })
        }),
        Collection<DocumentRetentionSubject>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementRetentionSubjects,
            () => new CreateIndexModel<DocumentRetentionSubject>[]
{
            // One snapshot per governed record — re-evaluation overwrites it in place.
            new CreateIndexModel<DocumentRetentionSubject>(
                Builders<DocumentRetentionSubject>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SubjectType).Ascending(x => x.SubjectId),
                new CreateIndexOptions<DocumentRetentionSubject>
                {
                    Unique = true,
                    Name = "ux_dm_retention_subjects_subject_active",
                    PartialFilterExpression = Builders<DocumentRetentionSubject>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentRetentionSubject>(
                Builders<DocumentRetentionSubject>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.IsDispositionEligible)
                    .Ascending(x => x.IsBlockedByLegalHold).Ascending(x => x.RetentionDueDate),
                new CreateIndexOptions { Name = "ix_dm_retention_subjects_eligibility" }),
            new CreateIndexModel<DocumentRetentionSubject>(
                Builders<DocumentRetentionSubject>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId),
                new CreateIndexOptions { Name = "ix_dm_retention_subjects_register_entry" })
        }),
        Collection<DocumentRetirementCase>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementRetirementCases,
            () => new CreateIndexModel<DocumentRetirementCase>[]
{
            new CreateIndexModel<DocumentRetirementCase>(
                Builders<DocumentRetirementCase>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.CaseNumber),
                new CreateIndexOptions<DocumentRetirementCase>
                {
                    Unique = true,
                    Name = "ux_dm_retirement_cases_entry_number_active",
                    PartialFilterExpression = Builders<DocumentRetirementCase>.Filter.Eq(x => x.IsDeleted, false)
                })
        }),
        Collection<DocumentSignaturePolicy>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementSignaturePolicies,
            () => new CreateIndexModel<DocumentSignaturePolicy>[]
{
            new CreateIndexModel<DocumentSignaturePolicy>(
                Builders<DocumentSignaturePolicy>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.PolicyKey),
                new CreateIndexOptions<DocumentSignaturePolicy>
                {
                    Unique = true,
                    Name = "ux_dm_signature_policies_key_active",
                    PartialFilterExpression = Builders<DocumentSignaturePolicy>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentSignaturePolicy>(
                Builders<DocumentSignaturePolicy>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SignableSubjectType).Ascending(x => x.PolicyStatus),
                new CreateIndexOptions { Name = "ix_dm_signature_policies_subject_status" })
        }),
        Collection<DocumentSignatureRecord>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementSignatureRecords,
            () => new CreateIndexModel<DocumentSignatureRecord>[]
{
            new CreateIndexModel<DocumentSignatureRecord>(
                Builders<DocumentSignatureRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SignatureNumber),
                new CreateIndexOptions<DocumentSignatureRecord>
                {
                    Unique = true,
                    Name = "ux_dm_signature_records_number_active",
                    PartialFilterExpression = Builders<DocumentSignatureRecord>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentSignatureRecord>(
                Builders<DocumentSignatureRecord>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SubjectType).Ascending(x => x.SubjectId).Descending(x => x.SignedAt),
                new CreateIndexOptions { Name = "ix_dm_signature_records_subject_signed" }),
            // The duplicate-signature guard on the sign path.
            new CreateIndexModel<DocumentSignatureRecord>(
                Builders<DocumentSignatureRecord>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SubjectType).Ascending(x => x.SubjectId)
                    .Ascending(x => x.SignatureMeaning).Ascending(x => x.ObjectFingerprint),
                new CreateIndexOptions { Name = "ix_dm_signature_records_subject_meaning_fingerprint" }),
            new CreateIndexModel<DocumentSignatureRecord>(
                Builders<DocumentSignatureRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SignatureRequestId),
                new CreateIndexOptions { Name = "ix_dm_signature_records_request" }),
            new CreateIndexModel<DocumentSignatureRecord>(
                Builders<DocumentSignatureRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SignatureStatus),
                new CreateIndexOptions { Name = "ix_dm_signature_records_status" })
        }),
        Collection<DocumentSignatureRequest>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementSignatureRequests,
            () => new CreateIndexModel<DocumentSignatureRequest>[]
{
            new CreateIndexModel<DocumentSignatureRequest>(
                Builders<DocumentSignatureRequest>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.SignatureRequestNumber),
                new CreateIndexOptions<DocumentSignatureRequest>
                {
                    Unique = true,
                    Name = "ux_dm_signature_requests_number_active",
                    PartialFilterExpression = Builders<DocumentSignatureRequest>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentSignatureRequest>(
                Builders<DocumentSignatureRequest>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SubjectType).Ascending(x => x.SubjectId),
                new CreateIndexOptions { Name = "ix_dm_signature_requests_subject" }),
            new CreateIndexModel<DocumentSignatureRequest>(
                Builders<DocumentSignatureRequest>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.RequestStatus).Ascending(x => x.DueDate),
                new CreateIndexOptions { Name = "ix_dm_signature_requests_status_due" })
        }),
        Collection<DocumentSignedObjectFingerprint>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementSignedObjectFingerprints,
            () => new CreateIndexModel<DocumentSignedObjectFingerprint>[]
{
            new CreateIndexModel<DocumentSignedObjectFingerprint>(
                Builders<DocumentSignedObjectFingerprint>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.SubjectType).Ascending(x => x.SubjectId).Descending(x => x.GeneratedAt),
                new CreateIndexOptions { Name = "ix_dm_signed_object_fingerprints_subject_generated" })
        }),
        Collection<DocumentSuspensionCase>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementSuspensionCases,
            () => new CreateIndexModel<DocumentSuspensionCase>[]
{
            new CreateIndexModel<DocumentSuspensionCase>(
                Builders<DocumentSuspensionCase>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.CaseNumber),
                new CreateIndexOptions<DocumentSuspensionCase>
                {
                    Unique = true,
                    Name = "ux_dm_suspension_cases_entry_number_active",
                    PartialFilterExpression = Builders<DocumentSuspensionCase>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentSuspensionCase>(
                Builders<DocumentSuspensionCase>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.CaseStatus),
                new CreateIndexOptions { Name = "ix_dm_suspension_cases_status" })
        }),
        Collection<DocumentTemporaryControlledIssue>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementTemporaryControlledIssues,
            () => new CreateIndexModel<DocumentTemporaryControlledIssue>[]
{
            new CreateIndexModel<DocumentTemporaryControlledIssue>(
                Builders<DocumentTemporaryControlledIssue>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.IssueNumber),
                new CreateIndexOptions<DocumentTemporaryControlledIssue>
                {
                    Unique = true,
                    Name = "ux_dm_temporary_controlled_issues_number_active",
                    PartialFilterExpression = Builders<DocumentTemporaryControlledIssue>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<DocumentTemporaryControlledIssue>(
                Builders<DocumentTemporaryControlledIssue>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.DowntimeEventId).Ascending(x => x.IssueStatus),
                new CreateIndexOptions { Name = "ix_dm_temporary_controlled_issues_event_status" }),
            new CreateIndexModel<DocumentTemporaryControlledIssue>(
                Builders<DocumentTemporaryControlledIssue>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.IssueStatus).Ascending(x => x.ReconciliationDueDate),
                new CreateIndexOptions { Name = "ix_dm_temporary_controlled_issues_status_due" }),
            new CreateIndexModel<DocumentTemporaryControlledIssue>(
                Builders<DocumentTemporaryControlledIssue>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId),
                new CreateIndexOptions { Name = "ix_dm_temporary_controlled_issues_register_entry" })
        }),
        Collection<TemporaryInstructionControl>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementTemporaryInstructionControls,
            () => new CreateIndexModel<TemporaryInstructionControl>[]
{
            new CreateIndexModel<TemporaryInstructionControl>(
                Builders<TemporaryInstructionControl>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId),
                new CreateIndexOptions<TemporaryInstructionControl>
                {
                    Unique = true,
                    Name = "ux_dm_temporary_instruction_controls_entry_active",
                    PartialFilterExpression = Builders<TemporaryInstructionControl>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TemporaryInstructionControl>(
                Builders<TemporaryInstructionControl>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.TemporaryInstructionStatus).Ascending(x => x.ValidUntil),
                new CreateIndexOptions { Name = "ix_dm_temporary_instruction_controls_status_validity" })
        }),
        Collection<DocumentTrainingAssignment>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementTrainingAssignments,
            () => new CreateIndexModel<DocumentTrainingAssignment>[]
{
            new CreateIndexModel<DocumentTrainingAssignment>(
                Builders<DocumentTrainingAssignment>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.RequirementId),
                new CreateIndexOptions { Name = "ix_dm_training_assignments_entry_requirement" })
        }),
        Collection<DocumentTrainingMatrixRequirement>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementTrainingRequirements,
            () => new CreateIndexModel<DocumentTrainingMatrixRequirement>[]
{
            new CreateIndexModel<DocumentTrainingMatrixRequirement>(
                Builders<DocumentTrainingMatrixRequirement>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.RegisterEntryId).Ascending(x => x.RequirementKey),
                new CreateIndexOptions<DocumentTrainingMatrixRequirement>
                {
                    Unique = true,
                    Name = "ux_dm_training_requirements_entry_key_active",
                    PartialFilterExpression = Builders<DocumentTrainingMatrixRequirement>.Filter.Eq(x => x.IsDeleted, false)
                })
        }),
        Collection<TemplateVariantLocalizationProfile>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementVariantLocalizationProfiles,
            () => new CreateIndexModel<TemplateVariantLocalizationProfile>[]
{
            new CreateIndexModel<TemplateVariantLocalizationProfile>(
                Builders<TemplateVariantLocalizationProfile>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.TemplateVariantId),
                new CreateIndexOptions<TemplateVariantLocalizationProfile>
                {
                    Unique = true,
                    Name = "ux_dm_variant_localization_profiles_variant_active",
                    PartialFilterExpression = Builders<TemplateVariantLocalizationProfile>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TemplateVariantLocalizationProfile>(
                Builders<TemplateVariantLocalizationProfile>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ParentTemplateMasterId),
                new CreateIndexOptions { Name = "ix_dm_variant_localization_profiles_parent_master" }),
            new CreateIndexModel<TemplateVariantLocalizationProfile>(
                Builders<TemplateVariantLocalizationProfile>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.VariantLanguageCode).Ascending(x => x.LocalAdoptionStatus),
                new CreateIndexOptions { Name = "ix_dm_variant_localization_profiles_language_adoption" })
        }),
        Collection<TemplateVariantParentChangeAssessment>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementVariantParentChangeAssessments,
            () => new CreateIndexModel<TemplateVariantParentChangeAssessment>[]
{
            new CreateIndexModel<TemplateVariantParentChangeAssessment>(
                Builders<TemplateVariantParentChangeAssessment>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.TemplateVariantId).Descending(x => x.AssessedAt),
                new CreateIndexOptions { Name = "ix_dm_variant_parent_change_assessments_variant_assessed" })
        }),
        Collection<TemplateVariantReviewEvidence>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementVariantReviewEvidence,
            () => new CreateIndexModel<TemplateVariantReviewEvidence>[]
{
            new CreateIndexModel<TemplateVariantReviewEvidence>(
                Builders<TemplateVariantReviewEvidence>.IndexKeys.Ascending(x => x.TenantId)
                    .Ascending(x => x.TemplateVariantId).Ascending(x => x.EvidenceType).Ascending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "ix_dm_variant_review_evidence_variant_type_created" })
        }),
        Collection<ControlledDocumentRegistrationOperation>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementControlledDocumentRegistrationOperations,
            () => new CreateIndexModel<ControlledDocumentRegistrationOperation>[]
{
            new CreateIndexModel<ControlledDocumentRegistrationOperation>(
                Builders<ControlledDocumentRegistrationOperation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<ControlledDocumentRegistrationOperation>
                {
                    Unique = true,
                    Name = "ux_dm_registration_tenant_idempotency_active",
                    PartialFilterExpression = Builders<ControlledDocumentRegistrationOperation>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<ControlledDocumentRegistrationOperation>(
                Builders<ControlledDocumentRegistrationOperation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.ControlledDocumentId),
                new CreateIndexOptions<ControlledDocumentRegistrationOperation>
                {
                    Unique = true,
                    Name = "ux_dm_registration_tenant_document_active",
                    // Partial indexes don't support $ne/$not, so match on the GUID's BSON type
                    // (subtype-4 binary) to include only rows where ControlledDocumentId is set.
                    PartialFilterExpression = Builders<ControlledDocumentRegistrationOperation>.Filter.And(
                        Builders<ControlledDocumentRegistrationOperation>.Filter.Eq(x => x.IsDeleted, false),
                        Builders<ControlledDocumentRegistrationOperation>.Filter.Type(x => x.ControlledDocumentId, BsonType.Binary))
                }),
            new CreateIndexModel<ControlledDocumentRegistrationOperation>(
                Builders<ControlledDocumentRegistrationOperation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.MasterRegisterEntryId),
                new CreateIndexOptions<ControlledDocumentRegistrationOperation>
                {
                    Unique = true,
                    Name = "ux_dm_registration_tenant_register_active",
                    // Partial indexes don't support $ne/$not, so match on the GUID's BSON type
                    // (subtype-4 binary) to include only rows where MasterRegisterEntryId is set.
                    PartialFilterExpression = Builders<ControlledDocumentRegistrationOperation>.Filter.And(
                        Builders<ControlledDocumentRegistrationOperation>.Filter.Eq(x => x.IsDeleted, false),
                        Builders<ControlledDocumentRegistrationOperation>.Filter.Type(x => x.MasterRegisterEntryId, BsonType.Binary))
                }),
            new CreateIndexModel<ControlledDocumentRegistrationOperation>(
                Builders<ControlledDocumentRegistrationOperation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Status)
                    .Descending(x => x.UpdatedAt),
                new CreateIndexOptions { Name = "ix_dm_registration_tenant_status_updated" })
        }),
        Collection<CorporateCollectionInstanceProvisioningOperation>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementCorporateCollectionProvisioningOperations,
            () => new CreateIndexModel<CorporateCollectionInstanceProvisioningOperation>[]
{
            new CreateIndexModel<CorporateCollectionInstanceProvisioningOperation>(
                Builders<CorporateCollectionInstanceProvisioningOperation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<CorporateCollectionInstanceProvisioningOperation>
                {
                    Unique = true,
                    Name = "ux_dm_corporate_provisioning_tenant_idempotency_active",
                    PartialFilterExpression = Builders<CorporateCollectionInstanceProvisioningOperation>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<CorporateCollectionInstanceProvisioningOperation>(
                Builders<CorporateCollectionInstanceProvisioningOperation>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.CorporateOwnerId)
                    .Ascending(x => x.BaselineReleaseId)
                    .Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_dm_corporate_provisioning_owner_baseline_status" })
        }),
        // No custom index in the pre-refactor monolith — declared so the collection is in the
        // manifest (EveryCollectionTouches), carrying only the implicit _id. A recorded gap, not a
        // hidden one; add real indexes here if a read path needs them.
        Collection<DocumentMasterRegisterEntry>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementMasterRegister,
            () => System.Array.Empty<CreateIndexModel<DocumentMasterRegisterEntry>>()),
        // No custom index in the pre-refactor monolith — declared so the collection is in the
        // manifest (EveryCollectionTouches), carrying only the implicit _id. A recorded gap, not a
        // hidden one; add real indexes here if a read path needs them.
        Collection<DocumentVariant>(
            SchemaProfile.DocumentManagement,
            PlatformCollections.DocumentManagementDocumentVariants,
            () => System.Array.Empty<CreateIndexModel<DocumentVariant>>()),
    };
}
