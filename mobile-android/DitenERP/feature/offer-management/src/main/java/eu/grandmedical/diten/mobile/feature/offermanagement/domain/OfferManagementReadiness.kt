package eu.grandmedical.diten.mobile.feature.offermanagement.domain

import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import java.time.Instant

/**
 * Full domain model of an offer-management readiness record.
 *
 * Every state is a strongly-typed [ReadinessState] (never a raw int) so the rest
 * of the app is insulated from the backend's integer wire encoding. [syncStatus]
 * is LOCAL-only metadata (not part of the backend contract): it tells the UI
 * whether this record is server-authoritative ([SyncStatus.SYNCED]),
 * optimistic/in-flight ([SyncStatus.PENDING]) or needs attention
 * ([SyncStatus.FAILED]).
 */
@Suppress("LongParameterList") // Faithful mirror of the backend readiness aggregate.
data class OfferManagementReadiness(
    val id: String,
    val code: String,
    val displayName: String,
    val offerReadinessState: ReadinessState,
    val offerWorkflowBoundaryState: ReadinessState,
    val approvalWorkflowBoundaryState: ReadinessState,
    val candidateAcceptanceBoundaryState: ReadinessState,
    val offerDocumentBoundaryState: ReadinessState,
    val compensationDataBoundaryState: ReadinessState,
    val benefitsDataBoundaryState: ReadinessState,
    val payrollDataBoundaryState: ReadinessState,
    val consentPreconditionState: ReadinessState,
    val dataMinimizationState: ReadinessState,
    val retentionPolicyState: ReadinessState,
    val evidencePolicyState: ReadinessState,
    val notificationDependencyState: ReadinessState,
    val documentDependencyState: ReadinessState,
    val dependencyStates: Map<String, ReadinessState> = emptyMap(),
    val sourceContractVersion: String,
    val offerReadinessVersion: Long,
    val deferredReason: String? = null,
    val lastEvaluatedAt: Instant? = null,
    val syncStatus: SyncStatus = SyncStatus.SYNCED,
)

/**
 * A new record to create. Facet defaults mirror the backend's own defaults
 * (Draft top-level; Blocked for the offer/approval/candidate/document workflow
 * boundaries; Deferred for the data boundaries, policies and dependencies).
 */
@Suppress("LongParameterList") // Create request mirrors the backend's full facet set.
data class NewOfferManagement(
    val code: String,
    val displayName: String,
    val offerReadinessState: ReadinessState = ReadinessState.Draft,
    val offerWorkflowBoundaryState: ReadinessState = ReadinessState.Blocked,
    val approvalWorkflowBoundaryState: ReadinessState = ReadinessState.Blocked,
    val candidateAcceptanceBoundaryState: ReadinessState = ReadinessState.Blocked,
    val offerDocumentBoundaryState: ReadinessState = ReadinessState.Blocked,
    val compensationDataBoundaryState: ReadinessState = ReadinessState.Deferred,
    val benefitsDataBoundaryState: ReadinessState = ReadinessState.Deferred,
    val payrollDataBoundaryState: ReadinessState = ReadinessState.Deferred,
    val consentPreconditionState: ReadinessState = ReadinessState.Deferred,
    val dataMinimizationState: ReadinessState = ReadinessState.Deferred,
    val retentionPolicyState: ReadinessState = ReadinessState.Deferred,
    val evidencePolicyState: ReadinessState = ReadinessState.Deferred,
    val notificationDependencyState: ReadinessState = ReadinessState.Deferred,
    val documentDependencyState: ReadinessState = ReadinessState.Deferred,
    val dependencyStates: Map<String, ReadinessState> = emptyMap(),
    val sourceContractVersion: String = DEFAULT_SOURCE_CONTRACT_VERSION,
    val offerReadinessVersion: Long = 1L,
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
