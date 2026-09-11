package eu.grandmedical.diten.mobile.feature.compensationbenefits.data.dto

import kotlinx.serialization.Serializable

/**
 * Wire DTOs for the compensation-benefits endpoints. These match the MEASURED
 * backend contract EXACTLY:
 *  - property names are camelCase (kotlinx.serialization uses the Kotlin name,
 *    matching the backend's default camelCase JSON policy);
 *  - every readiness state is an **Int** because HCM has no
 *    `JsonStringEnumConverter` — the enum is serialized as its integer code.
 *
 * ⚠️ This module's int codes DIFFER from the pilot: `Ready=1, Deferred=2`
 * (Draft=0, Blocked=3, NotRequired=4, Archived=5). So the backend facet defaults
 * encode as: Draft (0) top-level, Blocked (3) boundary facets, Deferred (2)
 * dependency/policy facets.
 *
 * The gateway wraps each of these in the shared
 * [NetworkEnvelope][eu.grandmedical.diten.mobile.core.network.NetworkEnvelope].
 */

/**
 * `POST api/compensation-benefits` body. Facet defaults mirror the backend
 * defaults (Draft=0 top-level, Blocked=3 boundaries, Deferred=2 dependencies);
 * `sourceContractVersion` defaults to "v1" (never `x.y.z`, which the backend rejects).
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend create request.
data class CompensationBenefitsCreateRequestDto(
    val code: String,
    val displayName: String,
    val compensationBenefitsReadinessState: Int = 0,
    val compensationPlanBoundaryState: Int = 3,
    val benefitProgramBoundaryState: Int = 3,
    val payGradeMappingBoundaryState: Int = 3,
    val benefitEnrollmentBoundaryState: Int = 3,
    val compensationReviewBoundaryState: Int = 3,
    val automatedDecisionBoundaryState: Int = 3,
    val compensationSourceDependencyState: Int = 2,
    val benefitProviderSourceDependencyState: Int = 2,
    val documentDependencyState: Int = 2,
    val notificationDependencyState: Int = 2,
    val consentPreconditionState: Int = 2,
    val dataMinimizationState: Int = 2,
    val retentionPolicyState: Int = 2,
    val evidencePolicyState: Int = 2,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val compensationBenefitsReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
)

/**
 * Full readiness DTO returned by `GET api/compensation-benefits/{id}` and
 * `POST api/compensation-benefits/{id}/evaluate`. Adds [id] and the nullable
 * [lastEvaluatedAt] (ISO datetime) on top of the create request's fields.
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend readiness DTO.
data class CompensationBenefitsReadinessDto(
    val id: String,
    val code: String,
    val displayName: String,
    val compensationBenefitsReadinessState: Int = 0,
    val compensationPlanBoundaryState: Int = 3,
    val benefitProgramBoundaryState: Int = 3,
    val payGradeMappingBoundaryState: Int = 3,
    val benefitEnrollmentBoundaryState: Int = 3,
    val compensationReviewBoundaryState: Int = 3,
    val automatedDecisionBoundaryState: Int = 3,
    val compensationSourceDependencyState: Int = 2,
    val benefitProviderSourceDependencyState: Int = 2,
    val documentDependencyState: Int = 2,
    val notificationDependencyState: Int = 2,
    val consentPreconditionState: Int = 2,
    val dataMinimizationState: Int = 2,
    val retentionPolicyState: Int = 2,
    val evidencePolicyState: Int = 2,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val compensationBenefitsReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
    val lastEvaluatedAt: String? = null,
)

/**
 * Light list item from `GET api/compensation-benefits`. A projection of the full
 * DTO (the backend omits most facet states from the list payload — only the
 * top-level state and four representative facets).
 */
@Serializable
data class CompensationBenefitsReadinessListItemDto(
    val id: String,
    val code: String,
    val displayName: String,
    val compensationBenefitsReadinessState: Int = 0,
    val compensationPlanBoundaryState: Int = 3,
    val compensationSourceDependencyState: Int = 2,
    val benefitEnrollmentBoundaryState: Int = 3,
    val automatedDecisionBoundaryState: Int = 3,
    val sourceContractVersion: String = "v1",
    val lastEvaluatedAt: String? = null,
)
