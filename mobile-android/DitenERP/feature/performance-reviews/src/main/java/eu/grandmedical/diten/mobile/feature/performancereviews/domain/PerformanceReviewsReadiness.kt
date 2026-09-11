package eu.grandmedical.diten.mobile.feature.performancereviews.domain

import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import java.time.Instant

/**
 * Full domain model of a performance-review readiness record.
 *
 * Every state is a strongly-typed [ReadinessState] (never a raw int) so the rest
 * of the app is insulated from the backend's integer wire encoding. [syncStatus]
 * is LOCAL-only metadata (not part of the backend contract): it tells the UI
 * whether this record is server-authoritative ([SyncStatus.SYNCED]),
 * optimistic/in-flight ([SyncStatus.PENDING]) or needs attention
 * ([SyncStatus.FAILED]).
 */
@Suppress("LongParameterList") // Faithful mirror of the backend readiness aggregate.
data class PerformanceReviewsReadiness(
    val id: String,
    val code: String,
    val displayName: String,
    val performanceReviewReadinessState: ReadinessState,
    val reviewCycleBoundaryState: ReadinessState,
    val goalDependencyState: ReadinessState,
    val scoringBoundaryState: ReadinessState,
    val ratingBoundaryState: ReadinessState,
    val calibrationBoundaryState: ReadinessState,
    val rankingBoundaryState: ReadinessState,
    val automatedDecisionBoundaryState: ReadinessState,
    val managerReviewUxBoundaryState: ReadinessState,
    val employeeReviewUxBoundaryState: ReadinessState,
    val compensationDataBoundaryState: ReadinessState,
    val benefitsDataBoundaryState: ReadinessState,
    val payrollDataBoundaryState: ReadinessState,
    val documentDependencyState: ReadinessState,
    val notificationDependencyState: ReadinessState,
    val consentPreconditionState: ReadinessState,
    val dataMinimizationState: ReadinessState,
    val retentionPolicyState: ReadinessState,
    val evidencePolicyState: ReadinessState,
    val dependencyStates: Map<String, ReadinessState> = emptyMap(),
    val sourceContractVersion: String,
    val performanceReviewReadinessVersion: Long,
    val deferredReason: String? = null,
    val lastEvaluatedAt: Instant? = null,
    val syncStatus: SyncStatus = SyncStatus.SYNCED,
)

/**
 * A new record to create. Facet defaults mirror the backend's own defaults
 * (Draft top-level readiness; Deferred for the goal/dependency/policy/consent
 * facets; Blocked for every runtime/UX/compensation boundary).
 */
@Suppress("LongParameterList") // Create request mirrors the backend's full facet set.
data class NewPerformanceReviews(
    val code: String,
    val displayName: String,
    val performanceReviewReadinessState: ReadinessState = ReadinessState.Draft,
    val reviewCycleBoundaryState: ReadinessState = ReadinessState.Blocked,
    val goalDependencyState: ReadinessState = ReadinessState.Deferred,
    val scoringBoundaryState: ReadinessState = ReadinessState.Blocked,
    val ratingBoundaryState: ReadinessState = ReadinessState.Blocked,
    val calibrationBoundaryState: ReadinessState = ReadinessState.Blocked,
    val rankingBoundaryState: ReadinessState = ReadinessState.Blocked,
    val automatedDecisionBoundaryState: ReadinessState = ReadinessState.Blocked,
    val managerReviewUxBoundaryState: ReadinessState = ReadinessState.Blocked,
    val employeeReviewUxBoundaryState: ReadinessState = ReadinessState.Blocked,
    val compensationDataBoundaryState: ReadinessState = ReadinessState.Blocked,
    val benefitsDataBoundaryState: ReadinessState = ReadinessState.Blocked,
    val payrollDataBoundaryState: ReadinessState = ReadinessState.Blocked,
    val documentDependencyState: ReadinessState = ReadinessState.Deferred,
    val notificationDependencyState: ReadinessState = ReadinessState.Deferred,
    val consentPreconditionState: ReadinessState = ReadinessState.Deferred,
    val dataMinimizationState: ReadinessState = ReadinessState.Deferred,
    val retentionPolicyState: ReadinessState = ReadinessState.Deferred,
    val evidencePolicyState: ReadinessState = ReadinessState.Deferred,
    val dependencyStates: Map<String, ReadinessState> = emptyMap(),
    val sourceContractVersion: String = DEFAULT_SOURCE_CONTRACT_VERSION,
    val performanceReviewReadinessVersion: Long = 1L,
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
