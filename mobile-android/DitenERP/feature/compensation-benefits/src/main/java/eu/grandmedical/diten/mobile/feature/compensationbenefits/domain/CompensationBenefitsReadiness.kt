package eu.grandmedical.diten.mobile.feature.compensationbenefits.domain

import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import java.time.Instant

/**
 * Full domain model of a compensation-and-benefits readiness record.
 *
 * Every state is a strongly-typed [ReadinessState] (never a raw int) so the rest
 * of the app is insulated from the backend's integer wire encoding. [syncStatus]
 * is LOCAL-only metadata (not part of the backend contract): it tells the UI
 * whether this record is server-authoritative ([SyncStatus.SYNCED]),
 * optimistic/in-flight ([SyncStatus.PENDING]) or needs attention
 * ([SyncStatus.FAILED]).
 */
@Suppress("LongParameterList") // Faithful mirror of the backend readiness aggregate.
data class CompensationBenefitsReadiness(
    val id: String,
    val code: String,
    val displayName: String,
    val compensationBenefitsReadinessState: ReadinessState,
    val compensationPlanBoundaryState: ReadinessState,
    val benefitProgramBoundaryState: ReadinessState,
    val payGradeMappingBoundaryState: ReadinessState,
    val benefitEnrollmentBoundaryState: ReadinessState,
    val compensationReviewBoundaryState: ReadinessState,
    val automatedDecisionBoundaryState: ReadinessState,
    val compensationSourceDependencyState: ReadinessState,
    val benefitProviderSourceDependencyState: ReadinessState,
    val documentDependencyState: ReadinessState,
    val notificationDependencyState: ReadinessState,
    val consentPreconditionState: ReadinessState,
    val dataMinimizationState: ReadinessState,
    val retentionPolicyState: ReadinessState,
    val evidencePolicyState: ReadinessState,
    val dependencyStates: Map<String, ReadinessState> = emptyMap(),
    val sourceContractVersion: String,
    val compensationBenefitsReadinessVersion: Long,
    val deferredReason: String? = null,
    val lastEvaluatedAt: Instant? = null,
    val syncStatus: SyncStatus = SyncStatus.SYNCED,
)

/**
 * A new record to create. Facet defaults mirror the backend's own defaults:
 * a Draft top-level state, Blocked boundary facets, and Deferred dependency /
 * policy facets. (In THIS module Deferred=2 and Blocked=3 — see [ReadinessState].)
 */
@Suppress("LongParameterList") // Create request mirrors the backend's full facet set.
data class NewCompensationBenefits(
    val code: String,
    val displayName: String,
    val compensationBenefitsReadinessState: ReadinessState = ReadinessState.Draft,
    val compensationPlanBoundaryState: ReadinessState = ReadinessState.Blocked,
    val benefitProgramBoundaryState: ReadinessState = ReadinessState.Blocked,
    val payGradeMappingBoundaryState: ReadinessState = ReadinessState.Blocked,
    val benefitEnrollmentBoundaryState: ReadinessState = ReadinessState.Blocked,
    val compensationReviewBoundaryState: ReadinessState = ReadinessState.Blocked,
    val automatedDecisionBoundaryState: ReadinessState = ReadinessState.Blocked,
    val compensationSourceDependencyState: ReadinessState = ReadinessState.Deferred,
    val benefitProviderSourceDependencyState: ReadinessState = ReadinessState.Deferred,
    val documentDependencyState: ReadinessState = ReadinessState.Deferred,
    val notificationDependencyState: ReadinessState = ReadinessState.Deferred,
    val consentPreconditionState: ReadinessState = ReadinessState.Deferred,
    val dataMinimizationState: ReadinessState = ReadinessState.Deferred,
    val retentionPolicyState: ReadinessState = ReadinessState.Deferred,
    val evidencePolicyState: ReadinessState = ReadinessState.Deferred,
    val dependencyStates: Map<String, ReadinessState> = emptyMap(),
    val sourceContractVersion: String = DEFAULT_SOURCE_CONTRACT_VERSION,
    val compensationBenefitsReadinessVersion: Long = 1L,
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
