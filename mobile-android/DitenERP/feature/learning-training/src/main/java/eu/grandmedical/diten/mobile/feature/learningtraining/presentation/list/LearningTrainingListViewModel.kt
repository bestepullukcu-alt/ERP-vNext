package eu.grandmedical.diten.mobile.feature.learningtraining.presentation.list

import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.LearningTrainingRepository
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Observes the offline-first cached list ([LearningTrainingRepository.observeList])
 * and exposes it as a [UiResult]. Pull-to-refresh delegates to
 * [LearningTrainingRepository.refresh]; a failure surfaces as a one-shot message
 * effect (the cached list keeps rendering).
 */
@HiltViewModel
class LearningTrainingListViewModel @Inject constructor(
    private val repository: LearningTrainingRepository,
) : MviViewModel<LearningTrainingListState, LearningTrainingListEvent, LearningTrainingListEffect>(
    LearningTrainingListState(),
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

    override suspend fun handleEvent(event: LearningTrainingListEvent) {
        when (event) {
            LearningTrainingListEvent.Refresh -> refresh()
        }
    }

    private suspend fun refresh() {
        setState { copy(isRefreshing = true) }
        val result = repository.refresh()
        if (result is UiResult.Error) {
            sendEffect(LearningTrainingListEffect.ShowMessage(result.message))
        }
        setState { copy(isRefreshing = false) }
    }
}
