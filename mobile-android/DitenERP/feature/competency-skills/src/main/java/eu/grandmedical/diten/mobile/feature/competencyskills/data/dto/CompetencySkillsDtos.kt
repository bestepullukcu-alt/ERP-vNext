package eu.grandmedical.diten.mobile.feature.competencyskills.data.dto

import kotlinx.serialization.Serializable

/**
 * Wire DTOs for the competency-skills endpoints. These match the MEASURED
 * backend contract EXACTLY:
 *  - property names are camelCase (kotlinx.serialization uses the Kotlin name);
 *  - every readiness state is an **Int** because HCM has no
 *    `JsonStringEnumConverter` — the enum is serialized as its integer code.
 *
 * The competency-skills enum encodes `Draft=0, Ready=1, Deferred=2, Blocked=3,
 * NotRequired=4, Archived=5` (Ready/Deferred are swapped vs. applicant-intake,
 * SAME as candidate-pipeline), so the numeric defaults below use `Deferred=2`
 * and `Blocked=3`.
 *
 * The gateway wraps each of these in the shared
 * [NetworkEnvelope][eu.grandmedical.diten.mobile.core.network.NetworkEnvelope].
 */

/**
 * `POST api/competency-skills` body. Facet defaults mirror the backend defaults
 * (Deferred=2 for the dependency/policy facets, Blocked=3 for the
 * assessment-workflow, scoring, rating, calibration, ranking, automated-decision
 * and UX boundaries, Draft=0 top-level readiness); `sourceContractVersion`
 * defaults to "v1" (never `x.y.z`, which the backend rejects).
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend create request.
data class CompetencySkillsCreateRequestDto(
    val code: String,
    val displayName: String,
    val competencySkillsReadinessState: Int = 0,
    val assessmentWorkflowBoundaryState: Int = 3,
    val competencyFrameworkDependencyState: Int = 2,
    val skillTaxonomyDependencyState: Int = 2,
    val skillScoringBoundaryState: Int = 3,
    val ratingBoundaryState: Int = 3,
    val calibrationBoundaryState: Int = 3,
    val rankingBoundaryState: Int = 3,
    val automatedDecisionBoundaryState: Int = 3,
    val managerAssessmentUxBoundaryState: Int = 3,
    val employeeAssessmentUxBoundaryState: Int = 3,
    val documentDependencyState: Int = 2,
    val notificationDependencyState: Int = 2,
    val consentPreconditionState: Int = 2,
    val dataMinimizationState: Int = 2,
    val retentionPolicyState: Int = 2,
    val evidencePolicyState: Int = 2,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val competencySkillsReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
)

/**
 * Full readiness DTO returned by `GET api/competency-skills/{id}` and
 * `POST api/competency-skills/{id}/evaluate`. Adds [id] and the nullable
 * [lastEvaluatedAt] (ISO datetime) on top of the create request's fields.
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend readiness DTO.
data class CompetencySkillsReadinessDto(
    val id: String,
    val code: String,
    val displayName: String,
    val competencySkillsReadinessState: Int = 0,
    val assessmentWorkflowBoundaryState: Int = 3,
    val competencyFrameworkDependencyState: Int = 2,
    val skillTaxonomyDependencyState: Int = 2,
    val skillScoringBoundaryState: Int = 3,
    val ratingBoundaryState: Int = 3,
    val calibrationBoundaryState: Int = 3,
    val rankingBoundaryState: Int = 3,
    val automatedDecisionBoundaryState: Int = 3,
    val managerAssessmentUxBoundaryState: Int = 3,
    val employeeAssessmentUxBoundaryState: Int = 3,
    val documentDependencyState: Int = 2,
    val notificationDependencyState: Int = 2,
    val consentPreconditionState: Int = 2,
    val dataMinimizationState: Int = 2,
    val retentionPolicyState: Int = 2,
    val evidencePolicyState: Int = 2,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val lastEvaluatedAt: String? = null,
    val competencySkillsReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
)

/**
 * Light list item from `GET api/competency-skills`. A projection of the full DTO
 * (the backend omits most facet states from the list payload — it returns only
 * the assessment-workflow, skill-taxonomy, skill-scoring and automated-decision
 * boundaries alongside the top-level readiness).
 */
@Serializable
data class CompetencySkillsReadinessListItemDto(
    val id: String,
    val code: String,
    val displayName: String,
    val competencySkillsReadinessState: Int = 0,
    val assessmentWorkflowBoundaryState: Int = 3,
    val skillTaxonomyDependencyState: Int = 2,
    val skillScoringBoundaryState: Int = 3,
    val automatedDecisionBoundaryState: Int = 3,
    val sourceContractVersion: String = "v1",
    val lastEvaluatedAt: String? = null,
)
