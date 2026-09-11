package eu.grandmedical.diten.mobile.feature.performancereviews.data.dto

import kotlinx.serialization.Serializable

/**
 * Wire DTOs for the performance-reviews endpoints. These match the MEASURED
 * backend contract EXACTLY:
 *  - property names are camelCase (kotlinx.serialization uses the Kotlin name);
 *  - every readiness state is an **Int** because HCM has no
 *    `JsonStringEnumConverter` — the enum is serialized as its integer code, and
 *    the performance-reviews enum codes are
 *    `Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5`.
 *
 * The gateway wraps each of these in the shared
 * [NetworkEnvelope][eu.grandmedical.diten.mobile.core.network.NetworkEnvelope].
 */

/**
 * `POST api/performance-reviews` body. Facet defaults mirror the backend defaults
 * (Draft=0 readiness; Deferred=2 for goal/dependency/policy/consent facets;
 * Blocked=3 for every runtime/UX/compensation boundary); `sourceContractVersion`
 * defaults to "v1" (never `x.y.z`, which the backend rejects).
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend create request.
data class PerformanceReviewsCreateRequestDto(
    val code: String,
    val displayName: String,
    val performanceReviewReadinessState: Int = 0,
    val reviewCycleBoundaryState: Int = 3,
    val goalDependencyState: Int = 2,
    val scoringBoundaryState: Int = 3,
    val ratingBoundaryState: Int = 3,
    val calibrationBoundaryState: Int = 3,
    val rankingBoundaryState: Int = 3,
    val automatedDecisionBoundaryState: Int = 3,
    val managerReviewUxBoundaryState: Int = 3,
    val employeeReviewUxBoundaryState: Int = 3,
    val compensationDataBoundaryState: Int = 3,
    val benefitsDataBoundaryState: Int = 3,
    val payrollDataBoundaryState: Int = 3,
    val documentDependencyState: Int = 2,
    val notificationDependencyState: Int = 2,
    val consentPreconditionState: Int = 2,
    val dataMinimizationState: Int = 2,
    val retentionPolicyState: Int = 2,
    val evidencePolicyState: Int = 2,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val performanceReviewReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
)

/**
 * Full readiness DTO returned by `GET api/performance-reviews/{id}` and
 * `POST api/performance-reviews/{id}/evaluate`. Adds [id] and the nullable
 * [lastEvaluatedAt] (ISO datetime) on top of the create request's fields.
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend readiness DTO.
data class PerformanceReviewsReadinessDto(
    val id: String,
    val code: String,
    val displayName: String,
    val performanceReviewReadinessState: Int = 0,
    val reviewCycleBoundaryState: Int = 3,
    val goalDependencyState: Int = 2,
    val scoringBoundaryState: Int = 3,
    val ratingBoundaryState: Int = 3,
    val calibrationBoundaryState: Int = 3,
    val rankingBoundaryState: Int = 3,
    val automatedDecisionBoundaryState: Int = 3,
    val managerReviewUxBoundaryState: Int = 3,
    val employeeReviewUxBoundaryState: Int = 3,
    val compensationDataBoundaryState: Int = 3,
    val benefitsDataBoundaryState: Int = 3,
    val payrollDataBoundaryState: Int = 3,
    val documentDependencyState: Int = 2,
    val notificationDependencyState: Int = 2,
    val consentPreconditionState: Int = 2,
    val dataMinimizationState: Int = 2,
    val retentionPolicyState: Int = 2,
    val evidencePolicyState: Int = 2,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val lastEvaluatedAt: String? = null,
    val performanceReviewReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
)

/**
 * Light list item from `GET api/performance-reviews`. A projection of the full
 * DTO (the backend omits most facet states from the list payload — it exposes
 * only the top-level readiness plus the review-cycle, scoring and
 * automated-decision boundaries).
 */
@Serializable
data class PerformanceReviewsReadinessListItemDto(
    val id: String,
    val code: String,
    val displayName: String,
    val performanceReviewReadinessState: Int = 0,
    val reviewCycleBoundaryState: Int = 3,
    val scoringBoundaryState: Int = 3,
    val automatedDecisionBoundaryState: Int = 3,
    val sourceContractVersion: String = "v1",
    val lastEvaluatedAt: String? = null,
)
