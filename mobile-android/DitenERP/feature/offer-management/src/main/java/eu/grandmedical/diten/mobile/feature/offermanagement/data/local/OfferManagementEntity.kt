package eu.grandmedical.diten.mobile.feature.offermanagement.data.local

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
 * [dependencyStates] round-trips through [OfferManagementConverters]; timestamps
 * through the shared `DitenTypeConverters`.
 */
@Entity(
    tableName = "offer_management",
    indices = [
        Index(value = ["tenant_id", "legal_entity_id"]),
    ],
)
@Suppress("LongParameterList") // Cache row mirrors the full readiness aggregate.
data class OfferManagementEntity(
    @PrimaryKey
    @ColumnInfo(name = "id")
    val id: String,
    @Embedded
    val scope: ScopeKeys,
    @ColumnInfo(name = "code")
    val code: String,
    @ColumnInfo(name = "display_name")
    val displayName: String,
    @ColumnInfo(name = "offer_readiness_state")
    val offerReadinessState: Int,
    @ColumnInfo(name = "offer_workflow_boundary_state")
    val offerWorkflowBoundaryState: Int,
    @ColumnInfo(name = "approval_workflow_boundary_state")
    val approvalWorkflowBoundaryState: Int,
    @ColumnInfo(name = "candidate_acceptance_boundary_state")
    val candidateAcceptanceBoundaryState: Int,
    @ColumnInfo(name = "offer_document_boundary_state")
    val offerDocumentBoundaryState: Int,
    @ColumnInfo(name = "compensation_data_boundary_state")
    val compensationDataBoundaryState: Int,
    @ColumnInfo(name = "benefits_data_boundary_state")
    val benefitsDataBoundaryState: Int,
    @ColumnInfo(name = "payroll_data_boundary_state")
    val payrollDataBoundaryState: Int,
    @ColumnInfo(name = "consent_precondition_state")
    val consentPreconditionState: Int,
    @ColumnInfo(name = "data_minimization_state")
    val dataMinimizationState: Int,
    @ColumnInfo(name = "retention_policy_state")
    val retentionPolicyState: Int,
    @ColumnInfo(name = "evidence_policy_state")
    val evidencePolicyState: Int,
    @ColumnInfo(name = "notification_dependency_state")
    val notificationDependencyState: Int,
    @ColumnInfo(name = "document_dependency_state")
    val documentDependencyState: Int,
    @ColumnInfo(name = "dependency_states")
    val dependencyStates: Map<String, Int> = emptyMap(),
    @ColumnInfo(name = "source_contract_version")
    val sourceContractVersion: String,
    @ColumnInfo(name = "offer_readiness_version")
    val offerReadinessVersion: Long,
    @ColumnInfo(name = "deferred_reason")
    val deferredReason: String? = null,
    @ColumnInfo(name = "last_evaluated_at")
    val lastEvaluatedAt: Instant? = null,
    @ColumnInfo(name = "sync_status")
    val syncStatus: SyncStatus = SyncStatus.SYNCED,
)
