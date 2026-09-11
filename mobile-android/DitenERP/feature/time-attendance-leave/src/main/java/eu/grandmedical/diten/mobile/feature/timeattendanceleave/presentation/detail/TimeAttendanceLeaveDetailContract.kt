package eu.grandmedical.diten.mobile.feature.timeattendanceleave.presentation.detail

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.TimeAttendanceLeaveReadiness

data class TimeAttendanceLeaveDetailState(
    val record: UiResult<TimeAttendanceLeaveReadiness?> = UiResult.Loading,
    val isBusy: Boolean = false,
) : UiState

sealed interface TimeAttendanceLeaveDetailEvent : UiEvent {
    /** Ask the backend to (re)evaluate this record. */
    data object Evaluate : TimeAttendanceLeaveDetailEvent

    /** Delete this record (backend + cache). */
    data object Delete : TimeAttendanceLeaveDetailEvent
}

sealed interface TimeAttendanceLeaveDetailEffect : UiEffect {
    data class ShowMessage(val message: String) : TimeAttendanceLeaveDetailEffect
    data object NavigateBack : TimeAttendanceLeaveDetailEffect
}
