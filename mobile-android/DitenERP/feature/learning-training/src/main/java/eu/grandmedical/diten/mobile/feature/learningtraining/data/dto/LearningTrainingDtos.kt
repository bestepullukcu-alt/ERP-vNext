package eu.grandmedical.diten.mobile.feature.learningtraining.data.dto

import kotlinx.serialization.Serializable

/**
 * Wire DTOs for the learning-training endpoints. These match the MEASURED backend
 * contract EXACTLY:
 *  - property names are camelCase (kotlinx.serialization uses the Kotlin name,
 *    which matches ASP.NET Core's default camelCase JSON);
 *  - every readiness state is an **Int** because HCM has no
 *    `JsonStringEnumConverter` — the enum is serialized as its integer code
 *    (`Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5`).
 *
 * The gateway wraps each of these in the shared
 * [NetworkEnvelope][eu.grandmedical.diten.mobile.core.network.NetworkEnvelope].
 */

/**
 * `POST api/learning-training` body. Facet defaults mirror the backend defaults
 * (Draft=0 top-level state, Blocked=3 for the six behaviour boundaries,
 * Deferred=2 for the dependency/policy facets); `sourceContractVersion` defaults
 * to "v1" (never `x.y.z`, which the backend rejects).
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend create request.
data class LearningTrainingCreateRequestDto(
    val code: String,
    val displayName: String,
    val learningTrainingReadinessState: Int = 0,
    val courseCatalogBoundaryState: Int = 3,
    val enrollmentWorkflowBoundaryState: Int = 3,
    val completionTrackingBoundaryState: Int = 3,
    val certificationBoundaryState: Int = 3,
    val assessmentScoringBoundaryState: Int = 3,
    val automatedDecisionBoundaryState: Int = 3,
    val learningContentDependencyState: Int = 2,
    val skillTaxonomyDependencyState: Int = 2,
    val documentDependencyState: Int = 2,
    val notificationDependencyState: Int = 2,
    val consentPreconditionState: Int = 2,
    val dataMinimizationState: Int = 2,
    val retentionPolicyState: Int = 2,
    val evidencePolicyState: Int = 2,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val learningTrainingReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
)

/**
 * Full readiness DTO returned by `GET api/learning-training/{id}` and
 * `POST api/learning-training/{id}/evaluate`. Adds [id] and the nullable
 * [lastEvaluatedAt] (ISO datetime) on top of the create request's fields.
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend readiness DTO.
data class LearningTrainingReadinessDto(
    val id: String,
    val code: String,
    val displayName: String,
    val learningTrainingReadinessState: Int = 0,
    val courseCatalogBoundaryState: Int = 3,
    val enrollmentWorkflowBoundaryState: Int = 3,
    val completionTrackingBoundaryState: Int = 3,
    val certificationBoundaryState: Int = 3,
    val assessmentScoringBoundaryState: Int = 3,
    val automatedDecisionBoundaryState: Int = 3,
    val learningContentDependencyState: Int = 2,
    val skillTaxonomyDependencyState: Int = 2,
    val documentDependencyState: Int = 2,
    val notificationDependencyState: Int = 2,
    val consentPreconditionState: Int = 2,
    val dataMinimizationState: Int = 2,
    val retentionPolicyState: Int = 2,
    val evidencePolicyState: Int = 2,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val lastEvaluatedAt: String? = null,
    val learningTrainingReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
)

/**
 * Light list item from `GET api/learning-training`. A projection of the full DTO:
 * the backend list payload carries only the top-level state plus the course
 * catalog, skill taxonomy, assessment scoring and automated decision facets.
 */
@Serializable
data class LearningTrainingReadinessListItemDto(
    val id: String,
    val code: String,
    val displayName: String,
    val learningTrainingReadinessState: Int = 0,
    val courseCatalogBoundaryState: Int = 3,
    val skillTaxonomyDependencyState: Int = 2,
    val assessmentScoringBoundaryState: Int = 3,
    val automatedDecisionBoundaryState: Int = 3,
    val sourceContractVersion: String = "v1",
    val lastEvaluatedAt: String? = null,
)
