package eu.grandmedical.diten.mobile.feature.timeattendanceleave.presentation.list

import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.timeattendanceleave.domain.TimeAttendanceLeaveRepository
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Observes the offline-first cached list ([TimeAttendanceLeaveRepository.observeList])
 * and exposes it as a [UiResult]. Pull-to-refresh delegates to
 * [TimeAttendanceLeaveRepository.refresh]; a failure surfaces as a one-shot message
 * effect (the cached list keeps rendering).
 */
@HiltViewModel
class TimeAttendanceLeaveListViewModel @Inject constructor(
    private val repository: TimeAttendanceLeaveRepository,
) : MviViewModel<TimeAttendanceLeaveListState, TimeAttendanceLeaveListEvent, TimeAttendanceLeaveListEffect>(
    TimeAttendanceLeaveListState(),
) {

    init {
        observeList()
    }

    private fun observeList() {
        viewModelScope.launch {
            repository.observeList().collect { list ->
                setState { copy(items = UiResult.Success(list)) }
            }
        }
    }

    override suspend fun handleEvent(event: TimeAttendanceLeaveListEvent) {
        when (event) {
            TimeAttendanceLeaveListEvent.Refresh -> refresh()
        }
    }

    private suspend fun refresh() {
        setState { copy(isRefreshing = true) }
        val result = repository.refresh()
        if (result is UiResult.Error) {
            sendEffect(TimeAttendanceLeaveListEffect.ShowMessage(result.message))
        }
        setState { copy(isRefreshing = false) }
    }
}
