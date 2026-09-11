package eu.grandmedical.diten.mobile.feature.employeeonboarding.domain

import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import java.time.Instant

/**
 * Full domain model of an employee-onboarding readiness record.
 *
 * Every state is a strongly-typed [ReadinessState] (never a raw int) so the rest
 * of the app is insulated from the backend's integer wire encoding. [syncStatus]
 * is LOCAL-only metadata (not part of the backend contract): it tells the UI
 * whether this record is server-authoritative ([SyncStatus.SYNCED]),
 * optimistic/in-flight ([SyncStatus.PENDING]) or needs attention
 * ([SyncStatus.FAILED]).
 */
@Suppress("LongParameterList") // Faithful mirror of the backend readiness aggregate.
data class EmployeeOnboardingReadiness(
    val id: String,
    val code: String,
    val displayName: String,
    val onboardingReadinessState: ReadinessState,
    val lifecycleBoundaryState: ReadinessState,
    val checklistBoundaryState: ReadinessState,
    val managerActionBoundaryState: ReadinessState,
    val employeeActionBoundaryState: ReadinessState,
    val candidateTransitionBoundaryState: ReadinessState,
    val identityProvisioningBoundaryState: ReadinessState,
    val accessProvisioningBoundaryState: ReadinessState,
    val deviceEquipmentProvisioningBoundaryState: ReadinessState,
    val documentDependencyState: ReadinessState,
    val notificationDependencyState: ReadinessState,
    val consentPreconditionState: ReadinessState,
    val dataMinimizationState: ReadinessState,
    val retentionPolicyState: ReadinessState,
    val evidencePolicyState: ReadinessState,
    val dependencyStates: Map<String, ReadinessState> = emptyMap(),
    val sourceContractVersion: String,
    val onboardingReadinessVersion: Long,
    val deferredReason: String? = null,
    val lastEvaluatedAt: Instant? = null,
    val syncStatus: SyncStatus = SyncStatus.SYNCED,
)

/**
 * A new record to create. Facet defaults mirror the backend's own defaults
 * (Draft onboarding state; Blocked for the workflow/action/provisioning-execution
 * boundaries; Deferred for the candidate-transition, provisioning-dependency,
 * document/notification dependency and policy facets).
 */
@Suppress("LongParameterList") // Create request mirrors the backend's full facet set.
data class NewEmployeeOnboarding(
    val code: String,
    val displayName: String,
    val onboardingReadinessState: ReadinessState = ReadinessState.Draft,
    val lifecycleBoundaryState: ReadinessState = ReadinessState.Blocked,
    val checklistBoundaryState: ReadinessState = ReadinessState.Blocked,
    val managerActionBoundaryState: ReadinessState = ReadinessState.Blocked,
    val employeeActionBoundaryState: ReadinessState = ReadinessState.Blocked,
    val candidateTransitionBoundaryState: ReadinessState = ReadinessState.Deferred,
    val identityProvisioningBoundaryState: ReadinessState = ReadinessState.Deferred,
    val accessProvisioningBoundaryState: ReadinessState = ReadinessState.Deferred,
    val deviceEquipmentProvisioningBoundaryState: ReadinessState = ReadinessState.Deferred,
    val documentDependencyState: ReadinessState = ReadinessState.Deferred,
    val notificationDependencyState: ReadinessState = ReadinessState.Deferred,
    val consentPreconditionState: ReadinessState = ReadinessState.Deferred,
    val dataMinimizationState: ReadinessState = ReadinessState.Deferred,
    val retentionPolicyState: ReadinessState = ReadinessState.Deferred,
    val evidencePolicyState: ReadinessState = ReadinessState.Deferred,
    val dependencyStates: Map<String, ReadinessState> = emptyMap(),
    val sourceContractVersion: String = DEFAULT_SOURCE_CONTRACT_VERSION,
    val onboardingReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
) {
    companion object {
        /**
         * The default source-contract version. Deliberately "v1" and NOT a
         * `x.y.z` string: the backend rejects version strings matching that
         * pattern (a learned marker guard).
         */
        const val DEFAULT_SOURCE_CONTRACT_VERSION = "v1"
    }
}
