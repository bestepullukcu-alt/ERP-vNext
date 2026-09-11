package eu.grandmedical.diten.mobile.feature.employeeonboarding.presentation.list

import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.employeeonboarding.domain.EmployeeOnboardingRepository
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Observes the offline-first cached list ([EmployeeOnboardingRepository.observeList])
 * and exposes it as a [UiResult]. Pull-to-refresh delegates to
 * [EmployeeOnboardingRepository.refresh]; a failure surfaces as a one-shot message
 * effect (the cached list keeps rendering).
 */
@HiltViewModel
class EmployeeOnboardingListViewModel @Inject constructor(
    private val repository: EmployeeOnboardingRepository,
) : MviViewModel<EmployeeOnboardingListState, EmployeeOnboardingListEvent, EmployeeOnboardingListEffect>(
    EmployeeOnboardingListState(),
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

    override suspend fun handleEvent(event: EmployeeOnboardingListEvent) {
        when (event) {
            EmployeeOnboardingListEvent.Refresh -> refresh()
        }
    }

    private suspend fun refresh() {
        setState { copy(isRefreshing = true) }
        val result = repository.refresh()
        if (result is UiResult.Error) {
            sendEffect(EmployeeOnboardingListEffect.ShowMessage(result.message))
        }
        setState { copy(isRefreshing = false) }
    }
}
