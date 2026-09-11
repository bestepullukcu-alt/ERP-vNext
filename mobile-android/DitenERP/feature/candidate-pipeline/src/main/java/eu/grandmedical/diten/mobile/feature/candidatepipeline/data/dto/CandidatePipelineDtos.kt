package eu.grandmedical.diten.mobile.feature.candidatepipeline.data.dto

import kotlinx.serialization.Serializable

/**
 * Wire DTOs for the candidate-pipeline endpoints. These match the MEASURED
 * backend contract EXACTLY:
 *  - property names are camelCase (kotlinx.serialization uses the Kotlin name);
 *  - every readiness state is an **Int** because HCM has no
 *    `JsonStringEnumConverter` — the enum is serialized as its integer code.
 *
 * The candidate-pipeline enum encodes `Draft=0, Ready=1, Deferred=2, Blocked=3,
 * NotRequired=4, Archived=5` (Ready/Deferred are swapped vs. applicant-intake),
 * so the numeric defaults below use `Deferred=2` and `Blocked=3`.
 *
 * The gateway wraps each of these in the shared
 * [NetworkEnvelope][eu.grandmedical.diten.mobile.core.network.NetworkEnvelope].
 */

/**
 * `POST api/candidate-pipeline` body. Facet defaults mirror the backend defaults
 * (Deferred=2 for most, Blocked=3 for the candidate-communication and
 * automated-decision boundaries, Draft=0 pipeline readiness);
 * `sourceContractVersion` defaults to "v1" (never `x.y.z`, which the backend rejects).
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend create request.
data class CandidatePipelineCreateRequestDto(
    val code: String,
    val displayName: String,
    val pipelineReadinessState: Int = 0,
    val pipelineStageGovernanceState: Int = 2,
    val interviewSchedulingReadinessState: Int = 2,
    val interviewerAssignmentReadinessState: Int = 2,
    val evaluationGovernanceState: Int = 2,
    val candidateCommunicationBoundaryState: Int = 3,
    val consentPreconditionState: Int = 2,
    val dataMinimizationState: Int = 2,
    val retentionPolicyState: Int = 2,
    val evidencePolicyState: Int = 2,
    val calendarDependencyState: Int = 2,
    val notificationDependencyState: Int = 2,
    val documentDependencyState: Int = 2,
    val automatedDecisionBoundaryState: Int = 3,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val pipelineReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
)

/**
 * Full readiness DTO returned by `GET api/candidate-pipeline/{id}` and
 * `POST api/candidate-pipeline/{id}/evaluate`. Adds [id] and the nullable
 * [lastEvaluatedAt] (ISO datetime) on top of the create request's fields.
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend readiness DTO.
data class CandidatePipelineReadinessDto(
    val id: String,
    val code: String,
    val displayName: String,
    val pipelineReadinessState: Int = 0,
    val pipelineStageGovernanceState: Int = 2,
    val interviewSchedulingReadinessState: Int = 2,
    val interviewerAssignmentReadinessState: Int = 2,
    val evaluationGovernanceState: Int = 2,
    val candidateCommunicationBoundaryState: Int = 3,
    val consentPreconditionState: Int = 2,
    val dataMinimizationState: Int = 2,
    val retentionPolicyState: Int = 2,
    val evidencePolicyState: Int = 2,
    val calendarDependencyState: Int = 2,
    val notificationDependencyState: Int = 2,
    val documentDependencyState: Int = 2,
    val automatedDecisionBoundaryState: Int = 3,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val lastEvaluatedAt: String? = null,
    val pipelineReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
)

/**
 * Light list item from `GET api/candidate-pipeline`. A projection of the full DTO
 * (the backend omits most facet states from the list payload).
 */
@Serializable
data class CandidatePipelineReadinessListItemDto(
    val id: String,
    val code: String,
    val displayName: String,
    val pipelineReadinessState: Int = 0,
    val pipelineStageGovernanceState: Int = 2,
    val interviewSchedulingReadinessState: Int = 2,
    val interviewerAssignmentReadinessState: Int = 2,
    val evaluationGovernanceState: Int = 2,
    val sourceContractVersion: String = "v1",
    val lastEvaluatedAt: String? = null,
)
