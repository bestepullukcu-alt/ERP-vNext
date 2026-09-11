package eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.dto

import kotlinx.serialization.Serializable

/**
 * Wire DTOs for the time-attendance-leave endpoints. These match the MEASURED
 * backend contract EXACTLY:
 *  - property names are camelCase (kotlinx.serialization uses the Kotlin name);
 *  - every readiness state is an **Int** because HCM has no
 *    `JsonStringEnumConverter` — the enum is serialized as its integer code.
 *
 * The time-attendance-leave enum encodes `Draft=0, Ready=1, Deferred=2, Blocked=3,
 * NotRequired=4, Archived=5` (Ready/Deferred are swapped vs. applicant-intake,
 * matching candidate-pipeline), so the numeric defaults below use `Deferred=2`
 * and `Blocked=3`.
 *
 * The gateway wraps each of these in the shared
 * [NetworkEnvelope][eu.grandmedical.diten.mobile.core.network.NetworkEnvelope].
 */

/**
 * `POST api/time-attendance-leave` body. Facet defaults mirror the backend
 * defaults (Blocked=3 for the six boundary facets, Deferred=2 for the
 * dependency/policy facets, Draft=0 top-level readiness); `sourceContractVersion`
 * defaults to "v1" (never `x.y.z`, which the backend rejects).
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend create request.
data class TimeAttendanceLeaveCreateRequestDto(
    val code: String,
    val displayName: String,
    val timeAttendanceLeaveReadinessState: Int = 0,
    val timesheetIntakeBoundaryState: Int = 3,
    val attendanceSyncBoundaryState: Int = 3,
    val leaveRequestBoundaryState: Int = 3,
    val leaveBalanceBoundaryState: Int = 3,
    val scheduleConsumptionBoundaryState: Int = 3,
    val automatedDecisionBoundaryState: Int = 3,
    val timeAttendanceSourceDependencyState: Int = 2,
    val leaveSourceDependencyState: Int = 2,
    val documentDependencyState: Int = 2,
    val notificationDependencyState: Int = 2,
    val consentPreconditionState: Int = 2,
    val dataMinimizationState: Int = 2,
    val retentionPolicyState: Int = 2,
    val evidencePolicyState: Int = 2,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val timeAttendanceLeaveReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
)

/**
 * Full readiness DTO returned by `GET api/time-attendance-leave/{id}` and
 * `POST api/time-attendance-leave/{id}/evaluate`. Adds [id] and the nullable
 * [lastEvaluatedAt] (ISO datetime) on top of the create request's fields.
 */
@Serializable
@Suppress("LongParameterList") // Faithful mirror of the backend readiness DTO.
data class TimeAttendanceLeaveReadinessDto(
    val id: String,
    val code: String,
    val displayName: String,
    val timeAttendanceLeaveReadinessState: Int = 0,
    val timesheetIntakeBoundaryState: Int = 3,
    val attendanceSyncBoundaryState: Int = 3,
    val leaveRequestBoundaryState: Int = 3,
    val leaveBalanceBoundaryState: Int = 3,
    val scheduleConsumptionBoundaryState: Int = 3,
    val automatedDecisionBoundaryState: Int = 3,
    val timeAttendanceSourceDependencyState: Int = 2,
    val leaveSourceDependencyState: Int = 2,
    val documentDependencyState: Int = 2,
    val notificationDependencyState: Int = 2,
    val consentPreconditionState: Int = 2,
    val dataMinimizationState: Int = 2,
    val retentionPolicyState: Int = 2,
    val evidencePolicyState: Int = 2,
    val dependencyStates: Map<String, Int> = emptyMap(),
    val sourceContractVersion: String = "v1",
    val lastEvaluatedAt: String? = null,
    val timeAttendanceLeaveReadinessVersion: Long = 1L,
    val deferredReason: String? = null,
)

/**
 * Light list item from `GET api/time-attendance-leave`. A projection of the full
 * DTO (the backend omits most facet states from the list payload — it keeps the
 * top-level readiness, timesheet-intake, time-attendance source dependency, leave
 * balance and automated-decision facets).
 */
@Serializable
data class TimeAttendanceLeaveReadinessListItemDto(
    val id: String,
    val code: String,
    val displayName: String,
    val timeAttendanceLeaveReadinessState: Int = 0,
    val timesheetIntakeBoundaryState: Int = 3,
    val timeAttendanceSourceDependencyState: Int = 2,
    val leaveBalanceBoundaryState: Int = 3,
    val automatedDecisionBoundaryState: Int = 3,
    val sourceContractVersion: String = "v1",
    val lastEvaluatedAt: String? = null,
)
