package eu.grandmedical.diten.mobile.feature.performancereviews.presentation.list

import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.performancereviews.domain.PerformanceReviewsRepository
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Observes the offline-first cached list ([PerformanceReviewsRepository.observeList])
 * and exposes it as a [UiResult]. Pull-to-refresh delegates to
 * [PerformanceReviewsRepository.refresh]; a failure surfaces as a one-shot message
 * effect (the cached list keeps rendering).
 */
@HiltViewModel
class PerformanceReviewsListViewModel @Inject constructor(
    private val repository: PerformanceReviewsRepository,
) : MviViewModel<PerformanceReviewsListState, PerformanceReviewsListEvent, PerformanceReviewsListEffect>(
    PerformanceReviewsListState(),
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

    override suspend fun handleEvent(event: PerformanceReviewsListEvent) {
        when (event) {
            PerformanceReviewsListEvent.Refresh -> refresh()
        }
    }

    private suspend fun refresh() {
        setState { copy(isRefreshing = true) }
        val result = repository.refresh()
        if (result is UiResult.Error) {
            sendEffect(PerformanceReviewsListEffect.ShowMessage(result.message))
        }
        setState { copy(isRefreshing = false) }
    }
}
