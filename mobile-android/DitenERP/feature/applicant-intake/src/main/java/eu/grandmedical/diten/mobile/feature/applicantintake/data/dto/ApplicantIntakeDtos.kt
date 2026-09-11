package eu.grandmedical.diten.mobile.feature.applicantintake.data.dto

import kotlinx.serialization.Serializable

/**
 * Wire DTOs for the applicant-intake endpoints. These match the MEASURED backend
 * contract EXACTLY:
 *  - property names are camelCase (kotlinx.serialization uses the Kotlin name);
 *  - every readiness state is an **Int** because HCM has no
 *    `JsonStringEnumConverter` — the enum is serialized as its integer code.
 *
 * The gateway wraps each of these in the shared
 * [NetworkEnvelope][eu.grandmedical.diten.mobile.core.network.NetworkEnvelope].
 */

/**
 * `POST api/applicant-intake` body. Facet defaults mirror the backend defaults
 * (Deferred=1 for most, Blocked=3 for public UX, Draft=0 intake); `sourceContractVersion`
 * defaults to "v1" (never `x.y.z`, which the backend rejects).
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend create request.
data class ApplicantIntakeCreateRequestDto(
    val code: String,
    val displayName: String,
    val intakeState: Int = 0,
    val sourceChannelState: Int = 1,
    val consentPreconditionState: Int = 1,
    val dataMinimizationState: Int = 1,
    val duplicateHandlingState: Int = 1,
    val retentionPolicyState: Int = 1,
    val evidencePolicyState: Int = 1,
    val applicantIdentityBoundaryState: Int = 1,
    val publicUxBoundaryState: Int = 3,
    val documentDependencyState: Int = 1,
    val notificationDependencyState: Int = 1,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val applicantIntakeVersion: Long = 1L,
    val deferredReason: String? = null,
)

/**
 * Full readiness DTO returned by `GET api/applicant-intake/{id}` and
 * `POST api/applicant-intake/{id}/evaluate`. Adds [id] and the nullable
 * [lastEvaluatedAt] (ISO datetime) on top of the create request's fields.
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend readiness DTO.
data class ApplicantIntakeReadinessDto(
    val id: String,
    val code: String,
    val displayName: String,
    val intakeState: Int = 0,
    val sourceChannelState: Int = 1,
    val consentPreconditionState: Int = 1,
    val dataMinimizationState: Int = 1,
    val duplicateHandlingState: Int = 1,
    val retentionPolicyState: Int = 1,
    val evidencePolicyState: Int = 1,
    val applicantIdentityBoundaryState: Int = 1,
    val publicUxBoundaryState: Int = 3,
    val documentDependencyState: Int = 1,
    val notificationDependencyState: Int = 1,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val applicantIntakeVersion: Long = 1L,
    val deferredReason: String? = null,
    val lastEvaluatedAt: String? = null,
)

/**
 * Light list item from `GET api/applicant-intake`. A projection of the full DTO
 * (the backend omits most facet states from the list payload).
 */
@Serializable
data class ApplicantIntakeReadinessListItemDto(
    val id: String,
    val code: String,
    val displayName: String,
    val intakeState: Int = 0,
    val sourceChannelState: Int = 1,
    val consentPreconditionState: Int = 1,
    val dataMinimizationState: Int = 1,
    val duplicateHandlingState: Int = 1,
    val sourceContractVersion: String = "v1",
    val lastEvaluatedAt: String? = null,
)
