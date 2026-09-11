package eu.grandmedical.diten.mobile.feature.learningtraining.data.local

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
 * [dependencyStates] round-trips through [LearningTrainingConverters]; timestamps
 * through the shared `DitenTypeConverters`.
 */
@Entity(
    tableName = "learning_training",
    indices = [
        Index(value = ["tenant_id", "legal_entity_id"]),
    ],
)
@Suppress("LongParameterList") // Cache row mirrors the full readiness aggregate.
data class LearningTrainingEntity(
    @PrimaryKey
    @ColumnInfo(name = "id")
    val id: String,
    @Embedded
    val scope: ScopeKeys,
    @ColumnInfo(name = "code")
    val code: String,
    @ColumnInfo(name = "display_name")
    val displayName: String,
    @ColumnInfo(name = "learning_training_readiness_state")
    val learningTrainingReadinessState: Int,
    @ColumnInfo(name = "course_catalog_boundary_state")
    val courseCatalogBoundaryState: Int,
    @ColumnInfo(name = "enrollment_workflow_boundary_state")
    val enrollmentWorkflowBoundaryState: Int,
    @ColumnInfo(name = "completion_tracking_boundary_state")
    val completionTrackingBoundaryState: Int,
    @ColumnInfo(name = "certification_boundary_state")
    val certificationBoundaryState: Int,
    @ColumnInfo(name = "assessment_scoring_boundary_state")
    val assessmentScoringBoundaryState: Int,
    @ColumnInfo(name = "automated_decision_boundary_state")
    val automatedDecisionBoundaryState: Int,
    @ColumnInfo(name = "learning_content_dependency_state")
    val learningContentDependencyState: Int,
    @ColumnInfo(name = "skill_taxonomy_dependency_state")
    val skillTaxonomyDependencyState: Int,
    @ColumnInfo(name = "document_dependency_state")
    val documentDependencyState: Int,
    @ColumnInfo(name = "notification_dependency_state")
    val notificationDependencyState: Int,
    @ColumnInfo(name = "consent_precondition_state")
    val consentPreconditionState: Int,
    @ColumnInfo(name = "data_minimization_state")
    val dataMinimizationState: Int,
    @ColumnInfo(name = "retention_policy_state")
    val retentionPolicyState: Int,
    @ColumnInfo(name = "evidence_policy_state")
    val evidencePolicyState: Int,
    @ColumnInfo(name = "dependency_states")
    val dependencyStates: Map<String, Int> = emptyMap(),
    @ColumnInfo(name = "source_contract_version")
    val sourceContractVersion: String,
    @ColumnInfo(name = "learning_training_readiness_version")
    val learningTrainingReadinessVersion: Long,
    @ColumnInfo(name = "deferred_reason")
    val deferredReason: String? = null,
    @ColumnInfo(name = "last_evaluated_at")
    val lastEvaluatedAt: Instant? = null,
    @ColumnInfo(name = "sync_status")
    val syncStatus: SyncStatus = SyncStatus.SYNCED,
)
