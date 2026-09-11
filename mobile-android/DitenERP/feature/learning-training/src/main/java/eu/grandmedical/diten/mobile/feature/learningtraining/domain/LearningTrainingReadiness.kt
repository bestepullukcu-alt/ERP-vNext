package eu.grandmedical.diten.mobile.feature.learningtraining.domain

import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import java.time.Instant

/**
 * Full domain model of a learning-training readiness record.
 *
 * Every state is a strongly-typed [ReadinessState] (never a raw int) so the rest
 * of the app is insulated from the backend's integer wire encoding. [syncStatus]
 * is LOCAL-only metadata (not part of the backend contract): it tells the UI
 * whether this record is server-authoritative ([SyncStatus.SYNCED]),
 * optimistic/in-flight ([SyncStatus.PENDING]) or needs attention
 * ([SyncStatus.FAILED]).
 */
@Suppress("LongParameterList") // Faithful mirror of the backend readiness aggregate.
data class LearningTrainingReadiness(
    val id: String,
    val code: String,
    val displayName: String,
    val learningTrainingReadinessState: ReadinessState,
    val courseCatalogBoundaryState: ReadinessState,
    val enrollmentWorkflowBoundaryState: ReadinessState,
    val completionTrackingBoundaryState: ReadinessState,
    val certificationBoundaryState: ReadinessState,
    val assessmentScoringBoundaryState: ReadinessState,
    val automatedDecisionBoundaryState: ReadinessState,
    val learningContentDependencyState: ReadinessState,
    val skillTaxonomyDependencyState: ReadinessState,
    val documentDependencyState: ReadinessState,
    val notificationDependencyState: ReadinessState,
    val consentPreconditionState: ReadinessState,
    val dataMinimizationState: ReadinessState,
    val retentionPolicyState: ReadinessState,
    val evidencePolicyState: ReadinessState,
    val dependencyStates: Map<String, ReadinessState> = emptyMap(),
    val sourceContractVersion: String,
    val learningTrainingReadinessVersion: Long,
    val deferredReason: String? = null,
    val lastEvaluatedAt: Instant? = null,
    val syncStatus: SyncStatus = SyncStatus.SYNCED,
)

/**
 * A new record to create. Facet defaults mirror the backend's own defaults
 * (Draft top-level state, Blocked for the six behaviour boundaries, Deferred for
 * the dependency/policy facets).
 */
@Suppress("LongParameterList") // Create request mirrors the backend's full facet set.
data class NewLearningTraining(
    val code: String,
    val displayName: String,
    val learningTrainingReadinessState: ReadinessState = ReadinessState.Draft,
    val courseCatalogBoundaryState: ReadinessState = ReadinessState.Blocked,
    val enrollmentWorkflowBoundaryState: ReadinessState = ReadinessState.Blocked,
    val completionTrackingBoundaryState: ReadinessState = ReadinessState.Blocked,
    val certificationBoundaryState: ReadinessState = ReadinessState.Blocked,
    val assessmentScoringBoundaryState: ReadinessState = ReadinessState.Blocked,
    val automatedDecisionBoundaryState: ReadinessState = ReadinessState.Blocked,
    val learningContentDependencyState: ReadinessState = ReadinessState.Deferred,
    val skillTaxonomyDependencyState: ReadinessState = ReadinessState.Deferred,
    val documentDependencyState: ReadinessState = ReadinessState.Deferred,
    val notificationDependencyState: ReadinessState = ReadinessState.Deferred,
    val consentPreconditionState: ReadinessState = ReadinessState.Deferred,
    val dataMinimizationState: ReadinessState = ReadinessState.Deferred,
    val retentionPolicyState: ReadinessState = ReadinessState.Deferred,
    val evidencePolicyState: ReadinessState = ReadinessState.Deferred,
    val dependencyStates: Map<String, ReadinessState> = emptyMap(),
    val sourceContractVersion: String = DEFAULT_SOURCE_CONTRACT_VERSION,
    val learningTrainingReadinessVersion: Long = 1L,
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
