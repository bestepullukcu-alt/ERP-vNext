package eu.grandmedical.diten.mobile.feature.learningtraining.presentation.detail

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.MviViewModel
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.LearningTrainingRepository
import eu.grandmedical.diten.mobile.feature.learningtraining.presentation.navigation.LearningTrainingFeature
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Observes a single record from the cache (offline-first) and runs the
 * server-authoritative Evaluate / Delete actions. The record [id] arrives as the
 * nav argument via [SavedStateHandle].
 */
@HiltViewModel
class LearningTrainingDetailViewModel @Inject constructor(
    private val repository: LearningTrainingRepository,
    savedStateHandle: SavedStateHandle,
) : MviViewModel<LearningTrainingDetailState, LearningTrainingDetailEvent, LearningTrainingDetailEffect>(
    LearningTrainingDetailState(),
) {

    private val id: String = savedStateHandle.get<String>(LearningTrainingFeature.ARG_ID).orEmpty()

    init {
        observeRecord()
    }

    private fun observeRecord() {
        viewModelScope.launch {
            repository.observe(id).collect { record ->
                setState { copy(record = UiResult.Success(record)) }
            }
        }
    }

    override suspend fun handleEvent(event: LearningTrainingDetailEvent) {
        when (event) {
            LearningTrainingDetailEvent.Evaluate -> evaluate()
            LearningTrainingDetailEvent.Delete -> delete()
        }
    }

    private suspend fun evaluate() {
        setState { copy(isBusy = true) }
        val result = repository.evaluate(id)
        if (result is UiResult.Error) {
            sendEffect(LearningTrainingDetailEffect.ShowMessage(result.message))
        }
        setState { copy(isBusy = false) }
    }

    private suspend fun delete() {
        setState { copy(isBusy = true) }
        when (val result = repository.delete(id)) {
            is UiResult.Success -> sendEffect(LearningTrainingDetailEffect.NavigateBack)
            is UiResult.Error -> {
                sendEffect(LearningTrainingDetailEffect.ShowMessage(result.message))
                setState { copy(isBusy = false) }
            }

            UiResult.Loading -> setState { copy(isBusy = false) }
        }
    }
}
