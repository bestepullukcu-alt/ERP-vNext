package eu.grandmedical.diten.mobile.feature.timeattendanceleave.presentation.create

import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.data.CodeDefaults
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.NewTimeAttendanceLeave
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.ReadinessState
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.TimeAttendanceLeaveRepository
import javax.inject.Inject

/**
 * Drives the create form. The Code field is pre-filled with a marker-safe
 * [CodeDefaults.defaultCode] (editable); facet states default to the backend
 * defaults. Submit is offline-first: [TimeAttendanceLeaveRepository.create] writes
 * a PENDING row locally and returns success even with no connectivity, upon which
 * we emit [TimeAttendanceLeaveCreateEffect.NavigateBack]. A failure surfaces a message.
 */
@HiltViewModel
class TimeAttendanceLeaveCreateViewModel @Inject constructor(
    private val repository: TimeAttendanceLeaveRepository,
) : MviViewModel<TimeAttendanceLeaveCreateState, TimeAttendanceLeaveCreateEvent, TimeAttendanceLeaveCreateEffect>(
    TimeAttendanceLeaveCreateState(
        code = CodeDefaults.defaultCode(),
        sourceContractVersion = NewTimeAttendanceLeave.DEFAULT_SOURCE_CONTRACT_VERSION,
    ),
) {

    override suspend fun handleEvent(event: TimeAttendanceLeaveCreateEvent) {
        when (event) {
            is TimeAttendanceLeaveCreateEvent.CodeChanged ->
                setState { copy(code = event.value, errorMessage = null) }

            is TimeAttendanceLeaveCreateEvent.DisplayNameChanged ->
                setState { copy(displayName = event.value, errorMessage = null) }

            is TimeAttendanceLeaveCreateEvent.SourceContractVersionChanged ->
                setState { copy(sourceContractVersion = event.value, errorMessage = null) }

            is TimeAttendanceLeaveCreateEvent.DeferredReasonChanged ->
                setState { copy(deferredReason = event.value) }

            is TimeAttendanceLeaveCreateEvent.StateChanged ->
                setState { copy(states = states + (event.field to event.value)) }

            TimeAttendanceLeaveCreateEvent.Submit -> submit()
        }
    }

    private suspend fun submit() {
        val current = state.value
        if (!current.canSubmit) return
        setState { copy(isSubmitting = true, errorMessage = null) }

        val result = repository.create(current.toNewTimeAttendanceLeave())
        when (result) {
            is UiResult.Success -> sendEffect(TimeAttendanceLeaveCreateEffect.NavigateBack)
            is UiResult.Error -> setState { copy(isSubmitting = false, errorMessage = result.message) }
            UiResult.Loading -> setState { copy(isSubmitting = false) }
        }
    }
}

/** Builds the domain create model from the current form state. */
private fun TimeAttendanceLeaveCreateState.toNewTimeAttendanceLeave(): NewTimeAttendanceLeave {
    fun field(f: TimeAttendanceLeaveStateField): ReadinessState = states[f] ?: f.default
    return NewTimeAttendanceLeave(
        code = code.trim(),
        displayName = displayName.trim(),
        readinessState = field(TimeAttendanceLeaveStateField.Readiness),
        timesheetIntakeBoundaryState = field(TimeAttendanceLeaveStateField.TimesheetIntakeBoundary),
        attendanceSyncBoundaryState = field(TimeAttendanceLeaveStateField.AttendanceSyncBoundary),
        leaveRequestBoundaryState = field(TimeAttendanceLeaveStateField.LeaveRequestBoundary),
        leaveBalanceBoundaryState = field(TimeAttendanceLeaveStateField.LeaveBalanceBoundary),
        scheduleConsumptionBoundaryState = field(TimeAttendanceLeaveStateField.ScheduleConsumptionBoundary),
        automatedDecisionBoundaryState = field(TimeAttendanceLeaveStateField.AutomatedDecisionBoundary),
        timeAttendanceSourceDependencyState = field(TimeAttendanceLeaveStateField.TimeAttendanceSourceDependency),
        leaveSourceDependencyState = field(TimeAttendanceLeaveStateField.LeaveSourceDependency),
        documentDependencyState = field(TimeAttendanceLeaveStateField.DocumentDependency),
        notificationDependencyState = field(TimeAttendanceLeaveStateField.NotificationDependency),
        consentPreconditionState = field(TimeAttendanceLeaveStateField.ConsentPrecondition),
        dataMinimizationState = field(TimeAttendanceLeaveStateField.DataMinimization),
        retentionPolicyState = field(TimeAttendanceLeaveStateField.RetentionPolicy),
        evidencePolicyState = field(TimeAttendanceLeaveStateField.EvidencePolicy),
        sourceContractVersion = sourceContractVersion.trim().ifBlank {
            NewTimeAttendanceLeave.DEFAULT_SOURCE_CONTRACT_VERSION
        },
        deferredReason = deferredReason.trim().ifBlank { null },
    )
}
