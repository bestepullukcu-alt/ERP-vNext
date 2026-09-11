package eu.grandmedical.diten.mobile.feature.employeeonboarding.data.local

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
 * [dependencyStates] round-trips through [EmployeeOnboardingConverters]; timestamps
 * through the shared `DitenTypeConverters`.
 */
@Entity(
    tableName = "employee_onboarding",
    indices = [
        Index(value = ["tenant_id", "legal_entity_id"]),
    ],
)
@Suppress("LongParameterList") // Cache row mirrors the full readiness aggregate.
data class EmployeeOnboardingEntity(
    @PrimaryKey
    @ColumnInfo(name = "id")
    val id: String,
    @Embedded
    val scope: ScopeKeys,
    @ColumnInfo(name = "code")
    val code: String,
    @ColumnInfo(name = "display_name")
    val displayName: String,
    @ColumnInfo(name = "onboarding_readiness_state")
    val onboardingReadinessState: Int,
    @ColumnInfo(name = "lifecycle_boundary_state")
    val lifecycleBoundaryState: Int,
    @ColumnInfo(name = "checklist_boundary_state")
    val checklistBoundaryState: Int,
    @ColumnInfo(name = "manager_action_boundary_state")
    val managerActionBoundaryState: Int,
    @ColumnInfo(name = "employee_action_boundary_state")
    val employeeActionBoundaryState: Int,
    @ColumnInfo(name = "candidate_transition_boundary_state")
    val candidateTransitionBoundaryState: Int,
    @ColumnInfo(name = "identity_provisioning_boundary_state")
    val identityProvisioningBoundaryState: Int,
    @ColumnInfo(name = "access_provisioning_boundary_state")
    val accessProvisioningBoundaryState: Int,
    @ColumnInfo(name = "device_equipment_provisioning_boundary_state")
    val deviceEquipmentProvisioningBoundaryState: Int,
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
    @ColumnInfo(name = "onboarding_readiness_version")
    val onboardingReadinessVersion: Long,
    @ColumnInfo(name = "deferred_reason")
    val deferredReason: String? = null,
    @ColumnInfo(name = "last_evaluated_at")
    val lastEvaluatedAt: Instant? = null,
    @ColumnInfo(name = "sync_status")
    val syncStatus: SyncStatus = SyncStatus.SYNCED,
)
