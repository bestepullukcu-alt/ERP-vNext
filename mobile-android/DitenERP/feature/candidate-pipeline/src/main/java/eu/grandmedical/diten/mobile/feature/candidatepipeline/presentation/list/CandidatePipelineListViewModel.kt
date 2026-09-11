package eu.grandmedical.diten.mobile.feature.candidatepipeline.presentation.list

import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.candidatepipeline.domain.CandidatePipelineRepository
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Observes the offline-first cached list ([CandidatePipelineRepository.observeList])
 * and exposes it as a [UiResult]. Pull-to-refresh delegates to
 * [CandidatePipelineRepository.refresh]; a failure surfaces as a one-shot message
 * effect (the cached list keeps rendering).
 */
@HiltViewModel
class CandidatePipelineListViewModel @Inject constructor(
    private val repository: CandidatePipelineRepository,
) : MviViewModel<CandidatePipelineListState, CandidatePipelineListEvent, CandidatePipelineListEffect>(
    CandidatePipelineListState(),
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

    override suspend fun handleEvent(event: CandidatePipelineListEvent) {
        when (event) {
            CandidatePipelineListEvent.Refresh -> refresh()
        }
    }

    private suspend fun refresh() {
        setState { copy(isRefreshing = true) }
        val result = repository.refresh()
        if (result is UiResult.Error) {
            sendEffect(CandidatePipelineListEffect.ShowMessage(result.message))
        }
        setState { copy(isRefreshing = false) }
    }
}
