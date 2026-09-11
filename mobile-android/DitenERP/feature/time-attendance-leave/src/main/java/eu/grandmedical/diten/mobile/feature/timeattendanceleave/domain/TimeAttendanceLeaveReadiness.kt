package eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain

import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import java.time.Instant

/**
 * Full domain model of a time-attendance-leave readiness record.
 *
 * Every state is a strongly-typed [ReadinessState] (never a raw int) so the rest
 * of the app is insulated from the backend's integer wire encoding. [syncStatus]
 * is LOCAL-only metadata (not part of the backend contract): it tells the UI
 * whether this record is server-authoritative ([SyncStatus.SYNCED]),
 * optimistic/in-flight ([SyncStatus.PENDING]) or needs attention
 * ([SyncStatus.FAILED]).
 */
@Suppress("LongParameterList") // Faithful mirror of the backend readiness aggregate.
data class TimeAttendanceLeaveReadiness(
    val id: String,
    val code: String,
    val displayName: String,
    val readinessState: ReadinessState,
    val timesheetIntakeBoundaryState: ReadinessState,
    val attendanceSyncBoundaryState: ReadinessState,
    val leaveRequestBoundaryState: ReadinessState,
    val leaveBalanceBoundaryState: ReadinessState,
    val scheduleConsumptionBoundaryState: ReadinessState,
    val automatedDecisionBoundaryState: ReadinessState,
    val timeAttendanceSourceDependencyState: ReadinessState,
    val leaveSourceDependencyState: ReadinessState,
    val documentDependencyState: ReadinessState,
    val notificationDependencyState: ReadinessState,
    val consentPreconditionState: ReadinessState,
    val dataMinimizationState: ReadinessState,
    val retentionPolicyState: ReadinessState,
    val evidencePolicyState: ReadinessState,
    val dependencyStates: Map<String, ReadinessState> = emptyMap(),
    val sourceContractVersion: String,
    val readinessVersion: Long,
    val deferredReason: String? = null,
    val lastEvaluatedAt: Instant? = null,
    val syncStatus: SyncStatus = SyncStatus.SYNCED,
)

/**
 * A new record to create. Facet defaults mirror the backend's own defaults
 * (Blocked for the six boundary facets, Deferred for the dependency/policy
 * facets, Draft top-level readiness).
 */
@Suppress("LongParameterList") // Create request mirrors the backend's full facet set.
data class NewTimeAttendanceLeave(
    val code: String,
    val displayName: String,
    val readinessState: ReadinessState = ReadinessState.Draft,
    val timesheetIntakeBoundaryState: ReadinessState = ReadinessState.Blocked,
    val attendanceSyncBoundaryState: ReadinessState = ReadinessState.Blocked,
    val leaveRequestBoundaryState: ReadinessState = ReadinessState.Blocked,
    val leaveBalanceBoundaryState: ReadinessState = ReadinessState.Blocked,
    val scheduleConsumptionBoundaryState: ReadinessState = ReadinessState.Blocked,
    val automatedDecisionBoundaryState: ReadinessState = ReadinessState.Blocked,
    val timeAttendanceSourceDependencyState: ReadinessState = ReadinessState.Deferred,
    val leaveSourceDependencyState: ReadinessState = ReadinessState.Deferred,
    val documentDependencyState: ReadinessState = ReadinessState.Deferred,
    val notificationDependencyState: ReadinessState = ReadinessState.Deferred,
    val consentPreconditionState: ReadinessState = ReadinessState.Deferred,
    val dataMinimizationState: ReadinessState = ReadinessState.Deferred,
    val retentionPolicyState: ReadinessState = ReadinessState.Deferred,
    val evidencePolicyState: ReadinessState = ReadinessState.Deferred,
    val dependencyStates: Map<String, ReadinessState> = emptyMap(),
    val sourceContractVersion: String = DEFAULT_SOURCE_CONTRACT_VERSION,
    val readinessVersion: Long = 1L,
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
