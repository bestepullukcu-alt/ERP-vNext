package eu.grandmedical.diten.mobile.feature.offermanagement.data.dto

import kotlinx.serialization.Serializable

/**
 * Wire DTOs for the offer-management endpoints. These match the MEASURED backend
 * contract EXACTLY:
 *  - property names are camelCase (kotlinx.serialization uses the Kotlin name);
 *  - every readiness state is an **Int** because HCM has no
 *    `JsonStringEnumConverter` — the enum is serialized as its integer code
 *    (`Draft=0, Ready=1, Deferred=2, Blocked=3, NotRequired=4, Archived=5`).
 *
 * The gateway wraps each of these in the shared
 * [NetworkEnvelope][eu.grandmedical.diten.mobile.core.network.NetworkEnvelope].
 */

/**
 * `POST api/offer-management` body. Facet defaults mirror the backend defaults
 * (Draft=0 top-level; Blocked=3 for the offer/approval/candidate/document
 * workflow boundaries; Deferred=2 for the data boundaries, policies and
 * dependencies); `sourceContractVersion` defaults to "v1" (never `x.y.z`, which
 * the backend rejects).
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend create request.
data class OfferManagementCreateRequestDto(
    val code: String,
    val displayName: String,
    val offerReadinessState: Int = 0,
    val offerWorkflowBoundaryState: Int = 3,
    val approvalWorkflowBoundaryState: Int = 3,
    val candidateAcceptanceBoundaryState: Int = 3,
    val offerDocumentBoundaryState: Int = 3,
    val compensationDataBoundaryState: Int = 2,
    val benefitsDataBoundaryState: Int = 2,
    val payrollDataBoundaryState: Int = 2,
    val consentPreconditionState: Int = 2,
    val dataMinimizationState: Int = 2,
    val retentionPolicyState: Int = 2,
    val evidencePolicyState: Int = 2,
    val notificationDependencyState: Int = 2,
    val documentDependencyState: Int = 2,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val offerReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
)

/**
 * Full readiness DTO returned by `GET api/offer-management/{id}` and
 * `POST api/offer-management/{id}/evaluate`. Adds [id] and the nullable
 * [lastEvaluatedAt] (ISO datetime) on top of the create request's fields.
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend readiness DTO.
data class OfferManagementReadinessDto(
    val id: String,
    val code: String,
    val displayName: String,
    val offerReadinessState: Int = 0,
    val offerWorkflowBoundaryState: Int = 3,
    val approvalWorkflowBoundaryState: Int = 3,
    val candidateAcceptanceBoundaryState: Int = 3,
    val offerDocumentBoundaryState: Int = 3,
    val compensationDataBoundaryState: Int = 2,
    val benefitsDataBoundaryState: Int = 2,
    val payrollDataBoundaryState: Int = 2,
    val consentPreconditionState: Int = 2,
    val dataMinimizationState: Int = 2,
    val retentionPolicyState: Int = 2,
    val evidencePolicyState: Int = 2,
    val notificationDependencyState: Int = 2,
    val documentDependencyState: Int = 2,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val lastEvaluatedAt: String? = null,
    val offerReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
)

/**
 * Light list item from `GET api/offer-management`. A projection of the full DTO
 * (the backend omits most facet states from the list payload — it keeps the
 * top-level state plus the offer/approval/candidate workflow boundaries).
 */
@Serializable
data class OfferManagementReadinessListItemDto(
    val id: String,
    val code: String,
    val displayName: String,
    val offerReadinessState: Int = 0,
    val offerWorkflowBoundaryState: Int = 3,
    val approvalWorkflowBoundaryState: Int = 3,
    val candidateAcceptanceBoundaryState: Int = 3,
    val sourceContractVersion: String = "v1",
    val lastEvaluatedAt: String? = null,
)
