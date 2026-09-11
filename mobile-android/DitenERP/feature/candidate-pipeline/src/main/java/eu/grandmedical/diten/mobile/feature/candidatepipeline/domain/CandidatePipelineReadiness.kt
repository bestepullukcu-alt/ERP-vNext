package eu.grandmedical.diten.mobile.feature.candidatepipeline.domain

import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import java.time.Instant

/**
 * Full domain model of a candidate-pipeline readiness record.
 *
 * Every state is a strongly-typed [ReadinessState] (never a raw int) so the rest
 * of the app is insulated from the backend's integer wire encoding. [syncStatus]
 * is LOCAL-only metadata (not part of the backend contract): it tells the UI
 * whether this record is server-authoritative ([SyncStatus.SYNCED]),
 * optimistic/in-flight ([SyncStatus.PENDING]) or needs attention
 * ([SyncStatus.FAILED]).
 */
@Suppress("LongParameterList") // Faithful mirror of the backend readiness aggregate.
data class CandidatePipelineReadiness(
    val id: String,
    val code: String,
    val displayName: String,
    val pipelineReadinessState: ReadinessState,
    val pipelineStageGovernanceState: ReadinessState,
    val interviewSchedulingReadinessState: ReadinessState,
    val interviewerAssignmentReadinessState: ReadinessState,
    val evaluationGovernanceState: ReadinessState,
    val candidateCommunicationBoundaryState: ReadinessState,
    val consentPreconditionState: ReadinessState,
    val dataMinimizationState: ReadinessState,
    val retentionPolicyState: ReadinessState,
    val evidencePolicyState: ReadinessState,
    val calendarDependencyState: ReadinessState,
    val notificationDependencyState: ReadinessState,
    val documentDependencyState: ReadinessState,
    val automatedDecisionBoundaryState: ReadinessState,
    val dependencyStates: Map<String, ReadinessState> = emptyMap(),
    val sourceContractVersion: String,
    val pipelineReadinessVersion: Long,
    val deferredReason: String? = null,
    val lastEvaluatedAt: Instant? = null,
    val syncStatus: SyncStatus = SyncStatus.SYNCED,
)

/**
 * A new record to create. Facet defaults mirror the backend's own defaults
 * (Deferred for most facets, Blocked for the candidate-communication and
 * automated-decision boundaries, Draft pipeline readiness).
 */
@Suppress("LongParameterList") // Create request mirrors the backend's full facet set.
data class NewCandidatePipeline(
    val code: String,
    val displayName: String,
    val pipelineReadinessState: ReadinessState = ReadinessState.Draft,
    val pipelineStageGovernanceState: ReadinessState = ReadinessState.Deferred,
    val interviewSchedulingReadinessState: ReadinessState = ReadinessState.Deferred,
    val interviewerAssignmentReadinessState: ReadinessState = ReadinessState.Deferred,
    val evaluationGovernanceState: ReadinessState = ReadinessState.Deferred,
    val candidateCommunicationBoundaryState: ReadinessState = ReadinessState.Blocked,
    val consentPreconditionState: ReadinessState = ReadinessState.Deferred,
    val dataMinimizationState: ReadinessState = ReadinessState.Deferred,
    val retentionPolicyState: ReadinessState = ReadinessState.Deferred,
    val evidencePolicyState: ReadinessState = ReadinessState.Deferred,
    val calendarDependencyState: ReadinessState = ReadinessState.Deferred,
    val notificationDependencyState: ReadinessState = ReadinessState.Deferred,
    val documentDependencyState: ReadinessState = ReadinessState.Deferred,
    val automatedDecisionBoundaryState: ReadinessState = ReadinessState.Blocked,
    val dependencyStates: Map<String, ReadinessState> = emptyMap(),
    val sourceContractVersion: String = DEFAULT_SOURCE_CONTRACT_VERSION,
    val pipelineReadinessVersion: Long = 1L,
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
