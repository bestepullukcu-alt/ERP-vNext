package eu.grandmedical.diten.mobile.feature.learningtraining.presentation.detail

import eu.grandmedical.diten.mobile.core.common.UiResult
import eu.grandmedical.diten.mobile.core.common.mvi.UiEffect
import eu.grandmedical.diten.mobile.core.common.mvi.UiEvent
import eu.grandmedical.diten.mobile.core.common.mvi.UiState
import eu.grandmedical.diten.mobile.feature.learningtraining.domain.LearningTrainingReadiness

data class LearningTrainingDetailState(
    val record: UiResult<LearningTrainingReadiness?> = UiResult.Loading,
    val isBusy: Boolean = false,
) : UiState

sealed interface LearningTrainingDetailEvent : UiEvent {
    /** Ask the backend to (re)evaluate this record. */
    data object Evaluate : LearningTrainingDetailEvent

    /** Delete this record (backend + cache). */
    data object Delete : LearningTrainingDetailEvent
}

sealed interface LearningTrainingDetailEffect : UiEffect {
    data class ShowMessage(val message: String) : LearningTrainingDetailEffect
    data object NavigateBack : LearningTrainingDetailEffect
}
