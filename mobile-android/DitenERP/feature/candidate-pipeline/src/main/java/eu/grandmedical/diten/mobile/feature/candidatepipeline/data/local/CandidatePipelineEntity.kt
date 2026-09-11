package eu.grandmedical.diten.mobile.feature.candidatepipeline.data.local

import androidx.room.ColumnInfo
import androidx.room.Embedded
import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey
import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import java.time.Instant

/**
 * The feature's cache row. Follows the `:core:database` reference template:
 * an embedded [ScopeKeys] (so `tenant_id` + `legal_entity_id` are real, indexed
 * columns and the partition boundary is explicit) plus the [syncStatus]
 * offline-first metadata.
 *
 * Readiness states are stored as their Int [code] (compact, matches the wire);
 * [dependencyStates] round-trips through [CandidatePipelineConverters]; timestamps
 * through the shared `DitenTypeConverters`.
 */
@Entity(
    tableName = "candidate_pipeline",
    indices = [
        Index(value = ["tenant_id", "legal_entity_id"]),
    ],
)
@Suppress("LongParameterList") // Cache row mirrors the full readiness aggregate.
data class CandidatePipelineEntity(
    @PrimaryKey
    @ColumnInfo(name = "id")
    val id: String,
    @Embedded
    val scope: ScopeKeys,
    @ColumnInfo(name = "code")
    val code: String,
    @ColumnInfo(name = "display_name")
    val displayName: String,
    @ColumnInfo(name = "pipeline_readiness_state")
    val pipelineReadinessState: Int,
    @ColumnInfo(name = "pipeline_stage_governance_state")
    val pipelineStageGovernanceState: Int,
    @ColumnInfo(name = "interview_scheduling_readiness_state")
    val interviewSchedulingReadinessState: Int,
    @ColumnInfo(name = "interviewer_assignment_readiness_state")
    val interviewerAssignmentReadinessState: Int,
    @ColumnInfo(name = "evaluation_governance_state")
    val evaluationGovernanceState: Int,
    @ColumnInfo(name = "candidate_communication_boundary_state")
    val candidateCommunicationBoundaryState: Int,
    @ColumnInfo(name = "consent_precondition_state")
    val consentPreconditionState: Int,
    @ColumnInfo(name = "data_minimization_state")
    val dataMinimizationState: Int,
    @ColumnInfo(name = "retention_policy_state")
    val retentionPolicyState: Int,
    @ColumnInfo(name = "evidence_policy_state")
    val evidencePolicyState: Int,
    @ColumnInfo(name = "calendar_dependency_state")
    val calendarDependencyState: Int,
    @ColumnInfo(name = "notification_dependency_state")
    val notificationDependencyState: Int,
    @ColumnInfo(name = "document_dependency_state")
    val documentDependencyState: Int,
    @ColumnInfo(name = "automated_decision_boundary_state")
    val automatedDecisionBoundaryState: Int,
    @ColumnInfo(name = "dependency_states")
    val dependencyStates: Map<String, Int> = emptyMap(),
    @ColumnInfo(name = "source_contract_version")
    val sourceContractVersion: String,
    @ColumnInfo(name = "pipeline_readiness_version")
    val pipelineReadinessVersion: Long,
    @ColumnInfo(name = "deferred_reason")
    val deferredReason: String? = null,
    @ColumnInfo(name = "last_evaluated_at")
    val lastEvaluatedAt: Instant? = null,
    @ColumnInfo(name = "sync_status")
    val syncStatus: SyncStatus = SyncStatus.SYNCED,
)
