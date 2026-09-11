package eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.mapper

import eu.grandmedical.diten.mobile.core.database.ScopeKeys
import eu.grandmedical.diten.mobile.core.database.entity.SyncStatus
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.dto.TimeAttendanceLeaveCreateRequestDto
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.dto.TimeAttendanceLeaveReadinessDto
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.dto.TimeAttendanceLeaveReadinessListItemDto
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.local.TimeAttendanceLeaveEntity
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.NewTimeAttendanceLeave
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.ReadinessState
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.TimeAttendanceLeaveListItem
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.TimeAttendanceLeaveReadiness
import java.time.Instant

/**
 * Pure mappers across the three representations (DTO / entity / domain), and the
 * one place the Int <-> [ReadinessState] wire encoding is applied. No side
 * effects, so they are trivially unit-testable on the JVM.
 */

/** Parses an ISO-8601 datetime; a null/blank/malformed value yields null (never throws). */
internal fun parseInstant(value: String?): Instant? =
    value?.takeIf { it.isNotBlank() }?.let { runCatching { Instant.parse(it) }.getOrNull() }

// --- DTO -> domain ------------------------------------------------------------

fun TimeAttendanceLeaveReadinessDto.toDomain(syncStatus: SyncStatus = SyncStatus.SYNCED): TimeAttendanceLeaveReadiness =
    TimeAttendanceLeaveReadiness(
        id = id,
        code = code,
        displayName = displayName,
        readinessState = ReadinessState.fromCode(timeAttendanceLeaveReadinessState),
        timesheetIntakeBoundaryState = ReadinessState.fromCode(timesheetIntakeBoundaryState),
        attendanceSyncBoundaryState = ReadinessState.fromCode(attendanceSyncBoundaryState),
        leaveRequestBoundaryState = ReadinessState.fromCode(leaveRequestBoundaryState),
        leaveBalanceBoundaryState = ReadinessState.fromCode(leaveBalanceBoundaryState),
        scheduleConsumptionBoundaryState = ReadinessState.fromCode(scheduleConsumptionBoundaryState),
        automatedDecisionBoundaryState = ReadinessState.fromCode(automatedDecisionBoundaryState),
        timeAttendanceSourceDependencyState = ReadinessState.fromCode(timeAttendanceSourceDependencyState),
        leaveSourceDependencyState = ReadinessState.fromCode(leaveSourceDependencyState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        readinessVersion = timeAttendanceLeaveReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = syncStatus,
    )

// --- domain (new) -> create request DTO ---------------------------------------

fun NewTimeAttendanceLeave.toCreateRequest(): TimeAttendanceLeaveCreateRequestDto =
    TimeAttendanceLeaveCreateRequestDto(
        code = code,
        displayName = displayName,
        timeAttendanceLeaveReadinessState = readinessState.code,
        timesheetIntakeBoundaryState = timesheetIntakeBoundaryState.code,
        attendanceSyncBoundaryState = attendanceSyncBoundaryState.code,
        leaveRequestBoundaryState = leaveRequestBoundaryState.code,
        leaveBalanceBoundaryState = leaveBalanceBoundaryState.code,
        scheduleConsumptionBoundaryState = scheduleConsumptionBoundaryState.code,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState.code,
        timeAttendanceSourceDependencyState = timeAttendanceSourceDependencyState.code,
        leaveSourceDependencyState = leaveSourceDependencyState.code,
        documentDependencyState = documentDependencyState.code,
        notificationDependencyState = notificationDependencyState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        timeAttendanceLeaveReadinessVersion = readinessVersion,
        deferredReason = deferredReason,
    )

// --- domain (new) -> entity (local PENDING write) -----------------------------

@Suppress("LongParameterList")
fun NewTimeAttendanceLeave.toEntity(
    id: String,
    scope: ScopeKeys,
    syncStatus: SyncStatus = SyncStatus.PENDING,
): TimeAttendanceLeaveEntity =
    TimeAttendanceLeaveEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        readinessState = readinessState.code,
        timesheetIntakeBoundaryState = timesheetIntakeBoundaryState.code,
        attendanceSyncBoundaryState = attendanceSyncBoundaryState.code,
        leaveRequestBoundaryState = leaveRequestBoundaryState.code,
        leaveBalanceBoundaryState = leaveBalanceBoundaryState.code,
        scheduleConsumptionBoundaryState = scheduleConsumptionBoundaryState.code,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState.code,
        timeAttendanceSourceDependencyState = timeAttendanceSourceDependencyState.code,
        leaveSourceDependencyState = leaveSourceDependencyState.code,
        documentDependencyState = documentDependencyState.code,
        notificationDependencyState = notificationDependencyState.code,
        consentPreconditionState = consentPreconditionState.code,
        dataMinimizationState = dataMinimizationState.code,
        retentionPolicyState = retentionPolicyState.code,
        evidencePolicyState = evidencePolicyState.code,
        dependencyStates = dependencyStates.mapValues { it.value.code },
        sourceContractVersion = sourceContractVersion,
        readinessVersion = readinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = null,
        syncStatus = syncStatus,
    )

/** Entity (a PENDING local row) -> create request, for the sync push. */
fun TimeAttendanceLeaveEntity.toCreateRequest(): TimeAttendanceLeaveCreateRequestDto =
    TimeAttendanceLeaveCreateRequestDto(
        code = code,
        displayName = displayName,
        timeAttendanceLeaveReadinessState = readinessState,
        timesheetIntakeBoundaryState = timesheetIntakeBoundaryState,
        attendanceSyncBoundaryState = attendanceSyncBoundaryState,
        leaveRequestBoundaryState = leaveRequestBoundaryState,
        leaveBalanceBoundaryState = leaveBalanceBoundaryState,
        scheduleConsumptionBoundaryState = scheduleConsumptionBoundaryState,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        timeAttendanceSourceDependencyState = timeAttendanceSourceDependencyState,
        leaveSourceDependencyState = leaveSourceDependencyState,
        documentDependencyState = documentDependencyState,
        notificationDependencyState = notificationDependencyState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        timeAttendanceLeaveReadinessVersion = readinessVersion,
        deferredReason = deferredReason,
    )

// --- DTO -> entity (server row cached as SYNCED) ------------------------------

fun TimeAttendanceLeaveReadinessDto.toEntity(scope: ScopeKeys): TimeAttendanceLeaveEntity =
    TimeAttendanceLeaveEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        readinessState = timeAttendanceLeaveReadinessState,
        timesheetIntakeBoundaryState = timesheetIntakeBoundaryState,
        attendanceSyncBoundaryState = attendanceSyncBoundaryState,
        leaveRequestBoundaryState = leaveRequestBoundaryState,
        leaveBalanceBoundaryState = leaveBalanceBoundaryState,
        scheduleConsumptionBoundaryState = scheduleConsumptionBoundaryState,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        timeAttendanceSourceDependencyState = timeAttendanceSourceDependencyState,
        leaveSourceDependencyState = leaveSourceDependencyState,
        documentDependencyState = documentDependencyState,
        notificationDependencyState = notificationDependencyState,
        consentPreconditionState = consentPreconditionState,
        dataMinimizationState = dataMinimizationState,
        retentionPolicyState = retentionPolicyState,
        evidencePolicyState = evidencePolicyState,
        dependencyStates = dependencyStates,
        sourceContractVersion = sourceContractVersion,
        readinessVersion = timeAttendanceLeaveReadinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

/**
 * List item DTO -> entity. The list payload omits most facet states, so those
 * columns are seeded with backend defaults; a later detail fetch/evaluate fills
 * in the authoritative values. The list payload DOES carry the top-level
 * readiness plus the timesheet-intake, time-attendance source dependency, leave
 * balance and automated-decision facets.
 */
fun TimeAttendanceLeaveReadinessListItemDto.toEntity(scope: ScopeKeys): TimeAttendanceLeaveEntity =
    TimeAttendanceLeaveEntity(
        id = id,
        scope = scope,
        code = code,
        displayName = displayName,
        readinessState = timeAttendanceLeaveReadinessState,
        timesheetIntakeBoundaryState = timesheetIntakeBoundaryState,
        attendanceSyncBoundaryState = ReadinessState.Blocked.code,
        leaveRequestBoundaryState = ReadinessState.Blocked.code,
        leaveBalanceBoundaryState = leaveBalanceBoundaryState,
        scheduleConsumptionBoundaryState = ReadinessState.Blocked.code,
        automatedDecisionBoundaryState = automatedDecisionBoundaryState,
        timeAttendanceSourceDependencyState = timeAttendanceSourceDependencyState,
        leaveSourceDependencyState = ReadinessState.Deferred.code,
        documentDependencyState = ReadinessState.Deferred.code,
        notificationDependencyState = ReadinessState.Deferred.code,
        consentPreconditionState = ReadinessState.Deferred.code,
        dataMinimizationState = ReadinessState.Deferred.code,
        retentionPolicyState = ReadinessState.Deferred.code,
        evidencePolicyState = ReadinessState.Deferred.code,
        dependencyStates = emptyMap(),
        sourceContractVersion = sourceContractVersion,
        readinessVersion = 1L,
        deferredReason = null,
        lastEvaluatedAt = parseInstant(lastEvaluatedAt),
        syncStatus = SyncStatus.SYNCED,
    )

// --- entity -> domain ---------------------------------------------------------

fun TimeAttendanceLeaveEntity.toDomain(): TimeAttendanceLeaveReadiness =
    TimeAttendanceLeaveReadiness(
        id = id,
        code = code,
        displayName = displayName,
        readinessState = ReadinessState.fromCode(readinessState),
        timesheetIntakeBoundaryState = ReadinessState.fromCode(timesheetIntakeBoundaryState),
        attendanceSyncBoundaryState = ReadinessState.fromCode(attendanceSyncBoundaryState),
        leaveRequestBoundaryState = ReadinessState.fromCode(leaveRequestBoundaryState),
        leaveBalanceBoundaryState = ReadinessState.fromCode(leaveBalanceBoundaryState),
        scheduleConsumptionBoundaryState = ReadinessState.fromCode(scheduleConsumptionBoundaryState),
        automatedDecisionBoundaryState = ReadinessState.fromCode(automatedDecisionBoundaryState),
        timeAttendanceSourceDependencyState = ReadinessState.fromCode(timeAttendanceSourceDependencyState),
        leaveSourceDependencyState = ReadinessState.fromCode(leaveSourceDependencyState),
        documentDependencyState = ReadinessState.fromCode(documentDependencyState),
        notificationDependencyState = ReadinessState.fromCode(notificationDependencyState),
        consentPreconditionState = ReadinessState.fromCode(consentPreconditionState),
        dataMinimizationState = ReadinessState.fromCode(dataMinimizationState),
        retentionPolicyState = ReadinessState.fromCode(retentionPolicyState),
        evidencePolicyState = ReadinessState.fromCode(evidencePolicyState),
        dependencyStates = dependencyStates.mapValues { ReadinessState.fromCode(it.value) },
        sourceContractVersion = sourceContractVersion,
        readinessVersion = readinessVersion,
        deferredReason = deferredReason,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )

fun TimeAttendanceLeaveEntity.toListItem(): TimeAttendanceLeaveListItem =
    TimeAttendanceLeaveListItem(
        id = id,
        code = code,
        displayName = displayName,
        readinessState = ReadinessState.fromCode(readinessState),
        sourceContractVersion = sourceContractVersion,
        lastEvaluatedAt = lastEvaluatedAt,
        syncStatus = syncStatus,
    )
