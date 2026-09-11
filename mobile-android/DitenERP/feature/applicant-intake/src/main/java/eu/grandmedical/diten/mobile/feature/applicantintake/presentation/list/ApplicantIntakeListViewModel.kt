package eu.grandmedical.diten.mobile.feature.applicantintake.presentation.list

import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.applicantintake.domain.ApplicantIntakeRepository
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Observes the offline-first cached list ([ApplicantIntakeRepository.observeList])
 * and exposes it as a [UiResult]. Pull-to-refresh delegates to
 * [ApplicantIntakeRepository.refresh]; a failure surfaces as a one-shot message
 * effect (the cached list keeps rendering).
 */
@HiltViewModel
class ApplicantIntakeListViewModel @Inject constructor(
    private val repository: ApplicantIntakeRepository,
) : MviViewModel<ApplicantIntakeListState, ApplicantIntakeListEvent, ApplicantIntakeListEffect>(
    ApplicantIntakeListState(),
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

    override suspend fun handleEvent(event: ApplicantIntakeListEvent) {
        when (event) {
            ApplicantIntakeListEvent.Refresh -> refresh()
        }
    }

    private suspend fun refresh() {
        setState { copy(isRefreshing = true) }
        val result = repository.refresh()
        if (result is UiResult.Error) {
            sendEffect(ApplicantIntakeListEffect.ShowMessage(result.message))
        }
        setState { copy(isRefreshing = false) }
    }
}
