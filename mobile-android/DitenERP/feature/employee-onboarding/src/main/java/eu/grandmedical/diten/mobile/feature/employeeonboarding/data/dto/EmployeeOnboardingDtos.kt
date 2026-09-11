package eu.grandmedical.diten.mobile.feature.employeeonboarding.data.dto

import kotlinx.serialization.Serializable

/**
 * Wire DTOs for the employee-onboarding endpoints. These match the MEASURED
 * backend contract EXACTLY:
 *  - property names are camelCase (kotlinx.serialization uses the Kotlin name);
 *  - every readiness state is an **Int** because HCM has no
 *    `JsonStringEnumConverter` — the enum is serialized as its integer code
 *    (`Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5`).
 *
 * The gateway wraps each of these in the shared
 * [NetworkEnvelope][eu.grandmedical.diten.mobile.core.network.NetworkEnvelope].
 */

/**
 * `POST api/employee-onboarding` body. Facet defaults mirror the backend defaults
 * (Draft=0 onboarding state; Blocked=3 for the workflow/action/execution boundaries;
 * Deferred=2 for the remaining boundary, dependency and policy facets);
 * `sourceContractVersion` defaults to "v1" (never `x.y.z`, which the backend rejects).
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend create request.
data class EmployeeOnboardingCreateRequestDto(
    val code: String,
    val displayName: String,
    val onboardingReadinessState: Int = 0,
    val lifecycleBoundaryState: Int = 3,
    val checklistBoundaryState: Int = 3,
    val managerActionBoundaryState: Int = 3,
    val employeeActionBoundaryState: Int = 3,
    val candidateTransitionBoundaryState: Int = 2,
    val identityProvisioningBoundaryState: Int = 2,
    val accessProvisioningBoundaryState: Int = 2,
    val deviceEquipmentProvisioningBoundaryState: Int = 2,
    val documentDependencyState: Int = 2,
    val notificationDependencyState: Int = 2,
    val consentPreconditionState: Int = 2,
    val dataMinimizationState: Int = 2,
    val retentionPolicyState: Int = 2,
    val evidencePolicyState: Int = 2,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val onboardingReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
)

/**
 * Full readiness DTO returned by `GET api/employee-onboarding/{id}` and
 * `POST api/employee-onboarding/{id}/evaluate`. Adds [id] and the nullable
 * [lastEvaluatedAt] (ISO datetime) on top of the create request's fields.
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend readiness DTO.
data class EmployeeOnboardingReadinessDto(
    val id: String,
    val code: String,
    val displayName: String,
    val onboardingReadinessState: Int = 0,
    val lifecycleBoundaryState: Int = 3,
    val checklistBoundaryState: Int = 3,
    val managerActionBoundaryState: Int = 3,
    val employeeActionBoundaryState: Int = 3,
    val candidateTransitionBoundaryState: Int = 2,
    val identityProvisioningBoundaryState: Int = 2,
    val accessProvisioningBoundaryState: Int = 2,
    val deviceEquipmentProvisioningBoundaryState: Int = 2,
    val documentDependencyState: Int = 2,
    val notificationDependencyState: Int = 2,
    val consentPreconditionState: Int = 2,
    val dataMinimizationState: Int = 2,
    val retentionPolicyState: Int = 2,
    val evidencePolicyState: Int = 2,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val onboardingReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
    val lastEvaluatedAt: String? = null,
)

/**
 * Light list item from `GET api/employee-onboarding`. A projection of the full DTO
 * (the backend omits most facet states from the list payload — it carries only the
 * lifecycle / checklist / manager-action boundaries alongside the onboarding state).
 */
@Serializable
data class EmployeeOnboardingReadinessListItemDto(
    val id: String,
    val code: String,
    val displayName: String,
    val onboardingReadinessState: Int = 0,
    val lifecycleBoundaryState: Int = 3,
    val checklistBoundaryState: Int = 3,
    val managerActionBoundaryState: Int = 3,
    val sourceContractVersion: String = "v1",
    val lastEvaluatedAt: String? = null,
)
